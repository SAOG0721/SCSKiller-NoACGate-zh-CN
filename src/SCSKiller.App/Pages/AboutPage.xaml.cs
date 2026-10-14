using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using SCSKiller.Core.App;

namespace SCSKiller.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutVm Vm { get; } = new();
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Vm.Watch(true);
        Unloaded += (_, _) => Vm.Watch(false);
    }

    // Checking and downloading never wait for the queue or an offline session: only applying does (Updater.ApplyAsync).
    void OnCheckForUpdates(object _, Microsoft.UI.Xaml.RoutedEventArgs __) { if (!Updater.Restarting) _ = Updater.CheckNowAsync(); }

    async void OnSource(object _, Microsoft.UI.Xaml.RoutedEventArgs __) => await Vm.GetSourceAsync();

    /// <summary>--screenshots: the trademarks open, or the first component's licence (scrolled to it).</summary>
    public void ShowExpanded(bool licence)
    {
        TrademarksBox.IsExpanded = !licence;
        if (licence && Components.ContainerFromIndex(0) is Microsoft.UI.Xaml.DependencyObject c
            && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(c) > 0 && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(c, 0) is Expander e)
        {
            e.IsExpanded = true;
            e.StartBringIntoView(new() { VerticalAlignmentRatio = 0 });
        }
    }
}

/// <summary>One row of THIRD-PARTY-NOTICES.md's component table, with its licence text (empty when the notices have none).</summary>
public sealed record Notice(string Name, string Version, string Licence, string Copyright, string Project, string Text, string Note)
{
    public string Header => $"{Name} · {Version}";
    /// <summary>The SPDX id, or what a LicenseRef / NOASSERTION means in words.</summary>
    public string LicenceName => Licence switch
    {
        "NOASSERTION" => "未声明许可证",
        _ when Licence.StartsWith("LicenseRef-Microsoft") => "Microsoft 许可证",
        _ when Licence.EndsWith("Proprietary") => "专有软件",
        _ => Licence,
    };
    public string Detail => $"{LicenceName} · {Copyright}";
    public bool HasNote => Note.Length > 0;
    public string Body => Text.Length > 0 ? Text : "此组件未随附许可证正文。";
}

public sealed class AboutVm : Bindable
{
    public string Version { get; } = "版本 " + (typeof(AboutVm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?")
        + $" · {AppVersion.Current.Channel} 渠道";

    // Every published channel's source link stays on this fork's GitHub; no Patreon token is sent there.
    public bool ShowsSource { get; } = UpdateFeeds.Source(AppVersion.Current) != null;
    public bool SourceIdle { get; private set; } = true;
    public string SourceStatus { get; private set; } = "";

    public Task GetSourceAsync()
    {
        try
        {
            var url = UpdateFeeds.Source(AppVersion.Current)!;
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            SourceStatus = "已在浏览器中打开本 fork 的源码。";
        }
        catch (Exception e) { SourceStatus = "无法打开源码链接：" + e.Message; }
        Changed();
        return Task.CompletedTask;
    }

    // "Check for updates": every installed build (the design data shows it too); the state of the update next to it.
    public bool ShowsCheck { get; } = Updater.Installed;
    public bool CanCheck => !Updater.Checking && !Updater.Restarting;
    public string UpdateNote => Updater.Downloading is { } d ? $"正在下载 SCSKiller {d}…"
        : Updater.Checking ? "正在检查更新…"
        : Updater.Ready is { } v ? AutoInstall.ReadyNote(v, App.Core.Settings)
        : Updater.UpToDate ? "SCSKiller 已是最新版本。" : "";
    public string? UpdateProblem => Updater.Problem;
    public bool HasUpdateProblem => Updater.Problem != null;

    public bool? InstallUpdatesAutomatically
    {
        get => App.Core.Settings.InstallUpdatesAutomatically;
        set
        {
            if (value is not { } v || v == App.Core.Settings.InstallUpdatesAutomatically) return;
            App.Core.Settings = App.Core.Settings with { InstallUpdatesAutomatically = v };
            Changed();   // the note says when it installs
        }
    }

    readonly Coalesced changed;
    /// <summary>While the page is shown.</summary>
    public void Watch(bool on)
    {
        Updater.Changed -= changed.Request;
        if (!on) return;
        Updater.Changed += changed.Request;
        Changed();
    }

    public List<Notice> Components { get; }
    public string Trademarks { get; }

    public AboutVm()
    {
        changed = new(App.Main.DispatcherQueue, Changed);
        using var s = typeof(AboutVm).Assembly.GetManifestResourceStream("THIRD-PARTY-NOTICES.md")!;
        (Components, Trademarks) = Parse(new StreamReader(s).ReadToEnd());
    }

    /// <summary>The "## Components" table (the last cell links a "### Ln" licence section, whose first ```text block is the
    /// text) and the "## Trademarks" bullets.</summary>
    public static (List<Notice>, string) Parse(string md)
    {
        var lines = md.Replace("\r\n", "\n").Split('\n');
        string Section(string title) => string.Join('\n', lines.SkipWhile(l => l != "## " + title).Skip(1).TakeWhile(l => !l.StartsWith("## ")));
        var texts = new Dictionary<string, string>();
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("### L"))
            {
                var start = Array.IndexOf(lines, "```text", i) + 1;
                texts[lines[i][4..].Trim()] = string.Join('\n', lines[start..Array.IndexOf(lines, "```", start)]);
            }
        // the notes under the table: "- **Name**: note"
        var notes = Section("Components").Split('\n').Where(l => l.StartsWith("- **") && l.Contains("**: "))
            .ToDictionary(l => l[4..l.IndexOf("**: ")], l => l[(l.IndexOf("**: ") + 4)..].Replace("`", ""));
        var rows = Section("Components").Split('\n').Where(l => l.StartsWith("| ") && !l.StartsWith("| Component"))
            .Select(l => l.Trim('|').Split(" | ").Select(c => c.Trim()).ToArray())
            .Select(c => new Notice(c[0], c[1], c[2], c[3], c[4], c[5].StartsWith('[') ? texts.GetValueOrDefault(c[5][1..c[5].IndexOf(']')], "") : "",
                notes.GetValueOrDefault(c[0], "")))
            .ToList();
        var marks = string.Join('\n', Section("Trademarks").Split('\n').Where(l => l.StartsWith("- ")).Select(l => l[2..]));
        return (rows, marks);
    }
}

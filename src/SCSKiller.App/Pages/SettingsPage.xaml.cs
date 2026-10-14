using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SCSKiller.Core;
using System.Globalization;
using SCSKiller.Core.App;

namespace SCSKiller.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsVm Vm { get; } = new();
    public AccountVm Patreon { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        var refresh = new Coalesced(DispatcherQueue, Vm.GamesChanged);   // reconcile changes the recorder count
        void OnChanged(GameState _) => refresh.Request();
        Loaded += (_, _) => { Patreon.Watch(true); App.Core.GameChanged += OnChanged; };
        Unloaded += (_, _) => { Patreon.Watch(false); App.Core.GameChanged -= OnChanged; };
        Vm.PropertyChanged += (_, _) => Patreon.Refresh();   // e.g. the share checkbox ticked: the prompt goes
    }

    // Account: failures show in the card (Account.Problem), so these never throw.
    void OnSignIn(object _, RoutedEventArgs __) => _ = App.Account.SignInAsync();
    void OnCancelSignIn(object _, RoutedEventArgs __) => App.Account.CancelSignIn();
    void OnRefreshAccount(object _, RoutedEventArgs __) => _ = App.Account.RefreshAsync();
    void OnSignOut(object _, RoutedEventArgs __) { if (!Updater.Restarting) _ = App.Account.SignOutAsync(); }

    // The latest stable, even though it's older than this pre-release (a downgrade, allowed this once).
    void OnBackToStable(object _, RoutedEventArgs __)
    {
        if (Updater.Restarting) return;   // the restart applies the update it waited for, or none
        App.Core.Settings = App.Core.Settings with { UpdateChannel = UpdateChannels.Stable };
        _ = Updater.CheckAsync(backToStable: true);
    }

    // The post-sign-in share prompt (Account.OffersSharing).
    void OnShare(object _, RoutedEventArgs __) => Answer(share: true);
    void OnNotNow(object _, RoutedEventArgs __) => Answer(share: false);
    void Answer(bool share)   // asked once, ever: either answer dismisses it
    {
        App.Core.Settings = App.Core.Settings with { SharePromptDismissed = true, ShareRecordings = share || App.Core.Settings.ShareRecordings };
        Vm.Refresh();   // the share checkbox; its PropertyChanged refreshes the prompt too
    }

    public void ScrollToEnd() => Scroller.ChangeView(null, Scroller.ScrollableHeight, null, true);
    /// <summary>--screenshots: the Patreon card at the top.</summary>
    public void ScrollToPatreon() => PatreonCard.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, VerticalOffset = -16, AnimationDesired = false });

    // SetCacheLimit is a global driver setting that needs admin: only after the user's explicit OK.
    async void OnApply(object _, RoutedEventArgs __)
    {
        var (label, limit) = SettingsVm.Sizes[SizeBox.SelectedIndex];
        if (!await App.ConfirmAsync(this, $"将着色器缓存上限设为 {label}？",
                $"这是 {Fmt.Vendor(App.Core.Vendor.Vendor)} 驱动的全局设置，会影响此电脑上的所有游戏，并一直保留到你再次更改。"
                + "Windows 会请求管理员权限。\n\n提高上限不会删除缓存；若将上限调低到当前用量以下，驱动会删除较旧的条目，相关游戏可能再次卡顿。"
                + "你可以随时恢复驱动默认值。",
                "更改上限")) return;

        ApplyButton.IsEnabled = false;
        var size = limit.IsDriverDefault ? "default" : limit.Bytes is { } b ? (b / (double)(1L << 30)).ToString(CultureInfo.InvariantCulture) : "unlimited";
        (Vm.ApplyMessage, Vm.ApplySeverity) = await AsAdminAsync(() => App.Core.Vendor.SetCacheLimit(limit), ["cache", "set", size]) switch
        {
            null => ("已取消。", InfoBarSeverity.Informational),
            { Ok: true } => ($"着色器缓存上限已设为 {label}。", InfoBarSeverity.Success),
            var r => ("未更改上限：" + r.Message, InfoBarSeverity.Error),
        };
        ApplyButton.IsEnabled = true;
        Vm.Refresh();
    }

    // Admin-only actions: in this process when it is already elevated (or shows the fake), else the bundled CLI behind a
    // UAC prompt (Elevated.Run). Null = the user cancelled the prompt; an exception is a failure like the CLI's.
    static Task<Elevated.Result?> AsAdminAsync(Action inProcess, string[] cliArgs) => Task.Run(() =>
    {
        try
        {
            if (App.Core is not Design.FakeScsKiller && !Elevated.IsAdmin) return Elevated.Run(cliArgs);
            inProcess();
            return new Elevated.Result(true, "");
        }
        catch (Exception ex) { return new Elevated.Result(false, ex.Message); }
    });

    // Auto Shader Compilation: a global driver setting + the driver's idle task, admin: only after the user's explicit OK.
    async void OnApplyAuto(object _, RoutedEventArgs __)
    {
        var level = Vm.AutoChoice;
        if (!await App.ConfirmAsync(this, level == AutoShaderCompilation.Off ? "关闭 NVIDIA 自动着色器编译？" : $"开启 NVIDIA 自动着色器编译（{level}）？",
                "自动着色器编译是 NVIDIA® 的测试版功能。", level == AutoShaderCompilation.Off ? "关闭" : "开启")) return;

        AutoApplyButton.IsEnabled = false;
        // --for-user: the elevated CLI refuses when UAC elevated another administrator (NvOSC would register the task for them).
        string[] cli = ["nvidia-auto-shader", level.ToString().ToLowerInvariant(), Elevated.ForUserArg, Elevated.CurrentUserSid];
        (Vm.AutoMessage, Vm.AutoSeverity) = await AsAdminAsync(() => App.Core.Vendor.SetAutoShaderCompilation(level), cli) switch
        {
            null => ("已取消。", InfoBarSeverity.Informational),
            { Ok: true } => (level == AutoShaderCompilation.Off ? "已关闭。" : $"已开启（{level}）。", InfoBarSeverity.Success),
            var r => ("未更改设置：" + r.Message, InfoBarSeverity.Error),
        };
        AutoApplyButton.IsEnabled = true;
        Vm.Refresh();   // re-reads the setting and the task
    }
}

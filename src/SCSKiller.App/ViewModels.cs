using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Games;
using SCSKiller.Core.Vendors;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;

namespace SCSKiller.App;

// View models are computed views over IScsKiller: Refresh() re-reads the facade and tells x:Bind to update everything.
public abstract class Bindable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed() => PropertyChanged?.Invoke(this, new(string.Empty));
}

static class Fmt
{
    public static string N(long? n) => n?.ToString("N0") ?? Format.Dash;
    public static string Duration(TimeSpan? t) => t is not { } d ? Format.Dash
        : d.TotalMinutes < 1 ? $"{Math.Max(1, (int)d.TotalSeconds)} 秒"
        : d.TotalHours < 1 ? $"{(int)Math.Round(d.TotalMinutes)} 分钟"
        : $"{(int)d.TotalHours} 小时" + (d.Minutes > 0 ? $" {d.Minutes} 分钟" : "");
    /// <summary>"today at 20:14", "yesterday at 20:14", else "on 26/09/2026" (the culture's short date).</summary>
    public static string When(DateTimeOffset at)
    {
        var t = at.LocalDateTime;
        return t.Date == DateTime.Today ? $"今天 {t:t}" : t.Date == DateTime.Today.AddDays(-1) ? $"昨天 {t:t}" : $"{t:d}";
    }
    public static string Vendor(GpuVendor v) => v switch
    {
        GpuVendor.Nvidia => "NVIDIA", GpuVendor.Amd => "AMD", GpuVendor.Intel => "Intel", _ => "GPU",
    };
    public static string Engine(EngineInfo? e) => e is null ? "未知引擎" : Format.Engine(e, e.Family == "Unreal" ? "UE" : null) + Guessed(e);
    public static string Guessed(EngineInfo e) => e.VersionGuessed ? "（推测）" : "";
    public static string AntiCheatName(AntiCheat a) => a switch
    {
        AntiCheat.EasyAntiCheat => "EasyAntiCheat", AntiCheat.BattlEye => "BattlEye", _ => "反作弊组件",
    };
    public static Style Style(string key) => (Style)Application.Current.Resources[key];
    /// <summary>"Severe shader-compilation stutter: reason", then the source and when it was checked; null when not listed.</summary>
    public static string? StutterTip(GameState s) => s.KnownStutter is { } k
        ? $"{(k.Severity == StutterSeverity.Severe ? "严重" : "中等")}着色器编译卡顿：{k.Reason}。\n来源：{(k.Source == StutterList.OwnMeasurement ? "SCSKiller 实测" : k.Source)}（核查于 {k.Date}）" : null;
    /// <summary>Library sections, in list order. GOG, Ubisoft, Battle.net, PURPLE, HoYoPlay and Gaijin games are Store.Other: told apart by their id prefix.</summary>
    public static readonly string[] Stores = ["Steam", "Epic Games", "Xbox / Game Pass", "EA app", "GOG", "Ubisoft Connect", "Battle.net", "PURPLE", "HoYoPlay", "Gaijin", "其他", AddedByYou];
    public const string AddedByYou = "手动添加";
    public static string StoreName(Game g) => g.Store switch
    {
        Store.Steam => "Steam", Store.Epic => "Epic Games", Store.Xbox => "Xbox / Game Pass", Store.EA => "EA app", Store.Manual => AddedByYou,
        _ => g.Id[..Math.Max(0, g.Id.IndexOf(':'))] switch { "gog" => "GOG", "ubisoft" => "Ubisoft Connect", "battlenet" => "Battle.net", "purple" => "PURPLE", "hoyoplay" => "HoYoPlay", "gaijin" => "Gaijin", _ => "其他" },
    };
    /// <summary>Measured driver cache when known, else the estimate.</summary>
    public static string Cache(GameState s) => s.CacheOnDisk is { } c ? Format.Bytes(c) : s.EstimatedCacheBytes is { } b ? "≈ " + Format.Bytes(b) : Format.Dash;
    /// <summary>The last compile's measured time, else the estimate: the estimate's rate is measured on a cold cache only.</summary>
    public static TimeSpan? CompileTime(GameState s) => s.LastWarmTime ?? s.EstimatedWarmTime;
    /// <summary>Tooltip for an estimated (not measured) cache value; null when measured.</summary>
    public static string? CacheTip(GameState s) => s.CacheOnDisk == null && s.EstimatedCacheBytes != null ? "编译后预计占用的驱动缓存" : null;
    /// <summary>One middleware tag: what it compiles and its DLLs.</summary>
    public static string TagTip(MiddlewareTag t) => Format.Middleware(t) + "\n    " + string.Join(", ", t.Dlls);
    public static bool Active(QueueItem q) => q.Stage is not (QueueStage.Done or QueueStage.Failed or QueueStage.Stopped);
    public static void PauseOrResume(QueueItem? running)
    {
        if (running?.Stage == QueueStage.Paused) App.Core.ResumeQueue(); else App.Core.PauseQueue();
    }

    /// <summary>The game's compile runs (or is paused), or is the next the running queue starts: Play waits, since the warm
    /// stages a process under the game's exe name. A stopped queue's items and "when idle" items don't block it.</summary>
    public static bool CompilingSoon(IEnumerable<QueueItem> queue, string gameId)
    {
        var compiles = queue.Where(q => !q.PlanCheck).ToList();
        return compiles.Any(q => q.GameId == gameId && Format.Running(q))
            || App.Core.QueueRunning && compiles.FirstOrDefault(q => q.Stage == QueueStage.Waiting && q.Note != ScsKiller.WhenIdleNote)?.GameId == gameId;
    }

    public const string CompilingTip = "正在编译，请完成后再启动游戏";


    /// <summary>Starts the game through its store (<see cref="StoreLaunch"/>); the error to show, or null. The design data starts nothing.</summary>
    public static string? Play(Game g)
    {
        if (App.Core is Design.FakeScsKiller) return null;
        try
        {
            if (StoreLaunch.Command(g) is not { } command) return $"找不到 {StoreName(g)} 启动 {g.Name} 的方式，请刷新游戏库。";
            System.Diagnostics.Process.Start(command)?.Dispose();
            return null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return $"无法{(g.Store == Store.Manual ? "" : "通过 " + StoreName(g))}启动 {g.Name}：{e.Message}";
        }
    }

    /// <summary>The Play button's tooltip when it can start the game.</summary>
    public static string PlayVia(Game g) => g.Store == Store.Manual ? $"启动 {Path.GetFileName(g.ExePath)}" : $"通过 {StoreName(g)} 启动";

    public static string RecorderSkip(string reason) => Format.RecorderSkip(reason);

    public static string RecorderNote(string note) => Format.RecorderNote(note);

    public static string RecorderRefused(string reason) => Format.RecorderRefused(reason);
}

/// <summary>Folds a burst of Core events into one run on the UI thread: a scan raises GameChanged once per game (62 at
/// once on a real library), and a page refresh per event kept the UI thread busy for seconds. Request from any thread.</summary>
public sealed class Coalesced(DispatcherQueue ui, Action run)
{
    int pending;

    public void Request()
    {
        if (Interlocked.Exchange(ref pending, 1) == 0 && !ui.TryEnqueue(() => { Volatile.Write(ref pending, 0); run(); }))
            Volatile.Write(ref pending, 0);   // the window is closing
    }
}

/// <summary>A read that must not run on the UI thread, such as the driver cache's usage and limit (NVIDIA's limit loads the
/// driver's profile database: ~130 ms measured; the usage walks the cache folder). <paramref name="read"/> runs on a
/// background thread, <paramref name="apply"/> on the UI thread. One read at a time: requests meanwhile fold into one more
/// read, so results apply in order. Request on the UI thread.</summary>
public sealed class BackgroundRead<T>(Func<T> read, Action<T> apply)
{
    bool busy, again;

    public async void Request()
    {
        if (busy) { again = true; return; }
        busy = true;
        try
        {
            do
            {
                again = false;
                apply(await Task.Run(read));
            } while (again);
        }
        finally { busy = false; }
    }
}

/// <summary>Game icons: a packaged (Xbox / Game Pass) game's own logo, else the exe's shell thumbnail (its own icon); null
/// when the exe isn't there or the icon failed to load after retries (the row shows its tile). UI thread only.</summary>
static class Icons
{
    // The stream stays open as long as its image: XAML may decode again from it (another size: Library 32 px, detail 56 px).
    static readonly Dictionary<string, (BitmapImage Image, IDisposable? Source)> cache = [];
    static readonly HashSet<string> failed = [];

    /// <summary>An icon failed after its row was built: Get now returns null for it, so rebuild the rows (UI thread).</summary>
    public static event Action? Failed;

    public static ImageSource? Get(string exePath)
    {
        if (failed.Contains(exePath) || !File.Exists(exePath)) return null;
        if (!cache.TryGetValue(exePath, out var entry))
        {
            var bmp = new BitmapImage();
            bmp.ImageFailed += (_, _) => Fail(exePath);   // a decode failure
            cache[exePath] = entry = (bmp, null);
            _ = LoadAsync(exePath, bmp);
        }
        return entry.Image;
    }

    static async Task LoadAsync(string path, BitmapImage bmp)
    {
        var logo = await Task.Run(() => PackageLogo(path));
        for (var attempt = 1; ; attempt++)
        {
            IRandomAccessStream? stream = null;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(logo ?? path);
                stream = logo != null ? await file.OpenReadAsync()
                    : await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64) ?? throw new IOException("no thumbnail");
                cache[path] = (bmp, stream);
                await bmp.SetSourceAsync(stream);
                return;
            }
            catch (Exception)
            {
                stream?.Dispose();
                // The shell often answers E_PENDING (0x8000000A) at first, while many icons are asked for at once (seen on
                // Elden Ring's and SCSKiller's own exe): retry for ~8 s before the tile. An unreadable exe fails every time.
                if (attempt == 5) { Fail(path); return; }
                await Task.Delay(250 << attempt);
            }
        }
    }

    /// <summary>The Square44x44Logo of the package the exe sits in (appxmanifest.xml in a parent folder): a GDK game's exe
    /// often has no icon of its own (Atomic Heart), and the shell then shows the generic application icon. Null when not
    /// packaged, unreadable (WindowsApps) or the logo is only there scale-qualified.</summary>
    static string? PackageLogo(string exePath)
    {
        try
        {
            for (var dir = Path.GetDirectoryName(exePath); dir != null; dir = Path.GetDirectoryName(dir))
            {
                var manifest = Path.Combine(dir, "appxmanifest.xml");
                if (!File.Exists(manifest)) continue;
                var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(manifest), "Square44x44Logo=\"([^\"]+)\"");
                return m.Success && Path.Combine(dir, m.Groups[1].Value) is var logo && File.Exists(logo) ? logo : null;
            }
        }
        catch (Exception) { }
        return null;
    }

    static void Fail(string path)
    {
        if (!failed.Add(path)) return;
        if (cache.Remove(path, out var entry)) entry.Source?.Dispose();
        Failed?.Invoke();
    }
}

/// <summary>A small tag on a library row (a middleware label, or "+N").</summary>
public sealed record TagChip(string Text, string Tip);

public sealed class GameRow(GameState s, bool queued = false, bool compiling = false)
{
    static readonly Color[] Tiles =
    [
        Color.FromArgb(255, 0x3b, 0x4a, 0x8c), Color.FromArgb(255, 0x6b, 0x3b, 0x5e), Color.FromArgb(255, 0x2f, 0x6b, 0x4f),
        Color.FromArgb(255, 0x2f, 0x5f, 0x73), Color.FromArgb(255, 0x7a, 0x4a, 0x2a), Color.FromArgb(255, 0x5a, 0x6b, 0x2f),
        Color.FromArgb(255, 0x3d, 0x55, 0x66), Color.FromArgb(255, 0x5c, 0x3b, 0x7a),
    ];

    public GameState State => s;
    public bool Playing => s.Playing;
    public string Id => s.Game.Id;
    public string Name => s.Game.Name;
    public ImageSource? Icon { get; } = Icons.Get(s.Game.ExePath);
    public bool NoIcon => Icon is null;
    public Brush Tile { get; } = TileOf(s.Game.Name);
    public string Initials => InitialsOf(s.Game.Name);
    public static Brush TileOf(string name) => new SolidColorBrush(Tiles[name.Sum(c => c) % Tiles.Length]);
    public static string InitialsOf(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsUpper(w[0]) || char.IsDigit(w[0])).ToArray();
        return words.Length >= 2 ? $"{words[0][0]}{words[1][0]}" : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }
    public string Sub => $"{Fmt.Engine(s.Engine)} · {StoreName}";
    public string StoreName { get; } = Fmt.StoreName(s.Game);
    /// <summary>Search: the name, the store as players name it, or the engine ("UE 5", "Unreal"), case-insensitive.</summary>
    public bool Matches(string term) => Name.Contains(term, StringComparison.OrdinalIgnoreCase) || StoreName.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Fmt.Engine(s.Engine).Contains(term, StringComparison.OrdinalIgnoreCase) || s.Engine?.Family.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    public bool HasTags => s.Middleware is { Count: > 0 };
    public bool HasTagLine => HasTags || IsKnownStutter || IsNoStutter;
    public bool IsKnownStutter => s.KnownStutter != null;
    public string? StutterTip => Fmt.StutterTip(s);
    /// <summary>On game-verdicts.json as no-stutter: compiling stays possible, but the bulk adds leave it out.</summary>
    public bool IsNoStutter => s.NoStutter != null;
    public string? NoStutterTip => s.NoStutter is { } reason ? Format.NoStutterReason(reason) : null;
    // The first three, then "+N" with the rest in its tooltip. After the stutter chip only one more chip fits before the
    // Shaders column: several become "N upscalers".
    const int MaxTags = 3;
    public List<TagChip> Tags => s.Middleware is not { Count: > 0 } m ? []
        : (IsKnownStutter || IsNoStutter) && m.Count > 1 ? [new TagChip($"{m.Count} 个超分辨率组件", string.Join("\n", m.Select(Fmt.TagTip)))]
        : [.. m.Take(MaxTags).Select(t => new TagChip(t.Label, Fmt.TagTip(t))),
           .. m.Count > MaxTags ? [new TagChip($"+{m.Count - MaxTags}", string.Join("\n", m.Skip(MaxTags).Select(Fmt.TagTip)))] : Array.Empty<TagChip>()];
    public string Shaders => Fmt.N(s.ShaderCount);
    public string Pipelines => Fmt.N(ScsKiller.PlanPipelines(s));
    public string Cache => Fmt.Cache(s);
    public string? CacheTip => Fmt.CacheTip(s);
    public Style CacheStyle => Fmt.Style(CacheTip != null ? "Secondary" : "BodyTextBlockStyle");
    public string Time => Fmt.Duration(Fmt.CompileTime(s));

    bool Partly => ScsKiller.IsPartlyWarmed(s);
    bool PartlyCompiled => ScsKiller.IsPartlyCompiled(s);
    public string StatusText => s.Status switch
    {
        _ when s.CompileUnreached => "编译未生效",
        _ when PartlyCompiled => "部分已编译",
        GameStatus.Warmed when Partly => "部分已预热",
        GameStatus.Warmed => "已预热",
        GameStatus.Ready => "可编译",
        _ when ScsKiller.NeedsOfflineSession(s) => "需要离线会话",
        GameStatus.NeedsRecording when ScsKiller.NeverRecorded(s) => "游玩过但未录到数据",
        GameStatus.NeedsRecording => ScsKiller.RecordedEnough(s) ? "需要继续录制" : "需要录制约 5 分钟",
        GameStatus.Stale => "需要重新编译",
        _ when s.ReasonFlags.HasFlag(GameReasonFlags.CacheUnreachable) => "不兼容",
        _ when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) => "待确认录制风险",
        _ => s.ShaderModBlocks ? "未编译" : s.Engine?.Encrypted == true ? "游戏文件已加密" : s.NotPlanned ? "不支持" : "暂不支持",
    };
    /// <summary>The whole reason: the row's tooltip and the game page's status text.</summary>
    public string FullNote => s.Status switch
    {
        _ when s.CompileUnreached || PartlyCompiled => Format.Reason(s),
        GameStatus.Warmed when Partly => $"驱动 {s.WarmedDriverVersion} · 首次启动仍编译了 {s.Careful!.LaunchCompiled * 100:0}%",
        GameStatus.Warmed => $"驱动 {s.WarmedDriverVersion}" + (ScsKiller.IsPartial(s.Plan) ? " · 录制后可编译其余部分"
            : ScsKiller.RtAfterRecording(s) ? " · 光线追踪还需要约 5 分钟录制" : ""),
        _ when ScsKiller.NeedsOfflineSession(s) => $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。可使用离线会话录制，或在游戏详情中确认风险后选择直接录制。",
        _ when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) => $"需要录制，但检测到 {Fmt.AntiCheatName(s.AntiCheat)}。请在详情页阅读风险说明并决定是否允许直接录制。",
        GameStatus.NeedsRecording when s.AntiCheat != AntiCheat.None && !s.AntiCheatRecordingAllowed => $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}；可在详情页确认风险后选择录制",
        GameStatus.NeedsRecording when s.RecordingPaused => Format.Reason(s),
        GameStatus.NeedsRecording when ScsKiller.NeverRecorded(s) => Fmt.RecorderRefused(s.RecorderRefused!),
        GameStatus.NeedsRecording when s.RecorderInstalled && !ScsKiller.RecordedEnough(s) => Format.RecorderOnNote(s),
        _ => Format.Reason(s),
    } + Format.ModNote(s);
    /// <summary>The row's note under the status: a few words (<see cref="Format.ShortNote"/>); null when the status says it all.</summary>
    public string? Note => s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) ? "可在详情页选择" : Format.ShortNote(s);
    public string RowNote => Playing ? "正在游玩" + (Note is { } n ? " · " + n : "") : Note ?? "";
    public bool HasRowNote => RowNote.Length > 0;
    public string RowTip => (Playing ? "正在游玩 · " : "") + FullNote;
    public Style StatusStyle => Fmt.Style(s.Status switch
    {
        _ when s.CompileUnreached || PartlyCompiled => "StatusWarn",
        _ when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) => "StatusWarn",
        GameStatus.Warmed when Partly => "StatusWarn",
        GameStatus.Warmed => "StatusAccent",
        GameStatus.Ready => "StatusPrimary",
        GameStatus.Unsupported => "StatusMuted",
        _ => "StatusWarn",
    });

    // One action per status (Main.dc.html): Details / Add to queue / Record / disabled Why?
    public bool Queued => queued;
    public bool IsAdd => s.Status is GameStatus.Ready or GameStatus.Stale && !s.CompileUnreached;
    public string AddText => queued ? "已在队列中" : "加入队列";
    public bool CanAdd => !queued;
    public bool IsRecord => s.Status == GameStatus.NeedsRecording && !s.RecorderInstalled && s.NoStutter == null && !ScsKiller.NeedsOfflineSession(s);
    public bool CanRecord => s.AntiCheat == AntiCheat.None || s.AntiCheatRecordingAllowed && s.RecorderOverride == RecorderOverride.On;
    public bool IsDetails => s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) || s.Status == GameStatus.Warmed || s.CompileUnreached || ScsKiller.NeedsOfflineSession(s) || (s.Status == GameStatus.NeedsRecording && (s.RecorderInstalled || s.NoStutter != null));
    public bool IsWhy => s.Status == GameStatus.Unsupported && !s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked);
    public bool IsEncrypted => s.Engine?.Encrypted == true;
    public string Reason => s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked)
        ? $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。请在游戏详情中阅读风险说明并决定是否录制。" : Format.Reason(s);

    // Play goes through the store (a game the user added: its exe); never for an anti-cheat game (SCSKiller never launches them)
    public bool CanLaunch { get; } = s.AntiCheat == AntiCheat.None && StoreLaunch.Supported(s.Game);
    public bool Compiling => compiling;
    public bool CanPlay => !Playing && !compiling;
    public string PlayText => Playing ? "运行中" : "启动游戏";
    public string PlayTip => Playing ? $"{Name} 正在运行" : compiling ? Fmt.CompilingTip : Fmt.PlayVia(s.Game);

    public override string ToString() => $"{Name}, {StatusText}, {RowTip}";   // list item name for screen readers
}

/// <summary>One store's section of the library list (the rows shown after the search filter).</summary>
public sealed class StoreGroup(string store, IEnumerable<GameRow> rows) : ObservableCollection<GameRow>(rows)
{
    public const string Recommended = "建议编译";
    public string Store => store;
    public string CountText => $"{Count} 个游戏" + (store == Recommended ? RecommendedNote() : "");
    // a row replaced in place (compiled, cleared) changes the count of compiled ones
    protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CountText)));
    }
    string RecommendedNote() => this.Count(r => r.State.Status == GameStatus.Warmed) is var done and > 0
        ? $" · 已知有卡顿 · {done} 个已编译" : " · 已知有卡顿";
}

public sealed class LibraryVm : Bindable
{
    static bool scanned;
    readonly BackgroundRead<(CacheUsage Usage, CacheLimit? Limit, long? Capped)> cache;

    public LibraryVm() => cache = new(() => (App.Core.Vendor.GetCacheUsage(), App.Core.Vendor.GetCacheLimit(), (App.Core.Vendor as AmdBackend)?.AppCache.DxcBytes()), ShowCache);

    public ObservableCollection<GameRow> Games { get; } = [];   // every game, sorted; the list shows Groups
    /// <summary>The list: rows matching <see cref="Filter"/> per store, empty stores left out. Filtering reuses the rows
    /// (no icon reads, no IO).</summary>
    public ObservableCollection<StoreGroup> Groups { get; } = [];
    string filter = "";
    public string Filter { get => filter; set { if (value.Trim() != filter) { filter = value.Trim(); ApplyFilter(); Changed(); } } }
    int shown;
    public bool Filtering => filter.Length > 0;
    public string ShownText => $"显示 {shown} / {Games.Count} 个游戏";
    public bool NoMatch => Filtering && Games.Count > 0 && shown == 0;
    public string NoMatchText => $"没有匹配“{filter}”的游戏";
    public bool Scanning { get; private set; }
    int refreshing;   // Rescan calls not finished: the scan, then a user refresh's server fetches
    public bool Refreshing => refreshing > 0;
    public string? RefreshNote { get; private set; }
    public bool HasRefreshNote => RefreshNote != null;
    public string? Error { get; private set; }
    public bool HasError => Error != null;
    public string? GpuNotice => Format.GpuNotice(App.Core.Vendor.Gpu, App.Core.Settings.GpuNoticeDismissed);
    public bool HasGpuNotice => GpuNotice != null;
    public void DismissGpuNotice()
    {
        App.Core.Settings = App.Core.Settings with { GpuNoticeDismissed = App.Core.Vendor.Gpu.Name };
        Changed();
    }

    public string Summary { get; private set; } = "";
    public int ReadyCount { get; private set; }     // ready, not queued yet and not a no-stutter game
    public int WaitingCount { get; private set; }
    public string AddAllText => $"全部加入队列（{ReadyCount}）";
    public bool CanAddAll => ReadyCount > 0 && !Scanning;
    public int RecommendedCount { get; private set; }   // known to stutter, ready and not queued yet
    public string AddRecommendedText => $"将建议游戏全部加入队列（{RecommendedCount}）";
    public bool CanAddRecommended => RecommendedCount > 0 && !Scanning;
    public bool CannotAddRecommended => !CanAddRecommended;   // the plain, disabled twin of the accent button (theme brushes stay in XAML)
    public string CompileQueueText => $"编译队列（{WaitingCount}）";
    public bool CanCompileQueue => WaitingCount > 0;

    public string CacheUsed { get; private set; } = "";
    public string CacheLimitText { get; private set; } = "";
    public double CachePercent { get; private set; }
    public bool CacheWarn { get; private set; }
    public string CacheWarnText { get; private set; } = "";
    public bool CanRaiseLimit => App.Core.Vendor.Caps.CacheSizeConfigurable;

    public string DriverMode => App.Core.Settings.OnDriverUpdate switch
    {
        DriverUpdateMode.Ask => "先询问我", DriverUpdateMode.WhenIdle => "电脑空闲时", _ => "关闭",
    };
    public string DriverModeNote => App.Core.Settings.OnDriverUpdate switch
    {
        DriverUpdateMode.Ask => "通过通知选择立即编译、空闲时编译或跳过",
        DriverUpdateMode.WhenIdle => "离开电脑时自动重新编译",
        _ => "在游戏库中手动重新编译",
    };
    public string Threads => App.Core.Settings.Threads.ToString();
    public string ThreadsOf => $"共 {Environment.ProcessorCount} 个线程";
    public string SpeedNote { get; private set; } = "";

    public void Load()
    {
        Refresh();
        if (!scanned) Rescan(force: false);
    }


    // The scan message waits 300 ms so a cached scan doesn't flicker it; the list fills when the scan ends.
    bool slowScan, forced;
    public bool ScanEmpty => slowScan && Games.Count == 0;
    public bool ScanBusy => slowScan && Games.Count > 0;
    public string ScanEmptyNote => "正在检查 Steam、Epic、Xbox、EA、GOG、Ubisoft Connect、Battle.net、PURPLE、HoYoPlay 和 Gaijin 中的游戏，并识别引擎。"
        + "首次扫描会读取各游戏的文件，可能需要一段时间。";
    public string ScanBusyNote => forced ? "正在重新检查游戏引擎与反作弊；完成后更新列表。"
        : "完成后更新列表。";

    public async void Rescan(bool force = true, bool userRequested = false)
    {
        Scanning = true; refreshing++; forced = force; Error = RefreshNote = null; Refresh();   // the summary says "Looking for games…" while nothing is listed
        var scan = force ? App.Core.RescanAsync(CancellationToken.None, userRequested) : App.Core.ScanAsync(CancellationToken.None, userRequested);
        if (await Task.WhenAny(scan, Task.Delay(300)) != scan) { slowScan = true; Changed(); }
        try { await scan; scanned = true; }
        catch (Exception e) { Error = "扫描失败：" + e.Message; }
        Scanning = slowScan = false;
        Refresh();
        if (userRequested && Error == null && App.Core is ScsKiller k)
            RefreshNote = k.ContentRoutes == null ? "已刷新本地游戏库；本 fork 暂未启用在线规则更新。" : await k.ServerRefresh switch
            {
                ScsKiller.ServerCheck.TooSoon => "几分钟前刚检查过服务器。",
                ScsKiller.ServerCheck.Unreachable => "无法连接 SCSKiller 服务器，列表保持原样。",
                _ => null,
            };
        refreshing--;
        Changed();
    }

    public void Refresh()
    {
        var core = App.Core;
        var list = core.Games.OrderBy(g => g.Status switch
        {
            GameStatus.Stale => 0, GameStatus.Warmed => 1, GameStatus.Ready => 2, GameStatus.NeedsRecording => 3, _ => 4,
        }).ThenBy(g => g.Game.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var queue = core.Queue.Where(q => !q.PlanCheck).ToList();
        var queued = queue.Where(Fmt.Active).Select(q => q.GameId).ToHashSet();
        GameRow Row(GameState g) => new(g, queued.Contains(g.Game.Id), Fmt.CompilingSoon(queue, g.Game.Id));
        if (!Games.Select(r => r.Id).SequenceEqual(list.Select(g => g.Game.Id)))
        {
            Games.Clear();
            foreach (var g in list) Games.Add(Row(g));
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
                if (Games[i].State != list[i] || Games[i].Queued != queued.Contains(list[i].Game.Id) || Games[i].Compiling != Fmt.CompilingSoon(queue, list[i].Game.Id)
                    || Games[i].Icon != Icons.Get(list[i].Game.ExePath)) Games[i] = Row(list[i]);
        }
        ApplyFilter();

        int ready = list.Count(g => g.Status is GameStatus.Ready or GameStatus.Stale);
        ReadyCount = Games.Count(r => r.IsAdd && !r.Queued && !r.IsNoStutter);
        RecommendedCount = RecommendedToAdd().Count;
        WaitingCount = queue.Count(q => q.Stage == QueueStage.Waiting);
        var stores = Fmt.Stores.Where(n => n != Fmt.AddedByYou && Games.Any(r => r.StoreName == n)).ToList();
        var storeText = stores.Count > 2 ? $"{stores.Count} 个平台" : string.Join("、", stores);
        bool allUnreal = list.Count > 0 && list.All(g => g.Engine?.Family == "Unreal");
        Summary = Scanning && list.Count == 0 ? "正在查找游戏…"
            : $"找到 {list.Count}{(allUnreal ? " 个 Unreal Engine 游戏" : " 个游戏")}{(storeText.Length > 0 ? "，来自" + storeText : "")} · {ready} 个可编译";

        cache.Request();

        var rate = ScsKiller.MeasuredRate(list);
        SpeedNote = (core.Settings.Priority == WarmPriority.BelowNormal ? "低于正常优先级" : "空闲优先级")
                    + (rate is { } r ? $" · 实测约 {r:N0} 条管线/秒" : "");
        Changed();
    }

    /// <summary>Games -> Groups: same order within a store (status, then name). The sections are rebuilt only when which
    /// rows show changes; a row whose state changed is swapped in place.</summary>
    void ApplyFilter()
    {
        var visible = Games.Where(r => filter.Length == 0 || r.Matches(filter)).ToList();
        // known-stutter games go on top (StutterList.Recommended), out of their store's section, compiled or not
        var byId = visible.ToDictionary(r => r.Id);
        var recommended = StutterList.Recommended(visible.Select(r => r.State)).Select(s => byId[s.Game.Id]).ToList();
        var groups = visible.Except(recommended).GroupBy(r => r.StoreName)
            .OrderBy(g => Array.IndexOf(Fmt.Stores, g.Key)).Select(g => (g.Key, Rows: g.ToList())).ToList();
        if (recommended.Count > 0) groups.Insert(0, (StoreGroup.Recommended, recommended));
        shown = groups.Sum(g => g.Rows.Count);
        if (groups.Select(g => g.Key).SequenceEqual(Groups.Select(g => g.Store))
            && groups.Zip(Groups).All(p => p.First.Rows.Select(r => r.Id).SequenceEqual(p.Second.Select(r => r.Id))))
        {
            foreach (var (want, have) in groups.Zip(Groups))
                for (int i = 0; i < want.Rows.Count; i++)
                    if (have[i] != want.Rows[i]) have[i] = want.Rows[i];
            return;
        }
        Groups.Clear();
        foreach (var (store, rows) in groups) Groups.Add(new StoreGroup(store, rows));
    }

    // Capped: AMD's limit is of its DirectX 12 cache alone, which the usage counts with the DirectX 11 one
    void ShowCache((CacheUsage Usage, CacheLimit? Limit, long? Capped) c)
    {
        var (usage, limit, capped) = c;
        CacheUsed = (usage.UpperBound ? "≤ " : "") + Format.Bytes(usage.BytesOnDisk);
        CacheLimitText = limit is null ? "上限未知" : limit.Bytes is { } lb ? capped is { } cb ? $"· DirectX 12 {Format.Bytes(cb)} / {Format.Bytes(lb)} 上限" : $"/ {Format.Bytes(lb)} 上限" : "无上限";
        CachePercent = limit?.Bytes is { } max && max > 0 ? Math.Min(100, 100.0 * (capped ?? usage.BytesOnDisk) / max) : 0;
        CacheWarn = limit?.Bytes is not null && (limit.IsDriverDefault || CachePercent > 75);
        CacheWarnText = limit?.IsDriverDefault == true ? "驱动默认上限可能导致旧游戏缓存被清除" : "缓存即将占满";
        Changed();
    }

    /// <summary>The Recommended section's games that can be queued now (not the ones that need a recording), every row, not the filtered view.</summary>
    List<GameRow> RecommendedToAdd()
    {
        var byId = Games.ToDictionary(r => r.Id);
        return StutterList.Recommended(Games.Select(r => r.State)).Select(s => byId[s.Game.Id]).Where(r => r.IsAdd && !r.Queued).ToList();
    }

    public void AddAllRecommended()
    {
        foreach (var g in RecommendedToAdd()) App.Core.Enqueue(g.Id);
        Refresh();
    }

    public void Play(GameRow row)
    {
        Error = Fmt.Play(row.State.Game);
        Changed();
    }

    public void AddAllReady()
    {
        foreach (var g in Games.Where(r => r.IsAdd && !r.Queued && !r.IsNoStutter).ToList()) App.Core.Enqueue(g.Id);
        Refresh();
    }
}

public sealed class DetailVm(string id) : Bindable
{
    GameState s = App.Core.Games.First(g => g.Game.Id == id);
    public GameRow Row { get; private set; } = new(App.Core.Games.First(g => g.Game.Id == id));
    public string? Error { get; set; }
    public bool HasError => Error != null;

    public void Refresh()
    {
        var now = App.Core.Games.FirstOrDefault(g => g.Game.Id == id);
        if (now != null && s.Playing && !now.Playing) ReadCaches();   // the game exited: its Windows and pipeline caches grew
        if (now != null && (now != s || Row.Icon != Icons.Get(now.Game.ExePath))) { s = now; Row = new GameRow(now); }   // or its icon failed: the tile
        Changed();
    }

    static string Sentence(string t) => t.Length > 0 ? char.ToUpperInvariant(t[0]) + t[1..] : t;
    bool Partial => ScsKiller.IsPartial(s.Plan);
    /// <summary>Needs a recording for its ray tracing only: the rest compiles (a partial compile, offered but not the primary action).</summary>
    bool RtPartial => s.Status == GameStatus.NeedsRecording && ScsKiller.NeedsRtRecording(s);
    /// <summary>Compiled, and a recording would add its ray tracing: the record tip, not the warning callout.</summary>
    bool RtAfter => ScsKiller.RtAfterRecording(s);
    public bool NoAntiCheat => s.AntiCheat == AntiCheat.None;
    /// <summary>The recorder may go next to the game: no anti-cheat, not a game added by hand whose folder isn't confirmed, and
    /// not one whose first d3d12.dll Streamline loads on NVIDIA.</summary>
    bool CanRecord => (NoAntiCheat || s.AntiCheatRecordingAllowed && s.RecorderOverride == RecorderOverride.On) && !s.RootUnconfirmed && !s.StreamlineFirst;

    public string Name => s.Game.Name;
    public string Sub => $"{Path.GetFileName(s.Game.ExePath)} · {Fmt.StoreName(s.Game)}";   // engine and API are tags

    // Status: the state in its colour and why; the actions sit beside it.
    public string StatusGlyph => s.Status switch
    {
        _ when s.CompileUnreached || ScsKiller.IsPartlyCompiled(s) => "",
        GameStatus.Warmed => "", GameStatus.Ready => "", GameStatus.NeedsRecording => "", GameStatus.Stale => "", _ => "",
    };
    public string StatusTitle => Row.StatusText;
    public string StatusReason => Sentence(s.Status switch
    {
        _ when s.CompileUnreached => "编译结果未进入游戏使用的缓存" + (s.WarmedAt != null ? "。可清空此游戏的缓存后重试" : ""),
        _ when ScsKiller.IsPartlyCompiled(s) => $"驱动跳过了 {Failed + Crashed:N0} 条管线，首次在游戏中使用时仍可能卡顿。后续 SCSKiller 更新可能改善此问题",
        GameStatus.Warmed when ScsKiller.IsPartlyWarmed(s) => Format.Reason(s),
        GameStatus.Warmed => $"已为{(ScsKiller.BothApis(s.Plan) ? " DirectX 11 和 DirectX 12、" : "")}驱动 {s.WarmedDriverVersion} 编译" + (s.WarmedAt is { } t ? $"（{t.LocalDateTime:d}）" : "")
            + (RtAfter ? "。光追效果仍需要录制" : ""),
        // a cache here: played since a clear or a stopped compile; then the planner's note when the row has one to say
        GameStatus.Ready => (s.CacheOnDisk > 0 ? $"尚未完整编译：驱动缓存中已有 {Format.Bytes(s.CacheOnDisk)}"
            : s.LastWarmTime == null ? "尚未编译" : "着色器缓存已清空，请重新编译")
            + (Row.Note != null ? "。" + Row.Note : ""),
        GameStatus.NeedsRecording => HasDbTeaser ? Format.Reason(s with { InCommunityDb = null }) + Format.ModNote(s) : Row.FullNote,   // the teaser says it
        GameStatus.Unsupported when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked)
            => $"需要录制，但检测到 {Fmt.AntiCheatName(s.AntiCheat)}。可在下方确认风险后选择直接录制。",
        _ => Format.Reason(s),
    } + (s.Status == GameStatus.NeedsRecording ? "" : Format.ModNote(s)))   // FullNote has it
        + (IsPartlyCompiled || s.CompileUnreached ? "。" : "");
    public bool IsPartlyCompiled => ScsKiller.IsPartlyCompiled(s);
    public const string PartlyCompiledWhy = "SCSKiller 已尝试编译此游戏，但显卡驱动拒绝了其中一部分管线。这可能是因为驱动或 Windows 无法按游戏使用的方式创建管线。被拒绝的管线会在游戏首次使用时自行编译，届时仍可能卡顿。更新驱动后，SCSKiller 会重新尝试编译。";

    // The manifest is public: a PC without "db" sees that the community database covers the game, and where to get it.
    // Shown, it is the page's only mention of the database.
    public bool HasDbTeaser => s is { InCommunityDb: true, Community: null } && !App.Account.HasDb
        && (s.Status == GameStatus.NeedsRecording || RtAfter || s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked));
    public string DbTeaser => $"上游社区数据库中，其他玩家已录制 {s.CommunityDbPsos:N0} 条管线。Patreon 支持者可直接使用这些数据编译。";
    public string DbTeaserLink => App.Account.SignedIn ? "上游 Patreon 会员资格" : "通过上游 Patreon 登录";
    public bool CanCompile => s.Status is GameStatus.Ready or GameStatus.Warmed or GameStatus.Stale || RtPartial;
    public string? GuessNote => s.Engine is { } e && s.Status is GameStatus.Ready or GameStatus.Warmed or GameStatus.Stale ? ScsKiller.GuessNote(e) : null;
    public bool HasGuessNote => GuessNote != null;
    // warmed: nothing urgent, unless only partly (the careful compile is offered); RT: Record comes first; no shader stutter: not needed
    public Style CompileStyle => Fmt.Style((OffersCareful || s.Status != GameStatus.Warmed && !RtPartial) && s.NoStutter == null ? "AccentButtonStyle" : "DefaultButtonStyle");
    bool Queued => App.Core.Queue.Any(q => q.GameId == id && Fmt.Active(q) && !q.PlanCheck);
    public bool CanAdd => !Queued;
    bool CompilingSoon => Fmt.CompilingSoon(App.Core.Queue, id);
    public bool ShowPlay => Row.CanLaunch;
    public bool CanPlay => !s.Playing && !CompilingSoon;
    public string PlayText => s.Playing ? "运行中" : "启动游戏";
    public string PlayTip => s.Playing ? $"{s.Game.Name} 正在运行" : CompilingSoon ? Fmt.CompilingTip : Fmt.PlayVia(s.Game);
    public bool IsManual => s.Game.Store == Store.Manual;
    public void Play()
    {
        Error = Fmt.Play(s.Game);
        Changed();
    }
    public string AddText => Queued ? "已在队列中" : OffersCareful ? "谨慎编译" : RtPartial ? "编译非光追部分" : "编译";
    /// <summary>Partly warmed by a fast compile: the compile button turns the careful compile on first.</summary>
    public bool OffersCareful => ScsKiller.IsPartlyWarmed(s) && s.Careful is { On: false };

    // AMD's careful compile: a switch in the compile card, off while the game is queued (a running compile keeps its schedule)
    public bool HasCareful => s.Careful != null && CanCompile;
    public bool? CarefulPending { get; set; }
    public bool CarefulOn => CarefulPending ?? s.Careful?.On == true;
    public bool CanToggleCareful => CarefulPending == null && !Queued;
    public string CarefulNote => (s.Careful is { Recorded: 0 } ? "分批使用少量线程编译游戏中录制的管线，让驱动保留更多缓存。此游戏尚无录制数据，暂时没有可谨慎编译的内容。"
        : $"分批使用 {ScsKiller.AmdCarefulThreads} 个线程编译游戏中录制的管线，让驱动保留更多缓存；其余管线仍以全速编译。"
          + (s.Careful?.Estimate is { } est ? $" 预计耗时 {Fmt.Duration(est)}" + (s.Careful.On || Fmt.CompileTime(s) is not { } fast ? "。" : $"，普通编译约需 {Fmt.Duration(fast)}。") : ""))
        + (s.Careful?.LaunchCompiled is { } lc ? $" 上次编译后，游戏首次启动仍自行编译了 {lc * 100:0}% 的管线。" : "");
    // Windows' shader cache of the game, read off the UI thread: Clear cache clears it too, so it can be the only cache to clear.
    BackgroundRead<long>? otherCaches;
    long otherBytes;
    public void ReadCaches() => (otherCaches ??= new(OtherCacheBytes, b => { otherBytes = b; Changed(); })).Request();
    long OtherCacheBytes()
    {
        try { return App.Core.GameCaches(id).Where(p => p.Name != ScsKiller.DriverPart).Sum(p => p.Bytes); }
        catch (Exception) { return 0; }   // e.g. the game is gone after a rescan
    }
    public bool HasCache => s.CacheOnDisk != null || otherBytes > 0 || s.Status is GameStatus.Warmed or GameStatus.Stale;
    public bool ShowClearCache => HasCache || CanCompile;
    // the driver cache, as Disk space shows it; the dialog lists Windows' cache with its own size
    public string ClearCacheText => s.CacheOnDisk != null ? $"清空缓存（{DiskValue}）" : "清空缓存";
    public string ClearCacheTip => HasCache ? "删除此游戏的着色器缓存（驱动和 Windows 缓存；可选择连游戏自带缓存一起删除），下次启动将重新生成"
        : "SCSKiller 首次编译此游戏或检测到其运行时，会识别对应的驱动缓存文件";

    public bool IsKnownStutter => s.KnownStutter != null;
    public string StutterReason => s.KnownStutter?.Reason ?? "";
    public bool IsNoStutter => s.NoStutter != null;
    public string NoStutterReason => s.NoStutter is { } reason ? Format.NoStutterReason(reason) : "";
    /// <summary>The source as a link when it's a URL; otherwise <see cref="StutterCitation"/> shows it as text.</summary>
    public Uri? StutterUri => Uri.TryCreate(s.KnownStutter?.Source, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u : null;
    public string StutterLinkText => StutterUri != null ? $"来源（{s.KnownStutter!.Date}）" : "";
    public string StutterCitation => s.KnownStutter is { } k && StutterUri == null ? $"来源：{k.Source}（{k.Date}）" : "";
    public string EngineChip => s.Engine is { } e ? Format.Engine(e, e.Family == "Unreal" ? "Unreal Engine" : null) + Fmt.Guessed(e) + (e.Fork != null ? " · 定制分支" : "") : "未知引擎";
    public string ApiChip => s.Engine?.GraphicsApi.Replace("D3D", "DirectX ") ?? "";
    public bool HasApi => ApiChip.Length > 0;
    public string AntiCheatChip => NoAntiCheat ? "未检测到反作弊" : Fmt.AntiCheatName(s.AntiCheat);
    public string AntiCheatTip => NoAntiCheat ? "未发现反作弊：可以录制此游戏"
        : $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。在下方明确确认风险后，可选择启用录制；游戏仍可能拒绝注入或封禁账号。";
    public bool HasMiddleware => s.Middleware is { Count: > 0 };
    IReadOnlyList<MiddlewareTag> Mw => s.Middleware ?? [];
    /// <summary>"Also compiles" only names tags with known pipelines (and DLSS, by the driver); else what was detected.</summary>
    public string MiddlewareChip => Mw.Where(t => t.Pipelines > 0).Select(t => t.Label)
            .Concat(Mw.Any(t => t.Label == "DLSS") ? ["DLSS（由驱动编译）"] : []).ToList() is { Count: > 0 } also
        ? "同时编译：" + string.Join(" · ", also) : "已检测到：" + string.Join(" · ", Mw.Select(t => t.Label));
    public string MiddlewareTip => "游戏目录中的超分辨率组件 DLL。任意游戏的录制识别该 DLL 版本后，就能随游戏一起编译其管线"
        + "；本机录制可识别，FSR 和 XeSS 也可使用其他玩家分享的数据"
        + (s.Plan is { MiddlewareItems: > 0 } p ? $"（本次计划包含 {Fmt.N(p.MiddlewareItems)} 条）" : "") + "。\n\n"
        + string.Join("\n", Mw.Select(Fmt.TagTip));

    // The one tip left: a game that can't compile before a recording, or not its ray tracing (a plan's other gaps are the
    // coverage card's "what's left").
    public bool HasRecordTip => CanRecord && (s.Status == GameStatus.NeedsRecording || RtAfter) && !RecordOn && !s.RecorderEffective && s.NoStutter == null;
    bool Enough => ScsKiller.RecordedEnough(s);
    public string RecordTipTitle => Enough ? "继续录制游玩过程" : "录制约 5 分钟游玩过程";
    public string RecordTip => RtPartial || RtAfter ? $"开启录制并启用光线追踪游玩{(Enough ? "" : "约 5 分钟")}。" + ScsKiller.RtWhy(App.Core.Vendor.Caps, s.Engine)
            + (RtAfter ? "其他部分已编译。" : "其他部分已经可以从游戏文件编译。")
        : Enough ? "继续开启录制并游玩，让 SCSKiller 收集尚缺的管线。"
        : "开启录制并正常游玩约 5 分钟。SCSKiller 会据此识别游戏的着色器布局，随后即可编译。";
    public bool ShowRecordAction => !RecordOn && !s.RecorderEffective && CanToggleRecord;

    // Shader coverage (PlanStats), in words: how much of what the planner found gets compiled, where it comes from, what's
    // left and what helps; every count in Details. No plan yet on a game that can compile: the card says when it's measured.
    PlanStats? P => s.Plan;
    int? StalePct => ScsKiller.CompiledOfPlanPercent(s);   // "Needs rebuilding" with a count: what the last compile covers of the plan now
    int? CompiledPct => ScsKiller.CompiledPercent(s) ?? StalePct;
    // a compile that didn't help: the ring is greyed out, nothing of it counts for the game
    int? Pct => s.CompileUnreached ? null : CompiledPct ?? ScsKiller.CoveragePercent(P);
    public bool HasCoverageCard => HasPlan || CanCompile;
    public bool HasPlan => P != null;
    public bool HasMeter => Pct != null;
    public bool LowCoverage => !s.CompileUnreached && (Pct is { } pct ? pct < 90 : Partial);   // more than 10% left out, like ScsKiller.IsPartial (the test for a plan without the count)
    public bool GoodCoverage => HasMeter && !LowCoverage;
    bool Dx11Only => P is { StageSets: 0, Generated: 0, D3D11Shaders: > 0 };
    public string CoverageHeadline => P == null ? "首次编译开始后即可得知覆盖情况"
        : s.CompileUnreached ? "编译结果未被此游戏使用"
        : StalePct is { } part ? $"已编译计划中约 {part}% 的管线；重新编译可加入其余部分"
        : CompiledPct is { } done ? $"驱动已编译计划中约 {done}% 的管线"
        : Dx11Only ? (Compiled ? "已覆盖" : "将覆盖") + "游戏文件中的全部 DirectX 11 着色器"
        : Pct is not { } pct ? "下次编译时重新测量"
        : (s.Status == GameStatus.Stale ? s.NewPipelines > 0 ? "新计划覆盖" : "上次计划覆盖" : Compiled ? "已覆盖" : "将覆盖")
          + (pct == 100 ? "此游戏已发现的全部着色器组合" : $"此游戏已发现的约 {pct}% 着色器组合");
    bool Rt => ScsKiller.NeedsRtRecording(s);
    bool RtUnseen => s.RtUnseen;
    bool RtInline => ScsKiller.RtInlineCovers(P);
    bool Compiled => s.WarmedAt != null && s.Status != GameStatus.Stale;

    /// <summary>Where the plan's pipelines come from, in a player's words; the recording's row carries the community note,
    /// and without one the community database's status is its own row. Ray tracing: covered or what it needs.</summary>
    public IReadOnlyList<DetailRow> Sources => P is not { } p ? [] : new DetailRow?[]
    {
        p.Generated > 0 ? new(ScsKiller.BothApis(p) ? "DirectX 12：来自游戏文件" : "来自游戏文件", Fmt.N(p.Generated)) : null,
        p.Recorded > 0 ? new(s.Community switch
        {
            null => "本机游玩录制",
            { WithLocalRecording: true } => "本机录制与上游社区数据库",
            _ => "上游社区数据库",
        }, Fmt.N(p.Recorded), CommunityLine) : null,
        s.Community == null && s.InCommunityDb is { } inDb && !HasDbTeaser ? new("上游社区数据库", inDb ? "已有此版本的录制数据" : "尚无此版本的数据") : null,
        p.MiddlewareItems > 0 ? new(p.MiddlewareSharedItems == 0 ? "超分辨率组件：来自录制"
            : p.MiddlewareSharedItems == p.MiddlewareItems ? "超分辨率组件：来自共享数据包" : "超分辨率组件：来自录制与共享数据包", Fmt.N(p.MiddlewareItems)) : null,
        p.D3D11Shaders > 0 ? new(ScsKiller.BothApis(p) ? "DirectX 11：来自游戏文件" : "DirectX 11 着色器", Fmt.N(p.D3D11Shaders)) : null,
        p.RtLibraries > 0 ? new("光线追踪效果", s.Engine?.NoRayTracing == true ? "此游戏未启用" : s.Engine?.NoRtPipelines == true ? "内联光追：来自游戏文件" : p.RtUncovered == 0 ? "已覆盖" : s.RtToPlan ? "正在检查" : RtUnseen ? "录制时未观察到" : RtInline ? "已覆盖内联部分" : !Rt ? "大部分已覆盖" : CanRecord ? "需要录制" : "尚未编译") : null,
    }.OfType<DetailRow>().ToList();
    public bool HasSources => Sources.Count > 0;
    /// <summary>The community database's line under the recording row; null = nothing to say.</summary>
    string? CommunityLine => s.Community is { } c
        ? $"已于 {c.DownloadedAt.LocalDateTime:d} 下载社区录制数据" + (s.RecordingSharedAt is { } at ? $" · 本机数据于 {at.LocalDateTime:d} 分享" : "")
        : s.RecordingSharedAt is { } at2 ? $"已于 {at2.LocalDateTime:d} 分享至上游社区数据库" : null;

    // What's left and what helps. LeftOut is mostly Uncovered (root signature slots SCSKiller can't build) or, with a learned
    // lookup, stage sets no recording showed yet (no root signature / template): a longer recording is what helps there.
    // Ray tracing the plan can't compile comes first, in the status's words (ScsKiller.RtNote).
    enum Left { AntiCheat, EngineSlots, UnknownSlots, NotSeen, Recording, PlayedClean, PlayedCompiles, NothingKnown, PlayOnly }
    long Missing => P is { } m ? Math.Max(m.LeftOut, m.Uncovered) : 0;   // a plan without LeftOut: Uncovered only
    Left LeftCase => !CanRecord ? Left.AntiCheat
        : Missing > 0 ? P!.Uncovered * 2 >= Missing ? P.Recorded == 0 ? Left.EngineSlots : Left.UnknownSlots : Left.NotSeen
        : RecordOn ? Left.Recording
        : L is { } l ? PlayCompiles(l) == 0 ? Left.PlayedClean : Left.PlayedCompiles   // what was measured beats what's likely
        : P is { Recorded: > 0 } ? Left.NothingKnown
        : Left.PlayOnly;
    public string LeftText => LeftSentences(tipAsks: false);
    /// <summary>tipAsks: the record tip already gives the ray tracing note and the recording advice.</summary>
    string LeftSentences(bool tipAsks) => string.Join(" ", new string?[]
    {
        RtUnseen ? "录制中尚未观察到光追效果。" : RtInline ? "内联光追效果仍需录制。" : !Rt || tipAsks ? null : CanRecord ? "光追效果尚未编译，需要开启录制并游玩。"
            : !NoAntiCheat ? $"光追效果尚未编译；检测到 {Fmt.AntiCheatName(s.AntiCheat)}，请先确认录制风险。"
            : s.RootUnconfirmed ? "光追效果尚未编译；请先确认游戏目录以启用录制。" : "光追效果尚未编译；此游戏的 DLSS 加载顺序阻止录制器工作。",
        LeftCase switch
        {
            Left.AntiCheat => !NoAntiCheat ? $"其余管线仍会在游玩时编译。检测到 {Fmt.AntiCheatName(s.AntiCheat)}；确认风险并启用录制后，SCSKiller 才能尝试收集。"
                : s.RootUnconfirmed ? "其余管线仍会在游玩时编译。请先在上方确认游戏目录，以便录制缺失内容。"
                : "其余管线仍会在游玩时编译。NVIDIA DLSS 的加载顺序使录制器无法收集它们。",
            Left.EngineSlots => "其余部分使用游戏引擎新增的着色器槽位。" + (tipAsks ? "" : "录制约 5 分钟可帮助 SCSKiller 重建这些管线。"),
            Left.UnknownSlots => "其余部分使用 SCSKiller 暂时无法重建的着色器槽位，因此仍会在游玩时编译。",
            Left.NotSeen => "SCSKiller 尚未观察到游戏如何设置其余部分。" + (tipAsks ? "" : "延长录制游玩时间可补全信息。"),
            Left.Recording => "录制已开启：新收集的内容会在下次编译时加入。",
            Left.PlayedCompiles => $"上次游玩仍有 {PlayCompiles(L!):N0} 条管线临时编译。" + (tipAsks ? "" : "开启录制可收集它们。"),
            _ when Rt || RtUnseen || RtInline => null,   // ray tracing is what's left
            Left.PlayedClean => "上次游玩未发现缺失的管线。",
            Left.NothingKnown => "已知内容没有缺失。",
            _ => "少数效果只在游玩时组合生成。" + (tipAsks ? "" : "短时间录制即可收集。"),
        },
        !Guessed ? null : tipAsks ? "部分着色器状态仍是推测值。" : "部分着色器状态仍是推测值；延长录制时间可提高准确性。",
    }.OfType<string>());
    // per-stage plans (AMD): the planner warns that a wrong guess costs a compile in game
    bool Guessed => P is { } g && g.GuessedUnits > 0.1 * (g.ExactUnits + g.InferredUnits + g.GuessedUnits);
    // not twice: a game that needs a recording has the button in its tip
    public bool ShowLeftRecord => ShowRecordAction && !HasRecordTip && s.NoStutter == null && (LeftCase is Left.EngineSlots or Left.NotSeen or Left.PlayedCompiles or Left.PlayOnly || Guessed || Rt || RtUnseen || RtInline);
    public bool ShowLeftCallout => HasPlan && ShowLeftRecord;   // before the first compile there's nothing left yet
    public string LeftTitle => Rt || RtUnseen ? "光追效果尚未编译" : "部分着色器仍会在游玩时编译";

    public string CoverageValue => Pct is { } pct ? $"{pct}%" : Dx11Only && !s.CompileUnreached ? "全部" : Format.Dash;
    public string CoverageLabel => s.CompileUnreached ? "未生效" : CompiledPct != null ? "已编译" : Pct != null || Dx11Only ? "覆盖" : "尚无数据";
    // A geometry can't be shared between two Paths: each arc gets its own (120 px box, 12 px stroke).
    public Geometry? GoodArc => GoodCoverage ? Arc() : null;
    public Geometry? LowArc => LowCoverage ? Arc() : null;
    Geometry Arc()
    {
        const double c = 60, r = 54;
        double a = Math.Min(Pct ?? 0, 99.99) / 100 * 2 * Math.PI;
        var figure = new PathFigure { StartPoint = new(c, c - r) };
        figure.Segments.Add(new ArcSegment { Point = new(c + r * Math.Sin(a), c - r * Math.Cos(a)), Size = new(r, r), IsLargeArc = a > Math.PI, SweepDirection = SweepDirection.Clockwise });
        var g = new PathGeometry();
        g.Figures.Add(figure);
        return g;
    }
    public bool HasTimeValue => Fmt.CompileTime(s) != null;
    public string TimeLabel => s.LastWarmTime != null ? "编译耗时" : "预计编译耗时";
    public string TimeValue => s.LastWarmTime is { TotalMinutes: >= 1, TotalHours: < 1 } t ? $"{t.Minutes} 分 {t.Seconds} 秒"   // measured: to the second
        : Fmt.Duration(Fmt.CompileTime(s));
    public bool HasDiskValue => s.CacheOnDisk != null || s.EstimatedCacheBytes != null;
    public string DiskLabel => s.CacheOnDisk != null ? "磁盘占用" : "预计磁盘占用";
    public string DiskValue => Format.Bytes(s.CacheOnDisk ?? s.EstimatedCacheBytes);
    public bool HasShaderCount => s.ShaderCount != null;
    public string ShaderCount => Fmt.N(s.ShaderCount);
    // measured frames when the recorder timed them, else the compile count
    long? StutterCount => F != null ? ShaderHitches.Count : L is { } l ? PlayCompiles(l) : null;
    public string StuttersValue => StutterCount is not { } n ? "" : n == 0 ? "无" : Fmt.N(n);
    public bool HasStutters => StutterCount > 0;
    public bool NoStutters => StutterCount == 0;
    // without frame times the count is of compiles, which may or may not have stuttered
    public string StuttersLabel => F != null ? "上次游玩的卡顿次数" : "游玩期间编译的管线";

    /// <summary>Every count, for power users.</summary>
    public IReadOnlyList<DetailRow> Details => new DetailRow?[]
    {
        P is { } p ? new("计划中的管线", Fmt.N(p.Recorded + p.Generated + p.MiddlewareItems)) : null,
        s.ShaderCount != null ? new("游戏文件中的着色器", Fmt.N(s.ShaderCount)) : null,
        P is { StageSets: > 0 } ? new("发现的着色器组合", Fmt.N(P.StageSets)) : null,
        P is { LeftOut: > 0 } ? new("未纳入计划", Fmt.N(P.LeftOut) + (P.Uncovered == P.LeftOut ? "（SCSKiller 暂无法重建的着色器槽位）" : P.Uncovered > 0 ? $"（其中 {P.Uncovered:N0} 条因着色器槽位暂无法重建）" : "")) : null,
        P is { StageSets: 0, Uncovered: > 0 } ? new("因着色器槽位无法重建而遗漏", Fmt.N(P.Uncovered)) : null,
        P != null ? new("已录制的管线", Fmt.N(P.Recorded)) : null,
        P != null ? new("从游戏文件构建", Fmt.N(P.Generated)) : null,
        P is { MiddlewareItems: > 0 } ? new("超分辨率组件管线", Fmt.N(P.MiddlewareItems)) : null,
        P is { D3D11Shaders: > 0 } ? new("DirectX 11 着色器", Fmt.N(P.D3D11Shaders)) : null,
        P is { RtLibraries: > 0 } ? new("光追着色器库", $"{P.RtLibraries:N0}" + (s.Engine?.NoRtPipelines == true ? "（游戏未使用）" : P.RtUncovered > 0 ? $"（{P.RtUncovered:N0} 条未编译）" : "")) : null,
        P is { RtInline: > 0 } ? new("内联光追着色器", Fmt.N(P.RtInline)) : null,
        s.InCommunityDb is { } db && !HasDbTeaser ? new("上游社区数据库", db ? $"{s.CommunityDbPsos:N0} 条管线" : "尚无数据") : null,
        s.Community is { } c ? new("社区录制数据", $"{c.Psos:N0} 条管线") : null,
        P != null ? new("合成的管线模板", Fmt.N(P.SynthesizedTemplates)) : null,
        P != null ? new("着色器布局", $"{P.RootSignatures:N0} · {RootSigSource(P)}") : null,
        P is { } u && u.ExactUnits + u.InferredUnits + u.GuessedUnits > 0 ? new("阶段单元：精确 / 推断 / 推测", $"{u.ExactUnits:N0} / {u.InferredUnits:N0} / {u.GuessedUnits:N0}") : null,
        P is { LayoutCoverage: > 0 } ? new("录制得到的顶点布局覆盖率", $"{P.LayoutCoverage:P1}") : null,
        s.CacheOnDisk != null || s.EstimatedCacheBytes != null ? new("驱动缓存", Fmt.Cache(s) + (s.CacheOnDisk != null ? "（实测）" : "（估计）")) : null,
    }.OfType<DetailRow>().ToList();


    // "Shipped with the shaders" is the carver's case.
    string RootSigSource(PlanStats p) =>
        p.RootSigRuleVerified ? "已在此引擎版本验证"
        : p.RootSignatures == 0 ? "不需要（DirectX 11）"
        : s.Engine?.Family == "Unreal" ? (p.Recorded > 0 ? "来自录制" : "尚未在此引擎版本验证")
        : p.Recorded > 0 ? "来自录制" : "随着色器提供";

    // Last compile: when, how long, what failed or was skipped and what to do; never compiled: what the first one costs.
    public bool HasCompileCard => CanCompile || s.LastWarmTime != null;
    public string CompileTitle => s.LastWarmTime == null ? "首次编译" : "上次编译";
    long Failed => s.LastWarmFailed ?? 0;
    long Skipped => s.LastWarmSkipped ?? 0;
    long Crashed => s.LastWarmCrashed ?? 0;
    public string CompileText => s.WarmedAt is { } at
        ? $"编译时间：{Fmt.When(at)}" + (s.LastWarmTime is { } t ? $"；耗时 {Fmt.Duration(t)}" : "") + $"；适用驱动 {s.WarmedDriverVersion}。"
          + (s.LastWarmFailed != null && Failed == 0 && Skipped == 0 && Crashed == 0 ? "没有失败项。" : "")   // null = not known (a warm from before these were kept): no claim
          + (Failed > 0 ? $"驱动跳过了 {Failed:N0} 个组合。" : "")
          + (s.CacheOnDisk is { } c ? $"占用磁盘 {Format.Bytes(c)}。" : "")
        : s.LastWarmTime is { } last ? $"着色器缓存已清空。上次编译耗时 {Fmt.Duration(last)}。"
        : (s.EstimatedWarmTime is { } est ? $"预计耗时 {Fmt.Duration(est)}，编译在后台运行，可继续使用电脑。" : "编译在后台运行，可继续使用电脑。")
          + (s.EstimatedCacheBytes is { } b ? $"预计增加约 {Format.Bytes(b)} 驱动着色器缓存。" : "");
    public string SkippedTitle => $"跳过 {Skipped + Crashed:N0} 条";
    long NeedsRecording => Math.Min(Skipped, s.LastWarmNeedsRecording ?? 0);   // of Skipped: flagged by the community recording
    long NotInGame => Skipped - NeedsRecording;
    public string? SkippedText => s.WarmedAt == null || Skipped + Crashed == 0 ? null
        : string.Join(" ", new[]
        {
            NotInGame > 0 ? $"跳过 {NotInGame:N0} 条：其着色器不在当前游戏版本中，无需处理。" : null,
            Crashed > 0 ? $"跳过 {Crashed:N0} 条：编译会使显卡驱动崩溃。若游戏需要，它会自行编译。" : null,
            NeedsRecording > 0 ? $"有 {NeedsRecording:N0} 条需要在本机录制：游戏或模组运行时才会生成其着色器，游戏文件中没有。开启“游玩时录制”并游玩以收集。" : null,
        }.OfType<string>());
    public bool HasSkipped => SkippedText != null;

    // Last play session (SessionStats, no timestamp): one bar, three segments, under the Record switch. Ready = from the
    // game's library or the driver cache; ray tracing state objects have their own line, so Requests isn't used.
    SessionStats? L => s.LastSession;
    public bool HasSession => L != null;
    public bool ShowSessionHint => L == null && CanRecord;
    public string SessionHeader => L is { } l ? $"上次游玩 · {Fmt.Duration(F?.Duration ?? l.Duration)}" : "";
    // ray tracing state objects included, so the headline agrees with the ray tracing line under it
    static long PlayCompiles(SessionStats l) => l.Compiles + l.StateObjectsCompiled;
    public string SessionVerdict
    {
        get
        {
            if (L is not { } l) return "";
            long play = PlayCompiles(l), started = l.StartupCompiles + l.StateObjectsStartupCompiled, ready = l.FromGameLibrary + l.CacheHits + l.StateObjectsReady;
            if (play + started == 0)
                return ready > 0 ? $"游玩期间没有临时编译：游戏使用的 {ready:N0} 个着色器组合均已准备好。" : "游玩期间没有临时编译。";
            // the longest compile call, of the PSOs only; whether a frame stuttered is the frame report's to say
            string worst = l.StateObjectsCompiled > 0 ? "" : play == 1 ? $"（耗时 {l.WorstCompileMs:0} 毫秒）" : $"（最慢一条耗时 {l.WorstCompileMs:0} 毫秒）";
            return (play == 0 ? "游玩期间没有临时编译。" : $"游玩期间临时编译了 {play:N0} 条管线{worst}。")
                + (started > 0 ? $"启动或加载期间另编译了 {started:N0} 条。" : "")
                + (ready > 0 ? $"其余 {ready:N0} 条已准备好。" : "");
        }
    }
    public bool HasStateObjectLine => L is { } l && l.StateObjectsReady + l.StateObjectsCompiled + l.StateObjectsStartupCompiled > 0;
    public string StateObjectLine => L is { } l ? $"光追管线：{l.StateObjectsReady:N0} 条已准备好，{l.StateObjectsCompiled:N0} 条在游玩时编译"
        + (l.StateObjectsStartupCompiled > 0 ? $"，{l.StateObjectsStartupCompiled:N0} 条在启动或加载时编译。" : "。") : "";
    public bool HasRayQueryLine => L?.RayQueryRecompiles > 0;
    public string RayQueryLine => L is { RayQueryRecompiles: > 0 and var n }
        ? $"有 {n:N0} 条光追管线每次启动时会被驱动部分重新编译。" : "";

    // The last launch's frame times (FrameLog): its shader stutters, other hitches and loading, the 1% low of play.
    public FrameReport? F => s.LastFrames;
    List<Hitch> ShaderHitches => F?.Hitches.Where(h => h.Cause == HitchCause.Shader).ToList() ?? [];
    public bool HasFrames => F != null;
    public string FramesVerdict => F is not { } f ? "" : FramesText(f, ShaderHitches);
    public string GraphEnd => Fmt.Duration(F?.Duration);
    public string FramesSummary => F is not { } f ? ""
        : $"启动阶段（游戏自身预编译着色器和首次加载）：{Fmt.Duration(f.Startup)}。游玩期间：{InPlay(f).Count(h => h.Ms >= 50):N0} 帧耗时至少 50 毫秒，"
          + $"{InPlay(f).Count(h => h.Ms >= 100):N0} 帧至少 100 毫秒。帧时间数据只保留在本机。";
    // the same list while the report is the same: a new one makes the slow-frames list rebuild every row at each refresh
    public IReadOnlyList<DetailRow> HitchRows => hitchRows.Report == F ? hitchRows.Rows
        : (hitchRows = (F, F?.Hitches.Select(h => new DetailRow($"{Clock(h.At)}  {Cause(h.Cause)}", Ms(h.Ms))).ToList() ?? [])).Rows;
    (FrameReport? Report, IReadOnlyList<DetailRow> Rows) hitchRows = (null, []);
    public static string HitchTip(Hitch h) => $"{Clock(h.At)} · {Ms(h.Ms)} · {Cause(h.Cause)}";
    static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    public bool HasHitchRows => F?.Hitches.Count > 0;

    // the frames of play, between startup and quitting, whatever slowed them (a load in play too)
    static IEnumerable<Hitch> InPlay(FrameReport f) => f.Hitches.Where(h => FrameLog.InPlay(h.At.TotalMilliseconds, h.Ms, f.Startup.TotalMilliseconds) && h.Cause != HitchCause.Quitting);
    static string Ms(double ms) => ms >= 1000 ? $"{ms / 1000:0.0} 秒" : $"{ms:0} 毫秒";
    static string Cause(HitchCause c) => c switch { HitchCause.Shader => "着色器编译", HitchCause.Other => "其他卡顿", HitchCause.Quitting => "退出游戏",
        HitchCause.LoadingShaders => "加载并编译着色器", _ => "加载" };

    /// <summary>"5 min: 3 stutters from shader compiles (worst 1.3 s), 2 other hitches, 1% low 35 fps. Loading: 4 slow
    /// frames compiling shaders (worst 9.4 s), 12 others." (compiling shaders: startup only)</summary>
    public static string FramesText(FrameReport f, IReadOnlyList<Hitch> shader)
    {
        int other = f.Hitches.Count(h => h.Cause == HitchCause.Other), loading = f.Hitches.Count(h => h.Cause == HitchCause.Loading);
        var compiling = f.Hitches.Where(h => h.Cause == HitchCause.LoadingShaders).ToList();
        return $"{Fmt.Duration(f.Duration)}：" + (shader.Count == 0 ? "无着色器编译卡顿"
                : $"{shader.Count:N0} 次着色器编译卡顿（最慢 {Ms(shader.Max(h => h.Ms))}）")
            + (other == 0 ? "，无其他卡顿" : $"，其他卡顿 {other:N0} 次")
            + $"，最低 1% 帧率 {f.Low1PctFps:0} fps。"
            + (compiling.Count + loading == 0 ? "" : "加载阶段：" + string.Join("，", new[]
            {
                compiling.Count == 0 ? null : $"{compiling.Count:N0} 帧因着色器编译而变慢（最慢 {Ms(compiling.Max(h => h.Ms))}）",
                loading == 0 ? null : compiling.Count == 0 ? $"{loading:N0} 帧变慢" : $"另有 {loading:N0} 帧变慢",
            }.OfType<string>()) + "。") ;
    }

    public GridLength LibraryWidth => new(L?.FromGameLibrary ?? 0, GridUnitType.Star);
    public GridLength HitsWidth => new(L?.CacheHits ?? 0, GridUnitType.Star);
    public GridLength CompilesWidth => new(L?.Compiles ?? 0, GridUnitType.Star);
    public double CompilesMinWidth => L?.Compiles > 0 ? 4 : 0;
    public string FromLibrary => Fmt.N(L?.FromGameLibrary);
    public string CacheHits => Fmt.N(L?.CacheHits);
    public string Compiles => Fmt.N(L?.Compiles);
    public string SessionLabel => L is { } l
        ? $"{l.Requests:N0} 次管线请求：{l.FromGameLibrary:N0} 条来自游戏自带库，{l.CacheHits:N0} 条已在驱动缓存中，{l.Compiles:N0} 条在游玩期间编译"
          + (l.StartupCompiles > 0 ? $"，{l.StartupCompiles:N0} 条在启动或加载时编译" : "")
          + (l.RayQueryRecompiles > 0 ? $"，{l.RayQueryRecompiles:N0} 条光追管线每次启动时被驱动部分重新编译" : "")
          + (l.StateObjectsReady + l.StateObjectsCompiled + l.StateObjectsStartupCompiled > 0 ? $"；光追管线 {l.StateObjectsReady:N0} 条已准备好，{l.StateObjectsCompiled:N0} 条在游玩期间编译" : "")
          + (l.StateObjectsStartupCompiled > 0 ? $"，{l.StateObjectsStartupCompiled:N0} 条在启动或加载时编译" : "")
        : "";

    public bool? RecordPending { get; set; }   // the state a recorder change that is running (off the UI thread) asked for
    public bool RecordOn => ShowOffline ? OfflineOn : RecordPending ?? s.RecorderEffective;
    public bool ShowStandardRecord => NoAntiCheat || ShowOffline;
    public string StandardRecordTitle => ShowOffline ? "允许离线录制会话" : "游玩时录制";
    public bool CanToggleRecord => ShowOffline ? OfflinePending == null && !s.OfflineRunning
        : RecordPending == null && AlongsidePending == null && NoAntiCheat && s.RecorderSkip == null;
    public bool ShowUseDefault => NoAntiCheat && RecordPending == null && s.RecorderOverride != RecorderOverride.Default && s.RecorderSkip == null;
    public string UseDefaultText => $"使用默认设置（{(App.Core.Settings.RecordAllGames && s.NoStutter == null ? "开启" : "关闭")}）";
    public string RecordNote => ShowOffline
        ? $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。此开关允许使用特定离线会话录制；下方还可单独选择直接录制，两种方式都存在风险。"
        : !NoAntiCheat
        ? $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。如需直接录制，请使用下方的单独开关并确认风险。"
        : s.RecorderSkip == ScsKiller.SkipManual ? "请先确认上方的游戏目录；SCSKiller 会在录制前检查整个目录中的反作弊组件。"
        : s.RecorderSkip == ScsKiller.SkipModNotChainable ? $"当前无法录制：{Fmt.RecorderSkip(s.RecorderSkip)}。{ScsKiller.NotChainableReason(s.RecorderMod)}"
        : s.RecorderSkip is { } skip ? $"当前无法录制：{Fmt.RecorderSkip(skip)}。"
        : "在游戏目录旁放置一个小型 d3d12.dll，录下编译计划遗漏的内容并统计帧时间，以便在这里查看卡顿。可随时关闭。若游戏有未被检测到的反作弊，请勿开启。"
          + (s.RecorderNote is { } note ? $"（{Fmt.RecorderNote(note)}）" : "")
          + (s.RecorderRefused is { } why && !(s.Status == GameStatus.NeedsRecording && ScsKiller.NeverRecorded(s)) ? " " + Fmt.RecorderRefused(why) : "");   // else the status says it

    public bool ShowAntiCheatRecording => !NoAntiCheat;
    public bool? AntiCheatRecordPending { get; set; }
    public bool AntiCheatRecordOn => AntiCheatRecordPending ?? s.AntiCheatRecordingAllowed && s.RecorderOverride == RecorderOverride.On;
    public bool CanToggleAntiCheatRecord => AntiCheatRecordPending == null && RecordPending == null && !s.RootUnconfirmed;
    public string AntiCheatRecordNote => $"检测到 {Fmt.AntiCheatName(s.AntiCheat)}。启用后，SCSKiller 会尝试把 d3d12.dll 放入游戏目录并在启动时录制。"
        + "游戏的反作弊可能拒绝运行、判定为篡改或封禁账号。本选项只解除 SCSKiller 自身的门禁，不会关闭或绕过游戏反作弊。"
        + (s.AntiCheatRecordingAllowed && s.RecorderSkip is { } skip ? $" 当前无法安装录制器：{Fmt.RecorderSkip(skip)}。" : "")
        + (s.RecorderNote is { } note ? $" 录制器状态：{Fmt.RecorderNote(note)}。" : "")
        + (s.RecorderRefused is { } refused ? " " + Fmt.RecorderRefused(refused) : "");

    // A mod's d3d12.dll where the recorder goes (ReShade, a wrapper): off = the game isn't recorded; on = the recorder chains to it
    public bool HasMod => s.RecorderMod != null && s.RecorderSkip is not (ScsKiller.SkipAntiCheat or ScsKiller.SkipShaderMod or ScsKiller.SkipManual or ScsKiller.SkipUnsupported or ScsKiller.SkipNotDx12 or ScsKiller.SkipStreamline);
    public string AlongsideTitle => $"与 {s.RecorderMod} 一同录制";
    public string AlongsideNote => $"录制器安装期间会临时重命名 {s.RecorderMod} 的 d3d12.dll；移除录制器时会原样恢复。";
    public bool? AlongsidePending { get; set; }
    public bool AlongsideOn => AlongsidePending ?? s.RecordAlongsideMod;
    public bool CanToggleAlongside => AlongsidePending == null && RecordPending == null;

    // An eligible EasyAntiCheat game (Games.OfflineEac): the card's switch allows offline sessions, each confirmed before it starts
    public bool ShowOffline => s.OfflineEligible;
    public bool? OfflinePending { get; set; }
    public bool OfflineOn => OfflinePending ?? s.OfflineRecord;
    public bool ShowOfflineOn => ShowOffline && OfflineOn;
    public bool OfflineStarting { get; set; }
    public bool CanStartOffline => s.OfflineRecord && OfflinePending == null && !s.OfflineRunning && !s.Playing && !OfflineStarting;
    public string OfflineButtonText => s.OfflineRunning ? "离线会话运行中…" : "离线启动并录制（自行承担风险）";
    public string OfflineRiskText => OfflineRisk;
    public const string OfflineRisk = "游戏将以不加载 EasyAntiCheat 的离线模式启动。退出游戏后 SCSKiller 会移除相关文件；如果游戏崩溃，则会在下次登录时清理。"
        + "在文件尚未清理完毕时联网游玩可能导致封禁。Steam 必须运行；会话期间不要卸载 SCSKiller。";

    public bool HasRecording => s.RecordingBytes > 0;
    public string RecordingSize => $"录制数据占用 {Format.Bytes(s.RecordingBytes)}（游戏目录与 SCSKiller 副本合计）";
    public bool RecordingPaused => s.RecordingPaused;
    public string RecordingPausedText => "录制已达到容量上限并暂停。请在设置中提高上限，或清空录制数据。";
    public bool ClearingRecording { get; set; }
    public bool CanClearRecording => !ClearingRecording && !s.Playing;
    public string ClearRecordingTip => s.Playing ? "请先退出游戏" : "删除已录制的管线，录制器仍会保留";

    public bool Playing => s.Playing;
    public bool Refreshing { get; private set; }
    public bool CanRefresh => !Refreshing;
    public async void RefreshGame()
    {
        if (Refreshing) return;
        Refreshing = true;
        Changed();
        try { await Task.Run(() => App.Core.RefreshGame(id)); Error = null; }
        catch (Exception e) { Error = e.Message; }
        Refreshing = false;
        ReadCaches();
        Refresh();
    }
}


/// <summary>A label and its value (the detail page's sources and Details); Note: a line under the label.</summary>
public sealed record DetailRow(string Label, string Value, string? Note = null)
{
    public bool HasNote => Note != null;
}

/// <summary>A queue row: the running item (1), a waiting item (2..) or a finished one (Number 0).</summary>
public sealed record QueueRow(string Id, int Number, string Name, string Exe, string Note, string Time, string Cache, QueueStage Stage,
    bool First = false, bool Last = false)
{
    public ImageSource? Icon { get; } = Icons.Get(Exe);   // a field: an icon that failed makes the row unequal, so Sync replaces it
    public bool NoIcon => Icon is null;
    public Brush Tile => GameRow.TileOf(Name);
    public string Initials => Name.Length > 0 ? GameRow.InitialsOf(Name) : "";
    public bool CanUp => !First;
    public bool CanDown => !Last;
    public string Glyph => Stage switch { QueueStage.Done => "\uE73E", QueueStage.Failed => "\uE7BA", _ => "\uE71A" };   // check, warning, stop
    public string TopLabel => $"将 {Name} 移到队首";
    public string UpLabel => $"将 {Name} 上移";
    public string DownLabel => $"将 {Name} 下移";
    public string RemoveLabel => $"从队列中移除 {Name}";
    public override string ToString() => Number > 0 ? $"{Number}. {Name}, {Note}, {Time}, {Cache}" : $"{Name}, {Note}";   // screen readers
}

public sealed class QueueVm : Bindable
{
    QueueItem? cur;
    long planned;
    List<(string Game, long Bytes)> growth = [];
    List<(long Growth, long Estimate)> sizes = [];
    (CacheUsage Usage, CacheLimit? Limit, long? Capped)? driver;
    readonly BackgroundRead<(CacheUsage, CacheLimit?, long?)> cache;

    public QueueVm() => cache = new(() => (App.Core.Vendor.GetCacheUsage(), App.Core.Vendor.GetCacheLimit(), (App.Core.Vendor as AmdBackend)?.AppCache.DxcBytes()),
        c => { driver = c; ShowCache(); Changed(); });
    Settings S { get => App.Core.Settings; set { App.Core.Settings = value; Changed(); } }

    public ObservableCollection<QueueRow> Waiting { get; } = [];
    public ObservableCollection<QueueRow> Finished { get; } = [];
    public bool Dragging { get; set; }   // don't rebuild Waiting under a drag in progress

    public QueueRow Current { get; private set; } = new("", 1, "", "", "", "", "", QueueStage.Waiting);
    public bool HasCurrent => cur != null;
    public bool Idle => cur == null;
    public bool Empty => cur == null && Waiting.Count == 0;
    public bool HasFinished => Finished.Count > 0;
    public bool CanStart => !App.Core.QueueRunning && Waiting.Count > 0;
    public bool CanRaiseLimit => App.Core.Vendor is not AmdBackend;   // AMD's limit is fixed (AmdAppCache.DxcCacheCap)
    public string Summary { get; private set; } = "";
    public string? PlanChecks { get; private set; }   // ScsKiller.PlanCheckLine
    public bool HasPlanChecks => PlanChecks != null;

    public string CurrentDetail
    {
        get
        {
            string stage = cur?.Stage switch
            {
                QueueStage.Indexing => "正在读取着色器", QueueStage.Planning => "正在生成计划", QueueStage.Materializing => "正在准备文件",
                QueueStage.Paused => cur.Note is { } n ? "已暂停：" + Format.QueueNote(n) : "已暂停",
                QueueStage.Warming when cur.Note is { } n => "正在编译：" + Format.QueueNote(n),   // stalled (ScsKiller.StalledNote)
                _ => "正在编译",
            };
            return cur?.Progress is { Total: > 0 } p
                ? $"{stage} · {Percent:0.0}% · {p.Done:N0} / {p.Total:N0} 条管线" + (p.PerSecond > 0 && !ScsKiller.Stalled(cur) ? $" · {p.PerSecond:N0}/秒" : "") + (p.Failed > 0 ? $" · {p.Failed:N0} 条失败" : "") + (p.Skipped > 0 ? $" · {p.Skipped:N0} 条跳过" : "")   // short (the Done note explains them); a count wraps whole
                : stage;
        }
    }
    public bool Indeterminate => cur != null && cur.Progress is not { Total: > 0 };
    public double Percent => cur?.Progress is { Total: > 0 } p ? 100.0 * p.Done / p.Total : 0;
    public string PauseText => cur?.Stage == QueueStage.Paused ? "继续" : "暂停";
    public bool CacheWarn { get; private set; }
    public string CacheWarnText { get; private set; } = "";

    public double Threads { get => S.Threads; set { if ((int)value != S.Threads) S = S with { Threads = (int)value }; } }
    public string ThreadsText => $"{S.Threads} / {Environment.ProcessorCount}";
    public double MaxThreads => Environment.ProcessorCount;
    public int Priority { get => (int)S.Priority; set { if (value >= 0 && value != (int)S.Priority) S = S with { Priority = (WarmPriority)value }; } }
    public string CpuSummary => $"CPU 使用：{S.Threads} / {Environment.ProcessorCount} 个线程，{(S.Priority == WarmPriority.BelowNormal ? "低于正常" : "空闲")}优先级";

    public void Refresh()
    {
        var core = App.Core;
        var all = core.Queue;
        var queue = all.Where(q => !q.PlanCheck).ToList();
        var checks = all.Where(q => q.PlanCheck && Fmt.Active(q)).ToList();
        PlanChecks = checks.Count > 0 ? $"正在检查 {checks.Count} 款游戏是否有新内容可编译" +
            (checks.All(q => q.Note == ScsKiller.WhenIdleNote) ? "（空闲时）" : "") : null;
        var games = core.Games.ToDictionary(g => g.Game.Id);

        TimeSpan? Left(QueueItem q) => ScsKiller.Stalled(q) ? null   // no estimate for a warm that stopped moving
            : q.Progress is { PerSecond: > 0 } p ? TimeSpan.FromSeconds((p.Total - p.Done) / p.PerSecond)
            : games.GetValueOrDefault(q.GameId) is { } g ? g.EstimatedWarmTime ?? g.LastWarmTime : null;
        QueueRow Row(QueueItem q, int number, bool first = false, bool last = false)
        {
            var g = games.GetValueOrDefault(q.GameId);
            var time = q.Stage == QueueStage.Done ? Fmt.Duration(g?.LastWarmTime) : Left(q) is { } t ? Fmt.Duration(t) + (q.Progress?.PerSecond > 0 ? " 剩余" : "") : Format.Dash;
            return new(q.GameId, number, g?.Game.Name ?? q.GameId, g?.Game.ExePath ?? "", NoteOf(q, g), time, g is null ? Format.Dash : Fmt.Cache(g), q.Stage, first, last);
        }

        cur = queue.FirstOrDefault(Format.Running);
        if (cur != null) Current = Row(cur, 1);
        var waiting = queue.Where(q => q.Stage == QueueStage.Waiting).ToList();
        int first = cur != null ? 2 : 1;
        if (!Dragging) Sync(Waiting, waiting.Select((q, i) => Row(q, first + i, i == 0, i == waiting.Count - 1)));
        Sync(Finished, queue.Where(q => !Fmt.Active(q)).Select(q => Row(q, 0)));

        var pending = queue.Where(Fmt.Active).ToList();
        var total = TimeSpan.FromTicks(pending.Sum(q => Left(q)?.Ticks ?? 0));
        bool unknown = pending.Any(q => Left(q) is null && !ScsKiller.Stalled(q));
        Summary = pending.Count == 0 ? "队列为空"
            : $"{pending.Count} 款游戏 · "
              + (total > TimeSpan.Zero ? $"合计约 {Fmt.Duration(total)}{(unknown ? "，另有计划待生成" : "")}" : "生成计划后才知道耗时")
              + (cur == null && !core.QueueRunning ? " · 尚未开始" : "");

        planned = pending.Sum(q => games.GetValueOrDefault(q.GameId)?.EstimatedCacheBytes ?? 0);
        var queued = pending.Select(q => games.GetValueOrDefault(q.GameId)).OfType<GameState>().ToList();
        growth = queued.Select(g => (g.Game.Name, ScsKiller.CacheGrowth(g))).ToList();
        sizes = queued.Select(g => (ScsKiller.CacheGrowth(g), g.EstimatedCacheBytes ?? 0)).ToList();
        ShowCache();
        Changed();
        cache.Request();
    }

    void ShowCache()
    {
        if (driver is not { } d) return;   // not read yet
        CacheWarnText = (d.Capped is { } used
            ? ScsKiller.LargeCompileWarning(sizes, App.Core.Vendor.Vendor) ?? AmdAppCache.QueueWarning(used, growth)   // one game over the cap is always over what's free too
            : ScsKiller.QueueCacheWarning(sizes, d.Usage.BytesOnDisk, planned, d.Limit?.Bytes, App.Core.Vendor.Vendor)) ?? "";
        CacheWarn = CacheWarnText.Length > 0;
    }

    static void Sync(ObservableCollection<QueueRow> target, IEnumerable<QueueRow> rows)
    {
        var list = rows.ToList();
        if (list.SequenceEqual(target)) return;
        target.Clear();
        foreach (var r in list) target.Add(r);
    }

    static string NoteOf(QueueItem q, GameState? g) => q.Stage switch
    {
        QueueStage.Failed => "失败：" + q.Error,
        QueueStage.Stopped when q.Progress is { } p => $"已停止于 {p.Done:N0} / {p.Total:N0}；重新加入队列可继续" + (q.Note is { } n ? " · " + Format.QueueNote(n) : ""),
        QueueStage.Stopped => "已停止",
        QueueStage.Done => q.Note is { } n ? "完成 · " + Format.QueueNote(n) : "完成",
        _ when q.Note != null => Format.QueueNote(q.Note),
        _ => g?.Plan is { } plan ? $"{plan.Recorded + plan.Generated + plan.MiddlewareItems:N0} 条管线{(plan.D3D11Shaders > 0 ? $" + {plan.D3D11Shaders:N0} 个 DirectX 11 着色器" : "")}" : "将先生成编译计划",
    };

    public void PauseOrResume() => Fmt.PauseOrResume(cur);
}

public sealed class SettingsVm : Bindable
{
    public static readonly (string Label, CacheLimit Limit)[] Sizes =
    [
        ("16 GB（驱动默认值）", new(16L << 30, true)), ("32 GB", new(32L << 30, false)),
        ("100 GB", new(100L << 30, false)), ("无限制", new(null, false)),
    ];
    public static readonly string[] SizeLabels = Sizes.Select(s => s.Label).ToArray();

    Settings S { get => App.Core.Settings; set { App.Core.Settings = value; Refresh(); } }
    IGpuVendorBackend V => App.Core.Vendor;
    // The driver cache's limit and usage, the drive's free space and NVIDIA's Auto Shader Compilation, read off the UI thread; null until the first read.
    (CacheLimit? Limit, CacheUsage Usage, string Free, AutoShaderState? Auto)? read;
    readonly BackgroundRead<(CacheLimit?, CacheUsage, string, AutoShaderState?)> cache;

    public SettingsVm()
    {
        // Both read the facade, so they can't be field initializers (a field initializer can't touch the instance).
        gpuChoices = GpuChoiceList();
        gpuIndex = Math.Max(0, Array.FindIndex(gpuChoices, c => string.Equals(c.Id, S.GpuChoice, StringComparison.OrdinalIgnoreCase)));
        cache = new(() =>
        {
            var usage = V.GetCacheUsage();
            string free;
            try { free = Format.Bytes(new DriveInfo(DriveOf(usage)).AvailableFreeSpace); } catch (Exception) { free = Format.Dash; }
            return (V.GetCacheLimit(), usage, free, V.GetAutoShaderCompilation());
        }, r =>
        {
            // Preselect the current custom limit, else 100 GB (the mockup's suggestion over the 16 GB default).
            if (read == null) SizeIndex = Array.FindIndex(Sizes, s => !s.Limit.IsDriverDefault && s.Limit == r.Item1) is >= 0 and var i ? i : 2;
            // The checkbox and level show the driver's state after every read (an apply's re-read included); Medium when off.
            AutoOn = r.Item4 is { Level: not AutoShaderCompilation.Off };
            AutoLevelIndex = r.Item4 is { Level: not AutoShaderCompilation.Off and var l } ? (int)l - 1 : 1;
            read = r;
            Changed();
        });
        cache.Request();
    }

    public string CacheTitle => $"{Fmt.Vendor(V.Vendor)} 着色器缓存大小";
    public bool IsNvidia => V.Vendor == GpuVendor.Nvidia;

    public static readonly string[] AutoLevels = ["低", "中", "高"];   // AutoShaderCompilation.Low..High
    public bool? AutoOn { get; set; }
    public int AutoLevelIndex { get; set; } = 1;
    public AutoShaderCompilation AutoChoice => AutoOn == true ? (AutoShaderCompilation)(Math.Clamp(AutoLevelIndex, 0, 2) + 1) : AutoShaderCompilation.Off;
    public string AutoShaderText => read == null ? Format.Dash : read.Value.Auto switch
    {
        null => "无法读取此设置",
        { Level: AutoShaderCompilation.Off } => "当前：关闭",
        { TaskReady: true, Level: var l } => $"当前：开启，{AutoLevels[(int)l - 1]}",
        _ => "设置已开启，但 NVIDIA 计划任务缺失；单击“应用”尝试修复",
    };
    public string? AutoMessage { get; set; }
    public Microsoft.UI.Xaml.Controls.InfoBarSeverity AutoSeverity { get; set; }
    public bool HasAutoMessage => AutoMessage != null;
    public string CacheIntro => (IsNvidia ? "这是全局驱动设置，对应 NVIDIA App › 图形 › 着色器缓存大小。" : "这是全局驱动设置。")
        + "缓存满时驱动会删除较旧的条目，相关游戏可能再次卡顿。";
    public bool Configurable => V.Caps.CacheSizeConfigurable;
    public bool NotConfigurable => !Configurable;
    public string NotConfigurableText => V.Vendor == GpuVendor.Amd
        ? $"AMD 驱动的 DirectX 12 着色器缓存固定为 {Format.Bytes(AmdAppCache.DxcCacheCap)}，没有大小设置。"
          + "超过上限后，驱动会在启动游戏时删除最久未使用的缓存；SCSKiller 会提示相关游戏重新编译。"
        : $"SCSKiller 暂时无法修改 {Fmt.Vendor(V.Vendor)} 驱动的缓存大小，只会显示用量。"
          + "如果游戏缓存被清理，请在显卡厂商控制面板中提高上限。";

    public string CurrentLimit => read == null ? Format.Dash : read.Value.Limit switch
    {
        null => "未知",
        { Bytes: null } => "无限制",
        { Bytes: 0 } => "已禁用",
        { Bytes: { } b, IsDriverDefault: var d } => Format.Bytes(b) + (d ? "（驱动默认值）" : ""),
    };
    public string Used => read is { Usage: var u } ? (u.UpperBound ? "≤ " : "") + Format.Bytes(u.BytesOnDisk) : Format.Dash;
    static string DriveOf(CacheUsage? u) => Path.GetPathRoot(u?.Path) is { Length: > 0 } r ? r : Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!;
    public string FreeLabel => $"{DriveOf(read?.Usage).TrimEnd('\\')} 可用空间";
    public string Free => read?.Free ?? Format.Dash;

    public int SizeIndex { get; set; } = 2;
    public string? ApplyMessage { get; set; }
    public Microsoft.UI.Xaml.Controls.InfoBarSeverity ApplySeverity { get; set; }
    public bool HasApplyMessage => ApplyMessage != null;
    public void Refresh()
    {
        Changed();
        cache.Request();
    }

    /// <summary>The adapter to compile for (<c>Settings.GpuChoice</c>): index 0 is the automatic rule — the non-software
    /// adapter with the most dedicated VRAM, what scskiller_warm defaults to — then one entry per adapter DXGI lists.
    /// A null id means "let <see cref="GpuBackends.Primary(IReadOnlyList{DxgiAdapter}, string?)"/> pick".</summary>
    readonly (string Label, string? Id, long Luid)[] gpuChoices;   // set in the constructor
    /// <summary>The saved choice's entry, 0 (automatic) when it names nothing DXGI lists any more.</summary>
    int gpuIndex;

    static (string Label, string? Id, long Luid)[] GpuChoiceList()
    {
        try
        {
            var all = GpuBackends.Adapters();
            var auto = GpuBackends.Primary(all, null);
            return [($"自动（显存最多的显卡：{auto?.Name ?? "无"}）", null, auto?.AdapterLuid ?? 0),
                .. all.Select(a => ($"{a.Gpu.Name}（{GpuBackends.Id(a)} · {Format.Bytes((long)a.Gpu.DedicatedVideoMemory)}）",
                    GpuBackends.Id(a), a.Gpu.AdapterLuid)).DistinctBy(c => c.Item2)];   // DXGI lists a repeated adapter twice (same ids)
        }
        catch (Exception) { return [("自动（显存最多的显卡）", null, 0)]; }   // no DXGI to enumerate (screenshots): the rule alone
    }

    public IReadOnlyList<string> GpuChoices => gpuChoices.Select(c => c.Label).ToArray();
    public int GpuIndex
    {
        get => gpuIndex;
        set
        {
            if (value < 0 || value >= gpuChoices.Length || value == gpuIndex) return;
            gpuIndex = value;
            S = S with { GpuChoice = gpuChoices[value].Id };   // ScsKiller.CreateDefault reads it: the next start uses it
            Changed();   // the note below
        }
    }
    public string GpuIntro => "SCSKiller 默认用“显存最多的显卡”编译着色器 —— 两块显存接近时它可能落在你并不想用的那块上。"
        + "这里可以指定用哪一块：驱动缓存、预热进程和社区共享包都按所选显卡选择。"
        + "所选显卡若被禁用或拔除，会自动回退到显存最多的那块。";
    /// <summary>Set when a saved choice isn't the adapter this process is already compiling for: the adapter, its caches,
    /// the warm exe and the shared packs are all picked at start-up (<see cref="ScsKiller.CreateDefault"/>), so a change
    /// takes effect at the next start. Only the automatic rule's own pick is silent: nothing was overruled.</summary>
    public string? GpuNote => S.GpuChoice is { Length: > 0 } && V.Gpu.AdapterLuid != gpuChoices[gpuIndex].Luid
        ? $"将在下次启动 SCSKiller 时生效；本次编译仍用 {V.Gpu.Name}。"
        : null;
    public bool HasGpuNote => GpuNote != null;
    public Microsoft.UI.Xaml.Controls.InfoBarSeverity GpuSeverity => Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;

    public int DriverMode
    {
        get => (int)S.OnDriverUpdate;
        set
        {
            if (value < 0 || value == DriverMode) return;
            S = S with { OnDriverUpdate = (DriverUpdateMode)value };
            try { App.Core.ApplyDriverUpdateMode(); }  // user action: registers or removes the sign-in check
            catch (Exception e) { ApplyMessage = "无法更新登录时检查任务：" + e.Message; ApplySeverity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error; Changed(); }
        }
    }
    public bool? PauseWhileGaming { get => S.PauseWhileGaming; set { if (value is { } v && v != S.PauseWhileGaming) S = S with { PauseWhileGaming = v }; } }
    public double BackgroundThreads { get => S.BackgroundThreads; set { if ((int)value != S.BackgroundThreads) S = S with { BackgroundThreads = (int)value }; } }
    public string BackgroundThreadsText => $"{S.BackgroundThreads} / {Environment.ProcessorCount}";
    public double MaxThreads => Environment.ProcessorCount;
    public bool HasLastRebuilt => LastRebuilt != "";
    public string LastRebuilt => App.Core.Games.Where(g => g.WarmedAt != null && g.WarmedDriverVersion != null).MaxBy(g => g.WarmedAt)?.WarmedDriverVersion is { } v ? $"上次为驱动 {v} 重新编译" : "";

    public bool? MaximumPlans { get => S.MaximumPlans; set { if (value is { } v && v != S.MaximumPlans) S = S with { MaximumPlans = v }; } }
    public bool? ShareRecordings { get => S.ShareRecordings; set { if (value is { } v && v != S.ShareRecordings) S = S with { ShareRecordings = v }; } }
    public bool? ActiveCheck { get => S.ActiveCheck; set { if (value is { } v && v != S.ActiveCheck) S = S with { ActiveCheck = v }; } }
    public void GamesChanged() => Changed();   // no driver-cache re-read (Refresh)
    public bool RecordAllGames { get => S.RecordAllGames; set { if (value != S.RecordAllGames) S = S with { RecordAllGames = value }; } }   // the core reconciles off this thread
    /// <summary>"Recording in 23 games · 5 skipped: anti-cheat"; not-DX12 and unsupported games aren't counted.</summary>
    public string RecordingCount
    {
        get
        {
            var games = App.Core.Games;
            var skipped = games.Where(g => g.RecorderSkip is ScsKiller.SkipAntiCheat or ScsKiller.SkipShaderMod or ScsKiller.SkipForeignDll or ScsKiller.SkipModNotChainable
                    or ScsKiller.SkipVulkanMod or ScsKiller.SkipNeedsAdmin or ScsKiller.SkipPackageD3D12 or ScsKiller.SkipStreamline)
                .GroupBy(g => g.RecorderSkip).OrderByDescending(x => x.Count()).Select(x => $" · {x.Count()} 款跳过：{Fmt.RecorderSkip(x.Key!)}");
            int n = games.Count(g => g.RecorderInstalled);
            return $"{n} 款游戏已安装录制器" + string.Concat(skipped);
        }
    }

    public static readonly int[] RecordingLimits = ScsKiller.RecordingLimits;
    public static readonly string[] RecordingLimitLabels = RecordingLimits.Select(n => n == 0 ? "无限制" : ScsKiller.LimitText(n)).ToArray();
    public int RecordingLimitIndex
    {
        get => Array.IndexOf(RecordingLimits, S.RecordingLimitMB);   // -1: a value set elsewhere, shown blank
        set { if (value >= 0 && RecordingLimits[value] != S.RecordingLimitMB) S = S with { RecordingLimitMB = RecordingLimits[value] }; }   // the core reconciles off this thread
    }
    /// <summary>Each game with a recording on disk, largest first: its size, and whether it reached the limit.</summary>
    public IReadOnlyList<DetailRow> Recordings => App.Core.Games.Where(g => g.RecordingBytes > 0).OrderByDescending(g => g.RecordingBytes)
        .Select(g => new DetailRow(g.Game.Name, Format.Bytes(g.RecordingBytes), g.RecordingPaused ? "已达到录制容量上限" : null)).ToList();
    public bool HasRecordings => App.Core.Games.Any(g => g.RecordingBytes > 0);
    public string RecordingSpace => App.Core.Games.Where(g => g.RecordingBytes > 0).ToList() is { Count: > 0 } r
        ? $"{r.Count} 款游戏的录制数据占用 {Format.Bytes(r.Sum(g => g.RecordingBytes))}" : "磁盘上没有录制数据";
    public bool? StartWithWindows
    {
        get => S.StartWithWindows;
        set
        {
            if (value is not { } v || v == S.StartWithWindows) return;
            S = S with { StartWithWindows = v };
            App.ApplyStartWithWindows();   // the sign-in entry: written or removed now (real data only)
        }
    }
    public bool? NotifyNewShaders { get => S.NotifyNewShaders; set { if (value is { } v && v != S.NotifyNewShaders) S = S with { NotifyNewShaders = v }; } }
    public bool? ScanAtStart { get => S.ScanAtStart; set { if (value is { } v && v != S.ScanAtStart) S = S with { ScanAtStart = v }; } }
    public bool? CloseQuits { get => S.CloseQuits; set { if (value is { } v && v != S.CloseQuits) S = S with { CloseQuits = v }; } }


    public double Threads { get => S.Threads; set { if ((int)value != S.Threads) S = S with { Threads = (int)value }; } }
    public string ThreadsText => $"{S.Threads} / {Environment.ProcessorCount}";
    /// <summary>0 = Auto; 1 means 2 (the smallest budget offered).</summary>
    public double MaxCompileMemory { get => S.MaxCompileMemoryGB; set { var v = (int)value == 1 ? 2 : (int)value; if (v != S.MaxCompileMemoryGB) S = S with { MaxCompileMemoryGB = v }; } }
    public string MaxCompileMemoryNote => $"自动模式会根据此电脑的内存选择上限（当前为 {ScsKiller.AutoCompileMemoryGB()} GB，可用内存约 {Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (double)(1L << 30))} GB）。调低上限可为其他程序留出更多内存。";
    public string MaxCompileMemoryText => S.MaxCompileMemoryGB > 0 ? $"{S.MaxCompileMemoryGB} GB" : $"自动（{ScsKiller.AutoCompileMemoryGB()} GB）";
    public string PriorityNote => (S.Priority == WarmPriority.BelowNormal ? "优先级低于正常。" : "空闲优先级。") + "线程越少，编译越慢，但对电脑使用的影响越小。";
}

/// <summary>Settings' Account card: a view over <see cref="App.Account"/>, which changes on worker threads.</summary>
public sealed class AccountVm : Bindable
{
    static Account A => App.Account;
    readonly Coalesced changed;
    public AccountVm() => changed = new(App.Main.DispatcherQueue, Changed);

    /// <summary>While the page is shown. Signed in with nothing read yet this session: reads the status.</summary>
    public void Watch(bool on)
    {
        A.Changed -= changed.Request;
        Updater.Changed -= changed.Request;
        if (!on) return;
        A.Changed += changed.Request;
        Updater.Changed += changed.Request;
        Changed();
        if (A.SignedIn && A.Status == null && !A.Busy) _ = A.RefreshAsync();
    }

    public bool SignedIn => A.SignedIn;
    public bool SignedOut => !A.SignedIn;
    public bool SigningIn => A.SigningIn;
    public bool Busy => A.Busy;
    public bool Idle => !A.Busy;
    public string? Problem => A.Problem;
    public bool HasProblem => A.Problem != null;
    public bool OffersSharing => A.OffersSharing(App.Core.Settings);
    public void Refresh() => Changed();   // the settings changed (OffersSharing)

    // "Download shared shader hashes": enabled only with "db" (follows sign-in, sign-out, refresh and a lapse through
    // Changed); the setting keeps its value. One quiet line under it: why it's off, or its last failure.
    public bool HasDb => A.HasDb;
    // Unticked without "db" whatever the setting says; the binding writes that false back, which must not reach the setting.
    public bool? UseCommunityDb
    {
        get => A.HasDb && App.Core.Settings.UseCommunityDb;
        set
        {
            if (!A.HasDb || value is not { } v || v == App.Core.Settings.UseCommunityDb) return;
            App.Core.Settings = App.Core.Settings with { UseCommunityDb = v };
            Changed();
        }
    }
    public string? DbNote => !A.SignedIn ? "上游社区数据库下载需要 Patreon 会员权限；本 fork 默认不连接该服务。"
        : A.Status is not { } st ? null   // not read yet: the status line above says "Checking…"
        : !A.HasDb ? "当前 Patreon 会员权限不包含社区数据库；已下载的内容仍可使用。"
        : (App.Core as ScsKiller)?.Community?.Problem;
    public bool HasDbNote => DbNote != null;

    public string Benefits => A.Status switch
    {
        null => A.Busy ? "正在检查会员状态…" : "尚未检查会员状态。",
        { Benefits: { } b } => b,   // the flag names live in Core (AccountStatus) so they're testable
        _ => "当前没有会员权益。Patreon 会员生效后，请单击“刷新状态”。",
    };

    // Update channel: a picker only when the token grants more than stable.
    static readonly Dictionary<string, string> ChannelNames = new()
    {
        [UpdateChannels.Stable] = "稳定版", [UpdateChannels.Beta] = "测试版（上游 Patreon 会员）",
        [UpdateChannels.Alpha] = "预览版（上游 Patreon 赞助者）", [UpdateChannels.Internal] = "内部版",
    };
    IReadOnlyList<string> Offered => UpdateChannels.Offered(A.Status?.Ent);
    string Chosen => UpdateChannels.Effective(App.Core.Settings.UpdateChannel, AppVersion.Current.Channel, A.Status?.Ent);
    List<string> labels = [];
    public List<string> ChannelLabels => labels.SequenceEqual(Offered.Select(c => ChannelNames[c])) ? labels : labels = Offered.Select(c => ChannelNames[c]).ToList();   // same list: the ComboBox keeps its selection
    public bool ShowsChannels => Updater.Installed && A.SignedIn && Offered.Count > 1;
    public bool CanPickChannel => !Updater.Restarting;
    public bool CanSignOut => Idle && !Updater.Restarting;   // signing out can change the channel a restart applies
    public int ChannelIndex
    {
        get => Math.Max(0, Offered.ToList().IndexOf(Chosen));
        set
        {
            if (value < 0 || value >= Offered.Count || Offered[value] == Chosen || Updater.Restarting) return;   // -1: the list was replaced
            App.Core.Settings = App.Core.Settings with { UpdateChannel = Offered[value] };
            _ = Updater.CheckAsync();
            Changed();
        }
    }
    /// <summary>"Go back to stable now": on an installed pre-release build, whatever the account.</summary>
    public bool OffersBackToStable => Updater.Installed && AppVersion.Current.Channel != UpdateChannels.Stable;
    public bool ShowsUpdates => ShowsChannels || OffersBackToStable;
    public string ChannelNote => Updater.Checking ? "正在检查本 fork 的更新…"
        : Updater.Ready is { } v ? AutoInstall.ReadyNote(v, App.Core.Settings)
        : (App.Core.Settings.InstallUpdatesAutomatically ? "更新会在后台下载，并在下次启动或退出时安装；编译或游戏运行期间不会安装。"
            : "更新会在后台下载；单击“重启以更新”后安装。") + "退出预发布渠道后，当前版本会保留到稳定版版本号超过它。";
    public string? UpdateProblem => Updater.Problem;
    public bool HasUpdateProblem => Updater.Problem != null;
}

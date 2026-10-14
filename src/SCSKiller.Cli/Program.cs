using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using CUE4Parse.Compression;
using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Games;
using SCSKiller.Core.Unreal;
using SCSKiller.Core.Vendors;

const string Usage = """
    用法：scskiller <命令>
      scan [--rescan]                             查找游戏并显示状态（--rescan：重新识别引擎）
      status [game]                               显示全部游戏，或指定游戏的详细状态
      compile <game|--all-ready> [--threads N] [--idle | --when-idle] [--careful | --fast]
                                                  --idle：后台优先级；--when-idle：仅在电脑空闲时运行；
                                                  --careful（AMD）：用较少线程分批编译录制的管线，速度较慢；
                                                  --fast：恢复单轮快速编译
      queue [command]                             编译队列调试命令；后续可从标准输入逐行发送：
                                                  list | add <game> | move <game> <index> | remove <game> | start | stop | wait | quit
      rewarm-stale [--if-driver-changed]          计划任务使用；按驱动更新设置重新预热
      cache get | cache set <GB|unlimited|default>
      cache clear <game> [--game-precache]        清理驱动和 Windows 的着色器缓存（先确认）；
                                                  --game-precache 还会清理游戏自己的预缓存
      nvidia-snapshot                             只读查看 NVIDIA 全局 DRS、自动着色器编译和空闲任务
      nvidia-auto-shader off|low|medium|high      设置 NVIDIA 自动着色器编译（需要管理员权限，先确认）
        （cache set / nvidia-auto-shader：--yes 跳过普通设置确认；--result <file> 输出 JSON；
         --for-user <SID> 限制提权操作的用户。反作弊录制许可始终单独确认。）
      record install|uninstall <game>
      record anti-cheat <game> on|off              对检测到反作弊的游戏单独允许/撤销直接录制（开启前警告）
      record clear <game>                         清理该游戏的录制数据（先确认）
      record alongside <game> on|off              与已有 mod 的 d3d12.dll 串接录制
      key <game> <hex>                            提供加密游戏的 AES 密钥（验证后只保存在本机）
      index <game> --out <dir>                    调试：导出引擎读取器的着色器索引
      rehydrate <game> <hash-only.db> --out <db> [--expect <content hash>]
                                                  从本机游戏文件补齐哈希录制引用的着色器字节
      reference export <game> [--out <file>]      导出此版本游戏的着色器清单供社区服务器使用
      task register|unregister                    注册或移除驱动更新后的登录/空闲预热任务
      fetch-codecs <dir>                          打包用：获取固定版本的 Oodle/zlib DLL
    <game> 可填写游戏 ID（如 steam:2909400）或不区分大小写的部分名称。
    """;

Console.OutputEncoding = System.Text.Encoding.UTF8;  // game names carry ™ and ®
RouteFailover.Product = "SCSKiller-CLI";
if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Console.WriteLine(Usage); return 0; }
// The app is handing over to Update.exe, which replaces this folder: start nothing (the scheduled task runs again later).
// Packaging never reads the app's data folder.
if (args[0] != "fetch-codecs" && Busy.Applying(AppStore.DefaultDir, DateTimeOffset.UtcNow))
{
    const string busy = "正在安装更新，请稍后重试";
    Console.Error.WriteLine(busy);
    // run elevated by the app, the exit code alone (0, for the scheduled task) would read as done
    try { if (Opt(Elevated.ResultArg) is { } file) Elevated.WriteResult(file, false, busy); }
    catch (ArgumentException) { }
    return 0;
}
int code;
try
{
    code = args[0] switch
    {
        "scan" => await Scan(args.Contains("--rescan")),
        "status" => await Status(args.Length > 1 ? args[1] : null),
        "compile" => await Compile(),
        "queue" => await QueueShell(),
        "rewarm-stale" => await RewarmStale(args.Contains("--if-driver-changed")),
        "cache" => args.ElementAtOrDefault(1) == "clear" ? await CacheClear() : Cache(),
        "record" => await Record(),
        "index" => await Index(),
        "rehydrate" => await RehydrateCommand(),
        "reference" when args.ElementAtOrDefault(1) == "export" => await ReferenceExport(),
        "key" => await Key(),
        "task" => TaskCommand(),
        "nvidia-snapshot" => NvidiaSnapshot(),
        "nvidia-auto-shader" => NvidiaAutoShader(),
        "fetch-codecs" => FetchCodecs(args.ElementAtOrDefault(1) ?? throw new ArgumentException("用法：scskiller fetch-codecs <dir>")),
        _ => Fail($"未知命令“{args[0]}”\n{Usage}"),
    };
}
catch (Exception e)
{
    code = Fail(e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException ? e.Message : e.ToString());
}
// Run elevated by the app (Elevated.Run): runas can't redirect stdout, so the outcome goes to a file.
string? resultFile = null;
try { resultFile = Opt(Elevated.ResultArg); }
catch (ArgumentException e) { code = Fail(e.Message); }
if (resultFile != null) Elevated.WriteResult(resultFile, code == 0, Outcome.Message ?? "");
return code;

static int Fail(string message)
{
    Console.Error.WriteLine("错误：" + message);
    Outcome.Message = message;
    return 1;
}

/// <summary>False (reported as an error) unless the answer is y.</summary>
static bool Confirm(string question)
{
    Console.Write(question + " [y/N，输入 y 确认] ");
    var piped = Console.IsInputRedirected;
    var answer = piped ? Elevated.ReadAnswer(Console.OpenStandardInput()) : Console.ReadLine()?.Trim();
    if (piped) Console.WriteLine(answer);
    if (string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)) return true;
    Fail(answer == null ? "已取消：标准输入没有回答（可传入 y 确认）" : $"已取消：输入的是“{answer}”，不是 y");
    return false;
}

static async Task<ScsKiller> Open(bool rescan = false)
{
    var k = ScsKiller.CreateDefault();
    k.Log = new StderrLog();   // synchronous: a message logged just before exit still prints
    await (rescan ? k.RescanAsync(CancellationToken.None) : k.ScanAsync(CancellationToken.None));
    return k;
}

static void BackgroundPriority()   // index/plan/materialize run in this process; the warm itself gets --priority idle
{
    using var self = Process.GetCurrentProcess();
    self.PriorityClass = ProcessPriorityClass.Idle;
}

static string StatusName(GameState s) => ScsKiller.NeedsOfflineSession(s) ? "需要离线录制" : s.Status switch
{
    GameStatus.Warmed => "已预热", GameStatus.Ready => "可编译", GameStatus.NeedsRecording => "需要录制",
    GameStatus.Stale => "需要重新编译", _ => "暂不支持",
};

static string AntiCheatName(AntiCheat antiCheat) => antiCheat switch
{
    AntiCheat.EasyAntiCheat => "EasyAntiCheat", AntiCheat.BattlEye => "BattlEye", _ => "其他反作弊",
};

static string StutterSeverityName(StutterSeverity severity) => severity == StutterSeverity.Severe ? "严重" : "中等";

static string Hms(TimeSpan? t) => t?.ToString(@"h\:mm\:ss") ?? "-";

static void Row(GameState s) =>
    Console.WriteLine($"{StatusName(s),-19} {s.Game.Id,-24} {s.Game.Name}{(s.AntiCheat != AntiCheat.None ? $"  [{AntiCheatName(s.AntiCheat)}]" : "")}{(s.KnownStutter is { } ks ? $"  [已知卡顿：{StutterSeverityName(ks.Severity)}]" : "")}{(s.NoStutter != null ? "  [无着色器卡顿]" : "")}{(s.RecorderInstalled ? "  [录制器已安装]" : "")}{(s.CacheOnDisk is { } c ? $"  [缓存 {Format.Bytes(c)}]" : "")}  - {Format.Reason(s)}");

/// <summary>The tags' <see cref="Format.Middleware"/> lines joined with " · ", with the DLL names when <paramref name="dlls"/>.</summary>
static string MiddlewareLine(IReadOnlyList<MiddlewareTag> tags, bool dlls) => string.Join(" · ", tags.Select(t =>
    Format.Middleware(t) + (dlls ? $" [{string.Join(", ", t.Dlls)}]" : "")));

static GameState Match(IReadOnlyList<GameState> games, string query)
{
    var hit = games.FirstOrDefault(s => s.Game.Id.Equals(query, StringComparison.OrdinalIgnoreCase))
              ?? games.FirstOrDefault(s => s.Game.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
    if (hit != null) return hit;
    var many = games.Where(s => s.Game.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    return many.Count switch
    {
        1 => many[0],
        0 => throw new ArgumentException($"没有匹配“{query}”的游戏"),
        _ => throw new ArgumentException($"“{query}”匹配多个游戏：{string.Join("、", many.Select(s => $"{s.Game.Name} ({s.Game.Id})"))}"),
    };
}

string? Opt(string name)
{
    int i = Array.IndexOf(args, name);
    if (i < 0) return null;
    return i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : throw new ArgumentException($"{name} 需要一个值");
}

/// <summary>Refuses an option the command doesn't take (a typo would otherwise run with the default).</summary>
void Only(params string[] known)
{
    if (args.Skip(1).FirstOrDefault(a => a.StartsWith("--") && !known.Contains(a)) is { } unknown) throw new ArgumentException($"未知选项“{unknown}”");
}

async Task<int> Scan(bool rescan)
{
    var k = await Open(rescan);
    Console.WriteLine($"显卡：{k.Vendor.Gpu.Name}，驱动 {k.Vendor.Gpu.DriverVersion}（{k.Vendor.Caps.Profile}）");
    foreach (var s in k.Games) Row(s);
    return 0;
}

async Task<int> Status(string? query)
{
    var k = await Open();
    if (query == null)
    {
        foreach (var s in k.Games)
        {
            Row(s);
            if (s.Middleware is { Count: > 0 } m) Console.WriteLine($"{"",15}中间件：{MiddlewareLine(m, false)}");
        }
        return 0;
    }
    var g = Match(k.Games, query);
    var rec = k.Store.LoadGame(g.Game.Id);
    Console.WriteLine($"""
        {g.Game.Name} ({g.Game.Id}, {g.Game.Store})
          安装目录  {g.Game.InstallDir}
          执行文件  {g.Game.ExePath}
          实际进程  {(rec.LaunchedExeName is { } l0 ? $"{l0}（{rec.LaunchedExeSeenAt:yyyy-MM-dd HH:mm} 检测到）" : "尚未检测到")}；预热使用 {ScsKiller.WarmExeName(g.Game, rec)}
          游戏引擎  {(g.Engine is { } e ? string.Join(' ', new[] { Format.Engine(e) + (e.VersionGuessed ? "（推测）" : ""), e.Fork, e.GraphicsApi }.OfType<string>()) + (e.Encrypted ? "，文件已加密" : "") : "-")}
          当前状态  {StatusName(g)}：{Format.Reason(g)}
          反作弊    {(g.AntiCheat == AntiCheat.None ? "未检测到" : AntiCheatName(g.AntiCheat))}
          着色器数  {g.ShaderCount?.ToString() ?? "-"}
          编译计划  {(g.Plan is { } p ? $"录制 {p.Recorded} + 生成 {p.Generated} + DirectX 11 着色器 {p.D3D11Shaders} + 中间件 {p.MiddlewareItems}；根签名 {p.RootSignatures}，规则已验证：{p.RootSigRuleVerified}" : "-")}
          卡顿记录  {(g.KnownStutter is { } st ? $"已知卡顿（{StutterSeverityName(st.Severity)}）：{st.Reason}（{(st.Source == StutterList.OwnMeasurement ? "SCSKiller 实测" : st.Source)}，核查于 {st.Date}）" : g.NoStutter is { } ns ? $"无着色器卡顿：{Format.NoStutterReason(ns)}" : "-")}
          中间件    {(g.Middleware is { Count: > 0 } mw ? MiddlewareLine(mw, true) : "-")}
          预计占用  {(g.EstimatedCacheBytes is { } est ? Format.Bytes(est) : "-")} 缓存；预计耗时 {Hms(g.EstimatedWarmTime)}
          谨慎模式  {CarefulLine(g.Careful)}
          上次预热  {(g.WarmedAt is { } w ? $"{w:yyyy-MM-dd HH:mm}，驱动 {g.WarmedDriverVersion}，耗时 {Hms(g.LastWarmTime)}" + (ScsKiller.WarmCounts(g.LastWarmFailed ?? 0, g.LastWarmSkipped ?? 0, g.LastWarmCrashed ?? 0) is { } counts ? $"；{counts}" : "") : "从未预热")}
          驱动缓存  {(g.CacheOnDisk is { } c ? $"磁盘上 {Format.Bytes(c)}（预热或游戏打开过的驱动缓存文件）" : "-")}
          缓存键    {(rec.CacheKeys.Count > 0 ? string.Join(", ", rec.CacheKeys.Order()) : "-")}{(k.WarmAgs(g.Game.Id) is { } ags ? $"（编译时注册 AGS 程序名 {ags.App}，键 {k.AgsKey(g.Game.Id)}；执行文件名大小写不影响结果）" : AmdAppCache.IsNameHashed(rec.CacheKeys, ScsKiller.WarmExeName(g.Game, rec)) == false ? "（应用配置键，不是执行文件名哈希；名称大小写不影响结果）" : "")}
          录制器    {(g.RecorderInstalled ? "已安装" : "未安装")}{(g.RecorderRefused is { } why ? $"；{(ScsKiller.NeverRecorded(g) ? "游玩过但未录到数据。" : "")}{Format.RecorderRefused(why)}" : "")}
          录制数据  {(g.RecordingBytes > 0 ? $"{Format.Bytes(g.RecordingBytes)}（游戏目录文件及 SCSKiller 副本）" : "-")}；每个游戏上限 {ScsKiller.LimitText(k.Settings.RecordingLimitMB)}{(g.RecordingPaused ? $"；{ScsKiller.PausedNote(k.Settings)}" : "")}
          上次游玩  {(g.LastSession is { } l ? $"{Hms(l.Duration)}，共 {l.Requests} 条管线：游戏自带库 {l.FromGameLibrary}、缓存命中 {l.CacheHits}、游玩时编译 {l.Compiles}（最长 {l.WorstCompileMs:0.0} 毫秒）{(l.StartupCompiles > 0 ? $"，启动或加载时编译 {l.StartupCompiles}" : "")}{(l.RayQueryRecompiles > 0 ? $"，每次启动仍需部分重编的光追管线 {l.RayQueryRecompiles}" : "")}{(l.StateObjectsReady + l.StateObjectsCompiled + l.StateObjectsStartupCompiled > 0 ? $"；光追状态对象：已就绪 {l.StateObjectsReady}、游玩时编译 {l.StateObjectsCompiled}{(l.StateObjectsStartupCompiled > 0 ? $"、启动或加载时编译 {l.StateObjectsStartupCompiled}" : "")}" : "")}" : "-")}
          首次启动  {(rec.FirstLaunch is { } fl ? $"{fl.At.LocalDateTime:yyyy-MM-dd HH:mm}；上次编译后缓存命中 {fl.Hits}，仍编译 {fl.Compiles}（已覆盖 {fl.Compiled * 100:0.0}%）" : "-")}
        """);
    return 0;
}

static string CarefulLine(CarefulCompile? c) => c is not { } x ? "-（仅 AMD）"
    : (x.On ? $"开启：录制的管线最多用 {ScsKiller.AmdCarefulThreads} 个线程分批编译，其余保持全速" : "关闭（使用 compile <game> --careful 开启）")
      + (x.Recorded == 0 ? "；尚无录制数据，暂无可分批编译的管线" : x.Estimate is { } t ? $"；预计耗时约 {ScsKiller.Duration(t)}" : "");

static void PrintQueueChanges(ScsKiller k)
{
    var lastPrint = DateTime.MinValue;
    var lastStage = new Dictionary<string, QueueStage>();
    k.QueueChanged += q =>
    {
        lock (lastStage)
        {
            bool stageChanged = !lastStage.TryGetValue(q.GameId, out var st) || st != q.Stage;
            if (!stageChanged && DateTime.Now - lastPrint < TimeSpan.FromSeconds(2)) return;
            lastStage[q.GameId] = q.Stage;
            lastPrint = DateTime.Now;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {q.GameId} {Format.QueueStageName(q.Stage)}{Format.QueueProgress(q)}{(q.Error != null ? " - " + q.Error : "")}");
        }
    };
}

async Task<int> RunQueue(ScsKiller k, IReadOnlyList<GameState> targets, bool whenIdle = false)
{
    if (targets.Count == 0) { Console.WriteLine("没有需要编译的游戏"); return 0; }
    PrintQueueChanges(k);
    bool cancelled = false;
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancelled = true;
        Console.WriteLine("正在停止；进行中的编译会完成，以便驱动写入缓存…");
        foreach (var q in k.Queue.Where(q => q.Stage == QueueStage.Waiting)) k.Remove(q.GameId);
        k.StopQueue();
    };
    foreach (var t in targets)
        if (whenIdle) k.EnqueueWhenIdle(t.Game.Id); else k.Enqueue(t.Game.Id);
    if (whenIdle) Console.WriteLine($"等待电脑空闲 {k.IdleAfter.TotalMinutes:0} 分钟；有输入时暂停");
    else k.StartQueue();
    await k.WhenQueueIdle();
    return !cancelled && k.Queue.Count > 0 && k.Queue.All(q => q.Stage == QueueStage.Done) ? 0 : 1;   // Ctrl+C: not everything compiled
}

// A debugging shell: the queue lives in this process, so commands come on stdin (or piped: echo add ff7 | scskiller queue).
async Task<int> QueueShell()
{
    var k = await Open();
    PrintQueueChanges(k);
    string Id(string[] w, int from, int to) => Match(k.Games, string.Join(' ', w[from..to])).Game.Id;
    var lines = (args.Length > 1 ? [string.Join(' ', args[1..])] : Array.Empty<string>()).Concat(StdinLines());
    foreach (var line in lines)
    {
        var w = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            switch (w.ElementAtOrDefault(0))
            {
                case null: break;
                case "list":
                    var all = k.Queue;
                    var queue = all.Where(q => !q.PlanCheck).ToList();
                    for (int i = 0; i < queue.Count; i++)
                        Console.WriteLine($"{i,3} {Format.QueueStageName(queue[i].Stage),-13} {queue[i].GameId}{(queue[i].Note is { } n ? "  (" + Format.QueueNote(n) + ")" : "")}");
                    Console.WriteLine(k.QueueRunning ? "队列运行中" : "队列未运行");
                    if (all.Count(q => q.PlanCheck && q.Stage is not (QueueStage.Done or QueueStage.Failed or QueueStage.Stopped)) is var checking and > 0)
                        Console.WriteLine($"正在检查 {checking} 款游戏是否有新内容可编译");
                    break;
                case "add" when w.Length > 1: k.Enqueue(Id(w, 1, w.Length)); break;
                case "remove" when w.Length > 1: k.Remove(Id(w, 1, w.Length)); break;
                case "move" when w.Length > 2 && int.TryParse(w[^1], out var to): k.MoveInQueue(Id(w, 1, w.Length - 1), to); break;
                case "start": k.StartQueue(); break;
                case "stop": k.StopQueue(); break;
                case "wait": await k.WhenQueueIdle(); break;
                case "quit": k.StopQueue(); await k.WhenQueueIdle(); return 0;   // graceful: the driver writes its cache
                default: Console.WriteLine("可用命令：list | add <game> | move <game> <index> | remove <game> | start | stop | wait | quit"); break;
            }
        }
        catch (ArgumentException e) { Console.WriteLine("错误：" + e.Message); }
    }
    await k.WhenQueueIdle();   // end of input: let a started queue finish
    return 0;

    static IEnumerable<string> StdinLines()
    {
        while (Console.ReadLine() is { } l) yield return l;
    }
}

async Task<int> CacheClear()
{
    if (args.Length < 3) return Fail("cache clear <game>");
    var k = await Open();
    var g = Match(k.Games, args[2]);
    var precache = args.Contains("--game-precache");
    var parts = k.GameCaches(g.Game.Id, precache);
    var gap = k.DriverCacheGap(g.Game.Id);
    if (parts.Count == 0) return Fail($"未找到 {g.Game.Name} 的着色器缓存文件" + (gap != null ? $"（{gap}）" : ""));
    foreach (var p in parts)
    {
        Console.WriteLine($"{p.Name}  {Format.Bytes(p.Bytes)}");
        foreach (var f in p.Files) Console.WriteLine($"  {f}");
    }
    if (gap != null) Console.WriteLine($"提示：{gap}");
    if (!precache && k.GameCaches(g.Game.Id, true).Where(p => p.Name is ScsKiller.PipelinePart or ScsKiller.PrecachePart).ToList() is { Count: > 0 } own)
        Console.WriteLine($"已保留游戏自己的缓存（{string.Join("、", own.Select(p => $"{p.Name} {Format.Bytes(p.Bytes)}"))}）；加入 --game-precache 才会清理");
    if (!Confirm($"删除上面列出的 {g.Game.Name} 着色器缓存（{string.Join("、", parts.Select(p => $"{p.Name} {Format.Bytes(p.Bytes)}"))}）？"
                 + "下次启动需重新生成缓存，状态会变成未预热。")) return 1;
    k.ClearGameCache(g.Game.Id, precache);   // a refusal throws InvalidOperationException: printed as "error: <reason>"
    Console.WriteLine($"已清理 {g.Game.Name} 的着色器缓存");
    return 0;
}

async Task<int> Compile()
{
    if (args.Length < 2) return Fail("compile <game|--all-ready> [--threads N] [--idle | --when-idle] [--careful | --fast]");
    Only("--all-ready", "--threads", "--idle", "--when-idle", "--careful", "--fast");
    int? threads = Opt("--threads") is { } t ? int.TryParse(t, out var n) && n > 0 ? n : throw new ArgumentException("--threads 需要正整数线程数") : null;
    var k = await Open();
    if (threads != null) k.ThreadsOverride = threads;
    bool whenIdle = args.Contains("--when-idle");
    k.Background = args.Contains("--idle");
    if (k.Background || whenIdle) BackgroundPriority();
    var targets = args[1] == "--all-ready"
        ? k.Games.Where(s => s.Status is GameStatus.Ready or GameStatus.Stale && s.NoStutter == null && !s.CompileUnreached).ToList()
        : [Match(k.Games, args[1])];
    if (targets is [{ ReasonFlags: var flags } refused] && flags.HasFlag(GameReasonFlags.CacheUnreachable)) return Fail($"{refused.Game.Name}：{Format.Reason(refused)}");
    if (args.Contains("--careful") || args.Contains("--fast"))
        foreach (var g in targets)
        {
            k.SetCarefulCompile(g.Game.Id, args.Contains("--careful"));
            Console.WriteLine($"{g.Game.Name}: {CarefulLine(k.Games.Single(s => s.Game.Id == g.Game.Id).Careful)}");
        }
    return await RunQueue(k, targets, whenIdle);
}

async Task<int> RewarmStale(bool ifDriverChanged)
{
    var k = await Open();
    var stale = ifDriverChanged ? k.DriverStaleGames() : k.StaleGames();
    if (stale.Count == 0) { Console.WriteLine("没有需要重新预热的游戏"); return 0; }
    switch (k.Settings.OnDriverUpdate)
    {
        case DriverUpdateMode.Off:
            Console.WriteLine($"{stale.Count} 个游戏需要重新预热；自动预热已关闭。");
            return 0;
        case DriverUpdateMode.Ask:
            if (!k.ShouldNotifyStale()) { Console.WriteLine($"{stale.Count} 个游戏需要重新预热；用户已对当前驱动选择跳过。"); return 0; }
            if (ScheduledTask.AppExe() is { } app)   // the app shows the notification (compile now / when idle / skip)
            {
                Process.Start(new ProcessStartInfo(app, ScheduledTask.DriverUpdatedArg) { UseShellExecute = false })?.Dispose();
                Console.WriteLine($"{stale.Count} 个游戏需要重新预热；已通过 {app} 通知用户");
                return 0;
            }
            Console.WriteLine($"{stale.Count} 个游戏需要重新预热（驱动 {k.Vendor.Gpu.DriverVersion}）：");
            foreach (var s in stale) Row(s);
            Console.WriteLine("运行 scskiller compile <game> 或打开 SCSKiller 重新预热。");
            return 0;
        default:
            BackgroundPriority();
            return await RunQueue(k, stale, whenIdle: true);
    }
}

int Cache()
{
    var v = GpuBackends.Detect();
    Console.WriteLine($"显卡：{v.Gpu.Name}（{v.Vendor}），驱动 {v.Gpu.DriverVersion}，配置 {v.Caps.Profile}");
    if (args.Length > 1 && args[1] == "set")
    {
        if (args.Length < 3) return Fail("cache set <GB|unlimited|default>");
        if (!v.Caps.CacheSizeConfigurable) return Fail("此显卡的缓存上限不可调整");
        var limit = args[2].ToLowerInvariant() switch
        {
            "default" => new CacheLimit(null, true),
            "unlimited" => new CacheLimit(null, false),
            var n when double.TryParse(n, System.Globalization.CultureInfo.InvariantCulture, out var gb) && gb > 0 => new CacheLimit((long)(gb * 1073741824), false),
            _ => throw new ArgumentException("容量必须是 GB 数值、unlimited 或 default"),
        };
        if (!Elevated.IsAdmin) return Fail("调整驱动着色器缓存上限需要管理员权限");
        var what = limit.IsDriverDefault ? "驱动默认值" : limit.Bytes == null ? "无限制" : Format.Bytes(limit.Bytes);
        if (!args.Contains(Elevated.YesArg) && !Confirm($"将 NVIDIA 全局着色器缓存上限设为 {what}？此驱动设置会影响所有游戏。"))
            return 1;
        v.SetCacheLimit(limit);
        Outcome.Message = $"着色器缓存上限已设为 {what}。";
    }
    var usage = v.GetCacheUsage();
    if (v is AmdBackend)   // GetCacheUsage sums both folders
        Console.WriteLine($"缓存：磁盘占用 {(usage.UpperBound ? "<= " : "")}{Format.Bytes(usage.BytesOnDisk)} = {AmdBackend.CacheDir} {Format.Bytes(AmdBackend.Bytes(AmdBackend.CacheDir))} (D3D12)" +
                          $" + {AmdBackend.D3D11CacheDir} {Format.Bytes(AmdBackend.Bytes(AmdBackend.D3D11CacheDir))} (D3D11)");
    else Console.WriteLine($"缓存：{usage.Path}，磁盘占用 {(usage.UpperBound ? "<= " : "")}{Format.Bytes(usage.BytesOnDisk)}");
    var l =v.GetCacheLimit();
    Console.WriteLine($"上限：{(l == null ? "无法读取" : l.Bytes == null ? "无限制" : Format.Bytes(l.Bytes))}{(l?.IsDriverDefault == true ? "（驱动默认值）" : "")}");
    if (v is NvidiaBackend nv)
    {
        var (id, raw, def) = nv.ReadCacheSetting();
        Console.WriteLine($"       设置 0x{id:X8} '{NvidiaBackend.CacheSizeSettingName}' = {(raw is { } r ? $"0x{r:X8}" : "未设置")}，驱动默认值 0x{def:X8}");
    }
    return 0;
}

async Task<int> Record()
{
    bool alongside = args.Length > 3 && args[1] == "alongside" && args[3] is "on" or "off";
    bool antiCheat = args.Length > 3 && args[1] == "anti-cheat" && args[3] is "on" or "off";
    if (!alongside && !antiCheat && (args.Length < 3 || args[1] is not ("install" or "uninstall" or "clear")))
        return Fail("用法：record install|uninstall|clear <game> | record alongside|anti-cheat <game> on|off");
    var k = await Open();
    var g = Match(k.Games, args[2]);
    if (antiCheat)
    {
        bool on = args[3] == "on";
        if (on && g.AntiCheat == AntiCheat.None) return Fail($"{g.Game.Name} 当前未检测到反作弊；普通录制请使用 record install");
        if (on && !Confirm($"检测到 {g.AntiCheat}。直接录制会在游戏目录放入 d3d12.dll；游戏可能拒绝运行、判定篡改或封禁账号。"
            + "此选项只解除 SCSKiller 自身的门禁，不会关闭或绕过游戏反作弊。仍要开启吗？")) return 1;
        k.SetAntiCheatRecording(g.Game.Id, on);
        var state = k.Games.Single(s => s.Game.Id == g.Game.Id);
        Console.WriteLine($"{g.Game.Name}：反作弊直接录制许可已{(on ? "开启" : "关闭")}；录制器{(state.RecorderInstalled ? "已安装" : "未安装")}" +
            (state.RecorderSkip is { } skip ? $"；原因：{skip}" : state.RecorderNote is { } note ? $"；状态：{note}" : ""));
        return 0;
    }
    if (args[1] == "clear")
    {
        if (g.RecordingBytes == 0) return Fail($"{g.Game.Name} 没有录制数据");
        if (!Confirm($"删除 {g.Game.Name} 的录制数据（{Format.Bytes(g.RecordingBytes)}）？已录到的管线会被移除；"
                     + "只有开启录制并再次由游戏创建时才能重新获得，编译计划会退回到游戏文件可提供的内容。")) return 1;
        k.ClearRecording(g.Game.Id);   // a refusal throws InvalidOperationException: printed as "error: <reason>"
        Console.WriteLine($"已清理 {g.Game.Name} 的录制数据");
        return 0;
    }
    if (alongside)
    {
        var mod = k.RecorderMod(g.Game.Id);
        k.SetRecordAlongsideMod(g.Game.Id, args[3] == "on");
        var s = k.Games.Single(x => x.Game.Id == g.Game.Id);
        Console.WriteLine($"与 {mod ?? "mod"} 串接录制：{args[3]}；录制器{(s.RecorderInstalled ? "已安装" : "未安装")}"
            + ((s.RecorderSkip == ScsKiller.SkipModNotChainable ? ScsKiller.NotChainableReason(s.RecorderMod) : s.RecorderSkip ?? s.RecorderNote) is { } why ? $" ({why})" : ""));
        return 0;
    }
    if (args[1] == "install") k.InstallRecorder(g.Game.Id); else k.UninstallRecorder(g.Game.Id);
    Console.WriteLine($"录制器已{(args[1] == "install" ? "安装" : "卸载")}：{Path.GetDirectoryName(g.Game.ExePath)}");
    return 0;
}

async Task<int> Key()
{
    if (args.Length < 3) return Fail("key <game> <hex>");
    var k = await Open();
    var g = Match(k.Games, args[1]);
    if (!k.SetEncryptionKey(g.Game.Id, args[2])) return Fail($"此密钥无法打开 {g.Game.Name} 的文件");
    await k.RescanAsync(CancellationToken.None);
    Console.WriteLine($"已接受 {g.Game.Name} 的密钥：{Format.Reason(k.Games.First(s => s.Game.Id == g.Game.Id))}");
    return 0;
}

static async Task<(ScsKiller K, GameState G, IEngineReader Reader, EngineInfo Engine, ShaderIndex Index)> OpenIndex(string query, IProgress<string>? log = null)
{
    var k = await Open();
    var g = Match(k.Games, query);
    var reader = ScsKiller.DefaultReaders();
    var engine = g.Engine ?? reader.Detect(g.Game) ?? throw new InvalidOperationException($"{g.Game.Name}：不支持此引擎");
    return (k, g, reader, engine, reader.Index(g.Game, engine, log, CancellationToken.None));
}

async Task<int> Index()
{
    if (args.Length < 2 || Opt("--out") is not { } outDir) return Fail("index <game> --out <dir>");
    var (_, _, _, engine, index) = await OpenIndex(args[1], new Progress<string>(Console.WriteLine));
    Directory.CreateDirectory(outDir);
    await File.WriteAllTextAsync(Path.Combine(outDir, "index.json"), JsonSerializer.Serialize(new { engine, index }, AppStore.Json));
    Console.WriteLine($"{index.Shaders.Count} 个着色器、{index.Maps.Count} 个着色器映射；内容哈希 {index.ContentHash} -> {outDir}");
    return 0;
}

async Task<int> RehydrateCommand()
{
    if (args.Length < 3 || Opt("--out") is not { } outDb) return Fail("rehydrate <game> <hash-only.db> --out <db> [--expect <content hash>]");
    var (_, g, reader, engine, index) = await OpenIndex(args[1]);
    Console.WriteLine($"索引内容哈希 {index.ContentHash}");
    var r = SCSKiller.Core.Planning.Rehydrate.Run(args[2], outDb, g.Game, engine, reader, index, Opt("--expect"),
        moreBlobs: h => SCSKiller.Core.Planning.Middleware.Blobs(g.Game, h));
    Console.WriteLine($"引用 {r.Referenced}、数据库中已有 {r.AlreadyPresent}、找到 {r.Found}（中间件 DLL 中 {r.FoundInMiddleware}）、缺失 {r.Missing.Count}" +
        (r.ContentHashMatches is { } m ? $"，内容哈希{(m ? "匹配" : "不匹配")}" : "") + $" -> {outDb}（{r.OutputBytes:N0} 字节）");
    foreach (var h in r.Missing) Console.WriteLine($"  缺失 {h}");
    return r.Complete ? 0 : 2;
}

async Task<int> ReferenceExport()
{
    if (args.Length < 3) return Fail("reference export <game> [--out <file>]");
    var (k, g, _, _, index) = await OpenIndex(args[2]);
    // middleware and runtime-built shaders aren't in the index: the recording has their bytes
    var recording = Path.Combine(k.Store.GameDir(g.Game.Id), "recording.db");
    var seen = File.Exists(recording)
        ? SCSKiller.Core.Planning.PsoDb.Read(recording).Where(r => r.Tag == 'B' && r.Payload.Length > 20 && !SCSKiller.Core.Carved.Dxbc.IsRootSignatureOnly(r.Payload.AsSpan(20)))
            .Select(r => Convert.ToHexStringLower(r.Payload.AsSpan(0, 20)))
        : [];
    var shaders = index.Shaders.Keys.Concat(seen).Distinct().Order(StringComparer.Ordinal).ToArray();
    var outFile = Opt("--out") ?? g.Game.Id.Replace(':', '_') + ".reference.json";
    await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(new
    {
        content_hash = index.ContentHash,
        store_build_key = g.Game.Version is { } v ? $"{g.Game.Id}@{v}" : null,
        shaders,
    }));
    Console.WriteLine($"{shaders.Length} 个着色器（索引中 {index.Shaders.Count} 个），内容哈希 {index.ContentHash} -> {outFile}");
    return 0;
}

// Read-only. Run it before and after switching something in the NVIDIA App and diff the two outputs.
static int NvidiaSnapshot()
{
    if (GpuBackends.Detect() is not NvidiaBackend nv) return Fail("未检测到 NVIDIA 显卡");
    Console.WriteLine($"驱动 {nv.Gpu.DriverVersion}");
    Console.WriteLine($"自动着色器编译：{nv.GetAutoShaderCompilation()?.ToString() ?? "无法读取"}（设置 0x{NvidiaBackend.AutoShaderCompilationId:X8}）");
    Console.WriteLine($"NvOSC.exe：{NvidiaBackend.NvOscPath() ?? "未找到"}");
    Console.WriteLine("基础配置：");
    foreach (var (id, name, type, value) in NvidiaBackend.BaseProfileSettings())
        Console.WriteLine($"  0x{id:X8} 类型 {type} = 0x{value:X8}  {name}");
    foreach (var task in new[] { $@"\Users\{Environment.UserName}{NvidiaBackend.AutoShaderTaskName}", NvidiaBackend.AutoShaderTaskName })
    {
        var psi = new ProcessStartInfo("schtasks.exe", ["/Query", "/TN", task, "/V", "/FO", "LIST"]) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        Console.WriteLine($"任务 {task}：" + (p.ExitCode == 0 ? output.Trim() : "未注册"));
    }
    Console.WriteLine($"NvOSC.exe 正在运行：{Process.GetProcessesByName("NvOSC").Length > 0}");
    return 0;
}

// What the Settings toggle does, for an elevated prompt: the driver setting + NvOSC.exe -register/-deregister.
int NvidiaAutoShader()
{
    if (!Enum.TryParse<AutoShaderCompilation>(args.ElementAtOrDefault(1), true, out var level) || !Enum.IsDefined(level) || int.TryParse(args[1], out _))
        return Fail("nvidia-auto-shader off|low|medium|high");
    if (GpuBackends.Detect() is not NvidiaBackend nv) return Fail("未检测到 NVIDIA 显卡");
    if (!Elevated.IsAdmin) return Fail("调整 NVIDIA 自动着色器编译需要管理员权限");
    // NvOSC.exe -register registers the idle task for the account running it: refuse when UAC elevated a different
    // administrator (over-the-shoulder), whose DXCache it would compile into. Same account + elevated token is fine.
    if (Opt(Elevated.ForUserArg) is { } sid && sid != Elevated.CurrentUserSid)
        return Fail($"Windows 使用其他账号（{WindowsIdentity.GetCurrent().Name}）提权，NVIDIA 任务会为该账号编译。"
                    + "请使用管理员账号登录 Windows，或请管理员为当前账号授予管理员权限。");
    if (!args.Contains(Elevated.YesArg) && !Confirm($"将 NVIDIA 自动着色器编译设为 {level}？这会修改全局驱动设置及空闲任务。"))
        return 1;
    nv.SetAutoShaderCompilation(level);
    Console.WriteLine($"当前：{nv.GetAutoShaderCompilation()?.ToString() ?? "无法读取"}");
    Outcome.Message = level == AutoShaderCompilation.Off ? "已关闭自动着色器编译。" : $"已开启自动着色器编译（{level}）。";
    return 0;
}

int TaskCommand()
{
    switch (args.ElementAtOrDefault(1))
    {
        case "register":
            ScheduledTask.Register(ScheduledTask.TaskExe() ?? Environment.ProcessPath!);
            Console.WriteLine($"已注册任务 {ScheduledTask.Name}");
            return 0;
        case "unregister":
            ScheduledTask.Unregister();
            Console.WriteLine($"已移除任务 {ScheduledTask.Name}");
            return 0;
        default:
            return Fail("task register|unregister");
    }
}

static int FetchCodecs(string dir)
{
    // Packaging (publish.ps1): the pinned DLLs in the build's cache, copied next to the exes, where they seed each user's
    // codecs folder (Codecs)
    try
    {
        Console.WriteLine(Codecs.Ensure(OodleHelper.OodleFileName, Codecs.DownloadOodle, dir));
        Console.WriteLine(Codecs.Ensure(ZlibHelper.DllName, p => ZlibHelper.DownloadDll(p, null!), dir));
        return 0;
    }
    catch (InvalidDataException e) { return Fail(e.Message); }
}

/// <summary>What the command's last word was (its success line or the error), for <see cref="Elevated.ResultArg"/>.</summary>
static class Outcome { public static string? Message; }

// Not Progress<T>: it posts to the thread pool, so its last messages could be lost when the process exits.
sealed class StderrLog : IProgress<string>
{
    public void Report(string value) => Console.Error.WriteLine("  " + value);
}

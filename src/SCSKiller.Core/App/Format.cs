using System.Text.RegularExpressions;

namespace SCSKiller.Core.App;

/// <summary>Text the app and the CLI show.</summary>
public static class Format
{
    public const string Dash = "—";

    /// <summary>"3.2 GB" from 1 GiB up, else whole MB; a dash for null.</summary>
    public static string Bytes(long? b) => b switch
    {
        null => Dash,
        >= 1L << 30 => $"{b / (double)(1L << 30):0.#} GB",
        _ => $"{b / (double)(1 << 20):0} MB",
    };

    /// <summary><see cref="ScsKiller.Duration"/>; a dash for null.</summary>
    public static string Duration(TimeSpan? t) => t is { } v ? ScsKiller.Duration(v) : Dash;

    /// <summary>"Carved DXBC", "Unity 2022.3.10"; just the family (or <paramref name="family"/>) when the reader found no
    /// version: Carved and FromSoft say "-", Unity "?".</summary>
    public static string Engine(EngineInfo e, string? family = null) =>
        e.Version is "" or "-" or "?" ? family ?? e.Family : $"{family ?? e.Family} {e.Version}";

    /// <summary>The item is being compiled: any stage before it finishes, paused included.</summary>
    public static bool Running(QueueItem q) => q.Stage is QueueStage.Indexing or QueueStage.Planning or QueueStage.Materializing
        or QueueStage.Warming or QueueStage.Paused;

    /// <summary>"FSR4: 80 known pipelines, compiled with the game", "DLSS: compiled by the NVIDIA driver itself",
    /// "XeSS: detected; added after a recording sees them".</summary>
    public static string Middleware(MiddlewareTag t) => t.Label + (t.Pipelines > 0 ? $"：已知 {t.Pipelines:N0} 条管线，会随游戏一起编译"
        : t.Label == "DLSS" ? "：由 NVIDIA 驱动自行编译" : "：已检测到，录制识别后可加入编译");

    /// <summary>The Library's notice that this GPU can't compile; null on NVIDIA and AMD, or once closed for this GPU
    /// (<paramref name="dismissedFor"/>: <see cref="Settings.GpuNoticeDismissed"/>).</summary>
    public static string? GpuNotice(GpuInfo gpu, string? dismissedFor) => gpu.Vendor is GpuVendor.Nvidia or GpuVendor.Amd || dismissedFor == gpu.Name ? null
        : gpu.Vendor switch
        {
            GpuVendor.Intel => "SCSKiller 暂不支持在 Intel 显卡上编译：尚未完成其驱动着色器缓存行为的测量，后续计划支持。",
            GpuVendor.Qualcomm => "SCSKiller 暂不支持在 Qualcomm 显卡上编译：尚未完成其驱动着色器缓存行为的测量。",
            _ => "SCSKiller 暂不支持在此显卡上编译：目前只了解 NVIDIA 与 AMD 驱动的着色器缓存机制。",
        };

    /// <summary>A Library row's note: a few words for the status's reason (the whole one is in the row's tooltip and on the
    /// game's page); null when the status title says it. Read from the core's reasons (ScsKiller.Evaluate, Planner.Check,
    /// the engine readers); an unknown one shows its first clause.</summary>
    public static string? ShortNote(GameState s)
    {
        var r = s.StatusReason;
        bool Has(string part) => r.Contains(part, StringComparison.Ordinal);
        bool Starts(string part) => r.StartsWith(part, StringComparison.Ordinal);
        var partly = ScsKiller.IsPartlyWarmed(s);
        return s.Status switch
        {
            _ when s.CompileUnreached => "此游戏需要专用加载方式",
            GameStatus.Warmed when ScsKiller.IsPartlyCompiled(s) => $"驱动跳过了 {(s.LastWarmFailed ?? 0) + (s.LastWarmCrashed ?? 0):N0} / {ScsKiller.PlanItems(s.Plan) ?? 0:N0} 条管线",
            GameStatus.Warmed when partly => $"驱动 {s.WarmedDriverVersion} · 首次启动仍编译了 {s.Careful!.LaunchCompiled * 100:0}%",
            GameStatus.Warmed when ScsKiller.RtAfterRecording(s) && s.NoStutter == null => s.RecorderInstalled ? "录制器已开启：请启用光追并游玩"
                : ScsKiller.RecordedEnough(s) ? "光追仍需要录制" : "光追还需要约 5 分钟录制",
            GameStatus.Warmed when ScsKiller.BothApis(s.Plan) => "DirectX 11 和 12" + (ScsKiller.IsPartial(s.Plan) ? " · 部分覆盖" : ""),
            GameStatus.Warmed => $"驱动 {s.WarmedDriverVersion}" + (ScsKiller.IsPartial(s.Plan) ? " · 部分覆盖" : ""),
            GameStatus.Stale => StaleNote(r),
            GameStatus.NeedsRecording when s.ReasonFlags.HasFlag(GameReasonFlags.OfflineSessionRequired) => "需要离线录制会话",
            GameStatus.NeedsRecording when s.NoStutter != null && !s.RecorderInstalled => "无需编译",
            GameStatus.NeedsRecording when s.RecordingPaused => "录制已达到容量上限",
            GameStatus.NeedsRecording when ScsKiller.NeverRecorded(s) => null,   // the title: ScsKiller.NothingRecordedTitle
            GameStatus.NeedsRecording when s.RecorderInstalled && !ScsKiller.RecordedEnough(s) => Has(Planning.Planner.Dx12Only) ? "录制器已开启：请用 DirectX 12 游玩" : "录制器已开启：请游玩约 5 分钟",
            GameStatus.NeedsRecording when Starts(ScsKiller.RtNeedsRecording) => "用于光线追踪效果" + (s.InCommunityDb == true ? " · 社区数据库已有录制" : ""),
            GameStatus.NeedsRecording when s.InCommunityDb == true => "社区数据库已有录制",
            GameStatus.NeedsRecording when Starts("the recording has no draws") => "请进入游戏世界并游玩",
            GameStatus.NeedsRecording when s.RecorderInstalled => "录制数据仍不足",
            GameStatus.NeedsRecording when s.RecorderSkip != null => "录制器当前不可用",
            GameStatus.NeedsRecording => "开启录制并游玩",
            GameStatus.Ready when Has("ray-traced effects aren't compiled") => s.AntiCheat == AntiCheat.None ? "光追效果尚未编译" : $"光追录制受 {AntiCheatName(s.AntiCheat)} 限制",
            GameStatus.Ready when Has(ScsKiller.RtUnseenNote) => "录制中未观察到光追",
            GameStatus.Ready when Has(ScsKiller.RtInlineNote) => "路径追踪需要录制",
            GameStatus.Ready when ScsKiller.IsPartial(s.Plan) => "部分覆盖",
            GameStatus.Ready when Has(Planning.Planner.UntestedNote) => "此引擎版本尚未测试",
            GameStatus.Ready when Has("for DirectX 12, ") => "DirectX 11 可编译；DirectX 12 需要录制",
            GameStatus.Ready when Has("also compiles every DirectX 11 shader") => "DirectX 11 和 12",
            GameStatus.Ready when Starts("compiles every DirectX 11 shader") => "DirectX 11",
            GameStatus.Ready => null,
            _ when s.ShaderModBlocks => $"{s.ShaderMod} 会改变全部管线",
            _ when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) => $"需确认{AntiCheatName(s.AntiCheat)}录制风险" + (s.InCommunityDb == true ? " · 社区数据库已有录制" : ""),
            _ when s.ReasonFlags.HasFlag(GameReasonFlags.CacheUnreachable) => "编译无法覆盖此游戏",
            _ when Has(ScsKiller.ManualNoRecording) => "需要录制：请先确认游戏目录",
            _ when s.Engine?.Encrypted == true => null,   // the title: "Encrypted game files"
            _ when s.Engine?.Unsupported is { } u => u.StartsWith("no D3D shaders", StringComparison.Ordinal) ? "未找到 DirectX 着色器" : "引擎暂不支持",
            _ when s.Engine == null => r == "engine not supported yet" ? "引擎暂不支持" : "无法读取游戏文件",
            _ when Starts("runs on ") => "游戏使用 " + FirstClause(r["runs on ".Length..]),
            _ when Starts("not supported on this GPU") => "此显卡暂不支持",
            _ => Reason(s),
        };
    }

    /// <summary>Player-facing reason. Core reasons remain stable for diagnostics and existing saved state.</summary>
    public static string Reason(GameState s)
    {
        var r = s.StatusReason;
        var db = s.InCommunityDb == true ? "；上游社区数据库已有此版本的录制数据" : "";
        if (s.CompileUnreached) return "编译结果未进入此游戏使用的驱动缓存；此游戏可能需要专用加载方式";
        if (ScsKiller.IsPartlyCompiled(s))
            return $"驱动跳过了 {(s.LastWarmFailed ?? 0) + (s.LastWarmCrashed ?? 0):N0} 条管线，首次在游戏中使用时仍可能卡顿";
        return s.Status switch
        {
            GameStatus.Warmed when ScsKiller.IsPartlyWarmed(s) => $"已为驱动 {s.WarmedDriverVersion} 部分预热；游戏首次启动仍编译了 {s.Careful!.LaunchCompiled * 100:0}% 的管线",
            GameStatus.Warmed => $"已为驱动 {s.WarmedDriverVersion} 预热" + (ScsKiller.RtAfterRecording(s) ? "；光追效果还需录制" : ""),
            GameStatus.Stale => StaleNote(r) + "；请重新编译",
            GameStatus.NeedsRecording when s.ReasonFlags.HasFlag(GameReasonFlags.OfflineSessionRequired) => "此游戏需要离线录制会话；请在详情页阅读风险说明",
            GameStatus.NeedsRecording when s.RecordingPaused => "录制达到容量上限并暂停；请调整上限或清空录制数据",
            GameStatus.NeedsRecording when ScsKiller.NeverRecorded(s) => "上次游玩未录到数据；请查看详情页中的录制器状态",
            GameStatus.NeedsRecording when r.StartsWith(ScsKiller.RtNeedsRecording, StringComparison.Ordinal) => "光追效果需要短时间录制" + db,
            GameStatus.NeedsRecording when r.StartsWith("the recording has no draws", StringComparison.Ordinal) => "录制中尚无绘制数据；请进入游戏世界并游玩" + db,
            GameStatus.NeedsRecording when s.RecorderInstalled => (ScsKiller.RecordedEnough(s)
                ? "录制器已开启；继续游玩以收集仍缺少的管线" : RecorderOnNote(s)) + db,
            GameStatus.NeedsRecording => "开启录制并正常游玩约 5 分钟，以收集编译所需的着色器布局" + db,
            GameStatus.Ready when s.ReasonFlags.HasFlag(GameReasonFlags.PlannerUntested) => "此引擎版本的编译规则尚未验证；录制可帮助完善计划",
            GameStatus.Ready when ScsKiller.IsPartial(s.Plan) => "可以编译已识别的部分；其余内容可能需要录制或在游玩时编译",
            GameStatus.Ready => "已具备编译条件" + (r.Contains("DirectX 11", StringComparison.Ordinal) ? "；包含 DirectX 11 着色器" : ""),
            _ when s.ReasonFlags.HasFlag(GameReasonFlags.AntiCheatRecordingBlocked) => $"需要录制，但检测到 {AntiCheatName(s.AntiCheat)}；请在详情页确认风险后选择是否录制" + db,
            _ when s.ReasonFlags.HasFlag(GameReasonFlags.CacheUnreachable) => "编译无法覆盖此游戏使用的驱动缓存；可清理未使用的缓存以释放空间",
            _ when s.ShaderModBlocks => $"{s.ShaderMod ?? "着色器模组"} 会改变全部管线，当前无法提前编译",
            _ when s.Engine?.Encrypted == true => "游戏文件已加密，无法读取着色器",
            _ when r.StartsWith("runs on ", StringComparison.Ordinal) => "游戏使用 " + r["runs on ".Length..],
            _ when r.StartsWith("not supported on this GPU", StringComparison.Ordinal) => "此显卡暂不支持提前编译",
            _ when r == "An app update is needed for support" => "需要更新 SCSKiller 才能支持此游戏",
            _ when r.Contains("crashes this game or the driver", StringComparison.Ordinal) => "提前编译可能导致游戏或驱动崩溃，此游戏已标记为不支持",
            _ when s.Engine?.Unsupported != null => "当前无法读取此游戏的着色器，暂不支持提前编译",
            _ when s.Engine == null => "暂时无法识别或读取此游戏的引擎和着色器",
            _ => "当前无法为此游戏生成可用的编译计划",
        };
    }

    /// <summary>A shader mod that doesn't block the game, after its reason: "; RenoDX changes this game's pipelines: ...".
    /// None for an unsupported game: its status and reason alone.</summary>
    public static string ModNote(GameState s) =>
        s is { ShaderMod: not null, ShaderModBlocks: false } && s.Status != GameStatus.Unsupported
            ? s.ShaderModLayer
                ? $"；{s.ShaderMod} 会改变游戏管线，编译会使用 ReShade 与模组的副本" +
                    (s.ShaderModAsD3D12 ? "；游戏运行时新增的管线需开启“与 ReShade 一同录制”才能收集" : "；游戏运行时新增的管线会随游玩录制")
                : $"；{s.ShaderMod} 会替换部分着色器，这些管线不会被提前编译"
            : "";

    /// <summary>Recorder in, not long enough yet: the DirectX 12 hint stays, a DirectX 11 run never loads the recorder.</summary>
    public static string RecorderOnNote(GameState s) => "录制器已开启：请游玩约 5 分钟"
        + (s.StatusReason.Contains(Planning.Planner.Dx12Only, StringComparison.Ordinal) ? "；请使用 DirectX 12，游戏也可能以 DirectX 11 运行" : "");

    public static string RecorderRefused(string reason)
    {
        if (reason == ScsKiller.NeverSawNote)
            return "上次启动时录制器未加载；目前无法判断是游戏、反作弊还是其他工具的加载顺序造成。";
        if (reason == ScsKiller.GameChangedNote)
            return "游戏文件发生变化，录制器已重新设置；请再次启动。";
        if (reason.StartsWith("The last launch ran ", StringComparison.Ordinal))
            return "上次启动运行了与录制器设置时不同的执行文件；现已重新设置，请再次启动。";
        const string prefix = "The last launch wasn't recorded: ";
        if (!reason.StartsWith(prefix, StringComparison.Ordinal)) return reason;
        var code = reason[prefix.Length..].TrimEnd('.');
        return code switch
        {
            "anti-cheat next to the exe" => "代理检测到执行文件旁的反作弊组件，因此拒绝录制。",
            "anti-cheat client loaded" => "代理检测到已加载的反作弊客户端，因此拒绝录制。",
            "Riot Games install" => "代理检测到 Riot Games 安装目录，因此拒绝录制。",
            "markers= malformed" => "代理的反作弊标记清单格式无效，因此拒绝录制。",
            _ => $"代理拒绝录制（原因代码：{code}）。",
        };
    }

    /// <summary>Queue notes are diagnostic values in Core; translate the known ones only when shown to a player.</summary>
    public static string QueueNote(string note)
    {
        if (note == ScsKiller.WhenIdleNote) return "电脑空闲时开始";
        if (note == ScsKiller.AnotherCompileNote) return "正在等待另一进程完成此游戏的编译";
        if (note == "already compiled: the new plan adds nothing") return "已经编译；新计划没有增加内容";
        if (note == "plan built") return "编译计划已生成";
        if (note == "paused until the PC is idle") return "已暂停，等待电脑空闲";
        if (note == "recovering from a GPU driver crash") return "正在从显卡驱动崩溃中恢复";
        if (note.StartsWith("no progress for ", StringComparison.Ordinal) && note.EndsWith(" min", StringComparison.Ordinal))
            return $"已 {note["no progress for ".Length..^" min".Length]} 分钟无进展";
        if (note.StartsWith("retrying ray tracing with fewer threads (", StringComparison.Ordinal) && note.EndsWith(')'))
            return $"正使用更少线程重试光追编译（{note["retrying ray tracing with fewer threads (".Length..^1]}）";
        if (note.StartsWith("paused while ", StringComparison.Ordinal) && note.EndsWith(" is running", StringComparison.Ordinal))
            return $"{note["paused while ".Length..^" is running".Length]} 运行时暂停";
        if (note.StartsWith("stopped while ", StringComparison.Ordinal) && note.Contains(" is running: continues when it exits", StringComparison.Ordinal))
        {
            var end = note.IndexOf(" is running: continues when it exits", StringComparison.Ordinal);
            return $"{note["stopped while ".Length..end]} 运行时暂停，退出后继续" + note[(end + " is running: continues when it exits".Length)..];
        }
        var counts = Regex.Replace(note, @"([\d,]+) failed \(the driver rejected them\)", "$1 条失败（驱动拒绝）");
        counts = Regex.Replace(counts, @"([\d,]+) skipped \(a shader not in this install\)", "$1 条跳过（当前游戏版本缺少着色器）");
        counts = Regex.Replace(counts, @"([\d,]+) skipped \((?:it crashes|they crash) the GPU driver\)", "$1 条跳过（会使显卡驱动崩溃）");
        if (counts != note) return counts.Replace(", ", "，", StringComparison.Ordinal);
        if (note.StartsWith("driver changed:", StringComparison.Ordinal) || note.StartsWith("game updated", StringComparison.Ordinal)
            || note.StartsWith("game shaders changed", StringComparison.Ordinal)) return StaleNote(note);
        return note;
    }

    public static string QueueStageName(QueueStage stage) => stage switch
    {
        global::SCSKiller.Core.QueueStage.Waiting => "等待中", global::SCSKiller.Core.QueueStage.Indexing => "读取着色器", global::SCSKiller.Core.QueueStage.Planning => "生成计划",
        global::SCSKiller.Core.QueueStage.Materializing => "准备文件", global::SCSKiller.Core.QueueStage.Warming => "编译中", global::SCSKiller.Core.QueueStage.Paused => "已暂停",
        global::SCSKiller.Core.QueueStage.Done => "已完成", global::SCSKiller.Core.QueueStage.Failed => "失败", _ => "已停止",
    };

    public static string QueueProgress(QueueItem q)
    {
        if (q.Progress is not { } p) return "";
        var counts = $"{p.Failed} 条失败" + (p.Skipped > 0 ? $"，{p.Skipped} 条跳过：当前版本缺少着色器" : "");
        return $" {p.Done}/{p.Total}（{counts}）{p.PerSecond:0} 条/秒"
            + (q.Stage is QueueStage.Done or QueueStage.Stopped or QueueStage.Warming && q.Note is { } note ? " - " + QueueNote(note) : "");
    }

    public static string NoStutterReason(string reason) => reason switch
    {
        "Its engine prepares shaders at startup" or "This game prepares its shaders itself" => "游戏会自行准备着色器，通常无需额外预编译",
        "The driver doesn't use a compile's cache for this game" => "驱动不会在此游戏中使用提前编译得到的缓存",
        "Its anti-cheat doesn't allow it" => "游戏反作弊不允许此项操作",
        "Compiling crashes this game or the driver" => "提前编译会使游戏或显卡驱动崩溃",
        "SCSKiller can't read this game's shaders yet" => "SCSKiller 暂时无法读取此游戏的着色器",
        _ => reason,
    };

    static string StaleNote(string r) =>
        r.StartsWith("driver changed:", StringComparison.Ordinal) ? $"驱动 {r[(r.LastIndexOf(' ') + 1)..]} 已清除缓存"
        : r == ScsKiller.TrimmedPartReason ? "驱动清理了部分缓存"
        : r == ScsKiller.TrimmedAllReason ? "着色器缓存已移除"
        : r.StartsWith("the compile didn't reach", StringComparison.Ordinal) ? "编译未进入游戏使用的缓存"
        : r.StartsWith("the game runs as", StringComparison.Ordinal) ? "执行文件名大小写已改变"
        : r.StartsWith("game updated", StringComparison.Ordinal) ? "游戏已更新"
        : r.StartsWith("game shaders changed", StringComparison.Ordinal) ? "游戏着色器已改变"
        : r.StartsWith("SCSKiller can now compile", StringComparison.Ordinal) ? "SCSKiller 可编译更多内容"
        : r.StartsWith("Maximum mode", StringComparison.Ordinal) ? "最大模式：还有内容待编译"
        : r.Contains(" new pipeline", StringComparison.Ordinal) ? r[..r.IndexOf(" new ", StringComparison.Ordinal)] + " 条新管线"
        : "编译状态已变化";

    static string FirstClause(string t) => t.Split([": ", "; ", " ("], 2, StringSplitOptions.None)[0];
    static string Sentence(string t) => t.Length > 0 ? char.ToUpperInvariant(t[0]) + t[1..] : t;

    static string AntiCheatName(AntiCheat a) => a switch { AntiCheat.EasyAntiCheat => "EasyAntiCheat", AntiCheat.BattlEye => "BattlEye", _ => "反作弊组件" };
}

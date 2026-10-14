using System.Globalization;
using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Vendors;

namespace SCSKiller.Tests.Platform;

public class FormatTests
{
    [Fact]
    public void Bytes_are_whole_megabytes_below_a_gigabyte_and_one_decimal_from_there()
    {
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(["—", "0 MB", "512 MB", "1024 MB", "1 GB", "3.2 GB"],
                new long?[] { null, 0, 512L << 20, (1L << 30) - 1, 1L << 30, (long)(3.2 * (1L << 30)) }.Select(Format.Bytes));
        }
        finally { CultureInfo.CurrentCulture = was; }
    }

    [Fact]
    public void A_duration_is_ScsKillers_or_a_dash()
    {
        Assert.Equal(Format.Dash, Format.Duration(null));
        Assert.Equal(ScsKiller.Duration(TimeSpan.FromMinutes(125)), Format.Duration(TimeSpan.FromMinutes(125)));
    }

    [Fact]
    public void An_item_runs_from_indexing_until_it_finishes_paused_included()
    {
        QueueStage[] running = [QueueStage.Indexing, QueueStage.Planning, QueueStage.Materializing, QueueStage.Warming, QueueStage.Paused];
        Assert.All(Enum.GetValues<QueueStage>(), s => Assert.Equal(running.Contains(s), Format.Running(new QueueItem("g", s, null, null))));
    }

    [Fact]
    public void A_middleware_tag_says_what_is_compiled()
    {
        Assert.Equal("FSR4：已知 80 条管线，会随游戏一起编译", Format.Middleware(new("FSR4", ["amdxcffx64.dll"], 80)));
        Assert.Equal("DLSS：由 NVIDIA 驱动自行编译", Format.Middleware(new("DLSS", ["nvngx_dlss.dll"], 0)));
        Assert.Equal("XeSS：已检测到，录制识别后可加入编译", Format.Middleware(new("XeSS", ["libxess.dll"], 0)));
    }

    static readonly EngineInfo Ue = new("Unreal", "4.27", null, "D3D12", false, null);

    static GameState S(GameStatus status, string reason, EngineInfo? engine = null, AntiCheat ac = AntiCheat.None) =>
        new(new Game("steam:1", "Game", Store.Steam, @"X:\g", @"X:\g\g.exe"), engine ?? Ue, ac, status, reason,
            null, null, null, null, "610.88", null, null, false, null);

    [Fact]
    public void Player_facing_reasons_explain_consent_and_common_states_without_changing_core_reasons()
    {
        var blocked = S(GameStatus.Unsupported, "needs a recording, which EasyAntiCheat blocks", ac: AntiCheat.EasyAntiCheat)
            with { ReasonFlags = GameReasonFlags.AntiCheatRecordingBlocked };
        Assert.Contains("确认风险", Format.Reason(blocked));
        Assert.Equal("needs a recording, which EasyAntiCheat blocks", blocked.StatusReason);
        Assert.Contains("游戏世界", Format.Reason(S(GameStatus.NeedsRecording, "the recording has no draws: play into the game world")));
        Assert.Contains("请重新编译", Format.Reason(S(GameStatus.Stale, "driver changed: 610.88 -> 616.92")));
        Assert.Contains("已具备编译条件", Format.Reason(S(GameStatus.Ready, "no recording needed")));
    }

    [Fact]
    public void Recorder_refusals_keep_the_proxy_cause_visible_in_Chinese()
    {
        Assert.Contains("反作弊客户端", Format.RecorderRefused("The last launch wasn't recorded: anti-cheat client loaded."));
        Assert.Contains("目前无法判断", Format.RecorderRefused(ScsKiller.NeverSawNote));
        Assert.Contains("反作弊录制许可", Format.RecorderSkip(ScsKiller.SkipAntiCheat));
        Assert.Equal("游戏退出后安装", Format.RecorderNote("installs when the game exits"));
    }

    [Fact]
    public void Queue_display_translates_known_reasons_without_changing_diagnostic_values()
    {
        Assert.Equal("电脑空闲时开始", Format.QueueNote(ScsKiller.WhenIdleNote));
        Assert.Equal("已 3 分钟无进展", Format.QueueNote(ScsKiller.StalledNote(TimeSpan.FromMinutes(3))));
        Assert.Equal("2 条失败（驱动拒绝），3 条跳过（会使显卡驱动崩溃）", Format.QueueNote(ScsKiller.WarmCounts(2, 0, 3)!));
        Assert.Equal("已暂停", Format.QueueStageName(QueueStage.Paused));
        var q = new QueueItem("g", QueueStage.Done, new WarmProgress(5, 9, 2, 10), null, ScsKiller.WarmCounts(2, 0));
        Assert.Contains("2 条失败（驱动拒绝）", Format.QueueProgress(q));
        Assert.Equal("diagnostic detail", Format.QueueNote("diagnostic detail"));
        Assert.Contains("自行准备着色器", Format.NoStutterReason("Its engine prepares shaders at startup"));
    }

    /// <summary>"Not supported yet · An app update is needed for support": an unsupported game's page and Library tooltip
    /// add no notes to its reason, not a shader mod's; other statuses keep them.</summary>
    [Fact]
    public void An_unsupported_game_shows_its_reason_alone()
    {
        var unsupported = S(GameStatus.Unsupported, "An app update is needed for support") with { ShaderMod = "RenoDX" };
        Assert.Equal("", Format.ModNote(unsupported));

        var warmed = unsupported with { Status = GameStatus.Warmed };
        Assert.StartsWith("；RenoDX ", Format.ModNote(warmed));
    }

    /// <summary>A Library row's note is a few words for each reason the core gives (the whole one stays the tooltip's).</summary>
    [Fact]
    public void A_row_note_is_a_short_label_of_the_reason()
    {
        const string packed = "no raw DXBC/DXIL shaders in its files (0.5 GB sampled): shaders are compressed or packed: needs an engine reader";
        var cases = new (GameState State, string? Note)[]
        {
            (S(GameStatus.Ready, Core.Planning.Planner.NoRecording), null),
            (S(GameStatus.Ready, "planned from a recording"), null),
            (S(GameStatus.Ready, Core.Planning.Planner.Untested), "此引擎版本尚未测试"),
            (S(GameStatus.Ready, "compiles every DirectX 11 shader"), "DirectX 11"),
            (S(GameStatus.Ready, "no recording needed; also compiles every DirectX 11 shader (the game may run on either)"), "DirectX 11 和 12"),
            (S(GameStatus.Ready, "compiles every DirectX 11 shader (the game may run on either); for DirectX 12, turn on recording and play for about 5 minutes"), "DirectX 11 可编译；DirectX 12 需要录制"),
            (S(GameStatus.Ready, "no recording needed; ray-traced effects aren't compiled: they need a recording, which BattlEye blocks", ac: AntiCheat.BattlEye), "光追录制受 BattlEye 限制"),
            (S(GameStatus.NeedsRecording, Core.Planning.Planner.Record), "开启录制并游玩"),
            (S(GameStatus.NeedsRecording, Core.Planning.Planner.Record) with { RecorderSkip = ScsKiller.SkipForeignDll }, "录制器当前不可用"),
            (S(GameStatus.NeedsRecording, Core.Planning.Planner.Record) with { RecorderInstalled = true }, "录制器已开启：请游玩约 5 分钟"),
            (S(GameStatus.NeedsRecording, Dx11Too) with { RecorderInstalled = true }, "录制器已开启：请用 DirectX 12 游玩"),
            (S(GameStatus.NeedsRecording, Core.Planning.Planner.Record + "; " + ScsKiller.InDbNote) with { InCommunityDb = true }, "社区数据库已有录制"),
            (S(GameStatus.NeedsRecording, ScsKiller.RtNote(false)) with { InCommunityDb = false }, "用于光线追踪效果"),
            (S(GameStatus.NeedsRecording, "the recording has no draws: play into the game world"), "请进入游戏世界并游玩"),
            (S(GameStatus.Warmed, "warmed for driver 1.0; " + ScsKiller.RtAfterRecordingNote) with { ReasonFlags = GameReasonFlags.RayTracingRecordingAvailable }, "光追还需要约 5 分钟录制"),
            (S(GameStatus.Warmed, "warmed for driver 1.0; " + ScsKiller.RtAfterRecordingNote) with { RecorderInstalled = true, ReasonFlags = GameReasonFlags.RayTracingRecordingAvailable }, "录制器已开启：请启用光追并游玩"),
            (S(GameStatus.Unsupported, "needs a recording, which EasyAntiCheat blocks", ac: AntiCheat.EasyAntiCheat) with { ReasonFlags = GameReasonFlags.AntiCheatRecordingBlocked }, "需确认EasyAntiCheat录制风险"),
            (S(GameStatus.Unsupported, "needs a recording, which its anti-cheat blocks", ac: AntiCheat.Other) with { ReasonFlags = GameReasonFlags.AntiCheatRecordingBlocked }, "需确认反作弊组件录制风险"),
            (S(GameStatus.Unsupported, "needs a recording, which its anti-cheat blocks; " + ScsKiller.InDbNote, ac: AntiCheat.Other) with { InCommunityDb = true, ReasonFlags = GameReasonFlags.AntiCheatRecordingBlocked },
                "需确认反作弊组件录制风险 · 社区数据库已有录制"),
            (S(GameStatus.Unsupported, packed, Ue with { Version = "-", Unsupported = packed }), "引擎暂不支持"),
            (S(GameStatus.Unsupported, "encrypted game files (needs the game's AES key)", Ue with { Encrypted = true, Unsupported = "encrypted game files (needs the game's AES key)" }), null),
            (S(GameStatus.Unsupported, "no D3D shaders (SF_VULKAN_SM5)", Ue with { Unsupported = "no D3D shaders (SF_VULKAN_SM5)" }), "未找到 DirectX 着色器"),
            (S(GameStatus.Unsupported, "engine not supported yet") with { Engine = null }, "引擎暂不支持"),
            (S(GameStatus.Unsupported, "Access to the path is denied.") with { Engine = null }, "无法读取游戏文件"),
            (S(GameStatus.Unsupported, "runs on Vulkan"), "游戏使用 Vulkan"),
            (S(GameStatus.Unsupported, "runs on DirectX 11 (its config)"), "游戏使用 DirectX 11"),
            (S(GameStatus.Unsupported, "not supported on this GPU yet"), "此显卡暂不支持"),
            (S(GameStatus.Stale, "driver changed: 596.36 -> 610.88"), "驱动 610.88 已清除缓存"),
            (S(GameStatus.Stale, ScsKiller.TrimmedPartReason), "驱动清理了部分缓存"),
            (S(GameStatus.Stale, ScsKiller.MissesGameReason), "编译未进入游戏使用的缓存"),
            (S(GameStatus.Unsupported, ScsKiller.CantReachReason) with { ReasonFlags = GameReasonFlags.CacheUnreachable }, "编译无法覆盖此游戏"),
            (S(GameStatus.Stale, "game updated since the warm (build 1 -> 2)"), "游戏已更新"),
            (S(GameStatus.Stale, "game shaders changed since the warm"), "游戏着色器已改变"),
            (S(GameStatus.Stale, "SCSKiller can now compile 120 more pipelines for this game"), "SCSKiller 可编译更多内容"),
            (S(GameStatus.Stale, "1,234 new pipelines recorded; compile again to include them"), "1,234 条新管线"),
            (S(GameStatus.Warmed, "warmed for driver 610.88"), "驱动 610.88"),
            (S(GameStatus.Warmed, "warmed for driver 610.88") with { Plan = new PlanStats(0, 38_114, 0, 0, true, D3D11Shaders: 41_206) }, "DirectX 11 和 12"),
            (S(GameStatus.Warmed, "warmed for driver 610.88") with { Plan = new PlanStats(0, 0, 0, 0, true, D3D11Shaders: 41_206) }, "驱动 610.88"),
            (S(GameStatus.Warmed, "warmed for driver 610.88") with { Plan = new PlanStats(0, 900, 0, 0, true, D3D11Shaders: 10, Uncovered: 101) }, "DirectX 11 和 12 · 部分覆盖"),
            (S(GameStatus.Warmed, ScsKiller.UnreachedReason) with { CompileUnreached = true }, "此游戏需要专用加载方式"),
            (Tekken(GameStatus.Warmed), $"驱动跳过了 {180_000:N0} / {258_210:N0} 条管线"),
            (Tekken(GameStatus.Warmed) with { CompileUnreached = true, StatusReason = ScsKiller.UnreachedReason }, "此游戏需要专用加载方式"),
            (Tekken(GameStatus.Stale) with { StatusReason = "driver changed: 610.88 -> 616.92" }, "驱动 616.92 已清除缓存"),
        };
        Assert.All(cases, c => Assert.Equal(c.Note, Format.ShortNote(c.State)));
    }

    static GameState Tekken(GameStatus status) => S(status, "partly compiled for driver 610.88: the driver skipped 180,000 of 258,210 pipelines")
        with { Plan = new PlanStats(39_870, 218_340, 0, 0, true), LastWarmFailed = 179_990, LastWarmCrashed = 10 };

    /// <summary>The threshold sits between the warms measured on a dev PC (at most 0.4% rejected) and TEKKEN 8 on Windows 10's
    /// D3D12 runtime (about 70%). Unknown counts make no claim.</summary>
    [Fact]
    public void A_warm_is_partly_compiled_when_the_driver_rejects_more_than_a_tenth_of_its_plan()
    {
        Assert.False(ScsKiller.IsPartlyCompiled(92, 24_993));         // the highest share measured, 0.37%
        Assert.False(ScsKiller.IsPartlyCompiled(1_000, 10_000));
        Assert.True(ScsKiller.IsPartlyCompiled(1_001, 10_000));
        Assert.True(ScsKiller.IsPartlyCompiled(180_000, 258_210));
        Assert.False(ScsKiller.IsPartlyCompiled(null, 258_210));      // a warm from before the counts were kept
        Assert.False(ScsKiller.IsPartlyCompiled(180_000, null));      // no plan
        Assert.True(ScsKiller.IsPartlyCompiled(Tekken(GameStatus.Warmed)));   // crashed items count with the rejected ones
        Assert.False(ScsKiller.IsPartlyCompiled(Tekken(GameStatus.Warmed) with { LastWarmCrashed = null, LastWarmFailed = 25_000 }));
        Assert.False(ScsKiller.IsPartlyCompiled(Tekken(GameStatus.Stale)));
        Assert.False(ScsKiller.IsPartlyCompiled(Tekken(GameStatus.Warmed) with { CompileUnreached = true }));
        Assert.False(ScsKiller.IsPartlyCompiled(Tekken(GameStatus.Warmed) with { LastWarmFailed = 0, LastWarmCrashed = 0, LastWarmSkipped = 200_000 }));   // not in the install: the game never creates them
        Assert.Equal($"the driver skipped {180_000:N0} of {258_210:N0} pipelines", ScsKiller.PartlyCompiledNote(Tekken(GameStatus.Warmed)));
        Assert.Equal($"the driver refused {180_000:N0} of {258_210:N0} pipelines, so those can still stutter the first time", ScsKiller.RefusedNote(Tekken(GameStatus.Warmed)));
        Assert.Equal(30, ScsKiller.CompiledPercent(Tekken(GameStatus.Warmed)));   // 78,210 of 258,210 compiled, floored
        Assert.Null(ScsKiller.CompiledPercent(Tekken(GameStatus.Warmed) with { LastWarmFailed = 92 }));
    }

    [Fact]
    public void A_plan_compiles_both_apis_with_DirectX_12_pipelines_and_DirectX_11_shaders()
    {
        Assert.True(ScsKiller.BothApis(new PlanStats(0, 38_114, 0, 0, true, D3D11Shaders: 41_206)));
        Assert.True(ScsKiller.BothApis(new PlanStats(500, 0, 0, 0, true, D3D11Shaders: 10)));   // DirectX 12 from a recording
        Assert.False(ScsKiller.BothApis(new PlanStats(0, 0, 0, 0, true, D3D11Shaders: 41_206)));
        Assert.False(ScsKiller.BothApis(new PlanStats(0, 38_114, 0, 0, true)));
        Assert.False(ScsKiller.BothApis(null));
    }

    const string Dx11Too = Core.Planning.Planner.Record + "; " + Core.Planning.Planner.Dx12Only;

    /// <summary>A game that may run on DirectX 11 never loads the recorder there: the whole reason keeps saying so.</summary>
    [Fact]
    public void The_recorder_on_note_keeps_the_DirectX_12_hint()
    {
        var s = S(GameStatus.NeedsRecording, Dx11Too) with { RecorderInstalled = true };
        Assert.Equal("录制器已开启：请游玩约 5 分钟；请使用 DirectX 12，游戏也可能以 DirectX 11 运行", Format.RecorderOnNote(s));
        Assert.Equal("录制器已开启：请游玩约 5 分钟", Format.RecorderOnNote(s with { StatusReason = Core.Planning.Planner.Record }));
    }

    static GpuInfo Gpu(GpuVendor v, string name, ulong vram) => new(v, name, "1.0", 0, vram);

    [Fact]
    public void The_gpu_notice_shows_on_other_vendors_until_closed_for_that_gpu()
    {
        Assert.StartsWith("SCSKiller 暂不支持在 Intel 显卡上编译", Format.GpuNotice(Gpu(GpuVendor.Intel, "Intel Graphics", 0), null));
        Assert.StartsWith("SCSKiller 暂不支持在 Qualcomm 显卡上编译", Format.GpuNotice(Gpu(GpuVendor.Qualcomm, "Adreno", 0), null));
        Assert.StartsWith("SCSKiller 暂不支持在此显卡上编译", Format.GpuNotice(Gpu(GpuVendor.Unknown, "no D3D adapter", 0), null));
        Assert.Null(Format.GpuNotice(Gpu(GpuVendor.Nvidia, "NVIDIA GeForce", 8UL << 30), null));
        Assert.Null(Format.GpuNotice(Gpu(GpuVendor.Amd, "AMD Radeon", 8UL << 30), null));
        Assert.Null(Format.GpuNotice(Gpu(GpuVendor.Intel, "Intel Graphics", 0), "Intel Graphics"));
        Assert.NotNull(Format.GpuNotice(Gpu(GpuVendor.Intel, "Intel Arc", 0), "Intel Graphics"));   // another GPU than the one it was closed for
        Assert.Null(AppStore.DefaultSettings.GpuNoticeDismissed);
    }

    [Fact]
    public void A_laptop_with_integrated_intel_and_a_discrete_gpu_uses_the_discrete_one_and_gets_no_notice()
    {
        var intel = new DxgiAdapter(Gpu(GpuVendor.Intel, "Intel Graphics", 128UL << 20), 0x46A6, 0);
        foreach (var dgpu in new[] { Gpu(GpuVendor.Nvidia, "NVIDIA GeForce Laptop GPU", 8UL << 30), Gpu(GpuVendor.Amd, "AMD Radeon", 8UL << 30) })
        {
            var discrete = new DxgiAdapter(dgpu, 0x10, 0);
            Assert.Equal(dgpu, GpuBackends.Primary([intel, discrete]));   // DXGI lists the iGPU first on most laptops
            Assert.Equal(dgpu, GpuBackends.Primary([discrete, intel]));
            Assert.Null(Format.GpuNotice(GpuBackends.Primary([intel, discrete])!, null));
        }
        Assert.Equal(intel.Gpu, GpuBackends.Primary([intel]));
    }

    /// <summary>The Library replaces a game's row whenever the game is evaluated again (a recorder update, a download), and
    /// rebuilds its sections when one moves: with the list's default add/remove animations, rows vanished and came back.</summary>
    [Fact]
    public void The_library_list_changes_rows_without_an_animation()
    {
        var page = System.Xml.Linq.XDocument.Load(Path.Combine(TestEnv.RepoRoot, "src", "SCSKiller.App", "Pages", "LibraryPage.xaml"));
        System.Xml.Linq.XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var list = page.Descendants(ui + "ListView").Single(e => (string?)e.Attribute(x + "Name") == "List");
        var transitions = Assert.Single(list.Elements(ui + "ListView.ItemContainerTransitions"));
        Assert.Empty(Assert.Single(transitions.Elements(ui + "TransitionCollection")).Elements());
    }

    [Fact]
    public void An_engine_without_a_version_is_just_its_family()
    {
        EngineInfo E(string family, string version) => new(family, version, null, "D3D12", false, null);
        Assert.Equal("Carved DXBC", Format.Engine(E("Carved", "DXBC")));
        Assert.Equal("UE 5.4", Format.Engine(E("Unreal", "5.4"), "UE"));
        Assert.Equal(["Carved", "FromSoft", "Unity", "Carved"], new[] { E("Carved", "-"), E("FromSoft", "-"), E("Unity", "?"), E("Carved", "") }.Select(e => Format.Engine(e)));
    }
}

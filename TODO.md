# SCSKiller-NoACGate-zh-CN 开发 TODO

基线：上游 `BlueHeisenberg/SCSKiller` 的 `b10c1e38`（v1.2.5）。本仓库是面向简体中文用户的 fork。`NoACGate` 指解除 **SCSKiller 自身**对着色器录制器的反作弊硬门禁；它不会关闭、绕过或保证兼容游戏的反作弊，也不保证账号安全。

## 0. 仓库与工作区

- [x] 将 GitHub fork 改名为 `SAOG0721/SCSKiller-NoACGate-zh-CN`。
- [x] 本地 `origin` 指向 fork，`upstream` 指向原项目，保留 Git 历史；在 `codex/zh-cn-no-ac-gate` 分支开发。
- [x] 本 TODO 已提交到本地 Git（`ae68229`）；后续实现仍在分支上。
- [x] 首轮实现已提交为 `448c52c` 并推送到 fork 的 `codex/zh-cn-no-ac-gate` 分支；后续任务继续在此分支进行。

## 1. 录制器：警告后由用户选择

- [x] 已实现逐游戏、默认关闭的反作弊录制许可；全局“录制所有游戏”不会自动授予许可。关闭时立即撤销准入，游戏运行时的文件卸载等退出后处理。
- [x] 游戏详情页已有中文风险说明与确认操作；取消确认不写入许可。
- [x] 核心层的可用性、安装前后复查、后台复查和代理 DLL 的准入已接入同一份逐游戏许可；保留游戏运行、手动目录和已有 mod DLL 的独立限制。
- [x] 游戏库把“仅因反作弊录制许可未确认而受阻”的游戏标为“待确认录制风险”，提供“详情”入口；详情页说明风险、当前安装限制和上次录制结果。代理明确拒绝时显示其原因；录制器完全未加载时说明原因尚无法判定。
- [x] 已验证默认拒绝、明确允许、运行中关闭立即撤销准入、游戏退出后卸载、后台复查及原生代理的合成录制；不以这些结果声称实机兼容。

## 2. 简体中文汉化

- [ ] 按本 fork 的简体中文目标汉化可见文案，保持持久化状态、命令协议及日志标识稳定；如需双语切换再引入资源体系。
- [ ] 主要 UI 的静态文案、常见动态状态、弹窗、托盘与通知已有汉化；已补齐 Core 状态的展示层说明、社区账户/缓存提示、详情页覆盖率与帧时间统计，以及内置的 22 条卡顿说明。本轮进一步汉化社区下载与匿名分享错误、编译队列阶段/常见说明和已知的“无需预编译”原因，并补齐游戏库、详情页和编译队列遗漏的英文悬停提示与运行状态。假数据截图已确认详情页、反作弊选择页、设置页的窄窗口无明显截断；来自外部服务的未知文本及底层诊断原文仍可能保留原文。
- [x] 影响行为的状态原因改用 `GameReasonFlags`：反作弊录制门禁、离线会话、缓存不可达、光追录制机会和新版计划器通知不再解析英文 `StatusReason`。`Format.ShortNote` 等显示层仍解析英文原始原因，待继续汉化。
- [x] 已提供中文 `README.zh-CN.md`，保留上游版权、许可证和来源。
- [x] CLI 帮助、常见输出及 `record anti-cheat <game> on|off` 已汉化；启用命令始终要求手动确认风险。
- [ ] CLI 的列表、详情页、录制器常见诊断、安装限制与队列阶段/进度已改用中文说明；文件系统异常和未知技术型输出继续逐项核对。

## 3. fork 共存与更新

- [x] 已隔离数据目录、录制准入文件与 ledger、代理导出标记、单实例标识、计划任务、启动项及更新互斥量；同一游戏仍只能在 `d3d12.dll` 位置安装一个版本的录制器。
- [x] 更新馈送与包下载地址改指本 fork 的 GitHub Releases，未发布前不会从官方渠道安装二进制。
- [x] 首次欢迎内容改为内置中文 fork 说明，避免上游远程欢迎文案覆盖风险提示；向上游发送匿名活跃检查改为默认关闭，设置页说明发送去向。关于页的源码链接直接打开本 fork 的 GitHub，不再把 Patreon 令牌发往 GitHub。
- [ ] 首次发布前配置本 fork 自己的更新签名与版本渠道，并审查仍指向上游服务的社区分享/内容入口。
- [x] 窗口标题已标明 NoACGate 中文版。
- [x] 便携版目录、ZIP、发布标题使用 fork 名称。GitHub Actions 工作流改动因凭据缺少 `workflow` 权限仍留本地，不作为首个便携预览版的发布通道；以后使用 CI 发布时再处理权限和标签验证。
- [x] 首个便携预览版采用独立版本号 `v1.2.5-noacgate.1`，未签名且不启用自动更新。
- [ ] Velopack 安装包 ID、独立签名密钥、更新渠道及与原版安装程序的共存验证属于将来提供安装版的任务。

## 4. 上游链接与自动联网入口（补充）

- [x] **暂时停用 fork 自动更新。** `Updater.Installed` 在 `ForkUpdatesEnabled=false` 时为假，启动定时检查、手动检查与启动时安装均不执行；关于页显示“尚未提供独立签名安装包”，更新按钮与渠道入口暂时隐藏。`UpdateFeeds` 虽已改指本 fork，`FeedTrust.ReleaseKeys` 仍是上游密钥，因此当前不能据此发布更新。
- [ ] 完成独立包 ID、签名密钥、馈送、版本渠道及原版共存验证后，才恢复更新检查/下载/安装；决定本 fork 的自动安装默认值，并测试已有安装和暂存更新的迁移行为。
- [x] 默认 `README.md` 改为 fork 首页，原项目全文移至 `README.upstream.md` 并标为存档；首页不再展示上游下载或提交 Issue 的按钮。`CONTRIBUTING.md` 与 `SECURITY.md` 已改为本 fork 的录制规则与当前支持状态，不再沿用上游的反作弊禁令和响应承诺。关于页的上游网站/Patreon 链接已明确标为上游；应用内未发现“提交 Issue”按钮。
- [ ] GitHub 仓库 API 当前显示本 fork 的 `has_issues=false`。若日后开启 fork Issues，再检查 `.github/ISSUE_TEMPLATE` 并加入有效的 fork 反馈入口；正式发布后再加入本 fork 的下载链接。在此之前保持“暂无下载/Issues 未开放”的说明。
- [x] **盘点其它上游网络服务。** 本 fork 不再设置 `ContentRoutes`/`UserFetch`，启动和手动扫描仅使用内置规则，不自动请求上游在线内容；社区数据库与共享包同步统一受 `UseCommunityDb` 控制，默认关闭。匿名分享与活跃检查默认关闭；Patreon 登录、社区下载、分享与活跃检查在界面中标明连接上游，需由用户分别主动使用。`RouteFailover.DefaultRoutes` 仍供这些明确选择的功能使用。新增“未开启时不请求社区清单、开启后可同步”的测试。

## 5. 验证与交付

- [x] .NET 10 SDK 已安装在用户目录；Core、WinUI App 和原生代理已构建通过。新增的核心测试通过；隔离 WARP 测试目录中，默认模式不录制，明确许可后生成录制数据。
- [x] 在隔离测试目录验证安装、撤销、文件变化、与原版代理标记/外来 `d3d12.dll` 的共存行为；假数据截图验证中文 UI。原版代理模拟文件不会被本 fork 认作自己的录制器，已有外来 DLL 不被覆盖；新旧两份本地分发目录可分别保留、回退。此项是合成验证，不代表与实际原版程序同时运行或真实游戏验收。
- [ ] 实际游戏中是否加载代理、是否录到着色器、反作弊是否阻止/处罚，必须由用户在自愿承担风险的测试环境中单独验收。
- [x] **本机部署分发版本。**按用户最终选择，`build/publish.ps1 -NoOodle -FrameworkDependent` 已生成不含 .NET / Windows App SDK 运行时的本机精简 ZIP；当前开发版本为 `0.0.0-internal.0+2f8b817`。它依赖目标电脑已安装 x64 .NET Runtime 10 和 Windows App SDK Runtime 2.5.1，要求写在包内 `RUNTIME-REQUIREMENTS.txt`。当前 ZIP、解压目录及固定部署目录已核对；固定部署版 CLI 与 WinUI 假数据截图均退出码 0。未在真实游戏目录安装录制器。
- [ ] **清理旧本地副本。** 用户已授权移除旧版 fork 部署目录和 ZIP。自动审批先后拒绝了批量递归删除、单个旧部署目录的递归删除，以及工作区内单个旧 ZIP 的删除，均只返回 `blocked by policy`；因此目前没有删除任何旧副本。待人工清理的对象仅限 `%LOCALAPPDATA%\Codex\Deployments\SCSKiller-NoACGate-zh-CN-{77db4b2,3668dd2,5f1be07,c851b5a,155251f}`、`dist\archive` 内的旧版 ZIP / 解压目录，以及 `dist\probe-framework` 和 `dist\candidate-*` 临时构建目录；保留固定部署目录、当前 `dist\SCSKiller-NoACGate-zh-CN` 及同名 ZIP、Git 仓库和 `I:` 盘原版。

### 当前验证记录（2026-10-15）

- `dotnet build src/SCSKiller.App/SCSKiller.App.csproj -c Release --no-restore`：成功，6 条已有依赖/可空性警告。
- `cmake --build proxy/build --config Release`：成功；代理导出仅有本 fork 的 `SCSKillerNoACGate_StartWarm`。
- AppTests 611 项通过，UpdateTests 44 项通过；新增的许可、撤销、录制需求与代理身份测试通过。按名称筛选的 72 项反作弊相关测试中 71 项通过；剩余 1 项读取本机战网安装，未找到预期的《守望先锋》条目，与本次代码路径无关。
- 原生 `selftest anticheat -` 在隔离 WARP 目录：无许可时无 `scskiller.db`，成对准入文件写入 `allow_ac=1` 后生成 `scskiller.db`。这仅证明本 fork 的代理准入差异，不代表任何真实游戏能运行或录制。
- WinUI `--screenshots` 假数据模式生成深色与浅色截图，已检查反作弊游戏的录制卡片布局；游戏真实运行仍未测试。
- 本轮 WinUI 与 CLI Release 构建通过；状态原因改用稳定标识后，反作弊、格式、通知、欢迎页与活跃检查相关 66 项定向测试通过。随后运行非实机全套 1432 项，1430 项通过，2 项因沿用上游地址和默认活跃检查的旧预期失败；更新测试预期后，这 2 项均已定向重跑通过，尚未重新执行全套。
- 本轮中文动态说明与上游默认联网设置改动后，WinUI 和 CLI Release 构建通过；账户、格式、内置卡顿清单、游戏判定等 66 项定向测试通过。应用测试 614 项中 610 项通过，4 项仅因旧英文提示断言失败；更新断言后这 4 项已定向重跑通过。尚未再次全量重跑应用测试。
- 假数据截图新增“反作弊待确认”场景，深色普通窗口和窄窗口显示完整中文警告与选择开关；尚未验证实际游戏中的加载及录制。
- 汉化后 WinUI `--screenshots` 假数据模式退出码 0，生成 200 余张深浅色/窄窗口截图；人工检查了反作弊风险卡片、详情统计、设置的上游服务说明和关于页，未见明显文字截断。截图保存在本机 `AppData\Local\Codex\SCSKiller-qa-20261015-localized`，不代表真实游戏运行。
- 旧版 `77db4b2` 分发包：`dist\archive\SCSKiller-NoACGate-zh-CN-77db4b2.zip`，SHA-256 `063e90f46fa6dfb6b2e477fa3bbc3103af424a80e09fa3067b1301687d73b2f0`，118 MiB，727 个文件。部署目录：`%LOCALAPPDATA%\Codex\Deployments\SCSKiller-NoACGate-zh-CN-77db4b2\SCSKiller-NoACGate-zh-CN`。压缩包与部署目录的文件名集合一致，9 个关键文件哈希一致；没有 Oodle DLL。部署版 `--screenshots` 退出码 0，生成 229 张截图；已检查关于页的 fork 名称、中文反作弊卡片和“自动更新暂时停用”。部署版 CLI `--help` 显示中文。回退方式：关闭本 fork 后，核实该独立部署目录的绝对路径再移除；此次没有覆盖既有目录或原版安装。
- 上一条 `77db4b2` ZIP 已移至 `dist\archive\SCSKiller-NoACGate-zh-CN-77db4b2.zip` 并复核原 SHA-256，原部署目录保留。新一轮社区、分享和队列汉化：相关 62 项定向测试中 61 项通过，唯一失败为中文标点断言，修复后该项定向重跑通过；WinUI、CLI Release 均构建通过。
- 上一版 `3668dd2` ZIP 已移至 `dist\archive\SCSKiller-NoACGate-zh-CN-3668dd2.zip`，SHA-256 `2b292e03fe7a7388387960f6f5c2a803691f0032c2112c0c2bc17effea76d956`；该部署目录仍保留。
- 上一版 `5f1be07` ZIP 已移至 `dist\archive\SCSKiller-NoACGate-zh-CN-5f1be07.zip`，SHA-256 `05b60dd5e18f19a2e73fbe48ef2f0e3c3370cab205a8b73d2f2259a54aab7d4f`；部署目录仍保留。
- 之前的自包含包 `c851b5a`：`dist\archive\SCSKiller-NoACGate-zh-CN-c851b5a.zip`，SHA-256 `48e50049ff29f6917d9f1674cffa8f10899d276ea30b9792104a2ae04f784313`，727 个文件，无 Oodle DLL；部署目录 `%LOCALAPPDATA%\Codex\Deployments\SCSKiller-NoACGate-zh-CN-c851b5a\SCSKiller-NoACGate-zh-CN`。当时 ZIP 与部署目录的文件名集合及 9 个关键文件哈希一致，`--screenshots` 假数据模式退出码 0，生成 229 张截图，CLI `--help` 显示中文。该旧包仍在，待人工清理。
- 录制器文案补漏：GUI/CLI 共用中文安装限制与排队状态，OptiScaler 串接限制和未确认反作弊风险时的拒绝提示已汉化。相关 28 项定向测试及 WinUI、CLI 构建通过。
- 本轮复核代理 `admitted()` 与核心层 `SetAntiCheatRecording`、安装复查、撤销路径；两处 `allow_ac=1` 匹配后才跳过代理自身的反作弊拒绝检查。隔离目录中 15 项针对原版标记、外来 DLL、逐游戏许可、撤销和文件变化的测试通过。修正了源码与准入文件中仍称“无反作弊检查”的过时注释；功能路径未变。
- 2026-10-15 参考 `I:\GameTweakTools\SCSKiller-1.2.5-Portable\current` 盘点：原版 `current` 有 723 个文件、约 282 MiB，先前本 fork 自包含版有 727 个文件、约 283 MiB。该原版安装目录还含用户数据、Velopack 更新程序及一份已有的本地 `native\segheap\d3d12.dll` 修复，不应直接照搬。自包含版 GUI 和 CLI 有 220 个字节相同的文件，CLI 这份在 ZIP 中约 41.4 MiB；直接删除会破坏运行。单独省去 PDB 与 `createdump.exe` 仅约 0.55 MiB ZIP。本机已有 .NET 10.0.6 x64 与 Windows App SDK Runtime 2.5.1 x64，所以采用独立的依赖系统运行时构建，而非从自包含包手工删 DLL。
- 当前本机精简包 `2f8b817`：`dist\SCSKiller-NoACGate-zh-CN.zip`，SHA-256 `2870adfcbac708b4ee1b1d47d75b3f3cfe9f6632cdc6bdcbd3ca6b611dba8198`，约 25 MiB、119 个文件；无 .NET / Windows App SDK 运行时文件、PDB 或 Oodle DLL。`dist\SCSKiller-NoACGate-zh-CN` 和固定部署目录 `%LOCALAPPDATA%\Codex\Deployments\SCSKiller-NoACGate-zh-CN\SCSKiller-NoACGate-zh-CN` 的文件名集合及 10 个关键文件哈希一致。候选目录及固定部署目录的 WinUI `--screenshots` 假数据模式均退出码 0，各生成 229 张 PNG；固定部署版 CLI `--help` 退出码 0。交付记录在固定部署根目录的 `deployment-receipt.json`。真实游戏中的加载、录制与反作弊结果仍待用户单独验收。

## 6. 本地最小 Release 分发包（2026-10-15）

- [x] 从已验证的 `2f8b817` Release 配置、本机运行时依赖版生成 `dist\SCSKiller-NoACGate-zh-CN-local-Release-minimal.zip`：25,600,349 字节、116 个文件，SHA-256 `31a1cba2808ea6365445d8c0dba67db36ace98e9354811e04b592c44fdfbd9cf`。这是未签名、未发布 GitHub 的本地分发包；内嵌版本仍为 `0.0.0-internal.0+2f8b817`。相邻的 `.receipt.json` 记录包内容和验证结果。
- [x] 仅排除 `Microsoft.Web.WebView2.Core.dll`、`Microsoft.Web.WebView2.Core.Projection.dll`、`WebView2Loader.dll`，相对 119 文件版节省约 0.52 MiB ZIP。源码没有内嵌 WebView 控件，Patreon 登录通过系统浏览器；不过 `Microsoft.WinUI.dll` 静态引用 WebView2 投影，因此这项裁剪只对当前功能和测试过的路径成立。独立解压后，WinUI 假数据全页截图退出码 0、生成 229 张 PNG，CLI `--help` 退出码 0；10 个关键文件与 119 文件版哈希一致。没有实测在线登录、真实游戏或未来新增 WebView 控件。
- [x] 打包脚本提供 `-NoWebView`；首个便携预览包已从准确源码提交重新构建，复核清单、启动与 CLI。
- [ ] 公开发布前由用户人工审核 Draft；实机游戏与在线登录的结果不能由现有隔离测试代替。

## 7. 首个预览版 Draft 准备

- [x] 默认 README 改为面向用户的中文说明，签名状态不再沿用上游政策；原版赞助入口从 fork 仓库移除。
- [x] `docs/RELEASE_NOTES_v1.2.5-noacgate.1.md` 提供面向用户的中英双语发布说明、前置运行时、安装步骤和反作弊风险。
- [x] 用 `-NoOodle -FrameworkDependent -NoWebView -Version 1.2.5-noacgate.1` 从干净提交 `b238c0c03993e0f925addf82bea2e84486324d5f` 生成 ZIP。包内 117 个文件与清单哈希一致；独立解压版 CLI 中文帮助退出码 0，WinUI 假数据截图退出码 0 并生成 229 张 PNG，已检查窄窗口反作弊选择页。ZIP 为 25,604,818 字节，SHA-256 `0342402968e7eeaf171e25bfdeb742c641f87e9f4df6d4232f70aa2957326871`。
- [x] 不含 `.github/workflows` 改动的源码已同步到 fork 默认分支和开发分支。GitHub Draft + Pre-release ID `413311171` 指向上述构建提交；已上传 `SCSKiller-NoACGate-zh-CN.zip` 和 `SHA256SUMS.txt`，远端附件大小、ZIP SHA-256、正文与本地相符。
- [ ] 用户人工审核 Draft 后再决定是否公开发布；当前保持 `draft=true`，不推送会触发工作流的标签。
- 手动清理建议：新 ZIP 已不含上述 3 个二进制文件；如果保留之前解压的 119 文件版而希望手动裁剪，只针对该版顶层的这 3 个文件。`notices\WebView2-NOTICE.txt` 和第三方声明保留。较旧的 fork 部署目录、旧 ZIP、旧解压目录及临时构建目录见第 5 节的待清理条目；在决定是否保留完整包作回退前，先不要删除当前固定部署目录或 `I:` 盘原版。

## 已确认的源码入口

- 门禁：`src/SCSKiller.App/ViewModels.cs` 的录制开关和提示；`src/SCSKiller.Core/App/ScsKiller.cs` 的 `RecorderSkip`、`Install`、`AntiCheatFound`、`CheckRecorderGames`；`proxy/proxy.cpp` 的 `admitted`。
- 持久化：`src/SCSKiller.Core/App/AppStore.cs` 的 `GameRecord`；界面状态：`src/SCSKiller.Core/Contracts.cs` 的 `GameState`。
- 汉化：`src/SCSKiller.App/Pages/*.xaml`、`ViewModels.cs`、`src/SCSKiller.Cli/`；当前大量文案硬编码。
- 共存与更新：`AppStore.cs` 的数据根目录、`src/SCSKiller.App/Program.cs` 的实例标识、`src/SCSKiller.Core/App/Updates.cs` 的 fork 更新地址。

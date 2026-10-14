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
- [ ] 复查状态/提示：需要录制的反作弊游戏应能给出选择入口；已允许但实际被游戏或代理拒绝时显示真实原因。
- [x] 已验证默认拒绝、明确允许、运行中关闭立即撤销准入、游戏退出后卸载、后台复查及原生代理的合成录制；不以这些结果声称实机兼容。

## 2. 简体中文汉化

- [ ] 按本 fork 的简体中文目标汉化可见文案，保持持久化状态、命令协议及日志标识稳定；如需双语切换再引入资源体系。
- [ ] 汉化主要 UI：导航、游戏库、队列、详情和关于页的静态文案已有第一轮；设置、动态状态、弹窗和错误文案仍待补齐，并检查窄窗口布局。
- [ ] 清理用英文 `StatusReason` 文案判断业务状态的分支，改用稳定的状态/原因标识，再翻译展示文字。
- [x] 已提供中文 `README.zh-CN.md`，保留上游版权、许可证和来源。
- [ ] 汉化 CLI 帮助与面向用户的输出。

## 3. fork 共存与更新

- [x] 已隔离数据目录、录制准入文件与 ledger、代理导出标记、单实例标识、计划任务、启动项及更新互斥量；同一游戏仍只能在 `d3d12.dll` 位置安装一个版本的录制器。
- [x] 更新馈送与包下载地址改指本 fork 的 GitHub Releases，未发布前不会从官方渠道安装二进制。
- [ ] 首次发布前配置本 fork 自己的更新签名与版本渠道，并审查仍指向上游服务的社区分享/内容入口。
- [ ] 检查包名、窗口标题、版本号和发布说明，使用户能辨认安装的是这个 fork。

## 4. 验证与交付

- [x] .NET 10 SDK 已安装在用户目录；Core、WinUI App 和原生代理已构建通过。新增的核心测试通过；隔离 WARP 测试目录中，默认模式不录制，明确许可后生成录制数据。
- [ ] 在隔离的测试目录验证安装、撤销、文件变化、原版共存与中文 UI；保留回退手段。
- [ ] 实际游戏中是否加载代理、是否录到着色器、反作弊是否阻止/处罚，必须由用户在自愿承担风险的测试环境中单独验收。

### 当前验证记录（2026-10-15）

- `dotnet build src/SCSKiller.App/SCSKiller.App.csproj -c Release --no-restore`：成功，6 条已有依赖/可空性警告。
- `cmake --build proxy/build --config Release`：成功；代理导出仅有本 fork 的 `SCSKillerNoACGate_StartWarm`。
- AppTests 611 项通过，UpdateTests 44 项通过；新增的许可、撤销、录制需求与代理身份测试通过。按名称筛选的 72 项反作弊相关测试中 71 项通过；剩余 1 项读取本机战网安装，未找到预期的《守望先锋》条目，与本次代码路径无关。
- 原生 `selftest anticheat -` 在隔离 WARP 目录：无许可时无 `scskiller.db`，成对准入文件写入 `allow_ac=1` 后生成 `scskiller.db`。这仅证明本 fork 的代理准入差异，不代表任何真实游戏能运行或录制。
- WinUI `--screenshots` 假数据模式生成深色与浅色截图，已检查反作弊游戏的录制卡片布局；游戏真实运行仍未测试。

## 已确认的源码入口

- 门禁：`src/SCSKiller.App/ViewModels.cs` 的录制开关和提示；`src/SCSKiller.Core/App/ScsKiller.cs` 的 `RecorderSkip`、`Install`、`AntiCheatFound`、`CheckRecorderGames`；`proxy/proxy.cpp` 的 `admitted`。
- 持久化：`src/SCSKiller.Core/App/AppStore.cs` 的 `GameRecord`；界面状态：`src/SCSKiller.Core/Contracts.cs` 的 `GameState`。
- 汉化：`src/SCSKiller.App/Pages/*.xaml`、`ViewModels.cs`、`src/SCSKiller.Cli/`；当前大量文案硬编码。
- 共存与更新：`AppStore.cs` 的数据根目录、`src/SCSKiller.App/Program.cs` 的实例标识、`src/SCSKiller.Core/App/Updates.cs` 的 fork 更新地址。

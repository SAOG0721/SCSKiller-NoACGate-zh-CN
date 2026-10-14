# SCSKiller-NoACGate-zh-CN 开发 TODO

基线：上游 `BlueHeisenberg/SCSKiller` 的 `b10c1e38`（v1.2.5）。本仓库是面向简体中文用户的 fork。`NoACGate` 指解除 **SCSKiller 自身**对着色器录制器的反作弊硬门禁；它不会关闭、绕过或保证兼容游戏的反作弊，也不保证账号安全。

## 0. 仓库与工作区

- [x] 将 GitHub fork 改名为 `SAOG0721/SCSKiller-NoACGate-zh-CN`。
- [x] 本地 `origin` 指向 fork，`upstream` 指向原项目，保留 Git 历史；在 `codex/zh-cn-no-ac-gate` 分支开发。
- [ ] 提交本 TODO 和后续实现；确认远端分支及工作区状态。

## 1. 录制器：警告后由用户选择

- [ ] 明确每个游戏独立、默认关闭的反作弊录制许可。检测结果仍显示；全局“录制所有游戏”不能自动为反作弊游戏授予许可。用户关闭时撤销许可并拆除本 fork 安装的录制器。
- [ ] 在游戏详情页提供中文风险说明与确认操作。确认前不安装；取消确认后保持关闭。说明 `d3d12.dll` 注入可能被游戏拒绝或导致封禁，且“允许”只作用于 SCSKiller 自身的限制。
- [ ] 让核心层的录制器可用性、安装前复查、安装后复查、后台复查、文件变化处理和代理 DLL 的录制准入都遵循同一份按游戏保存的许可。保留游戏运行时不得写入、手动游戏目录确认、已有 mod DLL 不得覆盖、文件变动重新核查等独立条件。
- [ ] 复查状态/提示：需要录制的反作弊游戏应能给出选择入口；已允许但实际被游戏或代理拒绝时显示真实原因。
- [ ] 为“默认拒绝 → 确认允许 → 关闭撤销”和安装前后/后台检查添加有意义的回归验证；不以静态检查声称实机兼容。

## 2. 简体中文汉化

- [ ] 先建立可维护的 `zh-CN`/英文资源与语言选择或系统语言回退；保持持久化状态、命令协议及日志中的标识稳定。
- [ ] 汉化主要 UI：游戏库、游戏详情、队列、设置、关于、弹窗、错误及录制器风险文案，检查窄窗口和长文本布局。
- [ ] 清理用英文 `StatusReason` 文案判断业务状态的分支，改用稳定的状态/原因标识，再翻译展示文字。
- [ ] 汉化 CLI 帮助与面向用户的输出，并提供中文 README/使用说明；保留上游版权、许可证和来源。

## 3. fork 共存与更新

- [ ] 将本 fork 的本地数据目录、单实例标识、任务/互斥量等与官方版隔离，避免两个版本互相覆盖状态或管理同一个录制器。
- [ ] 更新检查只指向本 fork 的发布渠道，或在首次发布前关闭自动更新入口，避免拉取上游二进制覆盖 fork。
- [ ] 检查包名、窗口标题、版本号和发布说明，使用户能辨认安装的是这个 fork。

## 4. 验证与交付

- [ ] 使用项目要求的 .NET 10 SDK 与 C++ 工具链构建，运行相关 .NET/代理测试。当前机器上次检查只有 .NET 9 SDK，需先复核环境。
- [ ] 在隔离的测试目录验证安装、撤销、文件变化、原版共存与中文 UI；保留回退手段。
- [ ] 实际游戏中是否加载代理、是否录到着色器、反作弊是否阻止/处罚，必须由用户在自愿承担风险的测试环境中单独验收。

## 已确认的源码入口

- 门禁：`src/SCSKiller.App/ViewModels.cs` 的录制开关和提示；`src/SCSKiller.Core/App/ScsKiller.cs` 的 `RecorderSkip`、`Install`、`AntiCheatFound`、`CheckRecorderGames`；`proxy/proxy.cpp` 的 `admitted`。
- 持久化：`src/SCSKiller.Core/App/AppStore.cs` 的 `GameRecord`；界面状态：`src/SCSKiller.Core/Contracts.cs` 的 `GameState`。
- 汉化：`src/SCSKiller.App/Pages/*.xaml`、`ViewModels.cs`、`src/SCSKiller.Cli/`；当前大量文案硬编码。
- 共存与更新：`AppStore.cs` 的数据根目录、`src/SCSKiller.App/Program.cs` 的实例标识、`src/SCSKiller.Core/App/Updates.cs` 的上游更新地址。

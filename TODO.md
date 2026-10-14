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
- [ ] 主要 UI 的静态文案、常见动态状态、弹窗、托盘与通知已有汉化；仍需补齐来自 Core 的状态/错误说明、社区/更新服务提示和细节页统计文案，并检查窄窗口布局。
- [x] 影响行为的状态原因改用 `GameReasonFlags`：反作弊录制门禁、离线会话、缓存不可达、光追录制机会和新版计划器通知不再解析英文 `StatusReason`。`Format.ShortNote` 等显示层仍解析英文原始原因，待继续汉化。
- [x] 已提供中文 `README.zh-CN.md`，保留上游版权、许可证和来源。
- [x] CLI 帮助、常见输出及 `record anti-cheat <game> on|off` 已汉化；启用命令始终要求手动确认风险。
- [ ] CLI 中由 Core 返回的原始原因、异常和部分协议型输出继续逐项核对。

## 3. fork 共存与更新

- [x] 已隔离数据目录、录制准入文件与 ledger、代理导出标记、单实例标识、计划任务、启动项及更新互斥量；同一游戏仍只能在 `d3d12.dll` 位置安装一个版本的录制器。
- [x] 更新馈送与包下载地址改指本 fork 的 GitHub Releases，未发布前不会从官方渠道安装二进制。
- [x] 首次欢迎内容改为内置中文 fork 说明，避免上游远程欢迎文案覆盖风险提示；向上游发送匿名活跃检查改为默认关闭，设置页说明发送去向。关于页的源码链接直接打开本 fork 的 GitHub，不再把 Patreon 令牌发往 GitHub。
- [ ] 首次发布前配置本 fork 自己的更新签名与版本渠道，并审查仍指向上游服务的社区分享/内容入口。
- [x] 窗口标题已标明 NoACGate 中文版。
- [ ] 便携版目录、ZIP、CI 附件和草稿 Release 标题改用 `SCSKiller-NoACGate-zh-CN`。脚本改动保存在本地分支 `codex/zh-cn-no-ac-gate-workflows`（`66ae437`），尚未发布；当前 Git 凭据缺少 GitHub `workflow` 权限，不能把 `.github/workflows/` 的改动推送到 fork。取得相应权限后再一并提交脚本与工作流，避免 CI 文件名不匹配。
- [ ] 首次发布前确定独立的 Velopack 安装包 ID、版本起点、签名密钥和中文发布说明；验证不会覆盖原版安装。

## 4. 上游链接与自动联网入口（补充）

- [x] **暂时停用 fork 自动更新。** `Updater.Installed` 在 `ForkUpdatesEnabled=false` 时为假，启动定时检查、手动检查与启动时安装均不执行；关于页显示“尚未提供独立签名安装包”，更新按钮与渠道入口暂时隐藏。`UpdateFeeds` 虽已改指本 fork，`FeedTrust.ReleaseKeys` 仍是上游密钥，因此当前不能据此发布更新。
- [ ] 完成独立包 ID、签名密钥、馈送、版本渠道及原版共存验证后，才恢复更新检查/下载/安装；决定本 fork 的自动安装默认值，并测试已有安装和暂存更新的迁移行为。
- [ ] **Issue、下载和文档入口收口到 fork。** `README.md` 中“Report a bug”“Request a game”、常见问题里的 Issue 和多个下载/“官方版本”链接仍指上游。GitHub 仓库 API 当前显示本 fork 的 `has_issues=false`，因此暂不能把 Issue 按钮直接换成一个不可用的 fork 地址：先移除或标明这些上游入口只服务原版；若日后开启 fork Issues，再改指本 fork 的模板。未发布前隐藏 fork 下载按钮或写明“暂无下载”，保留上游链接作为来源和作者归属。检查 `.github/ISSUE_TEMPLATE`、`CONTRIBUTING.md`、`SECURITY.md`：后两者仍陈述上游的反作弊禁令与响应承诺，不能直接代表本 fork。当前应用内未发现“提交 Issue”按钮；关于页的上游网站/Patreon 链接已明确标为上游，可保留并增设 fork 反馈入口。
- [ ] **盘点其它上游网络服务。** `RouteFailover.DefaultRoutes` 仍指 `api.scskiller.com/.io`，应用会自动获取上游内容清单；Patreon 登录、社区数据库与匿名分享也使用上游服务。决定每个入口是停用、保持明确告知并由用户主动开启，还是迁移到本 fork；验证默认设置不会无提示地向上游发送数据。已完成的欢迎页本地化和默认关闭活跃检查不等于整个联网审查完成。

## 5. 验证与交付

- [x] .NET 10 SDK 已安装在用户目录；Core、WinUI App 和原生代理已构建通过。新增的核心测试通过；隔离 WARP 测试目录中，默认模式不录制，明确许可后生成录制数据。
- [ ] 在隔离的测试目录验证安装、撤销、文件变化、原版共存与中文 UI；保留回退手段。
- [ ] 实际游戏中是否加载代理、是否录到着色器、反作弊是否阻止/处罚，必须由用户在自愿承担风险的测试环境中单独验收。

### 当前验证记录（2026-10-15）

- `dotnet build src/SCSKiller.App/SCSKiller.App.csproj -c Release --no-restore`：成功，6 条已有依赖/可空性警告。
- `cmake --build proxy/build --config Release`：成功；代理导出仅有本 fork 的 `SCSKillerNoACGate_StartWarm`。
- AppTests 611 项通过，UpdateTests 44 项通过；新增的许可、撤销、录制需求与代理身份测试通过。按名称筛选的 72 项反作弊相关测试中 71 项通过；剩余 1 项读取本机战网安装，未找到预期的《守望先锋》条目，与本次代码路径无关。
- 原生 `selftest anticheat -` 在隔离 WARP 目录：无许可时无 `scskiller.db`，成对准入文件写入 `allow_ac=1` 后生成 `scskiller.db`。这仅证明本 fork 的代理准入差异，不代表任何真实游戏能运行或录制。
- WinUI `--screenshots` 假数据模式生成深色与浅色截图，已检查反作弊游戏的录制卡片布局；游戏真实运行仍未测试。
- 本轮 WinUI 与 CLI Release 构建通过；状态原因改用稳定标识后，反作弊、格式、通知、欢迎页与活跃检查相关 66 项定向测试通过。随后运行非实机全套 1432 项，1430 项通过，2 项因沿用上游地址和默认活跃检查的旧预期失败；更新测试预期后，这 2 项均已定向重跑通过，尚未重新执行全套。
- 假数据截图新增“反作弊待确认”场景，深色普通窗口和窄窗口显示完整中文警告与选择开关；尚未验证实际游戏中的加载及录制。

## 已确认的源码入口

- 门禁：`src/SCSKiller.App/ViewModels.cs` 的录制开关和提示；`src/SCSKiller.Core/App/ScsKiller.cs` 的 `RecorderSkip`、`Install`、`AntiCheatFound`、`CheckRecorderGames`；`proxy/proxy.cpp` 的 `admitted`。
- 持久化：`src/SCSKiller.Core/App/AppStore.cs` 的 `GameRecord`；界面状态：`src/SCSKiller.Core/Contracts.cs` 的 `GameState`。
- 汉化：`src/SCSKiller.App/Pages/*.xaml`、`ViewModels.cs`、`src/SCSKiller.Cli/`；当前大量文案硬编码。
- 共存与更新：`AppStore.cs` 的数据根目录、`src/SCSKiller.App/Program.cs` 的实例标识、`src/SCSKiller.Core/App/Updates.cs` 的 fork 更新地址。

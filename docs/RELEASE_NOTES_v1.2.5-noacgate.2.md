# SCSKiller NoACGate 中文版 v1.2.5-noacgate.2

基于 SCSKiller 1.2.5 的非官方中文预览版。本次新增编译显卡选择，并修复录制到预编译流程中的三处问题。

## 本版更新

- **选择编译显卡**：在“设置 → 编译使用的显卡”中指定 GPU，也可使用 `cli\scskiller.exe gpu` 查看列表、`gpu use <id>` 指定显卡或 `gpu use auto` 恢复自动选择。自动模式按专用显存容量选择；指定显卡在启动时不可用则回退到自动模式。
- **切换在重启后生效**：同厂商显卡之间切换或切回“自动”也需完全退出并重新打开程序。运行中的编译继续使用原显卡；DXGI 相同标识条目的顺序变化不会使程序意外换卡。这项设置不改变游戏自身使用的显卡。
- **修复已有录制却无法编译**：部分仅能通过录制生成计划的 Unreal 游戏，在逐游戏确认反作弊录制许可后，库页面显示可编译，实际队列却仍被旧判定拦截。显示状态与预编译判定现已一致。
- **修复游戏运行期间录制导入失败**：允许读取录制器仍持有的 `scskiller.db`，完整记录可在游戏运行期间导入。收件箱仍须等游戏退出、确认没有新增内容后才能清空。
- **修复编译状态误报过期**：带有反作弊及 ReShade/RenoDX 的安装，不再因启动时游戏列表尚未发布而被误判为“上次编译没有经过 HDR mod”。

## 下载与升级

下载完整主包 **`SCSKiller-NoACGate-zh-CN.zip`**；`SHA256SUMS.txt` 用于校验。GitHub 自动提供的 `Source code` 压缩包不是可运行程序。

1. 系统须为 Windows x64。先安装 [.NET 10 Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/10.0)和 [Windows App SDK Runtime 2.5.1 或更新的兼容版本（x64）](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)。这两项运行时不包含在 ZIP 内。
2. 从通知区域菜单完全退出 SCSKiller，将 ZIP **完整解压到新目录**，再运行其中的 `SCSKiller.exe`。不要只替换单个 EXE；建议保留旧目录以便回退。
3. 本 ZIP 的数据目录仍为 `%LOCALAPPDATA%\SCSKiller-NoACGate-zh-CN`，保留该目录即可继续使用已有设置和录制；仅在 EXE 旁创建 `data` 目录不会改变数据位置。若此前自行采用带 `.portable` 标记的目录布局，请同时保留标记所在目录及其下完整的 `data` 目录。

这是未签名的精简便携包，没有安装程序，自动更新继续关闭。包内不含 Oodle DLL 和 WebView2 的三个二进制文件；部分游戏档案所需的 Oodle 可在首次使用时下载，账户登录使用系统浏览器。原版和本分支不要同时向同一游戏目录安装录制器。

## 验证与适用范围

- 本次非 GPU／非游戏自动化测试 **1442/1442 通过**，包含显卡选择、录制导入及编译状态回归测试。
- PR 作者提供了本地双显卡环境的验证结果，详见 [PR #1](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/pull/1)。自动化测试不代表所有游戏、显卡和驱动都已完成实机验证，也不保证游戏帧率或卡顿改善。
- 支持范围仍为 NVIDIA／AMD 的 DirectX 12 游戏及部分 NVIDIA DirectX 11 游戏。

**NoACGate 只移除了本程序自身的录制门禁，不会关闭或绕过游戏反作弊。** 反作弊游戏的录制仍默认关闭，须阅读提示并逐游戏确认。游戏仍可能拒绝加载录制器、无法启动、崩溃或处罚账号；请自行判断风险。

## 致谢

感谢贡献者 [@ohjoij-ys](https://github.com/ohjoij-ys) 在 [PR #1](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/pull/1) 中提供显卡选择、三处录制／编译修复及本地双显卡验证。也感谢 [BlueHeisenberg/SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) 原项目。

---

# SCSKiller NoACGate Chinese Edition v1.2.5-noacgate.2

An unofficial Chinese preview based on SCSKiller 1.2.5. This update adds GPU selection and fixes three problems between recording and precompilation.

## Changes

- **Choose the compilation GPU** in Settings, or use `cli\scskiller.exe gpu`, `gpu use <id>` and `gpu use auto`. Auto selects the adapter with the most dedicated video memory. An unavailable saved choice falls back to Auto at startup.
- **GPU changes take effect after a full restart**, including switches between adapters from the same vendor and a return to Auto. The running process keeps its original GPU, even if matching DXGI entries reorder. This setting does not change the GPU used by the game itself.
- **Recorded games can reach the compile queue**: for recording-only Unreal games with per-game anti-cheat recording consent, the queue now uses the same readiness verdict as the library.
- **Recordings can be imported while the game is running**: the reader can open `scskiller.db` while the recorder holds it. The inbox is emptied only after the game exits and its imported length is confirmed unchanged.
- **Completed compiles no longer appear stale because of a false HDR-mod mismatch** when anti-cheat and ReShade/RenoDX are present before the first scan publishes the game list.

## Download and upgrade

Download the complete **`SCSKiller-NoACGate-zh-CN.zip`** and verify it with `SHA256SUMS.txt`. GitHub's automatic `Source code` archives are not runnable packages.

1. On Windows x64, install [.NET 10 Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) and [Windows App SDK Runtime 2.5.1 or a newer compatible version (x64)](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads). Neither runtime is bundled.
2. Fully exit SCSKiller from its notification-area menu, extract the **entire ZIP into a new directory**, then run `SCSKiller.exe`. Keep the previous directory for rollback; do not replace only the EXE.
3. This ZIP continues to use `%LOCALAPPDATA%\SCSKiller-NoACGate-zh-CN` for settings and recordings; preserve that directory. Creating a `data` directory beside the EXE alone does not change the data location. If you previously arranged a layout with a `.portable` marker, preserve the marker's directory and its entire `data` subdirectory together.

This unsigned minimal portable package has no installer or automatic updates. It omits Oodle and three WebView2 binaries; Oodle may be downloaded when needed, and account sign-in uses the system browser. Do not let the original app and this fork install recorders into the same game directory at once.

## Validation and scope

All **1442 non-GPU/non-game automated tests passed**, including GPU-selection, recording-import and compile-state regressions. The contributor's local dual-GPU results are recorded in [PR #1](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/pull/1). These results do not establish compatibility or performance improvements for every game, GPU or driver. Support remains DirectX 12 on NVIDIA/AMD and some DirectX 11 games on NVIDIA.

**NoACGate removes only this app's recording gate. It does not disable or bypass a game's anti-cheat.** Recording remains off by default for anti-cheat games and requires explicit per-game confirmation. A game may reject the recorder, fail to start, crash, or penalize an account.

## Thanks

Thanks to [@ohjoij-ys](https://github.com/ohjoij-ys) for GPU selection, the three recording/compilation fixes and local dual-GPU verification in [PR #1](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/pull/1), and to the original [BlueHeisenberg/SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) project.

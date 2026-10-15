# SCSKiller NoACGate 中文版 v1.2.5-noacgate.1

这是基于 SCSKiller 1.2.5 的非官方中文预览版。它保留游玩前预编译着色器的功能，并允许你在看到风险警告后，逐个游戏选择是否尝试直接录制检测到反作弊的游戏。

## 本版内容

- 游戏库、游戏详情、设置、常见状态和命令行的主要说明已翻译为简体中文；少数外部服务内容或底层错误仍可能显示英文。
- 反作弊游戏的直接录制默认关闭。只有在该游戏的详情页阅读警告并确认后，程序才会尝试安装录制器；全局自动录制开关不会替你确认。关闭许可后会撤销录制准入，运行中的游戏退出后再完成文件清理。
- 本分支使用独立的应用数据、启动项和任务名称。自动更新、上游在线规则刷新、社区数据库下载、匿名分享和活跃检查默认关闭。你主动使用 Patreon 登录、社区下载或分享时仍会连接原项目的服务。

**NoACGate 只移除了本程序自身的录制门禁，不会关闭或绕过游戏反作弊。** 游戏仍可能拒绝加载 `d3d12.dll`、无法启动、崩溃或处罚账号。本版本不保证任何反作弊游戏能够运行或录制；请勿在无法承担风险的账号或环境中开启直接录制。

## 下载与安装

所有用户只需下载完整主包 **`SCSKiller-NoACGate-zh-CN.zip`**。这是 Windows x64 便携版，没有安装程序，也不会自动更新。

1. 先安装 [.NET 10 Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/10.0)和 [Windows App SDK Runtime 2.5.1 或更新的兼容版本（x64）](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)。这两项运行时不在 ZIP 内。
2. 完全退出正在运行的 SCSKiller，将 ZIP **完整解压到新目录**，运行其中的 `SCSKiller.exe`。不要在压缩包内运行或只替换单个 EXE。GitHub 自动提供的 `Source code` 压缩包不能直接运行。
3. 如果你使用过原版或旧的本分支，保留旧目录作为回退。两个版本不要同时向同一个游戏目录安装录制器；切换前先在原版本关闭该游戏的录制器，并退出游戏。

包内不含 Oodle DLL；读取某些游戏档案时可能另行下载所需组件。精简包移除了三个 WebView2 二进制文件，账户登录使用系统浏览器。程序和原项目各自保存应用数据，但同一游戏目录只能有一个 `d3d12.dll` 录制器。

本版仅面向 DirectX 12 游戏（NVIDIA、AMD）及部分 DirectX 11 游戏（NVIDIA）。预编译只能减少部分着色器编译卡顿；加载、资源流送和其他卡顿原因不在其作用范围内。具体游戏、显卡和驱动的效果可能不同。

程序未签名，Windows 可能显示“未知发布者”。请从[本仓库的 Releases](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/releases)下载，并用同一版本的 `SHA256SUMS.txt` 核对 ZIP。

---

# SCSKiller NoACGate Chinese Edition v1.2.5-noacgate.1

This is an unofficial Chinese preview based on SCSKiller 1.2.5. It retains pre-game shader compilation and lets you choose, for each game, whether to attempt direct recording after reading a warning when anti-cheat is detected.

## What's included

- The main library, game details, settings, common status messages, and command-line help are translated into Simplified Chinese. Some external content and low-level errors may remain in English.
- Direct recording in anti-cheat games is off by default. The recorder is attempted only after you read and confirm the warning on that game's detail page. The global auto-record setting does not grant this permission. Turning permission off revokes recording access; file cleanup may wait until a running game exits.
- This fork uses separate app data, startup entries, and task names. Automatic updates, upstream rule refreshes, community database downloads, anonymous sharing, and activity checks are off by default. Using Patreon sign-in, community downloads, or sharing voluntarily still contacts the original project's services.

**NoACGate removes only this app's recording gate. It does not disable or bypass a game's anti-cheat.** A game may reject `d3d12.dll`, fail to start, crash, or penalize an account. No anti-cheat game's launch or recording is guaranteed. Do not enable direct recording on an account or in an environment where you cannot accept these risks.

## Download and installation

Everyone downloads the complete **`SCSKiller-NoACGate-zh-CN.zip`** package. It is a portable Windows x64 build, without an installer or automatic updates.

1. Install [.NET 10 Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) and [Windows App SDK Runtime 2.5.1 or a newer compatible version (x64)](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads). Neither runtime is bundled in the ZIP.
2. Fully exit any running SCSKiller instance. Extract the **entire ZIP into a new directory**, then run `SCSKiller.exe`. Do not run it inside the archive or replace only one EXE. GitHub's automatic `Source code` archives are not runnable packages.
3. If you used the original app or an older build of this fork, keep the old directory for rollback. Do not let both versions install a recorder into the same game directory. Turn off recording in the old version and exit the game before switching.

Oodle is not bundled; some game archives may trigger a separate component download. The minimal package omits three WebView2 binaries; account sign-in opens the system browser. This fork and the original app store their app data separately, but one game directory has room for only one `d3d12.dll` recorder.

This build targets DirectX 12 games on NVIDIA and AMD and some DirectX 11 games on NVIDIA. Precompilation can reduce only some shader compilation stutter. It does not address loading, streaming, or other causes of stutter. Results vary by game, GPU, and driver.

The binaries are unsigned, so Windows may show “Unknown publisher.” Download from [this repository's Releases](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/releases) and check the ZIP against the accompanying `SHA256SUMS.txt`.

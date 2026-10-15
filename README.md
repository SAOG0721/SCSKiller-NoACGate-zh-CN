# SCSKiller NoACGate 中文版

这是 [SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) 的非官方简体中文分支。程序可在游玩前读取游戏中的着色器，并尝试将其预编译到显卡驱动缓存，以减少着色器首次编译造成的卡顿。本分支还允许你在阅读警告后，逐个游戏选择是否尝试录制检测到反作弊的游戏。

**NoACGate 只移除了本程序自身的录制限制。它不会关闭、绕过或削弱游戏反作弊。** 游戏仍可能拒绝加载录制器、无法启动、崩溃，或对账号采取措施。请自行判断风险，不要在无法承担这些后果的账号或环境中开启直接录制。

## 下载与运行

1. 从[本仓库的 Releases](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/releases)下载 `SCSKiller-NoACGate-zh-CN.zip`。如果列表中还没有可见版本，说明分发包尚未公开。
2. 在 Windows x64 电脑上安装 [.NET 10 Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/10.0)和 [Windows App SDK Runtime 2.5.1 或更新的兼容版本（x64）](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)。压缩包不包含这两项运行时。
3. 将 ZIP **完整解压到新目录**，运行其中的 `SCSKiller.exe`。请勿在压缩包内直接运行，也不要只复制单个 EXE。GitHub 自动生成的 `Source code` 压缩包不是可运行程序。

本分支目前只提供便携 ZIP，没有安装程序，自动更新保持关闭。升级时先退出正在运行的程序，再将新 ZIP 完整解压到另一目录。应用数据默认保存在 `%LOCALAPPDATA%\SCSKiller-NoACGate-zh-CN`；使用程序旁的 `data` 目录时则进入便携数据模式。

## 使用

- 打开游戏库，选择已识别的游戏，在游戏详情中查看是否可以预编译。预编译在游戏关闭时进行；实际覆盖率取决于游戏、显卡、驱动和已有录制数据。
- 需要补充录制的游戏，可在详情页查看录制器状态。录制器会尝试在游戏目录放入 `d3d12.dll`。如果目录中已有同名文件、游戏正在运行或存在其他安装限制，程序不会直接覆盖它。
- 检测到反作弊时，详情页另有“**反作弊游戏：允许直接录制**”开关。它默认关闭；阅读风险提示并确认后，程序才会对该游戏尝试安装录制器。设置页的“在所有兼容游戏中自动录制”不会代替这项逐游戏确认。关闭许可后，程序会撤销录制准入；若游戏正在运行，文件移除可能要等游戏退出。

本程序面向 Windows x64 的 DirectX 12 游戏（NVIDIA、AMD）和部分 DirectX 11 游戏（NVIDIA）。它只能处理着色器编译导致的部分卡顿，不能保证消除加载、资源流送或其他原因造成的卡顿，也不能保证每个游戏都能录制或预编译。

### 选择编译显卡

从 `v1.2.5-noacgate.2` 起提供此功能；使用 `v1.2.5-noacgate.1` 的用户请先升级。

在“设置”中找到“**编译使用的显卡**”，选择要为游戏预编译的显卡。默认“自动”使用 DXGI 列出的专用显存最多的非软件适配器；双显卡的显存容量接近时，它不一定是你希望使用的那块。这里的选择只决定 SCSKiller 为哪块显卡编译，不会更改游戏自身使用的显卡。

更改选择后，请**完全退出 SCSKiller，再重新打开**，同厂商显卡之间切换或切回“自动”也一样。关闭窗口默认只是隐藏到通知区域，可右键单击通知区域图标并选择“退出”。本次进程继续使用原显卡，下一次启动才会切换；若启动时找不到指定显卡，则回退到自动选择。

也可在程序目录使用命令行：

```text
.\cli\scskiller.exe gpu
.\cli\scskiller.exe gpu use <id>
.\cli\scskiller.exe gpu use auto
```

第一条列出显卡及其 ID；第二条将 `<id>` 替换为列表中的 ID，保存指定显卡；第三条恢复自动选择。使用命令行修改前请退出 GUI，避免 GUI 随后保存设置时覆盖修改。下一次 CLI 编译或计划任务运行会读取新选择。

## 与原版及在线服务

本分支使用独立的应用数据、启动项和任务名称。原版与本分支**不要同时向同一个游戏目录安装录制器**；同一位置只能放一个 `d3d12.dll`。切换版本前，先在当前版本中关闭该游戏的录制器并退出游戏。

本分支不自动连接上游在线规则。社区数据库下载、匿名分享和活跃检查默认关闭；主动登录 Patreon、开启社区下载或分享时，会连接原项目的服务。这些服务不由本分支运营。包中不含 Oodle DLL，遇到需要它的游戏档案时，程序可能另行下载。

## 贡献者

- [@ohjoij-ys](https://github.com/ohjoij-ys)：在 [PR #1](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN/pull/1) 中贡献显卡选择、录制后编译、运行中录制导入和编译状态判断修复，并提供本地双显卡环境的验证结果。感谢贡献！

## 来源与许可

本项目基于 [BlueHeisenberg/SCSKiller](https://github.com/BlueHeisenberg/SCSKiller)，保留原作者和第三方的版权声明。源码采用 [GPL-3.0-or-later](LICENSE)，另见[附加许可](LICENSE-EXCEPTION.txt)和[第三方声明](THIRD-PARTY-NOTICES.md)。原项目的介绍保存在[上游 README 存档](README.upstream.md)；其中的下载、反馈入口和性能数据均不属于本分支。

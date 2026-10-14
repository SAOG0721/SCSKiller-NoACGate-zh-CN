# SCSKiller-NoACGate-zh-CN

这是 [BlueHeisenberg/SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) 的简体中文 fork。原项目在游戏启动前把游戏着色器编译进显卡驱动缓存；详情请看[上游 README](https://github.com/BlueHeisenberg/SCSKiller#readme)和[架构说明](ARCHITECTURE.md)。本 fork 的开发进度见 [TODO.md](TODO.md)。

## 当前改动

- 为检测到反作弊的游戏增加逐游戏、默认关闭的**直接录制选择**。详情页显示中文风险说明；用户确认后才会允许安装录制器。全局“录制所有兼容游戏”不会代替这一确认。
- 保留反作弊检测和其他限制，例如游戏运行时不改写文件、手动添加的游戏先确认目录，以及不覆盖已有的 `d3d12.dll`。关闭选择后会撤销本 fork 的录制许可；游戏正在运行时，卸载文件要等游戏退出。
- 本 fork 的数据、单实例标识、计划任务、启动项、录制器标记和录制准入文件使用独立名称。两个版本仍不能同时在**同一游戏**的同一个 `d3d12.dll` 位置安装各自的录制器。
- 界面汉化正在进行，目前只有新增的录制风险提示及部分现有文案为中文。**尚无正式发布版本。**

`NoACGate` 仅表示解除 **SCSKiller 自身**的录制器反作弊硬门禁。它不会关闭或绕过游戏的反作弊。游戏可能阻止 DLL 加载、拒绝录制、崩溃，或对账号采取措施；本项目没有也无法提供兼容或账号安全保证。请选择自己有权测试的游戏与环境，不要把源码测试结果当成实机结果。

## 从源码构建

需要 Visual Studio 2022 的 C++ 桌面开发组件和 .NET 10 SDK。在仓库根目录运行：

```powershell
cmake -S proxy -B proxy/build -A x64
cmake --build proxy/build --config Release
dotnet build SCSKiller.slnx -c Release
dotnet test tests/SCSKiller.Tests -c Release --filter "Needs!=Gpu&Needs!=Game"
```

上述命令会生成本地程序与测试结果，不会把录制器安装到任何实际游戏。本 fork 仍在验证阶段，请先看 [TODO.md](TODO.md) 里的未完成项目。

## 许可证与来源

继承上游的 [GPL-3.0-or-later 许可证](LICENSE)、[附加许可](LICENSE-EXCEPTION.txt)及[第三方声明](THIRD-PARTY-NOTICES.md)。原作者和原项目的版权声明保留在仓库历史与原文件中。本 fork 的源码仓库：[SAOG0721/SCSKiller-NoACGate-zh-CN](https://github.com/SAOG0721/SCSKiller-NoACGate-zh-CN)。

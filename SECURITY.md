# SCSKiller-NoACGate-zh-CN 安全问题

## Reporting a vulnerability

本 fork 尚未发布稳定版本，也未承诺响应时限。请勿把本 fork 的漏洞报告提交给上游项目。若本仓库的 **Security → Report a vulnerability** 私密报告入口可用，请通过它提供受影响的提交或版本、复现步骤和影响；若入口不可用，请勿在公开讨论中贴出敏感细节。

## Scope

- The app, the `scskiller` command line and the native tools (`scskiller_warm.exe`, the recorder `d3d12.dll`).
- The recorder: anything it does in a game's process, and anything written outside SCSKiller's own folders or the folder
  of a game it's installed in.
- The updater and the update feed signature: a tampered feed or package the app accepts.
- The sign-in flow and the tokens the app stores.

当前只处理本 fork 仍在维护的源码和后续由本 fork 发布的版本；上游安装包不属于本 fork 的支持范围。

# 为 SCSKiller-NoACGate-zh-CN 贡献

本 fork 仍在开发，GitHub Issues 目前未开放；请勿将本 fork 的错误或游戏请求提交到上游项目。功能进度见 [TODO.md](TODO.md)。安全问题见 [SECURITY.md](SECURITY.md)。

## Anti-cheat games

本 fork 允许用户在详情页阅读风险提示后，逐游戏选择是否在检测到反作弊时尝试安装录制器。它不会关闭或绕过游戏的反作弊，也不保证代理能加载、游戏能运行或账号安全。此路径的变更应保留明确确认、默认关闭和撤销许可的行为，并分别报告源码测试与真实游戏验证结果。

## How pull requests are merged

提交 Pull Request 时请写明修改范围、验证方式和仍需手动确认的行为。本 fork 尚无正式发布版本，也不承诺 PR 的合并或发布时间。

## Sign-off (DCO)

Every commit needs a `Signed-off-by` line (`git commit -s`), certifying the
[Developer Certificate of Origin](https://developercertificate.org/): you wrote the change or have the right to submit
it under this project's licence. Use the commit author's name and email (a GitHub noreply address is fine). Commits
without it can't be merged. Contributions are licensed under GPL-3.0-or-later with the additional permission in
[LICENSE-EXCEPTION.txt](LICENSE-EXCEPTION.txt).

## Build

Visual Studio 2022 with the C++ desktop workload (MSVC x64, CMake), and the .NET 10 SDK:

```
cmake -S proxy -B proxy/build -A x64
cmake --build proxy/build --config Release
dotnet build SCSKiller.slnx -c Release
```

The CMake configure downloads AMD's `amd_ags_x64.dll`, pinned by SHA-256. Without it, AMD warms of AGS games use a plain
device.

## Tests

```
dotnet test tests/SCSKiller.Tests -c Release --filter "Needs!=Gpu&Needs!=Game"
```

CI runs this on a clean Windows machine with no games and no GPU, and it must pass. Tests that need more carry a trait:
`[Trait("Needs", "Gpu")]` drives a real GPU and its driver cache, `[Trait("Needs", "Game")]` reads installed games,
launchers or recordings. Such a test returns early when what it needs isn't there. Run them locally
(`dotnet test tests/SCSKiller.Tests -c Release`) when your change touches their area.

- No machine paths in tests. `TestEnv` finds games through Steam's library list, the `XboxGames` folders, GOG and
  `SCSKILLER_TEST_GAMES_ROOT` (`;`-separated folders of game install folders). GPU tests take a lock in
  `SCSKILLER_DEV_DIR` (default `%TEMP%\scskiller-test`).
- Tests never write into a game folder, change the driver's cache size or settings, or register scheduled tasks.

## Style

- Match the code around your change. One topic per PR, kept small.
- Comment only what the code can't say: a non-obvious reason, an invariant, a driver or engine workaround, a file-format
  detail, a measured fact. No commented-out code.
- Add a line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for anything a user would notice.
- Don't commit game files, shader dumps, recordings (`*.db`) or third-party binaries.

<#
.SYNOPSIS
Builds dist\SCSKiller-NoACGate-zh-CN\ and dist\SCSKiller-NoACGate-zh-CN.zip:

  SCSKiller.exe            WinUI app, self-contained (.NET + Windows App SDK), + oodle-data-shared.dll, zlib-ng2.dll
  cli\scskiller.exe        command line, self-contained, same Core project and commit, + the two codec DLLs
  cli\scskillerw.exe       the same CLI flagged as a GUI-subsystem exe: what the scheduled task runs (no console window)
  native\                  scskiller_warm.exe + the proxy d3d12.dll (the app looks in native\, the CLI in ..\native\)
                           + amd_ags_x64.dll (AMD AGS, downloaded by the proxy's CMake configure)
                           + segheap\scskiller_warm.exe (the same on the segment heap: NVIDIA's warm)
  THIRD-PARTY-NOTICES.md   every third-party component and its licence; notices\ holds the Microsoft packages' own notices

The CLI can't sit next to the app: scskiller.exe and SCSKiller.exe are the same file name on NTFS.

-NoOodle leaves oodle-data-shared.dll out (CI and anything published: Oodle is proprietary, redistribution not
established, THIRD-PARTY-NOTICES.md); UnrealReader and the FromSoft reader download it through CUE4Parse on first use.

-Version X.Y.Z[-pre] (a leading v is dropped) stamps every exe and DLL with the same ProductVersion (SignPath).
Without it: the v* tag at HEAD if there is exactly one, else the dev default 0.0.0-internal.0+<sha>.

-OutputSubdir builds under dist\<name>\, so a running copy in the default dist directory can stay open.

-FrameworkDependent omits the .NET and Windows App SDK runtimes. The target PC must already have x64 .NET 10
and Windows App SDK Runtime 2.5.1.

-NoWebView omits three WebView2 binaries from the framework-dependent package. The current app opens sign-in in the
system browser; retain the WebView2 notice because WinUI still declares the transitive dependency.
#>
param([string]$Configuration = "Release", [switch]$NoZip, [switch]$NoOodle, [string]$Version, [string]$OutputSubdir,
    [switch]$FrameworkDependent, [switch]$NoWebView)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

# 0. version: -Version, else the one v* tag at HEAD (promotions tag one commit several times: then say which)
if (-not $Version) {
    $tags = @(git -C $repo tag --points-at HEAD -l "v*")
    if ($tags.Count -gt 1) { throw "HEAD has several tags ($($tags -join ', ')): pass -Version" }
    if ($tags.Count -eq 1) { $Version = $tags[0] }
}
$Version = $Version -replace '^v', ''
if ($Version -and $Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') { throw "not a SemVer X.Y.Z[-pre]: $Version" }
if ($NoWebView -and -not $FrameworkDependent) { throw "-NoWebView requires -FrameworkDependent" }
if ($Version -and (git -C $repo status --porcelain --untracked-files=no)) { throw "versioned build requires a clean source checkout" }
$sourceCommit = (git -C $repo rev-parse HEAD).Trim()
# a versioned build verifies the server's signed lists: the rules key must be pinned (tools/release-sign keygen --for rules)
if ($Version -and (Get-Content (Join-Path $repo "src\SCSKiller.Core\App\ContentTrust.cs") -Raw) -notmatch '\["rules-a"\]\s*=\s*"[A-Za-z0-9+/]{43}="') {
    throw "ContentTrust.RulesKeys has no rules-a key: pin the public key from tools/release-sign keygen --for rules before a release"
}
$versionArgs = if ($Version) { @("-p:Version=$Version", "-p:IncludeSourceRevisionInInformationalVersion=false") } else { @() }
Write-Host "version: $(if ($Version) { $Version } else { '0.0.0-internal.0 (dev)' })"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
$dotnet = Join-Path $env:LOCALAPPDATA "Codex\SCSKiller-dotnet10\dotnet.exe"
if (-not (Test-Path $dotnet) -and $env:DOTNET_ROOT) { $dotnet = Join-Path $env:DOTNET_ROOT "dotnet.exe" }
if (-not (Test-Path $dotnet)) { $dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe" }
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
if ($OutputSubdir -and $OutputSubdir -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "invalid output subdirectory: $OutputSubdir" }
$dist = if ($OutputSubdir) { Join-Path $repo "dist\$OutputSubdir" } else { Join-Path $repo "dist" }
New-Item -ItemType Directory -Force $dist | Out-Null
$artifactName = "SCSKiller-NoACGate-zh-CN"
$out = Join-Path $dist $artifactName
$cli = Join-Path $out "cli"
$native = Join-Path $out "native"

function Run([string]$exe, [string[]]$argv) {
    & $exe @argv
    if ($LASTEXITCODE -ne 0) { throw "$exe $argv failed with exit code $LASTEXITCODE" }
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

# 1. native tools from the proxy CMake build
$build = Join-Path $repo "proxy\build"
$scskVersion = if ($Version) { $Version } else { "0.0.0-internal.0" }   # every time: a cached version must not carry over
Run cmake @("-S", (Join-Path $repo "proxy"), "-B", $build, "-A", "x64", "-DSCSK_VERSION=$scskVersion")
Run cmake @("--build", $build, "--config", "Release")
New-Item -ItemType Directory -Force $native | Out-Null
Copy-Item (Join-Path $build "Release\scskiller_warm.exe"), (Join-Path $build "Release\d3d12.dll"), (Join-Path $build "Release\amd_ags_x64.dll") $native
New-Item -ItemType Directory -Force (Join-Path $native "segheap") | Out-Null
Copy-Item (Join-Path $build "Release\segheap\scskiller_warm.exe") (Join-Path $native "segheap")

# 2. app + CLI
# the SDK reuses obj\...\apphost.exe until the assembly changes, so a CETCompat change alone would ship the old flag
Get-ChildItem (Join-Path $repo "src") -Recurse -Filter apphost.exe | Where-Object FullName -like "*\obj\*" | Remove-Item
Run $dotnet (@("publish", (Join-Path $repo "src\SCSKiller.App\SCSKiller.App.csproj"), "-c", $Configuration, "-r", "win-x64",
    "--self-contained", $selfContained, "-p:SelfContained=$selfContained", "-p:WindowsAppSDKSelfContained=$selfContained",
    "-p:Platform=x64", "-p:EnableMsixTooling=true", "-o", $out) + $versionArgs)   # MSIX tooling: else the app's .pri/.xbf aren't published and XAML crashes at start
Run $dotnet (@("publish", (Join-Path $repo "src\SCSKiller.Cli\SCSKiller.Cli.csproj"), "-c", $Configuration, "-r", "win-x64",
    "--self-contained", $selfContained, "-p:SelfContained=$selfContained", "-o", $cli) + $versionArgs)
if ($FrameworkDependent) {
    Get-ChildItem -LiteralPath $out -Recurse -File -Filter '*.pdb' | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
}
if ($NoWebView) {
    foreach ($name in "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.Core.Projection.dll", "WebView2Loader.dll") {
        $file = Join-Path $out $name
        if (-not (Test-Path -LiteralPath $file)) { throw "expected WebView2 file is missing before minimal-package trimming: $name" }
        Remove-Item -LiteralPath $file
    }
}

# 3. scskillerw.exe: optional header Subsystem (e_lfanew + 24 + 68) 3 = console -> 2 = GUI, as the SDK does for WinExe.
#    The apphost still runs scskiller.dll from the same folder.
$bytes = [IO.File]::ReadAllBytes((Join-Path $cli "scskiller.exe"))
$subsystem = [BitConverter]::ToInt32($bytes, 0x3C) + 24 + 68
if ($bytes[$subsystem] -ne 3) { throw "scskiller.exe: unexpected PE subsystem $($bytes[$subsystem])" }
$bytes[$subsystem] = 2
[IO.File]::WriteAllBytes((Join-Path $cli "scskillerw.exe"), $bytes)

# 3b. no .NET exe may be CET-compatible (Directory.Build.props): the flag is bit 0 of the data of the debug directory entry of
#     type 20 (IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS). PE32+: the debug directory is data directory 6, at optional header + 160.
foreach ($exe in "SCSKiller.exe", "cli\scskiller.exe", "cli\scskillerw.exe") {
    $b = [IO.File]::ReadAllBytes((Join-Path $out $exe))
    $pe = [BitConverter]::ToInt32($b, 0x3C)
    $opt = $pe + 24
    $sections = $opt + [BitConverter]::ToUInt16($b, $pe + 20)
    $rva = [BitConverter]::ToUInt32($b, $opt + 160)
    $size = [BitConverter]::ToUInt32($b, $opt + 164)
    $dir = $null
    for ($i = 0; $i -lt [BitConverter]::ToUInt16($b, $pe + 6); $i++) {
        $s = $sections + 40 * $i
        $va = [BitConverter]::ToUInt32($b, $s + 12)
        if ($rva -ge $va -and $rva -lt $va + [BitConverter]::ToUInt32($b, $s + 16)) { $dir = $rva - $va + [BitConverter]::ToUInt32($b, $s + 20) }
    }
    if ($null -eq $dir) { throw "${exe}: no debug directory" }
    for ($e = $dir; $e -lt $dir + $size; $e += 28) {
        if ([BitConverter]::ToUInt32($b, $e + 12) -eq 20 -and ([BitConverter]::ToUInt32($b, [BitConverter]::ToUInt32($b, $e + 24)) -band 1)) {
            throw "$exe is CET-compatible: .NET would fail-fast at start on Windows 10 builds without CET special APCs"
        }
    }
}

# 4. codecs next to both executables (UnrealReader loads them from AppContext.BaseDirectory), from a build-owned cache,
#    never the app's data folder: fetch-codecs keeps the pinned copies there (Codecs.Pinned), downloads one that is
#    missing or doesn't match, and fails the build when no copy matches
$codecs = @(if (-not $NoOodle) { "oodle-data-shared.dll" }) + "zlib-ng2.dll"
$cache = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "SCSKiller-build\codecs"
Run (Join-Path $cli "scskiller.exe") @("fetch-codecs", $cache)
foreach ($c in $codecs) { Copy-Item (Join-Path $cache $c) $cli; Copy-Item (Join-Path $cache $c) $out }

# 4b. licences: our notices and licence (GPL-3.0-or-later + the section 7 permission), plus the large notice files of the Microsoft packages as shipped
Copy-Item (Join-Path $repo "THIRD-PARTY-NOTICES.md") $out
foreach ($l in "LICENSE", "LICENSE-EXCEPTION.txt") { Copy-Item (Join-Path $repo $l) $out }
if ($FrameworkDependent) { Copy-Item (Join-Path $repo "build\runtime-requirements.zh-CN.txt") (Join-Path $out "RUNTIME-REQUIREMENTS.txt") }
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
New-Item -ItemType Directory -Force (Join-Path $out "notices") | Out-Null
# versions come from what was actually published (any SDK/runtime of this major or newer builds it)
$runtime = if (-not $FrameworkDependent) {
    ((Get-Content (Join-Path $out "SCSKiller.runtimeconfig.json") -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks |
        Where-Object name -eq "Microsoft.NETCore.App").version
}
$libs = (Get-Content (Join-Path $repo "src\SCSKiller.App\obj\project.assets.json") -Raw | ConvertFrom-Json).libraries.PSObject.Properties.Name   # the restore that built it
function PackageVersion($id) {
    $v = $libs | Where-Object { $_ -like "$id/*" } | Select-Object -First 1
    if (-not $v) { throw "$id is not among the app's restored packages" }
    $v.Split("/")[1]
}
$noticeFiles = @(
        @("microsoft.windowsappsdk.runtime\$(PackageVersion 'Microsoft.WindowsAppSDK.Runtime')\NOTICE.txt", "WindowsAppSDK-NOTICE.txt"),
        @("microsoft.windowsappsdk.winui\$(PackageVersion 'Microsoft.WindowsAppSDK.WinUI')\NOTICE.txt", "WinUI-NOTICE.txt"),
        @("microsoft.web.webview2\$(PackageVersion 'Microsoft.Web.WebView2')\NOTICE.txt", "WebView2-NOTICE.txt"),
        @("communitytoolkit.highperformance\$(PackageVersion 'CommunityToolkit.HighPerformance')\ThirdPartyNotices.txt", "CommunityToolkit-ThirdPartyNotices.txt"))
if (-not $FrameworkDependent) {
    $noticeFiles += ,@("microsoft.netcore.app.runtime.win-x64\$runtime\THIRD-PARTY-NOTICES.TXT", "dotnet-THIRD-PARTY-NOTICES.txt")
}
foreach ($n in $noticeFiles) {
    Copy-Item (Join-Path $nuget $n[0].ToLowerInvariant()) (Join-Path $out "notices\$($n[1])")   # THIRD-PARTY-NOTICES.md lists the versions shipped
}

# 5. layout check: the default bundle has its own runtime; the local lean build names external frameworks.
$expected = "SCSKiller.exe", "oodle-data-shared.dll", "zlib-ng2.dll", "cli\scskiller.exe", "cli\scskillerw.exe",
    "cli\oodle-data-shared.dll", "cli\zlib-ng2.dll", "native\scskiller_warm.exe", "native\segheap\scskiller_warm.exe", "native\d3d12.dll", "native\amd_ags_x64.dll",
    "SCSKiller.pri", "THIRD-PARTY-NOTICES.md", "LICENSE", "LICENSE-EXCEPTION.txt"
if ($FrameworkDependent) { $expected += "RUNTIME-REQUIREMENTS.txt" }
else { $expected += "hostfxr.dll", "coreclr.dll", "cli\hostfxr.dll", "cli\coreclr.dll", "Microsoft.WindowsAppRuntime.dll", "notices\dotnet-THIRD-PARTY-NOTICES.txt" }
if ($NoOodle) { $expected = $expected | Where-Object { $_ -notlike "*oodle-data-shared.dll" } }
$missing = $expected | Where-Object { -not (Test-Path (Join-Path $out $_)) }
if ($missing) { throw "$artifactName is missing: $($missing -join ', ')" }
if ($NoOodle -and (Get-ChildItem $out -Recurse -Filter "oodle-data-shared.dll")) { throw "-NoOodle, but oodle-data-shared.dll is in $artifactName" }
foreach ($rc in "SCSKiller.runtimeconfig.json", "cli\scskiller.runtimeconfig.json") {
    $options = (Get-Content (Join-Path $out $rc) -Raw | ConvertFrom-Json).runtimeOptions
    if ($FrameworkDependent) {
        if ($options.framework.name -ne "Microsoft.NETCore.App" -or $options.includedFrameworks) { throw "$rc does not require external .NET" }
    }
    elseif (-not $options.includedFrameworks) { throw "$rc is framework-dependent" }
}
if ($FrameworkDependent) {
    foreach ($name in "hostfxr.dll", "coreclr.dll", "System.Private.CoreLib.dll", "Microsoft.WindowsAppRuntime.dll",
        "cli\hostfxr.dll", "cli\coreclr.dll", "cli\System.Private.CoreLib.dll") {
        if (Test-Path (Join-Path $out $name)) { throw "framework-dependent build contains runtime file: $name" }
    }
}
if ($NoWebView) {
    foreach ($name in "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.Core.Projection.dll", "WebView2Loader.dll") {
        if (Test-Path -LiteralPath (Join-Path $out $name)) { throw "minimal package still contains $name" }
    }
}

# Bind a versioned package to its exact source commit and shipped file set.
if ($Version) {
    $hashes = [ordered]@{}
    Get-ChildItem -LiteralPath $out -Recurse -File | Sort-Object FullName | ForEach-Object {
        $path = [IO.Path]::GetRelativePath($out, $_.FullName).Replace('\', '/')
        $hashes[$path] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $manifest = [ordered]@{
        version = $Version
        tag = "v$Version"
        sourceCommit = $sourceCommit
        platform = "win-x64"
        frameworkDependent = [bool]$FrameworkDependent
        oodleBundled = -not [bool]$NoOodle
        webView2BinariesBundled = -not [bool]$NoWebView
        fileSha256 = $hashes
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'build-manifest.json') -Encoding utf8
}

# 6. zip (with the fork-named folder at its root)
if (-not $NoZip) {
    $zip = Join-Path $dist "$artifactName.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($out, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
}
$msg = "${out}: {0:N0} MB" -f ((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
if (-not $NoZip) { $msg += ", ${zip}: {0:N0} MB" -f ((Get-Item $zip).Length / 1MB) }
Write-Host $msg

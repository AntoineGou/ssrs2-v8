# SSRS2's V8 for ClearScript on Windows: ClearScript's native library (ClearScriptV8.win-<cpu>.dll) built from
# ClearScript 7.5.1.1's sources with patches\clearscript\*.patch, around V8 15.6.75.33 with
# patches/v8\*.patch (README.md says what they change).
#
#   ./build.ps1 <work folder> [cpu]     cpu: x64 (the default), x86 or arm64, all built on an x64 host
#
# Needs Visual Studio 2022 with the C++ workload, its Clang tools (ClearScript's projects are ClangCL's) and the Windows
# SDK with its debugging tools, as ClearScript's V8Update.cmd does. Downloads several GB the first time (depot_tools,
# V8 and its dependencies). Writes <work folder>\out\ClearScriptV8.win-<cpu>.dll.
param(
    [Parameter(Mandatory = $true)][string]$Work,
    [ValidateSet('x64', 'x86', 'arm64')][string]$Cpu = 'x64'
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$clearScriptTag = '7.5.1.1'
$v8Revision = '15.6.75.33'
# (ClearScript's project platforms, which are also the V8 output folders its projects link from)
$platform = @{ x64 = 'x64'; x86 = 'Win32'; arm64 = 'arm64' }[$Cpu]
$vcvarsArch = @{ x64 = 'x64'; x86 = 'x64_x86'; arm64 = 'x64_arm64' }[$Cpu]

function Invoke-Native([string]$What, [scriptblock]$Command, [string]$Log) {
    if ($Log) { & $Command *> $Log } else { & $Command }
    if ($LASTEXITCODE -ne 0) {
        if ($Log) { Get-Content $Log -Tail 60 }
        throw "$What failed ($LASTEXITCODE)"
    }
}

New-Item -ItemType Directory -Force $Work | Out-Null
$Work = (Resolve-Path $Work).Path
Write-Host "Building ClearScriptV8.win-$Cpu.dll in $Work"
# (no line ending conversion in any checkout, without touching the machine's git settings: the patches are LF's)
$env:GIT_CONFIG_COUNT = '1'
$env:GIT_CONFIG_KEY_0 = 'core.autocrlf'
$env:GIT_CONFIG_VALUE_0 = 'false'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio 2022 with the C++ workload is needed' }
$env:DEPOT_TOOLS_WIN_TOOLCHAIN = '0'
$env:vs2022_install = $vs

Set-Location $Work
if (-not (Test-Path ClearScript)) { Invoke-Native 'Cloning ClearScript' { git clone -q https://github.com/microsoft/ClearScript.git ClearScript } }
Set-Location ClearScript
$root = (Get-Location).Path
Invoke-Native 'Checking ClearScript out' { git checkout -q $clearScriptTag }
# (the series in order, ClearScript's library ported to V8 15.6 then SSRS2's changes, again whenever it changed)
$csPatches = Get-ChildItem (Join-Path $here 'patches\clearscript') -Filter *.patch | Sort-Object Name
$csPatched = "$clearScriptTag $((Get-Content -Raw $csPatches.FullName) -join '' | git hash-object --stdin)"
if (-not (Test-Path .ssrs2-patched) -or (Get-Content .ssrs2-patched) -ne $csPatched) {
    Invoke-Native 'Resetting ClearScript' { git checkout -q HEAD -- ClearScriptV8 }
    foreach ($p in $csPatches) { Invoke-Native "Applying $($p.Name)" { git apply --3way $p.FullName } }
    Set-Content .ssrs2-patched $csPatched
}

# V8: ClearScript's V8Update.cmd steps for another revision, with our patch and arguments
New-Item -ItemType Directory -Force V8\build | Out-Null
Set-Location V8\build
if (-not (Test-Path depot_tools)) {
    Invoke-WebRequest https://storage.googleapis.com/chrome-infra/depot_tools.zip -OutFile depot_tools.zip
    Expand-Archive depot_tools.zip depot_tools
}
$env:PATH = "$((Get-Location).Path)\depot_tools;$env:PATH"
if (-not (Test-Path .gclient)) { Invoke-Native 'gclient config' { gclient config https://chromium.googlesource.com/v8/v8 } 'config.log' }
$v8Patches = Get-ChildItem (Join-Path $here 'patches\v8') -Filter *.patch | Sort-Object Name
$patched = "$v8Revision $((Get-Content -Raw $v8Patches.FullName) -join '' | git hash-object --stdin)"
if (-not (Test-Path v8\.ssrs2-patched) -or (Get-Content v8\.ssrs2-patched) -ne $patched) {
    Write-Host "Downloading V8 $v8Revision and its dependencies"
    # (three tries: V8's download hooks run at once, and on Windows depot_tools' lock on gsutil's folder is a file opened
    # exclusively, which the others give up on after 30 seconds while the first installs gsutil)
    for ($try = 1; ; $try++) {
        gclient sync -r $v8Revision -D --force --reset --no-history *> "sync-$try.log"
        if ($LASTEXITCODE -eq 0) { break }
        if ($try -eq 3) { Get-Content "sync-$try.log" -Tail 60; throw "gclient sync failed ($LASTEXITCODE)" }
        Write-Host "gclient sync failed ($LASTEXITCODE), again"
    }
    Push-Location v8
    git config user.name ClearScript
    git config user.email clearscript@clearfoundry.net
    # (the series in order: ClearScript's changes ported to this V8, then SSRS2's)
    foreach ($p in $v8Patches) { Write-Host "Applying $($p.Name)"; Invoke-Native "Applying $($p.Name)" { git apply --3way --index $p.FullName } }
    # (not ClearScript's BuildPatch.txt: it only changes Linux builds)
    Set-Location third_party\icu
    Invoke-Native 'Patching ICU' { git apply --index --ignore-whitespace "$root\V8\ICUPatch.txt" }
    Pop-Location
    Set-Content v8\.ssrs2-patched $patched
}
if (-not (Test-Path json)) {
    Invoke-Native 'Cloning nlohmann/json' { git clone -q -n https://github.com/nlohmann/json.git }
    Push-Location json; Invoke-Native 'Checking json out' { git checkout -q v3.10.4 }; Pop-Location
}

# the compilers and tools for the target, as vcvarsall sets them
cmd /c "`"$vs\VC\Auxiliary\Build\vcvarsall.bat`" $vcvarsArch >nul && set" | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') { Set-Item "env:$($Matches[1])" $Matches[2] }
}

Set-Location v8
# V8 15.6 asks for the Windows SDK of Chromium's own toolchain (10.0.28000.0): a machine without it builds with its newest
$sdkVersion = [regex]::Match((Get-Content -Raw build\toolchain\win\setup_toolchain.py), "SDK_VERSION = '([0-9.]+)'").Groups[1].Value
$sdks = "${env:ProgramFiles(x86)}\Windows Kits\10\Include"
if (-not (Test-Path "$sdks\$sdkVersion\um\windows.h")) {
    $newest = Get-ChildItem $sdks -Directory | Where-Object { Test-Path "$($_.FullName)\um\windows.h" } |
        Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (-not $newest) { throw 'No Windows SDK' }
    Write-Host "Windows SDK $sdkVersion not installed: building with $($newest.Name)"
    foreach ($f in 'build\toolchain\win\setup_toolchain.py', 'build\vs_toolchain.py') {
        (Get-Content -Raw $f).Replace("SDK_VERSION = '$sdkVersion'", "SDK_VERSION = '$($newest.Name)'") | Set-Content -NoNewline $f
    }
    # (and the newest Windows version that SDK names: V8's NTDDI_VERSION is one only the newer SDK defines, read as 0)
    $ntddi = (Select-String -Path "$sdks\$($newest.Name)\shared\sdkddkver.h" -Pattern '^#define\s+(NTDDI_WIN\w+)\s' |
        Select-Object -Last 1).Matches[0].Groups[1].Value
    Write-Host "NTDDI_VERSION: $ntddi"
    (Get-Content -Raw build\config\win\BUILD.gn) -replace 'NTDDI_VERSION=NTDDI_\w+', "NTDDI_VERSION=$ntddi" |
        Set-Content -NoNewline build\config\win\BUILD.gn
}
Write-Host "Building V8 ($Cpu)"
$out = "out\$platform\Release"
# (in args.gn: PowerShell drops the quotes inside an argument given to a batch file, as gn is)
$gnArgs = 'fatal_linker_warnings=false', 'is_cfi=false', 'is_component_build=false', 'is_debug=false', "target_cpu=`"$Cpu`"",
    'use_clang_modules=false', 'use_custom_libcxx=false', 'use_thin_lto=false', 'v8_embedder_string="-ClearScript"',
    'v8_enable_fuzztest=false', 'v8_enable_pointer_compression=false', 'v8_enable_31bit_smis_on_64bit_arch=false',
    'v8_enable_temporal_support=false', 'v8_monolithic=true', 'v8_use_external_startup_data=false', "v8_target_cpu=`"$Cpu`"",
    'v8_disallow_js_in_api_interrupts_is_checked=false'
New-Item -ItemType Directory -Force $out | Out-Null
Set-Content "$out\args.gn" ($gnArgs -join "`n")
Invoke-Native 'gn gen' { gn gen $out } 'gn.log'
ninja -C $out obj\v8_monolith.lib *> build.log
if ($LASTEXITCODE -ne 0) {
    Get-Content build.log -Tail 60
    # (the record layouts Microsoft's rules gave the classes V8's layout checks fail on most, into layout-*.log: what a
    # port to this platform needs to see)
    Push-Location $out
    foreach ($o in 'js-interceptor-map-tq', 'js-atomics-synchronization-tq') {
        $command = ninja -t commands "obj/torque_generated_definitions/$o.obj" | Select-Object -Last 1
        if ($command) { cmd /c "$command -Xclang -fdump-record-layouts-simple" *> "..\..\..\layout-$o.log" }
    }
    Pop-Location
    throw 'Building V8 failed'
}
# (the library builds V8's ICU data into itself from here)
Copy-Item "$out\icudtl.dat" "$root\V8\icudtl.dat"

Write-Host "Linking ClearScriptV8.win-$Cpu.dll"
Set-Location $root
$project = "ClearScriptV8\win-$Cpu\ClearScriptV8.win-$Cpu.vcxproj"
Invoke-Native 'MSBuild' { msbuild $project -p:Configuration=Release -p:Platform=$platform "-p:SolutionDir=$root\" -m -v:minimal -nologo }
$dll = "$root\bin\Release\ClearScriptV8.win-$Cpu.dll"
New-Item -ItemType Directory -Force "$Work\out" | Out-Null
Copy-Item $dll "$Work\out\"

# the licences of what the library holds: ClearScript, nlohmann/json, V8 and the third-party code V8 links in
$licenses = "$Work\out\licenses"
if (Test-Path $licenses) { Remove-Item -Recurse -Force $licenses }
New-Item -ItemType Directory -Force "$licenses\v8" | Out-Null
Copy-Item License.txt "$licenses\ClearScript.txt"
Copy-Item V8\build\json\LICENSE.MIT "$licenses\nlohmann-json.txt"
Copy-Item V8\build\v8\LICENSE* "$licenses\v8\"
foreach ($f in 'abseil-cpp/LICENSE', 'dragonbox/src/LICENSE-Apache2-LLVM', 'dragonbox/src/LICENSE-Boost', 'fast_float/src/LICENSE-MIT',
        'fast_float/src/LICENSE-APACHE', 'fast_float/src/LICENSE-BOOST', 'fp16/LICENSE', 'highway/LICENSE', 'icu/LICENSE',
        'inspector_protocol/LICENSE', 'simdutf/LICENSE', 'zlib/LICENSE') {
    $to = "$licenses\v8\third_party\$f"
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item "V8\build\v8\third_party\$f" $to
}
Get-Item "$Work\out\ClearScriptV8.win-$Cpu.dll"

#!/usr/bin/env bash
# ClearScript's managed assemblies as SSRS2 builds them (SSRS2.ClearScript.Core.dll, SSRS2.ClearScript.V8.dll), from
# ClearScript 7.5.1.1's sources with patches/clearscript-managed/*.patch, for the libraries build.sh and build.ps1 make:
# the same on every platform, so built once. Needs git and a .NET SDK (8 or later).
#
#   ./build-managed.sh <work folder>
#
# Writes <work folder>/out-managed/lib/net8.0/SSRS2.ClearScript.Core.dll and SSRS2.ClearScript.V8.dll, and ClearScript's
# licence in out-managed/licenses. A changed build under its own names: the assemblies SSRS2.ClearScript.*, the
# namespaces SSRS2.ClearScript.* (ClearScript's API otherwise), the native library looked for
# SSRS2.ClearScriptV8.<rid>.<ext> (what build.sh and build.ps1 write). So an application can hold this build and
# ClearScript's own packages side by side, and nothing here passes for Microsoft's files. No ICU data assembly: the
# native libraries carry V8's ICU data. SSRS2.ClearScript.V8 needs Newtonsoft.Json 13.0.3, as ClearScript's own.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
work=${1:?usage: build-managed.sh <work folder>}
mkdir -p "$work"
work=$(cd "$work" && pwd)
clearscript_tag=7.5.1.1

cd "$work"
if [[ ! -d ClearScript ]]; then git clone -q https://github.com/microsoft/ClearScript.git ClearScript; fi
cd ClearScript
git checkout -q "$clearscript_tag"
# (the managed sources alone, each time from ClearScript's own: build.sh's patches of the native side, when this folder
# is its own, stay as they are)
git checkout -q HEAD -- ClearScript NetCore NetStandard
while IFS= read -r p; do echo "Applying $(basename "$p")"; git apply --3way "$p" || exit 1; done < <(ls "$here"/patches/clearscript-managed/*.patch | LC_ALL=C sort)
git reset -q -- ClearScript NetCore NetStandard

# The build's own names, in every managed source: the namespaces (Microsoft.ClearScript -> SSRS2.ClearScript), the
# friend assemblies (ClearScript.V8 -> SSRS2.ClearScript.V8), the native library's file name (ClearScriptV8.<rid> ->
# SSRS2.ClearScriptV8.<rid>: V8Proxy's name and the generated imports'), and the product's name. The version stays
# ClearScript's: the native library answers it and the managed side checks it.
while IFS= read -r -d '' f; do
    sed -i.ssrs2 \
        -e 's/Microsoft\.ClearScript/SSRS2.ClearScript/g' \
        -e 's/InternalsVisibleTo("ClearScript\./InternalsVisibleTo("SSRS2.ClearScript./g' \
        -e 's/"ClearScriptV8\(["\.]\)/"SSRS2.ClearScriptV8\1/g' \
        -e 's/AssemblyProduct("ClearScript")/AssemblyProduct("ClearScript (SSRS2 build)")/' \
        "$f"
    rm -f "$f.ssrs2"
done < <(find ClearScript NetCore -type f \( -name '*.cs' -o -name '*.csproj' \) -print0)
! grep -rq --include='*.cs' 'namespace Microsoft\.ClearScript' ClearScript || { echo "a namespace was not renamed"; exit 1; }

# (C# 12 for `scoped`; ClearScript's own warnings are not this build's to fail on; the same bytes from the same sources)
build="$work/managed-build"
rm -rf "$build"
dotnet build NetCore/ClearScript.V8/ClearScript.V8.csproj -c Release -o "$build" --nologo -v quiet \
    -p:Ssrs2Managed=true -p:TargetFrameworks=net8.0 -p:LangVersion=12.0 -p:TreatWarningsAsErrors=false \
    -p:Deterministic=true -p:ContinuousIntegrationBuild=true -p:DebugType=none -p:GenerateDocumentationFile=false

out="$work/out-managed"
rm -rf "$out"
mkdir -p "$out/lib/net8.0" "$out/licenses"
cp "$build/SSRS2.ClearScript.Core.dll" "$build/SSRS2.ClearScript.V8.dll" "$out/lib/net8.0/"
cp License.txt "$out/licenses/ClearScript.txt"
[[ ! -e "$build/ClearScript.V8.ICUData.dll" ]] || { echo "the ICU data assembly was built: the managed patch did not apply"; exit 1; }
ls -la "$out/lib/net8.0"

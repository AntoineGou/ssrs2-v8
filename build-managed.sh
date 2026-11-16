#!/usr/bin/env bash
# ClearScript's managed assemblies (ClearScript.Core.dll, ClearScript.V8.dll) built from ClearScript 7.5.1.1's sources
# with patches/clearscript-managed/*.patch, for the libraries build.sh and build.ps1 make: the same on every platform,
# so built once. Needs git and a .NET SDK (8 or later).
#
#   ./build-managed.sh <work folder>
#
# Writes <work folder>/out-managed/lib/net8.0/ClearScript.Core.dll and ClearScript.V8.dll, and ClearScript's licence in
# out-managed/licenses. No ICU data assembly: the native libraries carry V8's ICU data. ClearScript.V8 needs
# Newtonsoft.Json 13.0.3, as ClearScript's own package does.
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
# (the managed sources alone: build.sh's patches of the native side, when this folder is its own, stay as they are)
patches=()
while IFS= read -r p; do patches+=("$p"); done < <(ls "$here"/patches/clearscript-managed/*.patch | LC_ALL=C sort)
patched="$clearscript_tag $(cat "${patches[@]}" | git hash-object --stdin)"
if [[ "$(cat .ssrs2-managed-patched 2>/dev/null || true)" != "$patched" ]]; then
    git checkout -q HEAD -- ClearScript NetCore NetStandard
    for p in "${patches[@]}"; do echo "Applying $(basename "$p")"; git apply --3way "$p" || exit 1; done
    echo "$patched" > .ssrs2-managed-patched
fi

# (C# 12 for `scoped`; ClearScript's own warnings are not this build's to fail on; the same bytes from the same sources)
build="$work/managed-build"
rm -rf "$build"
dotnet build NetCore/ClearScript.V8/ClearScript.V8.csproj -c Release -o "$build" --nologo -v quiet \
    -p:Ssrs2Managed=true -p:TargetFrameworks=net8.0 -p:LangVersion=12.0 -p:TreatWarningsAsErrors=false \
    -p:Deterministic=true -p:ContinuousIntegrationBuild=true -p:DebugType=none -p:GenerateDocumentationFile=false

out="$work/out-managed"
rm -rf "$out"
mkdir -p "$out/lib/net8.0" "$out/licenses"
cp "$build/ClearScript.Core.dll" "$build/ClearScript.V8.dll" "$out/lib/net8.0/"
cp License.txt "$out/licenses/ClearScript.txt"
[[ ! -e "$build/ClearScript.V8.ICUData.dll" ]] || { echo "the ICU data assembly was built: the managed patch did not apply"; exit 1; }
ls -la "$out/lib/net8.0"

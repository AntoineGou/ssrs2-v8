#!/bin/bash
# SSRS2's V8 for ClearScript on macOS and Linux: ClearScript's native library (ClearScriptV8.<rid>) built from
# ClearScript 7.5.1.1's sources with patches/clearscript/*.patch, around V8 15.6.75.33 with
# patches/v8/*.patch (README.md says what they change).
#
#   ./build.sh <work folder> [cpu]     cpu: arm64, x64, or arm (Linux only); the host's by default
#
# macOS builds arm64 and x64 on either host. Linux builds x64, arm64 and arm on an x64 host, against V8's Debian
# sysroots (for older glibc versions than the build machine's: the build says which). Downloads several GB the first
# time (depot_tools, V8 and its dependencies); V8 itself takes about 15 minutes on an M-series Mac, one to two hours on a
# 4-core runner, and needs about 15 GB of disk a CPU.
# Writes <work folder>/out/ClearScriptV8.<rid>.<ext>.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mkdir -p "${1:?work folder}" && cd "$1" && pwd)"
cpu="${2:-}"
if [[ -z "$cpu" ]]; then case "$(uname -m)" in arm64|aarch64) cpu=arm64;; x86_64) cpu=x64;; *) echo "cpu?"; exit 1;; esac; fi
case "$(uname -s)" in
    Darwin) os=osx; ext=dylib; [[ "$cpu" == arm64 || "$cpu" == x64 ]] || { echo "macOS: arm64 or x64"; exit 1; } ;;
    Linux) os=linux; ext=so; [[ "$cpu" == arm64 || "$cpu" == x64 || "$cpu" == arm ]] || { echo "Linux: arm64, x64 or arm"; exit 1; } ;;
    *) echo "macOS or Linux (build.ps1 on Windows)"; exit 1 ;;
esac
clearscript_tag=7.5.1.1
v8_revision=15.6.75.33
rid="$os-$cpu"
echo "Building ClearScriptV8.$rid.$ext in $work"

cd "$work"
if [[ ! -d ClearScript ]]; then git clone -q https://github.com/microsoft/ClearScript.git ClearScript; fi
cd ClearScript
root="$PWD"
git checkout -q "$clearscript_tag"
# (the series in order, ClearScript's library ported to V8 15.6 then SSRS2's changes, again whenever it changed; the
# order is that of the names' bytes, 0031 before 0031b, whatever the locale: a glob sorted by a locale that passes
# over the hyphen puts 0031b first)
cs_patches=()
while IFS= read -r p; do cs_patches+=("$p"); done < <(ls "$here"/patches/clearscript/*.patch | LC_ALL=C sort)
cs_patched="$clearscript_tag $(cat "${cs_patches[@]}" | git hash-object --stdin)"
if [[ "$(cat .ssrs2-patched 2>/dev/null || true)" != "$cs_patched" ]]; then
    git checkout -q HEAD -- ClearScriptV8 Unix/ClearScriptV8/Makefile
    for p in "${cs_patches[@]}"; do git apply --3way "$p" || exit 1; done
    echo "$cs_patched" > .ssrs2-patched
fi

# V8: ClearScript's V8Update.sh steps for another revision, with our patch and arguments
mkdir -p V8/build
cd V8/build
if [[ ! -d depot_tools ]]; then git clone -q https://chromium.googlesource.com/chromium/tools/depot_tools.git; fi
export PATH="$PWD/depot_tools:$PATH"
if [[ ! -f .gclient ]]; then gclient config https://chromium.googlesource.com/v8/v8 > config.log 2>&1; fi
patched="$v8_revision $(cat "$here"/patches/v8/*.patch | git hash-object --stdin)"
if [[ "$(cat v8/.ssrs2-patched 2>/dev/null || true)" != "$patched" ]]; then
    echo "Downloading V8 $v8_revision and its dependencies"
    gclient sync -r "$v8_revision" -D --force --reset --no-history > sync.log 2>&1 || { tail -40 sync.log; exit 1; }
    # (the series in order: ClearScript's changes ported to this V8, then SSRS2's)
    (cd v8 && git config user.name ClearScript && git config user.email clearscript@clearfoundry.net \
        && for p in "$here"/patches/v8/*.patch; do echo "Applying $(basename "$p")"; git apply --3way --index "$p" || exit 1; done)
    # (not ClearScript's BuildPatch.txt, which drops V8's sysroot on Linux for the build machine's own headers: we keep
    # Debian 11's, for the libraries' glibc and the C++ library ClearScript's Linux polyfills are written for)
    (cd v8/third_party/icu && git apply --index --ignore-whitespace "$root/V8/ICUPatch.txt")
    echo "$patched" > v8/.ssrs2-patched
fi
if [[ ! -d json ]]; then git clone -q -n https://github.com/nlohmann/json.git && (cd json && git reset -q --hard v3.10.4); fi
cd v8
make_args=()
if [[ "$os" == linux ]]; then
    # (V8's host tools for a 32-bit target are 32-bit: the i386 sysroot too)
    sysroot_arch() { case "$1" in x64) echo amd64;; arm) echo armhf;; *) echo "$1";; esac; }
    for a in x64 $([[ "$cpu" == arm ]] && echo x86) "$cpu"; do
        build/linux/sysroot_scripts/install-sysroot.py --arch="$a" > "sysroot-$a.log" 2>&1 || { cat "sysroot-$a.log"; exit 1; }
    done
    # (the library compiled and linked against the same sysroot as V8: glibc's symbols there are 2.26's at most; it
    # links the C++ runtime statically, as ClearScript's does, and V8's sysroot leaves out the static archives: Debian
    # 11's own packages of them are unpacked into it, checked against their hashes)
    sysroot="$PWD/build/linux/debian_bullseye_$(sysroot_arch "$cpu")-sysroot"
    while read -r sha deb; do
        [[ "$deb" == *"_$(sysroot_arch "$cpu").deb" ]] || continue
        curl -sfL "https://deb.debian.org/debian/pool/main/g/gcc-10/$deb" -o "$work/$deb"
        echo "$sha  $work/$deb" | shasum -a 256 -c - > /dev/null || { echo "$deb: not the expected file"; exit 1; }
        dpkg-deb -x "$work/$deb" "$sysroot"
    done <<'DEBS'
20b1f56863dcc5f187d6cb53b2aa6149bd013be4ec67e10c2c31a22fda619a01 libstdc++-10-dev_10.2.1-6_amd64.deb
3b711374092b84e732c4e73cf33e02cff02e34a5f105eb6467de8d9038e4cd9d libgcc-10-dev_10.2.1-6_amd64.deb
cee7ca63613b50a8a51cc670db385c7e6d1181b72c94148fc8b7992e4570782d libstdc++-10-dev_10.2.1-6_arm64.deb
80fc73d339ada2e194175afe83cd89014565242153b6e3c2128849d8817da367 libgcc-10-dev_10.2.1-6_arm64.deb
c00e1a23851ebcb64b8d31e309e32399b1132cc7ba8eb55e8fed1c842c65beca libstdc++-10-dev_10.2.1-6_armhf.deb
74061bfde97be2c26b160cc81292ea58e3867023aa239abc8f35fed585d9c290 libgcc-10-dev_10.2.1-6_armhf.deb
DEBS
    make_args+=("CXXCROSSFLAGS=--sysroot=$sysroot")
fi
echo "Building V8 ($cpu)"
gn gen "out/$cpu/Release" --args="fatal_linker_warnings=false is_cfi=false is_component_build=false is_debug=false target_cpu=\"$cpu\" use_clang_modules=false use_custom_libcxx=false use_thin_lto=false v8_embedder_string=\"-ClearScript\" v8_enable_fuzztest=false v8_enable_pointer_compression=false v8_enable_31bit_smis_on_64bit_arch=false v8_enable_temporal_support=false v8_monolithic=true v8_use_external_startup_data=false v8_target_cpu=\"$cpu\" v8_disallow_js_in_api_interrupts_is_checked=false" > gn.log 2>&1 || { cat gn.log; exit 1; }
# (-k 0: every file that fails, not the first few, while V8 15.6 is ported to Linux's C++ library)
ninja -k 0 -C "out/$cpu/Release" obj/libv8_monolith.a > build.log 2>&1 || { grep -B2 -A6 "error:" build.log | tail -150; exit 1; }
# (the library builds V8's ICU data into itself from here)
cp "out/$cpu/Release/icudtl.dat" "$root/V8/icudtl.dat"

echo "Linking ClearScriptV8.$rid.$ext"
cd "$root"
out="$root/bin/Release/Unix"
lib="$out/ClearScriptV8.$rid.$ext"
rm -f "$lib"
make -f Unix/ClearScriptV8/Makefile CPU="$cpu" OUTDIR="$out" ${make_args[@]+"${make_args[@]}"} objdir "$lib"
mkdir -p "$work/out"
cp "$lib" "$work/out/"
# the licences of what the library holds: ClearScript, nlohmann/json, V8 and the third-party code V8 links in
licenses="$work/out/licenses"
rm -rf "$licenses"
mkdir -p "$licenses/v8"
cp License.txt "$licenses/ClearScript.txt"
cp V8/build/json/LICENSE.MIT "$licenses/nlohmann-json.txt"
cp V8/build/v8/LICENSE* "$licenses/v8/"
for f in abseil-cpp/LICENSE dragonbox/src/LICENSE-Apache2-LLVM dragonbox/src/LICENSE-Boost fast_float/src/LICENSE-MIT \
    fast_float/src/LICENSE-APACHE fast_float/src/LICENSE-BOOST fp16/LICENSE highway/LICENSE icu/LICENSE \
    inspector_protocol/LICENSE simdutf/LICENSE zlib/LICENSE; do
    mkdir -p "$licenses/v8/third_party/$(dirname "$f")"
    cp "V8/build/v8/third_party/$f" "$licenses/v8/third_party/$f"
done
if [[ "$os" == linux ]]; then
    glibc=$(V8/build/v8/third_party/llvm-build/Release+Asserts/bin/llvm-objdump -T "$lib" | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1)
    echo "Needs $glibc"
    # (ClearScript's entry points and its own code's symbols; V8's and the C++ runtime's stay inside: 86,000 made a
    # fifth of the library)
    exports=$(V8/build/v8/third_party/llvm-build/Release+Asserts/bin/llvm-objdump -T "$lib" | grep -v '\*UND\*' | grep -Ec '^[0-9a-f]{8,} ' || true)
    echo "Exports $exports symbols"
    (( exports < 20000 )) || { echo "V8's symbols are exported: the link's --exclude-libs is missing"; exit 1; }
fi
ls -la "$work/out/ClearScriptV8.$rid.$ext"

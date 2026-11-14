#!/bin/bash
# SSRS2's V8 for ClearScript on macOS and Linux: ClearScript's native library (ClearScriptV8.<rid>) built from
# ClearScript 7.5.1.1's sources with patches/clearscript/*.patch, around V8 15.6.75.33 with
# patches/v8/*.patch and patches/v8-build/*.patch (README.md says what they change). V8 is an official build with
# ThinLTO, its own C++ library (libc++) and the profile in profiles/; the library's own code is compiled by V8's clang
# against that libc++ and optimised with V8 at the link.
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
patched="$v8_revision $(cat "$here"/patches/v8/*.patch "$here"/patches/v8-build/*.patch | git hash-object --stdin)"
if [[ "$(cat v8/.ssrs2-patched 2>/dev/null || true)" != "$patched" ]]; then
    echo "Downloading V8 $v8_revision and its dependencies"
    gclient sync -r "$v8_revision" -D --force --reset --no-history > sync.log 2>&1 || { tail -40 sync.log; exit 1; }
    # (the series in order: ClearScript's changes ported to this V8, then SSRS2's)
    (cd v8 && git config user.name ClearScript && git config user.email clearscript@clearfoundry.net \
        && for p in "$here"/patches/v8/*.patch; do echo "Applying $(basename "$p")"; git apply --3way --index "$p" || exit 1; done)
    # (V8's build files, //build, are a repository of their own inside V8's)
    (cd v8/build && for p in "$here"/patches/v8-build/*.patch; do echo "Applying build/$(basename "$p")"; git apply --3way --index "$p" || exit 1; done)
    # (not ClearScript's BuildPatch.txt, which drops V8's sysroot on Linux for the build machine's own headers: we keep
    # Debian 11's, for the libraries' glibc)
    (cd v8/third_party/icu && git apply --index --ignore-whitespace "$root/V8/ICUPatch.txt")
    echo "$patched" > v8/.ssrs2-patched
fi
if [[ ! -d json ]]; then git clone -q -n https://github.com/nlohmann/json.git && (cd json && git reset -q --hard v3.10.4); fi
cd v8
if [[ "$os" == linux ]]; then
    # (V8's host tools for a 32-bit target are 32-bit: the i386 sysroot too)
    sysroot_arch() { case "$1" in x64) echo amd64;; arm) echo armhf;; *) echo "$1";; esac; }
    for a in x64 $([[ "$cpu" == arm ]] && echo x86) "$cpu"; do
        build/linux/sysroot_scripts/install-sysroot.py --arch="$a" > "sysroot-$a.log" 2>&1 || { cat "sysroot-$a.log"; exit 1; }
    done
    # (the library compiled and linked against the same sysroot as V8: glibc's symbols there are 2.26's at most; its C++
    # library is V8's libc++, linked in, and it links GCC's runtime (the unwinder of C++ exceptions) statically, as
    # ClearScript's does: V8's sysroot leaves out its static archives, and Debian 11's own package of them is unpacked
    # into it, checked against its hash)
    sysroot="$PWD/build/linux/debian_bullseye_$(sysroot_arch "$cpu")-sysroot"
    while read -r sha deb; do
        [[ "$deb" == *"_$(sysroot_arch "$cpu").deb" ]] || continue
        curl -sfL "https://deb.debian.org/debian/pool/main/g/gcc-10/$deb" -o "$work/$deb"
        echo "$sha  $work/$deb" | shasum -a 256 -c - > /dev/null || { echo "$deb: not the expected file"; exit 1; }
        dpkg-deb -x "$work/$deb" "$sysroot"
    done <<'DEBS'
3b711374092b84e732c4e73cf33e02cff02e34a5f105eb6467de8d9038e4cd9d libgcc-10-dev_10.2.1-6_amd64.deb
80fc73d339ada2e194175afe83cd89014565242153b6e3c2128849d8817da367 libgcc-10-dev_10.2.1-6_arm64.deb
74061bfde97be2c26b160cc81292ea58e3867023aa239abc8f35fed585d9c290 libgcc-10-dev_10.2.1-6_armhf.deb
DEBS
fi
# The profile V8 and the library are optimised with (README.md, "Profile"), unpacked where it changed only: the build
# lists it as an input of every file
profile="$work/ClearScriptV8.profdata"
python3 -c 'import lzma, sys; sys.stdout.buffer.write(lzma.open(sys.argv[1]).read())' "$here/profiles/ClearScriptV8.profdata.xz" > "$profile.new"
if cmp -s "$profile.new" "$profile"; then rm "$profile.new"; else mv "$profile.new" "$profile"; fi
# (V8's build also lists the file naming Chrome's own profile for the target, which a V8 checkout does not have: ours
# is given by pgo_data_path, the file only has to be there)
case "$os-$cpu" in osx-arm64) pgo_target=mac-arm;; osx-x64) pgo_target=mac;; *) pgo_target=linux;; esac
mkdir -p chrome/build
[[ -f "chrome/build/$pgo_target.pgo.txt" ]] || echo "profile given by pgo_data_path" > "chrome/build/$pgo_target.pgo.txt"
echo "Building V8 ($cpu)"
# (ClearScript's settings, then the build for speed: an official build, link-time optimised (ThinLTO) with the profile,
# no symbols, V8's own libc++ without its hardening (0007), no stack protector, bounds and return traps or zeroed stack
# variables (v8-build/0001); the unwind tables an official Linux build leaves out are kept, as in the builds before)
v8out="out/$cpu/Release"
gn gen "$v8out" --args="fatal_linker_warnings=false is_cfi=false is_component_build=false is_debug=false target_cpu=\"$cpu\" use_clang_modules=false v8_embedder_string=\"-ClearScript\" v8_enable_fuzztest=false v8_enable_pointer_compression=false v8_enable_31bit_smis_on_64bit_arch=false v8_enable_temporal_support=false v8_monolithic=true v8_use_external_startup_data=false v8_target_cpu=\"$cpu\" v8_disallow_js_in_api_interrupts_is_checked=false is_official_build=true use_custom_libcxx=true use_thin_lto=true thin_lto_enable_optimizations=true chrome_pgo_phase=2 pgo_data_path=\"$profile\" symbol_level=0 v8_symbol_level=0 init_stack_vars=false exclude_unwind_tables=false v8_enable_maglev_graph_printer=false" > gn.log 2>&1 || { cat gn.log; exit 1; }
# (-k 0: every file that fails, not the first few; libc++ and libc++abi, which the monolith does not hold, too)
ninja -k 0 -C "$v8out" obj/libv8_monolith.a buildtools/third_party/libc++ buildtools/third_party/libc++abi > build.log 2>&1 || { grep -B2 -A6 "error:" build.log | tail -150; exit 1; }
# (the library builds V8's ICU data into itself from here)
cp "$v8out/icudtl.dat" "$root/V8/icudtl.dat"
# (libc++ and libc++abi in one archive: on Linux, --exclude-libs keeps the symbols of an archive's members inside)
v8dir="$PWD"
llvm="$v8dir/third_party/llvm-build/Release+Asserts/bin"
cxxlib="$v8dir/$v8out/obj/libc++_ssrs2.a"
rm -f "$cxxlib"
"$llvm/llvm-ar" rcs "$cxxlib" "$v8out"/obj/buildtools/third_party/libc++/libc++/*.o "$v8out"/obj/buildtools/third_party/libc++abi/libc++abi/*.o

echo "Linking ClearScriptV8.$rid.$ext"
cd "$root"
# ClearScript's makefile with V8's compiler, C++ library and link-time optimisation: its code becomes part of the
# optimised program with V8's (V8's calls inlined into it, its own into V8's callbacks), with the same profile. The
# sources are named from the tree's root, as the profile names their functions (ClearScriptV8/<file>.cpp), and the
# link runs there too: the ICU data's .incbin is assembled at the link.
cxxflags="-nostdinc++ -isystem $v8dir/third_party/libc++/src/include -isystem $v8dir/third_party/libc++abi/src/include"
cxxflags+=" -I$v8dir/buildtools/third_party/libc++ -D_LIBCPP_HARDENING_MODE=_LIBCPP_HARDENING_MODE_NONE"
cxxflags+=" -D_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS -D_LIBCXXABI_DISABLE_VISIBILITY_ANNOTATIONS -D_LIBCPP_INSTRUMENTED_WITH_ASAN=0"
cxxflags+=" -flto=thin -fsplit-lto-unit -fno-stack-protector -fprofile-use=$profile"
cxxflags+=" -Wno-profile-instr-unprofiled -Wno-profile-instr-out-of-date -Wno-backend-plugin"
# (and V8's own target and CPU flags, as its compiles have them, after the makefile's --target: code compiled for
# another CPU or triple is not inlined into V8's at the link, nor V8's into it; the makefile's are armv6 for linux-arm
# where V8 is armv7 Thumb with NEON, x86-64 without SSE3 on Linux)
cxxflags+=" $(ninja -C "$v8dir/$v8out" -t commands obj/v8_libbase/bits.o | tail -1 | tr ' ' '\n' \
    | grep -E '^(--target=|-march=|-mcpu=|-mtune=|-mfloat-abi=|-mfpu=|-mthumb$|-m(32|64)$|-msse|-mavx|-mmacos-version-min=)' | tr '\n' ' ')"
ltoflags="-nostdlib++ -fuse-ld=lld -Wl,--thinlto-jobs=all -Wl,-mllvm,-import-instr-limit=30 $cxxlib"
if [[ "$os" == linux ]]; then
    cxxflags+=" --sysroot=$sysroot"
    # (the makefile's own flags, -static-libstdc++ aside)
    linkflags="-s -static-libgcc -ldl -lrt -lm --no-undefined -Wl,--exclude-libs,ALL $ltoflags"
else
    # (V8's clang is not Xcode's: it is given the SDK)
    cxxflags+=" -isysroot $(xcrun --show-sdk-path)"
    linkflags="-framework CoreFoundation $ltoflags"
fi
out="$root/bin/Release/Unix"
lib="$out/ClearScriptV8.$rid.$ext"
# (compiled again each time: the makefile does not know when the flags or the profile changed)
make -f Unix/ClearScriptV8/Makefile CPU="$cpu" OUTDIR="$out" clean
make -f Unix/ClearScriptV8/Makefile CPU="$cpu" OUTDIR="$out" CXX="$llvm/clang++" SRCDIR=ClearScriptV8 \
    CXXCROSSFLAGS="$cxxflags" CXXLINKFLAGS="$linkflags" objdir "$lib"
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
    inspector_protocol/LICENSE simdutf/LICENSE zlib/LICENSE libc++/src/LICENSE.TXT libc++abi/src/LICENSE.TXT; do
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
    # (V8's libc++ is inside: the library needs no C++ library of the system)
    needed=$(V8/build/v8/third_party/llvm-build/Release+Asserts/bin/llvm-readelf -d "$lib" | grep NEEDED | grep -o '\[.*\]' | tr '\n' ' ')
    echo "Needs $needed"
    [[ "$needed" != *"libstdc++"* && "$needed" != *"libc++"* ]] || { echo "A C++ runtime of the system is needed"; exit 1; }
else
    needed=$(otool -L "$lib" | tail -n +2 | awk '{print $1}' | tr '\n' ' ')
    echo "Needs $needed"
    [[ "$needed" != *"libc++"* ]] || { echo "The system's libc++ is needed"; exit 1; }
fi
ls -la "$work/out/ClearScriptV8.$rid.$ext"

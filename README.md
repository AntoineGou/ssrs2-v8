# SSRS2's V8 for ClearScript

ClearScript's native V8 library (`ClearScriptV8.<rid>`) as [SSRS2](https://ssrs2.net) builds it: around V8 15.6.75.33,
with changes that make the scripts of the pages SSRS2 prints faster (charts, dates and numbers above all). It takes the
place of the library in the `Microsoft.ClearScript.V8.Native.<rid>` packages: same file name, same API, used with the
`Microsoft.ClearScript.V8` 7.5.1.1 package.

On SSRS2's 300-page test report (a chart a page, drawn by Chart.js), its scripts took 815 ms against 1,419 ms with
ClearScript's own library (V8 14.7), and the whole report 1,458 ms against 2,063 (warm, on an M-series Mac), with the
same output byte for byte.

## What it changes

V8, in order (`patches/v8`): `0001-clearscript-7.5.1.1-port.patch`, ClearScript 7.5.1.1's own V8 changes ported to
15.6 (its host objects, interrupts, ICU data from memory...), then `0002-ssrs2.patch`, SSRS2's, below, then
`0003-windows-layouts.patch`, V8 15.6's object layouts made to agree with its Torque layouts under Microsoft's C++
library and layout rules (a configuration Chrome does not build: pointers uncompressed, the system's C++ library):

- Local time is UTC once the embedder gives V8 its ICU data (a server prints in UTC), and a date's fields are worked out
  without a time zone.
- `getUTCDate` and its kin are inlined by Maglev and TurboFan, as the local getters are.
- `new Date(number)` and `setUTCDate(number)` are CSA builtins.
- A DateToPrimitive protector: dates are compared or subtracted without calls while `Date.prototype`'s
  `@@toPrimitive` and `valueOf` are as built.
- `Number.prototype.toFixed` is a CSA builtin for up to 20 digits below 2^52, and TurboFan makes `+x.toFixed(f)` a
  number without the string.
- Plain decimals (`"-123.45"`) are made numbers without the runtime.
- `JSON.parse` reads a decimal of at most 15 significant digits and no exponent from the digits it scanned (Clinger's
  fast path: the digits an exact integer, one correctly rounded division by a power of ten up to 10^22), the number
  `StringToDouble` gives, without reading the characters again (a page's data as JSON: SSRS2's 300-page report parses
  1.6 million prices, its scripts about 6 ms faster; a million random decimals read as `Number` reads them).
- TurboFan folds `Date.UTC` of constants, and makes `new Date(c)` of a constant itself, its fields worked out when
  compiling (local time UTC): escape analysis drops a date nothing keeps, as a loop's bound
  (`d <= new Date(Date.UTC(2026, 9, 15))`).

The library, in order (`patches/clearscript`): `0001-v8-15.6-port.patch`, ClearScript 7.5.1.1's native side ported to
V8 15.6's API, with V8 15.6's ICU data built in (ClearScript's ICU data package holds V8 14.7's); then
`0002-ssrs2.patch`: V8 starts with no full collection below 768 MB of old space and inline caches polymorphic up to 16
maps, and `SSRS2_V8_FLAGS` adds flags for experiments; then `0003-direct-calls.patch`, the library's two exports through
which SSRS2's HTML engine lets scripts call .NET straight from V8, without ClearScript's host objects:
`Ssrs2_InstallDirectCalls` makes functions of a script object that call one host dispatcher with their arguments read in
place (numbers, booleans, strings as UTF-16, typed arrays as their memory) and take back a number, a string, null or an
error; `Ssrs2_InstallFastCalls` makes functions of int32 arguments answering an int32, each a host function pointer, as
V8 fast API calls (the optimizing compiler calls them directly), their templates kept once per isolate. Both are called
from inside a host call. `test/Smoke` installs one of each and calls them on every platform.

V8 is built with ClearScript's settings and `v8_disallow_js_in_api_interrupts_is_checked=false` (ClearScript runs V8's
tasks from its interrupt callback). Apart from local time, the changes keep the language's answers: `test/Smoke`
checks a set of them against Node's on every platform, and SSRS2's output matched the one with ClearScript's own library
byte for byte.

## Updating V8

The patches apply to a pristine V8 tag, ClearScript's first and SSRS2's on top, each with a three-way merge: a V8 patch
release (15.6.75.x) usually takes them as they are. For another V8 version, apply the series to the old tag as commits,
rebase them onto the new tag (conflicts come one patch at a time), and export each commit back to its file. When
ClearScript publishes its own changes for that V8, they replace both `0001` patches and only the `0002` ones are
ported. `build.sh` and
`build.ps1` take the revision from `v8_revision`.

## Platforms

osx-arm64, osx-x64, linux-x64, linux-arm64, linux-arm, win-x64, win-x86, win-arm64. The Linux libraries are built
against V8's Debian 11 sysroots, for older glibc versions than the build machine's (the build says which). GitHub
Actions builds each one (`.github/workflows/build.yml`) and loads it through ClearScript on a machine of its kind
(linux-arm under QEMU, osx-x64 under Rosetta).

## Releases

Each release has a zip per RID holding the library and its licences, `SHA256SUMS`, and GitHub's attestation of where it
was built: `gh attestation verify ClearScriptV8.linux-x64.zip --repo AntoineGou/ssrs2-v8`.

## Building

```
./build.sh <work folder> [arm64|x64|arm]      macOS (arm64, x64) or Linux on x64 (x64, arm64, arm)
./build.ps1 <work folder> [x64|x86|arm64]     Windows with Visual Studio 2022, its Clang tools and the Windows SDK
```

The first build downloads several GB (depot_tools, V8 and its dependencies). V8 takes about 15 minutes on an M-series
Mac and one to two hours on a 4-core machine. The library lands in `<work folder>/out`, with its licences.

## Licences

This repository's scripts, workflow and test are under the MIT licence (`LICENSE`). The V8 patch is under V8's licence
(BSD-3-Clause), the ClearScript patch under ClearScript's (MIT). A built library holds V8 (BSD-3-Clause), the
third-party code V8 links in (ICU, Abseil, Highway, simdutf, fast_float, dragonbox, FP16, zlib, inspector_protocol),
ClearScript (MIT) and nlohmann/json (MIT): their licences are in each release's `licenses` folder.

Not affiliated with the ClearScript or V8 projects. This repository is SSRS2's build: it takes no issues or pull
requests.

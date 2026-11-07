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
- The compilation cache checks a script's origin before its source: a long source's hash is little more than its
  length, so each entry left by a document gone (a module compiled again in each new context, its host-defined options
  another document's) cost a whole comparison of the source before its origin rejected it (a Three.js module of
  690 KB rendered again and again: 4% of the page's time).
- TurboFan folds `Date.UTC` of constants, and makes `new Date(c)` of a constant itself, its fields worked out when
  compiling (local time UTC): escape analysis drops a date nothing keeps, as a loop's bound
  (`d <= new Date(Date.UTC(2026, 9, 15))`).
- No low-priority foreground sweeping of the C++ heap: its task posts itself again at once while sweeping is in
  progress, which assumes a platform running it in idle time; ClearScript runs a foreground task at once, through the
  .NET thread pool and an interrupt of the isolate, and with documents on several isolates at once the posts became a
  storm of tasks (SSRS2's 15-chart report rendered 8 at a time fell from 30 documents a second to 1, now and then).
  Incremental sweeping at regular priority, with its delays, and concurrent sweeping still run.

The library, in order (`patches/clearscript`): `0001-v8-15.6-port.patch`, ClearScript 7.5.1.1's native side ported to
V8 15.6's API, with V8 15.6's ICU data built in (ClearScript's ICU data package holds V8 14.7's); then
`0002-ssrs2.patch`: V8 starts with no full collection below 768 MB of old space and inline caches polymorphic up to 16
maps, `SSRS2_V8_FLAGS` adds flags for experiments, and an interrupt of the isolate runs only V8's nestable tasks: a
non-nestable one may run JavaScript (a `FinalizationRegistry`'s cleanup), which V8 forbids in an interrupt (and which
would run in the middle of a script), and waits for the isolate scope's exit; and on Linux the library exports only its own
symbols: V8's and the C++ runtime's, linked from their static archives, stay inside it (86,000 dynamic symbols were a
fifth of the library, and V8's calls between its own functions went through the PLT; `build.sh` fails a library
exporting 20,000 or more); then `0003-direct-calls.patch`, the library's two exports through
which SSRS2's HTML engine lets scripts call .NET straight from V8, without ClearScript's host objects:
`Ssrs2_InstallDirectCalls` makes functions of a script object that call one host dispatcher with their arguments read in
place (numbers, booleans, strings as UTF-16, typed arrays as their memory) and take back a number, a string, null or an
error; `Ssrs2_InstallFastCalls` makes functions of int32 arguments answering an int32, each a host function pointer, as
V8 fast API calls (the optimizing compiler calls them directly), their templates kept once per isolate. Both are called
from inside a host call. `test/Smoke` installs one of each and calls them on every platform. Then
`0004-import-attributes.patch`, modules: an import's `type` attribute (`import t from "./t.txt" with { type: "text" }`,
`import(url, { with: { type: "json" } })`) reaches the host's module loader after the specifier, a NUL and `type=`
(ClearScript gives its loader the specifier alone: a text, JSON or CSS module could not be told from a script); a
dynamic import of a module whose evaluation throws rejects with its error, and one of a module awaiting at its top level
settles when its evaluation does (the import resolved with the namespace of a module that had thrown); and a synthetic
module (ClearScript's JSON modules) finds its context when evaluated (the context was read back with another tag than
it was stored with, V8 15.6 answered null, and V8 stopped the process: "Check failed: has_exception()").
`test/Smoke` imports a text module and a module that throws on every platform. Then `0005-microtask-checkpoint.patch`:
`Ssrs2_PerformMicrotaskCheckpoint`, an export running the current isolate's microtask queue at once, from inside a host
call too (HTML's "perform a microtask checkpoint": V8 runs the queue by itself only when the outermost script returns,
and SSRS2's frames run their parsers' scripts from inside the page's); 0 outside an isolate. Then
`0006-dynamic-import-deferred.patch`: `import()` has its module now (the specifier resolved, the source fetched and
compiled, so a blob URL revoked after the call still loads) and links and evaluates it in a microtask, after the script
that asked for it, as HTML's import() does (ClearScript evaluated it inside the call: the importing script's code after
`import()` ran after the module's). `test/Smoke` checks the order. Then `0007-creation-context.patch`:
`Ssrs2_InstallCreationContext`, called from inside a host call, makes a function answering the global object of the
context an object was created in, a revoked Proxy's too (WebIDL reports a callback's exceptions in its realm, and a
script cannot ask a revoked Proxy anything: its every operation throws). `test/Smoke` asks it of objects and revoked
Proxies of another context of the isolate. Then `0008-dynamic-import-task.patch`: `Ssrs2_InstallImportScheduler`, called from inside a
host call, makes a function the script gives its scheduler to: each `import()` of a module not evaluated yet is then
handed to it as a function running the evaluation, with the module's resource name, and the host calls it a task later,
as a browser's fetch completes (the microtasks of the importing script's task run first: 0006 evaluated it in a
microtask); a module evaluated already, and every one without a scheduler, still in a microtask. And a dynamically
imported module whose evaluation throws rejects the import alone: its evaluation's own promise is handled, where it was
left a rejection without a handler (a page's `unhandledrejection` though it caught the import). `test/Smoke` installs a
scheduler and checks both. Then `0009-dynamic-import-marker.patch`: a dynamic import's specifier reaches the host's module loader
ending with a NUL and `import()` (after 0004's type attribute), a static import's as before: ClearScript gives its
loader the specifier alone, and HTML treats the two apart (every `import()` of a module with a parse error rejects with
the one error its module record keeps, where a static graph reports the first parse error of its own). `test/Smoke`
checks that `import()`'s specifiers are marked and a static import's is not. Then `0010-import-attribute-keys.patch`: an
import's first attribute whose key is not `type` reaches the loader too, before the type: a NUL, `attr=` and the key
(HTML supports the type attribute alone: a static import of another is its importing module's SyntaxError, an `import()`
of one rejects with a TypeError); the loader reads it and takes it off. `test/Smoke` imports a module with one
statically and another with `import()`. Then `0011-heap-limit-on-expansion.patch`: the runtime's soft heap limit
(`MaxHeapSize`) is checked in V8's near-heap-limit expansion too, on the script's own thread: past it, the isolate is out of
memory and the script is terminated (the heap expanded that once, for V8 to unwind it), as ClearScript's heap watch does
under its termination policy. That watch runs from a timer through the .NET thread pool, which a busy host starves: a
script grew its heap to 12 GB past a 96 MB limit in 8 s while the timer waited. `test/Smoke` runs a script past a small
limit while the pool's threads are held, and checks it is stopped. Then `0012-kept-referrers.patch`: a dynamic import's referrer is found by
the live script that made its code once ClearScript's script cache (1,024 scripts) has dropped that script's entry: each
script holder keeps its document info, counted, while it lives, and the import callback looks there after the caches. In
an isolate kept for many documents, the host's own script compiled once at its start (SSRS2's DOM, which compiles event
handlers and string timers) fell out of the cache, and an `import()` in their code, outside a script's execution, failed
with "Invalid module load request": a test passed or failed by how many documents its isolate had run before.
`test/Smoke` runs 1,100 scripts after the host's, then calls its function importing a module, and checks the loader is
asked with the host's document. Then `0013-code-like.patch`: `Ssrs2_InstallCodeLike`, called from inside a host call,
makes a constructor whose instances V8 takes as code (V8's `ObjectTemplate::SetCodeLike`, TC39's dynamic code brand
checks): `eval` of one, and the `Function` constructor, compile its text (its `toString`), where V8 otherwise returns an
object unevaluated. An instance keeps the brand when the script gives it another prototype (V8 reads it from its map's
constructor), so the host makes Trusted Types' `TrustedScript` of it, as Chrome's is code-like: a page's
`eval(policy.createScript(code))` ran nothing. `test/Smoke` evaluates instances with another prototype through both, and
a plain object with the same `toString` through `eval`, which returns it.

V8 is built with ClearScript's settings and `v8_disallow_js_in_api_interrupts_is_checked=false` (ClearScript runs V8's
nestable tasks and its own calls from its interrupt callback). Apart from local time, the changes keep the language's answers: `test/Smoke`
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

A tag ending in `-mac` (`v15.6.75.33-ssrs2.7-mac`) builds, tests and publishes `osx-arm64` alone, as a prerelease: a
development round in about an hour and a half (a full release takes about two and a half, its slowest build osx-x64's).
Packages take full releases only: tag the same commit without `-mac` when the round is done.

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

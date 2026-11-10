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
library and layout rules (a configuration Chrome does not build: pointers uncompressed, the system's C++ library),
then `0004-eval-cache-by-context.patch`: V8's eval cache is keyed by the native context too (its id, which V8 gives
each context for the embedder's metrics, `Isolate::GetOrRegisterRecorderContextId`, holding the context weakly).
Indirect eval and the Function constructor are keyed by the context's closure, whose SharedFunctionInfo every context
shares, so a context got the code an earlier one had compiled from the same text, its script's eval origin (which
dynamic import's referrer and the stack read) the earlier context's: an `import()` in a later document's `new Function`
resolved against another document's base. A page calling `new Function` in a loop keeps hitting: the key gains one
number, compared first. Then `0005-immutable-proto-undetectable-typeof.patch`: `v8::ssrs2::SetImmutableProto(object)`
(`include/v8-ssrs2.h`, a header of its own so that only `api.cc` is compiled again) makes an existing object's prototype
immutable, its map copied with V8's immutable prototype bit and migrated as a prototype's map change is
(`ObjectTemplate::SetImmutableProto` gives only a template's new objects the bit: WebIDL's global prototype chain is
linked by a host's scripts after the context exists); and `typeof` answers "undefined" for an undetectable object that has
a named interceptor (ClearScript's `typeof` answered "object" for any object with one before it looked at undetectable,
as `Object::TypeOf` does not). Then `0006-prototype-setup.patch`: `v8::ssrs2::SetPrototypeSetupMode(on)` (for the calling
thread) leaves a prototype whose properties change in dictionary mode rather than making it fast again after each
change, as V8 does once inline caches used it (`JSObject::ReoptimizeIfPrototype`, linear in its properties each time: a
host redefining every member of its used prototypes, WebIDL's shapes, took n * n), and `v8::ssrs2::OptimizePrototype(object)`
makes an object changed so fast again, once (a prototype V8 keeps fast as V8 does, any other dictionary-mode object too:
a dictionary holds the same members in about twice the memory). Only how fast properties are found changes. SSRS2's own changes (`0002`):

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
a plain object with the same `toString` through `eval`, which returns it. Then `0014-undetectable.patch`: `Ssrs2_InstallUndetectable`, called from inside a host call, makes a
function `make(target, prototype)` whose objects V8 takes as undetectable (V8's `ObjectTemplate::MarkAsUndetectable`,
HTML's `[[IsHTMLDDA]]`): `typeof` answers `"undefined"`, they are falsy and `== null`, yet callable as `target` (a
call-as-function handler) with `target`'s indices (an indexed interceptor, enumerated by `target.length`) and every other
name their prototype's: the host gives its callable Proxy as both, and makes HTML's `document.all` of it, as Chrome's is
(`typeof document.all` was `"function"`). No named interceptor: ClearScript's own `typeof` answers `"object"` for an object
with one before it looks at undetectable. `test/Smoke` checks `typeof`, the comparisons, an index, a call, the keys and a
name read through the prototype. Then `0015-no-referrer.patch`: `Ssrs2_SetNoReferrerName` names a script the host keeps
(a live holder: 0012's kept document infos) as its stand-in for "no referrer". A dynamic import whose host-defined options
name a script the context no longer knows is loaded with that script's document info, rather than failing with "Invalid
module load request". V8's eval cache is keyed by the text and the outer function, which for an indirect `eval` and the
`Function` constructor is the context's empty function, shared by every context: in an isolate kept for many documents,
a later document's identical `eval` text got the code compiled for an earlier one, whose referrer named that document's
script, gone by then; the host's loader resolves the stand-in as HTML's import with no active script (the settings
object's base). `test/Smoke` makes a second engine of one runtime run the first's `eval` text after its script's holder is
disposed and 1,100 scripts have filled the cache, and checks the loader is asked with the stand-in. Then
`0016-incumbent.patch`: `Ssrs2_InstallIncumbent`, called from inside a host call, makes a function `withIncumbent(f)`
whose native wrapper `W`, called with a `this` and arguments, calls `f` with that `this`, the global of the incumbent
context (V8's `Isolate::GetIncumbentContext`: the context of the script function calling `W`, `W` being native) and
`W`'s arguments, and returns what `f` returns. The host keeps one context a frame and sees other frames' windows through
a view each context makes (HTML's WindowProxy is one object every realm holds): its window accessors (a frame's
`contentWindow`, an event's `target`) are made of `W`, `f` giving the window as the calling context sees it. Before, a
window read through another context's accessor was that context's view, or its own global, which V8's access checks
refuse another context. `test/Smoke` calls a wrapper directly and as an accessor and checks the global, the `this` and
the arguments `f` was given. Then `0017-context-links.patch`: a context's host callbacks (its host objects' interceptors
and functions, `performance.now`...) are given a link to the context as their data, not the context itself; the link is
emptied when the context is torn down and freed when V8 collects the data holding it. A disposed engine's script can
still run: an asynchronous WebAssembly compile it began finishes in a task V8 posts for the isolate, which resolves the
compile's promise in that context and runs its reactions there. A host function they called read the freed context
(SSRS2's WPT run of the `wasm` folder ended on a lock of a freed mutex, or SIGSEGV, at 6 renders at once); it now finds
no context and does nothing, as with no data. `Ssrs2_ContextLinks` (1) tells a host the library has it. `test/Smoke`
disposes an engine while such a compile runs and checks the reactions ran and the host function was not called. Then
`0018-wasm-memory-limit.patch`: the runtime's soft heap limit (`MaxHeapSize`) counts, besides the heap, the memory V8
accounts for outside it other than ArrayBuffers' (which `MaxArrayBufferAllocation` bounds): WebAssembly memories and
compiled modules, external strings. It is checked where it was (the heap watch timer, 0011's heap expansion) and after
each collection, which memory growing outside the heap brings about. A WebAssembly memory is 4 GB at most
(`--wasm_max_mem_pages=65536`, Chrome's and V8's limit for a 32-bit memory; a 64-bit one could be 16 GB). Before, a
page's WebAssembly memories were bounded by nothing: six of 1 GB filled grew SSRS2's process by 6 GB, a 64-bit one of
8 GB by 8; SSRS2 now stops such a page's scripts at its limit, with its note, after 1.1 GB and 0.1 GB.
`Ssrs2_WasmMemoryLimit` (1) tells a host the library has it. `test/Smoke` fills four memories of 128 MB under a 256 MB
limit and checks the script is stopped, and that a 64-bit memory of 8 GB is refused. Then
`0019-code-generation-check.patch`: `Ssrs2_InstallCodeGenCheck` makes a function, `setCodeGenCheck(check, message)`, that
gives a context a check of code made from strings. V8's modify-code-generation callback (the overload with
`is_code_like`), installed on the isolate the first time, calls the check of the compiling context with the value and
whether it is code-like: eval's argument (V8 gives it as not code-like; the callback reads the brand), a Function
constructor's source text, code-like when all its arguments were, and each of its arguments that is an object. The
check answers undefined (as given), a text (compiled in its place: a code-like object's own text) or a refusal, V8's
EvalError with the message given. Giving a check turns code generation from strings off on the context, so that V8
asks it for every string; a context without a check answers as V8 does without the callback. A host enforcing HTML's
Content-Security-Policy and Trusted Types on eval keeps V8's own eval and Function constructors: a direct eval of a
TrustedScript keeps its scope, where a host replacing eval made every eval indirect. Then `0020-entry-context.patch`:
`Ssrs2_InstallEntryContext` makes `entryGlobal()`, which answers while V8 runs its microtasks the global of the context
it runs the job in, or of one entered since (`Isolate::GetEnteredOrMicrotaskContext`), and null outside them: HTML's
entry settings object of a promise job is its callback's realm, which a host cannot know otherwise, V8 running the job
after the host's call returned. Then `0021-immutable-global-prototype.patch`: while a thread has
`Ssrs2_SetImmutableGlobalPrototype(1)` set, the contexts it makes have a global whose prototype cannot be changed
(`ObjectTemplate::SetImmutableProto` on the global's template), WebIDL's `[Global]` objects being immutable prototype
exotic objects; the global's prototype is then the object V8 makes with it, which a host takes as its global's
interface prototype. Then `0022-immutable-prototype.patch`: `Ssrs2_InstallImmutablePrototype` makes
`setImmutablePrototype(object)`, V8's `0005` from a script: WebIDL's immutable prototype exotic objects, a `[Global]`'s
whole prototype chain (Window.prototype, EventTarget.prototype), whose methods can still be added. Then
`0023-interceptors.patch`: `Ssrs2_InstallInterceptors` makes `interceptorFactory(handler, flags)`, which returns
`make(prototype)`: objects of one template whose indexed and string-named properties the handler's traps answer through
V8's interceptors (get, set, has, deleteProperty, defineProperty, getOwnPropertyDescriptor, ownKeys; a trap missing or
answering undefined leaves the request to V8's ordinary lookup), undetectable and callable with flags 4. WebIDL's legacy
platform objects answer `[[DefineOwnProperty]]` of a non-configurable value as their setter does, which a Proxy's
invariants forbid (a select's indices, a DOMStringMap's names), and HTML's document.all has HTMLAllCollection.prototype
as its prototype with its names and indices resolved. Then `0024-prototype-setup-function-script-id.patch`:
`Ssrs2_InstallPrototypeSetup` makes `prototypeSetup(on, objects)`, V8's `0006` from a script (`prototypeSetup(true)`, the
members redefined, `prototypeSetup(false, [the objects changed])`: a document's and each frame's WebIDL shapes, 25 ms and
about 1 MB a window before), and `Ssrs2_InstallFunctionScriptId` makes `functionScriptId(f)`, the id of the script f was
compiled from (`Function::ScriptId`, -1 for none): a host prints its own functions as built-ins by their script, without
a set of them per context. Then `0025-non-masking-interceptors.patch`: `interceptorFactory`'s flags 8 makes the named
interceptor non-masking (`v8::PropertyHandlerFlags::kNonMasking`: asked only for a name neither the object nor its
prototype chain has, a document's named properties; its own and inherited members are then read as an ordinary
object's), and the factory says it takes the flag (`interceptorFactory.nonMasking`). `test/Smoke` checks each: a direct eval keeping its scope and refused strings under a check,
`entryGlobal()` in a job calling another context's function, a global made immutable beside an ordinary one, an existing prototype locked, and an interceptor object defining a
non-configurable value and an undetectable one and a non-masking one asked for the missing names alone, a used
prototype's accessors redefined in setup mode and found again, and two functions' script ids.

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

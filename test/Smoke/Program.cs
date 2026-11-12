// dotnet Smoke.dll <folder>: runs smoke.js with the ClearScriptV8 library in <folder> and checks its answers against
// expected.txt, stock V8's (Node's) answers; and that the date's local time is UTC whatever the machine's time zone, as
// our V8 has it (the workflow sets another zone first).
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

// (ClearScript loads its library from the app's folder on .NET)
foreach (var library in Directory.GetFiles(args[0], "ClearScriptV8.*"))
    File.Copy(library, Path.Combine(AppContext.BaseDirectory, Path.GetFileName(library)), overwrite: true);
// (patches/clearscript/0027, first: a process makes one snapshot blob and every isolate made after it starts from it)
var snapshot = SnapshotContexts.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"snapshot contexts: {snapshot}");
var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "smoke.js"));
var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expected.txt")).Trim();
using var engine = new V8ScriptEngine();
var answer = (string)engine.Evaluate(script);
var offset = Convert.ToInt32(engine.Evaluate("new Date(0).getTimezoneOffset()"));
Console.WriteLine($"{answer}\ntime zone offset {offset}");
var calls = HostCalls.Check(engine, Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"host calls: {calls}");
using var moduleEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
var modules = Modules.Check(moduleEngine);
Console.WriteLine($"modules: {modules}");
var microtasks = Microtasks.Check(engine, Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"microtasks: {microtasks}");
var realms = CreationContexts.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"creation contexts: {realms}");
var scheduled = ImportSchedulers.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"import scheduler: {scheduled}");
var heap = HeapLimits.Check();
Console.WriteLine($"heap limit: {heap}");
var referrer = KeptReferrers.Check();
Console.WriteLine($"kept referrer: {referrer}");
var codeLike = CodeLikes.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"code-like: {codeLike}");
var undetectable = Undetectables.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"undetectable: {undetectable}");
var noReferrer = NoReferrers.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"no referrer: {noReferrer}");
var incumbent = Incumbents.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"incumbent: {incumbent}");
var evalCallers = EvalCallers.Check();
Console.WriteLine($"eval callers: {evalCallers}");
var links = ContextLinks.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"context links: {links}");
var wasmMemory = WasmMemoryLimits.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"wasm memory limit: {wasmMemory}");
var codeGen = CodeGenChecks.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"code generation check: {codeGen}");
var entry = EntryContexts.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"entry context: {entry}");
var immutable = ImmutableGlobals.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"immutable global prototype: {immutable}");
var immutablePrototype = ImmutablePrototypes.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"immutable prototype: {immutablePrototype}");
var interceptors = InterceptorObjects.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"interceptors: {interceptors}");
var prototypeSetup = PrototypeSetups.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"prototype setup: {prototypeSetup}");
var scriptIds = FunctionScriptIds.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"function script id: {scriptIds}");
var digests = TextDigests.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"text digest: {digests}");
var arena = StringArenas.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"string arena: {arena}");
var handleCalls = HandlesAndCalls.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"handles and calls: {handleCalls}");
var tables = InterceptorTables.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"interceptor tables: {tables}");
var guards = InterceptorTableGuards.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"interceptor table guards: {guards}");
var hostCheck = CodeGenHostChecks.Check(Directory.GetFiles(AppContext.BaseDirectory, "ClearScriptV8.*").First());
Console.WriteLine($"code generation host check: {hostCheck}");
if (answer == expected && offset == 0 && calls == HostCalls.Expected && modules == Modules.Expected && microtasks == Microtasks.Expected && realms == CreationContexts.Expected
    && scheduled == ImportSchedulers.Expected && heap == HeapLimits.Expected && referrer == KeptReferrers.Expected && codeLike == CodeLikes.Expected && undetectable == Undetectables.Expected && noReferrer == NoReferrers.Expected && incumbent == Incumbents.Expected
    && evalCallers == EvalCallers.Expected && links == ContextLinks.Expected && wasmMemory == WasmMemoryLimits.Expected
    && codeGen == CodeGenChecks.Expected && entry == EntryContexts.Expected && immutable == ImmutableGlobals.Expected
    && immutablePrototype == ImmutablePrototypes.Expected && interceptors == InterceptorObjects.Expected
    && prototypeSetup == PrototypeSetups.Expected && scriptIds == FunctionScriptIds.Expected && digests == TextDigests.Expected && snapshot == SnapshotContexts.Expected
    && arena == StringArenas.Expected && handleCalls == HandlesAndCalls.Expected && tables == InterceptorTables.Expected
    && guards == InterceptorTableGuards.Expected && hostCheck == CodeGenHostChecks.Expected) return 0;
Console.Error.WriteLine($"expected:\n{expected}\ntime zone offset 0\nhost calls: {HostCalls.Expected}\nmodules: {Modules.Expected}\nmicrotasks: {Microtasks.Expected}\ncreation contexts: {CreationContexts.Expected}\nimport scheduler: {ImportSchedulers.Expected}\nheap limit: {HeapLimits.Expected}\nkept referrer: {KeptReferrers.Expected}\ncode-like: {CodeLikes.Expected}\nundetectable: {Undetectables.Expected}\nno referrer: {NoReferrers.Expected}\nincumbent: {Incumbents.Expected}\neval callers: {EvalCallers.Expected}\ncontext links: {ContextLinks.Expected}\nwasm memory limit: {WasmMemoryLimits.Expected}\ncode generation check: {CodeGenChecks.Expected}\nentry context: {EntryContexts.Expected}\nimmutable global prototype: {ImmutableGlobals.Expected}\nimmutable prototype: {ImmutablePrototypes.Expected}\ninterceptors: {InterceptorObjects.Expected}\nprototype setup: {PrototypeSetups.Expected}\nfunction script id: {FunctionScriptIds.Expected}\nstring arena: {StringArenas.Expected}\nhandles and calls: {HandlesAndCalls.Expected}\ninterceptor tables: {InterceptorTables.Expected}\ninterceptor table guards: {InterceptorTableGuards.Expected}\ncode generation host check: {CodeGenHostChecks.Expected}");
return 1;

// patches/clearscript/0012: an import() in code a kept script made (a Function's, here called from the host, outside any
// script's execution) finds its referrer after the script cache has dropped that script's entry (1,100 scripts run since):
// the loader is asked, with the script's document; without the patch the import failed with "Invalid module load request".
static class KeptReferrers
{
    public const string Expected = "host.js ./m.js";

    internal sealed class Loader : Microsoft.ClearScript.DocumentLoader
    {
        public string Seen = "";
        public override Microsoft.ClearScript.Document LoadDocument(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback)
        {
            if (specifier.EndsWith("\0import()", StringComparison.Ordinal)) specifier = specifier[..^"\0import()".Length];
            Seen = $"{sourceInfo?.Name} {specifier}";
            return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///m.js")) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard }, "export default 1;");
        }

        public override Task<Microsoft.ClearScript.Document> LoadDocumentAsync(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback) =>
            Task.FromResult(LoadDocument(settings, sourceInfo, specifier, category, contextCallback));
    }

    public static string Check()
    {
        using var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
        var loader = new Loader();
        engine.DocumentSettings.Loader = loader;
        using var host = engine.Compile(new Microsoft.ClearScript.DocumentInfo("host.js"), "globalThis.go = () => Function(\"return import('./m.js')\")();");
        engine.Execute(host);
        for (int i = 0; i < 1100; i++) engine.Execute(new Microsoft.ClearScript.DocumentInfo("s" + i + ".js"), i + ";");
        try { engine.Invoke("go"); } catch (Exception e) { return "error " + e.Message; }
        return loader.Seen.Length > 0 ? loader.Seen : "not asked";
    }
}

// patches/clearscript/0015: the host's stand-in for a referrer the library no longer knows (Ssrs2_SetNoReferrerName). It
// was reached by V8's eval cache, keyed by the text and the context's empty function, handing the second engine the eval
// code the first compiled, whose referrer (a.js, gone) the library no longer knew. Since patches/v8/0004 the cache is keyed
// by the native context too: the second engine compiles its own code and its import names b.js, the stand-in left as a
// backstop for code whose referrer is gone.
static unsafe class NoReferrers
{
    public const string Expected = "a.js ./m.js|b.js ./m.js";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_SetNoReferrerName", out var export)) return "export missing";
        var set = (delegate* unmanaged[Stdcall]<byte*, int>)export;
        using var runtime = new V8Runtime();
        using var stand = runtime.Compile(new Microsoft.ClearScript.DocumentInfo("noref"), "0");
        fixed (byte* name = "noref\0"u8) set(name);
        const string code = "globalThis.go = () => (0, eval)(\"import('./m.js')\");";
        string first;
        using (var a = runtime.CreateScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports))
        {
            var loader = new KeptReferrers.Loader();
            a.DocumentSettings.Loader = loader;
            using (var script = a.Compile(new Microsoft.ClearScript.DocumentInfo("a.js"), code)) a.Execute(script);
            try { a.Invoke("go"); } catch (Exception e) { return "error " + e.Message; }
            first = loader.Seen;
        }
        using var b = runtime.CreateScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
        var second = new KeptReferrers.Loader();
        b.DocumentSettings.Loader = second;
        for (int i = 0; i < 1100; i++) b.Execute(new Microsoft.ClearScript.DocumentInfo("t" + i + ".js"), i + ";");
        using (var script = b.Compile(new Microsoft.ClearScript.DocumentInfo("b.js"), code)) b.Execute(script);
        try { b.Invoke("go"); } catch (Exception e) { return first + "|error " + e.Message; }
        return first + "|" + (second.Seen.Length > 0 ? second.Seen : "not asked");
    }
}

// patches/v8/0004: V8's eval cache keyed by the native context too (indirect eval and the Function constructor were keyed
// by the context's closure, which every context shares): two engines of one runtime, both alive, run the same text from
// their own scripts and each import names its own script (0016 and before: the second named a.js); the first, asked
// again, still its own.
static class EvalCallers
{
    public const string Expected = "a.js ./m.js,a.js ./m.js|b.js ./m.js,b.js ./m.js|a.js ./m.js,a.js ./m.js";

    public static string Check()
    {
        using var runtime = new V8Runtime();
        const string code = "globalThis.go = () => { Function(\"return import('./m.js')\")(); return 0; };"
            + "globalThis.ev = () => { (0, eval)(\"import('./m.js')\"); return 0; };";
        string Run(V8ScriptEngine engine, KeptReferrers.Loader loader)
        {
            try { engine.Invoke("go"); } catch (Exception e) { return "error " + e.Message; }
            var function = loader.Seen;
            try { engine.Invoke("ev"); } catch (Exception e) { return "error " + e.Message; }
            return function + "," + loader.Seen;
        }
        using var a = runtime.CreateScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
        var la = new KeptReferrers.Loader();
        a.DocumentSettings.Loader = la;
        using (var script = a.Compile(new Microsoft.ClearScript.DocumentInfo("a.js"), code)) a.Execute(script);
        using var b = runtime.CreateScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
        var lb = new KeptReferrers.Loader();
        b.DocumentSettings.Loader = lb;
        using (var script = b.Compile(new Microsoft.ClearScript.DocumentInfo("b.js"), code)) b.Execute(script);
        var first = Run(a, la);
        var second = Run(b, lb);
        var again = Run(a, la);
        return first + "|" + second + "|" + again;
    }
}

// patches/clearscript/0011: a script past the runtime's soft heap limit (MaxHeapSize) is stopped while the .NET thread pool,
// which ClearScript's heap watch timer runs on, is blocked: the near-heap-limit expansion checks the limit on the script's
// own thread (without it, the script ran on and its heap grew to 12 GB in 8 s).
static class HeapLimits
{
    public const string Expected = "stopped";

    static volatile bool s_released;

    public static string Check()
    {
        // (the pool's threads held, polling a flag: a wait the pool does not see as blocking, so no thread is added for it
        // until its starvation rule adds one each half second or so, behind the items queued before the timer's)
        ThreadPool.GetMinThreads(out int min, out _);
        for (int k = 0; k < 8 * Math.Max(min, Environment.ProcessorCount); k++) ThreadPool.QueueUserWorkItem(_ => { while (!s_released) Thread.Sleep(20); });
        Thread.Sleep(200);
        try
        {
            using var runtime = new V8Runtime("heap", new V8RuntimeConstraints { MaxOldSpaceSize = 64, MaxNewSpaceSize = 16, HeapExpansionMultiplier = 1.5 });
            runtime.MaxHeapSize = (UIntPtr)(96UL << 20);
            runtime.HeapSizeSampleInterval = TimeSpan.FromMilliseconds(10);
            using var engine = runtime.CreateScriptEngine();
            var started = System.Diagnostics.Stopwatch.StartNew();
            string outcome;
            try
            {
                engine.Execute("var keep = []; var t0 = Date.now(); while (Date.now() - t0 < 10000) keep.push(new Array(100000).fill(keep.length));");
                outcome = "not stopped";
            }
            catch (Microsoft.ClearScript.ScriptInterruptedException) { outcome = "stopped"; }
            catch (Microsoft.ClearScript.ScriptEngineException e) { outcome = e.Message.Contains("memory limit", StringComparison.Ordinal) ? "stopped" : "error " + e.Message; }
            return outcome == "stopped" && started.Elapsed > TimeSpan.FromSeconds(2) ? $"stopped after {started.Elapsed.TotalSeconds:0.0} s" : outcome;
        }
        finally { s_released = true; }
    }
}

// patches/clearscript/0008: with a scheduler installed, import() hands it each module not evaluated yet as a function and
// the module's name, the module evaluated when the function is called (not in a microtask); a dynamically imported module
// that throws rejects the import alone, its evaluation's own promise handled (no rejection without a handler is left).
static unsafe class ImportSchedulers
{
    public const string Expected = "1:before after:2:true:before after evaluated:bad:0";

    sealed class Loader : Microsoft.ClearScript.DocumentLoader
    {
        public override Microsoft.ClearScript.Document LoadDocument(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback)
        {
            if (specifier.EndsWith("\0import()", StringComparison.Ordinal)) specifier = specifier[..^"\0import()".Length];
            return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///" + specifier.TrimStart('.', '/'))) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
                specifier.Contains("side", StringComparison.Ordinal) ? "globalThis.order.push('evaluated');" : "throw new Error('bad');");
        }

        public override Task<Microsoft.ClearScript.Document> LoadDocumentAsync(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback) =>
            Task.FromResult(LoadDocument(settings, sourceInfo, specifier, category, contextCallback));
    }

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallImportScheduler", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
        int unhandled = 0;
        engine.PromiseRejectionCallback = (kind, promise, value) =>
        {
            if (kind == V8PromiseRejectionEventKind.RejectedWithoutHandler) unhandled++;
            else if (kind == V8PromiseRejectionEventKind.HandlerAddedAfterRejection) unhandled--;
        };
        engine.DocumentSettings.Loader = new Loader();
        engine.DocumentSettings.AccessFlags = Microsoft.ClearScript.DocumentAccessFlags.EnableAllLoading;
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "importScheduler\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("globalThis.queued = []; const made = install(); ssrs2.importScheduler((run, name) => { queued.push([run, name]); }); made"));
        engine.Execute(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///main2.js")) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
            "globalThis.order = ['before']; import('./side2.js'); import('./bad2.js').catch(e => { globalThis.caught = e.message; }); globalThis.order.push('after');");
        var before = (string)engine.Evaluate("order.join(' ')");
        var count = Convert.ToInt32(engine.Evaluate("queued.length"));
        // (the module's resource name, its document's: the URL's path or the URL, by platform)
        var name = ((string)engine.Evaluate("String(queued[0][1])")).EndsWith("side2.js", StringComparison.Ordinal) ? "true" : "false";
        engine.Execute("queued.forEach(q => q[0]())");
        engine.Execute("0");
        return $"{installed}:{before}:{count}:{name}:{engine.Evaluate("order.join(' ')")}:{engine.Evaluate("String(globalThis.caught)")}:{unhandled}";
    }
}

// patches/clearscript/0013: an instance of the code-like constructor is compiled by eval and the Function constructor (its
// toString), with another prototype too; a plain object with the same toString is returned by eval unevaluated.
static unsafe class CodeLikes
{
    public const string Expected = "1:42:7:true:object";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallCodeLike", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "codeLike\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate("class Script { toString() { return this.text; } }" +
            "const make = text => { const o = new ssrs2.codeLike(); Object.setPrototypeOf(o, Script.prototype); o.text = text; return o; };" +
            "const plain = new Script(); plain.text = '1';" +
            "[eval(make('6 * 7')), new Function(make('return 7'))(), make('x') instanceof Script, typeof eval(plain)].join(':')");
    }
}

// patches/clearscript/0014: an object of make(target, prototype) is undetectable (typeof "undefined", falsy, == null, not
// === undefined) yet callable as target, its indices target's and enumerated by target's length, its other names its
// prototype's (here target, a callable Proxy, as HTML's document.all is made of one).
static unsafe class Undetectables
{
    public const string Expected = "1:undefined:true:true:false:b:42:0,1:all";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallUndetectable", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "makeUndetectable\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate("const t = new Proxy(n => n * 2, { get: (f, k) => k === 'length' ? 2 : k === '0' ? 'a' : k === '1' ? 'b' : k === 'name' ? 'all' : undefined," +
            " has: (f, k) => k === '0' || k === '1' || k === 'length' });" +
            "const u = ssrs2.makeUndetectable(t, t);" +
            "[typeof u, !u, u == null, u === undefined, u[1], u(21), Object.keys(u).join(','), u.name].join(':')");
    }
}

// patches/clearscript/0007: the creation context of an object of another context of the isolate, a revoked Proxy's too,
// as its global object; undefined for a primitive.
static unsafe class CreationContexts
{
    public const string Expected = "1:true:true:true:true:undefined";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallCreationContext", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var runtime = new V8Runtime();
        using var a = runtime.CreateScriptEngine();
        using var b = runtime.CreateScriptEngine();
        a.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "creationGlobal\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(a.Evaluate("install()"));
        a.Script.other = b.Evaluate("globalThis");
        a.Script.plain = b.Evaluate("({})");
        // (made by a function of the other context called here: a revoked Proxy does not cross .NET)
        a.Script.makeRevoked = b.Evaluate("callable => { const r = Proxy.revocable(callable ? () => {} : {}, {}); r.revoke(); return r.proxy; }");
        return installed + ":" + a.Evaluate("[ssrs2.creationGlobal(plain) === other, ssrs2.creationGlobal(makeRevoked(true)) === other, " +
            "ssrs2.creationGlobal(makeRevoked(false)) === other, ssrs2.creationGlobal({}) === globalThis, String(ssrs2.creationGlobal(1))].join(':')");
    }
}

// patches/clearscript/0005: a microtask checkpoint from inside a host call runs the queue then, not when the script returns.
static unsafe class Microtasks
{
    public const string Expected = "1:1";

    public static string Check(V8ScriptEngine engine, string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_PerformMicrotaskCheckpoint", out var export)) return "export missing";
        var perform = (delegate* unmanaged[Stdcall]<int>)export;
        engine.AddHostObject("checkpoint", new Func<int>(() => perform()));
        return (string)engine.Evaluate("var flag = 0; Promise.resolve().then(() => { flag = 1; }); checkpoint() + ':' + flag");
    }
}

// patches/clearscript/0004: an import's type attribute reaches the loader after the specifier ("\0type=text"), a text
// module made of a JSON document is the text (a synthetic module, evaluated in its context), and a dynamic import of a
// module that throws rejects with its error; patches/clearscript/0006: a module import() asks for is evaluated after the
// script that asked (before after evaluated), not inside the call; patches/clearscript/0010: an attribute other than type
// reaches the loader too ("\0attr=unknown"), a static import's and import()'s.
static class Modules
{
    public const string Expected = "hello|loader saw ./t.txt type=text|bad module|before after evaluated|import() ./bad.js ./side.js ./dyn.js|attr ./plain.js=unknown ./dyn.js=other";

    sealed class Loader : Microsoft.ClearScript.DocumentLoader
    {
        public string Seen = "";
        // (patches/clearscript/0009: the dynamic imports' specifiers, which end with a NUL and "import()", a static
        // import's not)
        public string Dynamic = "import()";
        // (patches/clearscript/0010: an attribute other than type, its key after a NUL and "attr=", before the type)
        public string Attributes = "attr";

        public override Microsoft.ClearScript.Document LoadDocument(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback)
        {
            bool dynamic = specifier.EndsWith("\0import()", StringComparison.Ordinal);
            if (dynamic) specifier = specifier[..^"\0import()".Length];
            int attr = specifier.IndexOf("\0attr=", StringComparison.Ordinal);
            if (attr >= 0)
            {
                int end = specifier.IndexOf('\0', attr + 1);
                if (end < 0) end = specifier.Length;
                Attributes += $" {specifier[..attr]}={specifier[(attr + 6)..end]}";
                specifier = specifier[..attr] + specifier[end..];
            }
            if (dynamic) Dynamic += " " + specifier;
            if (specifier is "./plain.js" or "./dyn.js")
                return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///" + specifier.TrimStart('.', '/'))) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
                    "export default 1;");
            int marker = specifier.IndexOf("\0type=", StringComparison.Ordinal);
            if (marker >= 0)
            {
                Seen = $"{specifier[..marker]} type={specifier[(marker + 6)..]}";
                return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///t.txt")) { Category = Microsoft.ClearScript.DocumentCategory.Json }, "\"hello\"");
            }
            if (specifier.Contains("side", StringComparison.Ordinal))
                return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///side.js")) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
                    "globalThis.order.push('evaluated');");
            return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///" + specifier.TrimStart('.', '/'))) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
                "throw new Error('bad module');");
        }

        public override Task<Microsoft.ClearScript.Document> LoadDocumentAsync(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback) =>
            Task.FromResult(LoadDocument(settings, sourceInfo, specifier, category, contextCallback));
    }

    public static string Check(V8ScriptEngine engine)
    {
        var loader = new Loader();
        engine.DocumentSettings.Loader = loader;
        engine.DocumentSettings.AccessFlags = Microsoft.ClearScript.DocumentAccessFlags.EnableAllLoading;
        engine.Execute(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///main.js")) { Category = Microsoft.ClearScript.JavaScript.ModuleCategory.Standard },
            "import t from './t.txt' with { type: 'text' }; import './plain.js' with { unknown: 'x' }; globalThis.moduleText = t;"
            + " import('./bad.js').then(() => { globalThis.moduleError = 'resolved'; }, e => { globalThis.moduleError = e.message; });"
            + " globalThis.order = ['before']; import('./side.js'); globalThis.order.push('after'); import('./dyn.js', { with: { other: 'y' } });");
        return $"{engine.Script.moduleText}|loader saw {loader.Seen}|{engine.Script.moduleError}|{string.Join(" ", ((Microsoft.ClearScript.ScriptObject)engine.Script.order).PropertyIndices.Select(i => engine.Script.order[i]))}|{loader.Dynamic}|{loader.Attributes}";
    }
}

// SSRS2's direct and fast calls (patches/clearscript/0003): installed from inside a host call, as SSRS2's engine does,
// then called by the script: a fast call of two int32s (enough times for the optimizing compiler's direct call) and a
// direct call echoing a string, a number and a boolean.
static unsafe class HostCalls
{
    public static readonly string Expected = $"{Sum()} héllo|2.5|true";

    [StructLayout(LayoutKind.Sequential)]
    struct Value { public int Kind; public int Length; public double Number; public nint Data; }

    static char* s_answer;

    static int Sum() { int s = 0; for (int i = 0; i < 100000; i++) s = unchecked(s + i + 1); return s; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static int Add(int a, int b) => a + b;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static void Dispatch(int slot, Value* args, int count, Value* answer)
    {
        var text = count == 3 && args[0].Kind == 4 && args[1].Kind == 2 && args[2].Kind == 3
            ? $"{new string((char*)args[0].Data, 0, args[0].Length)}|{args[1].Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{(args[2].Number != 0 ? "true" : "false")}"
            : $"bad arguments ({count})";
        if (s_answer == null) s_answer = (char*)NativeMemory.Alloc(256, sizeof(char));
        int n = Math.Min(text.Length, 256);
        text.AsSpan(0, n).CopyTo(new Span<char>(s_answer, n));
        *answer = new Value { Kind = 4, Length = n, Data = (nint)s_answer };
    }

    public static string Check(V8ScriptEngine engine, string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallFastCalls", out var fastExport)
            || !NativeLibrary.TryGetExport(handle, "Ssrs2_InstallDirectCalls", out var directExport))
            return "exports missing";
        var installFast = (delegate* unmanaged[Stdcall]<byte*, int, byte**, int*, int*, void**, int>)fastExport;
        var installDirect = (delegate* unmanaged[Stdcall]<byte*, int, byte**, int*, void*, int>)directExport;
        engine.AddHostObject("install", new Func<bool>(() =>
        {
            fixed (byte* holder = "calls\0"u8, add = "add\0"u8, echo = "echo\0"u8)
            {
                byte* addName = add, echoName = echo;
                int arity = 2, fast = 1, slot = 0;
                void* function = (delegate* unmanaged[Cdecl]<int, int, int>)&Add;
                return installFast(holder, 1, &addName, &arity, &fast, &function) != 0
                    && installDirect(holder, 1, &echoName, &slot, (delegate* unmanaged[Cdecl]<int, Value*, int, Value*, void>)&Dispatch) != 0;
            }
        }));
        if (!(bool)engine.Evaluate("install()")) return "not installed";
        return (string)engine.Evaluate("var s = 0; for (var i = 0; i < 100000; i++) s = (s + calls.add(i, 1)) | 0; s + ' ' + calls.echo('héllo', 2.5, true)");
    }
}

// patches/clearscript/0016: withIncumbent(f)'s wrapper calls f with its this, the calling context's global and its own
// arguments, called directly and as an accessor; a function that is not one is refused.
static unsafe class Incumbents
{
    public const string Expected = "1:true:7:a,b:true:3:TypeError";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallIncumbent", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "withIncumbent\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate(
            "const w = ssrs2.withIncumbent(function (g, ...rest) { return [g === globalThis, this.v, rest.join(',')]; });" +
            "const o = { v: 7 }; const direct = w.call(o, 'a', 'b');" +
            "const p = { v: 3 }; Object.defineProperty(p, 'seen', { get: ssrs2.withIncumbent(function (g) { return [g === globalThis, this.v]; }) });" +
            "let refused = 'none'; try { ssrs2.withIncumbent(1); } catch (e) { refused = e.name; }" +
            "[...direct, ...p.seen, refused].join(':')");
    }
}

// patches/clearscript/0017: a disposed engine's script still runs when an asynchronous WebAssembly compile it began
// finishes (a task V8 posts for the isolate resolves its promise in the engine's context); a host function called from the
// promise's reactions finds no context and does nothing. Without the patch it read the freed context (a crash, or a call).
// The reactions mark shared memory another engine of the runtime reads, to know they ran.
static class ContextLinks
{
    public const string Expected = "export:ran:1";

    public static string Check(string library)
    {
        if (!NativeLibrary.TryGetExport(NativeLibrary.Load(library), "Ssrs2_ContextLinks", out _)) return "export missing";
        using var runtime = new V8Runtime();
        using var b = runtime.CreateScriptEngine();
        b.Execute("globalThis.ran = new Int32Array(new SharedArrayBuffer(4));");
        var a = runtime.CreateScriptEngine();
        var calls = 0;
        a.AddHostObject("note", new Action(() => Interlocked.Increment(ref calls)));
        a.Script.ran = b.Script.ran;
        a.Execute("note();");
        // one function of a million (i32.const 0, drop): its compile outlasts the dispose
        a.Execute(
            "const n = 1000000, body = 3 * n + 2;" +
            "const leb = v => { const out = []; do { let b = v & 127; v >>>= 7; if (v) b |= 128; out.push(b); } while (v); return out; };" +
            "const head = [0, 97, 115, 109, 1, 0, 0, 0, 1, 4, 1, 96, 0, 0, 3, 2, 1, 0, 10];" +
            "const sizes = [...leb(body + leb(body).length + 1), 1, ...leb(body), 0];" +
            "const bytes = new Uint8Array(head.length + sizes.length + body - 1);" +
            "bytes.set(head); bytes.set(sizes, head.length);" +
            "let k = head.length + sizes.length;" +
            "for (let i = 0; i < n; i++) { bytes[k++] = 0x41; bytes[k++] = 0; bytes[k++] = 0x1a; }" +
            "bytes[k] = 0x0b;" +
            "WebAssembly.compile(bytes).then(() => { Atomics.store(ran, 0, 1); note(); }, () => Atomics.store(ran, 0, 2));");
        a.Dispose();
        for (var i = 0; i < 500 && Convert.ToInt32(b.Evaluate("Atomics.load(ran, 0)")) == 0; i++) Thread.Sleep(10);
        var state = Convert.ToInt32(b.Evaluate("Atomics.load(ran, 0)"));
        return "export:" + (state == 1 ? "ran" : "state " + state) + ":" + calls;
    }
}

// patches/clearscript/0018: the soft heap limit (MaxHeapSize) counts WebAssembly memories, outside V8's heap: four of
// 128 MB filled under a 256 MB limit stop the script (it then runs 300 ms, past the heap watch timer's 10 ms; without the
// patch it ends normally); and a memory is 4 GB at most (a 64-bit one of 8 GB is refused with a RangeError).
static class WasmMemoryLimits
{
    public const string Expected = "export:ScriptEngineException:RangeError";

    public static string Check(string library)
    {
        if (!NativeLibrary.TryGetExport(NativeLibrary.Load(library), "Ssrs2_WasmMemoryLimit", out _)) return "export missing";
        string stopped;
        using (var runtime = new V8Runtime())
        {
            runtime.MaxHeapSize = (UIntPtr)(256UL << 20);
            runtime.HeapSizeSampleInterval = TimeSpan.FromMilliseconds(10);
            using var engine = runtime.CreateScriptEngine();
            try
            {
                engine.Execute("globalThis.ms = []; for (let i = 0; i < 4; i++) { const m = new WebAssembly.Memory({ initial: 2048 }); new Uint8Array(m.buffer).fill(1); ms.push(m); } const t = Date.now(); while (Date.now() - t < 300);");
                stopped = "not stopped";
            }
            catch (Exception e) { stopped = e.GetType().Name; }
        }
        using var other = new V8ScriptEngine();
        var refused = (string)other.Evaluate("try { new WebAssembly.Memory({ initial: 131072n, address: 'i64' }); 'made' } catch (e) { e.name }");
        return "export:" + stopped + ":" + refused;
    }
}

// patches/clearscript/0019: a context given a check of code made from strings: a direct eval keeps its scope, a string the
// check refuses is an EvalError with the message given (eval's and the Function constructor's), a value other than a
// string is eval's own answer; the check removed, strings compile again unchecked.
static unsafe class CodeGenChecks
{
    public const string Expected = "1:2:EvalError refused:3:EvalError refused:5:NOPE";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallCodeGenCheck", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "setCheck\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate(
            "ssrs2.setCheck((s, codeLike) => typeof s === 'string' && s.includes('NOPE') ? null : undefined, 'refused');" +
            "const e = f => { try { return String(f()); } catch (x) { return x.name + (x.message === 'refused' ? ' refused' : ''); } };" +
            "const r = [(function () { let l = 1; eval('l = 2'); return l; })(), e(() => eval('\"NOPE\"')), new Function('return 3')(), e(() => new Function('NOPE')), eval(5)];" +
            "ssrs2.setCheck(null); r.push(eval('\"NOPE\"')); r.join(':')");
    }
}

// patches/clearscript/0020: entryGlobal() is null in a script the host runs, and in a promise job the global of the job's
// context: here the context of the function the job calls, another engine's of the same runtime.
static unsafe class EntryContexts
{
    public const string Expected = "1:true:false:true";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallEntryContext", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var runtime = new V8Runtime();
        using var a = runtime.CreateScriptEngine();
        using var b = runtime.CreateScriptEngine();
        Func<int> installer = () => { fixed (byte* holder = "ssrs2\0"u8, name = "entryGlobal\0"u8) return install(holder, name); };
        a.AddHostObject("install", installer);
        b.AddHostObject("install", installer);
        var installed = Convert.ToInt32(a.Evaluate("install()")) & Convert.ToInt32(b.Evaluate("install()"));
        b.Execute("globalThis.seen = []; globalThis.job = () => seen.push(ssrs2.entryGlobal() === globalThis);");
        a.Script.job = b.Script.job;
        a.Execute("globalThis.outside = ssrs2.entryGlobal() === null; Promise.resolve().then(job); Promise.resolve().then(() => { globalThis.own = ssrs2.entryGlobal() === globalThis; });");
        return installed + ":" + a.Evaluate("String(outside)") + ":" + a.Evaluate("String(own === undefined ? 'none' : !own)") + ":" + b.Evaluate("seen.join()");
    }
}

// patches/clearscript/0021: a context made while Ssrs2_SetImmutableGlobalPrototype(1) is set has a global whose prototype
// cannot be changed (Reflect.setPrototypeOf false, Object.setPrototypeOf a TypeError, the same prototype accepted); one
// made after (0) an ordinary global.
static unsafe class ImmutableGlobals
{
    public const string Expected = "1:false:TypeError:true:true";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_SetImmutableGlobalPrototype", out var export)) return "export missing";
        var set = (delegate* unmanaged[Stdcall]<int, int>)export;
        using var runtime = new V8Runtime();
        var on = set(1);
        V8ScriptEngine immutable;
        try { immutable = runtime.CreateScriptEngine(); } finally { set(0); }
        using (immutable)
        using (var ordinary = runtime.CreateScriptEngine())
        {
            return on + ":" + immutable.Evaluate(
                "const p = Object.getPrototypeOf(globalThis); let t; try { Object.setPrototypeOf(globalThis, {}); t = 'none'; } catch (e) { t = e.name; }" +
                "[Reflect.setPrototypeOf(globalThis, {}), t, Reflect.setPrototypeOf(globalThis, p)].join(':')") + ":" + ordinary.Evaluate("String(Reflect.setPrototypeOf(globalThis, {}))");
        }
    }
}

// patches/clearscript/0022 with patches/v8/0005: setImmutablePrototype(object) locks an existing object's prototype, one
// already other objects' prototype too (Reflect.setPrototypeOf false, Object.setPrototypeOf a TypeError, the same one
// accepted, a property still added and found through the chain); a revoked Proxy answers false.
static unsafe class ImmutablePrototypes
{
    public const string Expected = "1:true:false:TypeError:true:7:false";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallImmutablePrototype", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "setImmutablePrototype\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate("class A { m() { return 1; } } class B extends A {} const b = new B(); b.m(); const p = A.prototype; const was = Object.getPrototypeOf(p);" +
            "const r = [ssrs2.setImmutablePrototype(p), Reflect.setPrototypeOf(p, {})]; try { Object.setPrototypeOf(p, null); r.push('none'); } catch (e) { r.push(e.name); }" +
            "r.push(Reflect.setPrototypeOf(p, was)); p.n = () => 7; r.push(b.n());" +
            "const { proxy, revoke } = Proxy.revocable({}, {}); revoke(); r.push(ssrs2.setImmutablePrototype(proxy)); r.join(':')");
    }
}

// patches/clearscript/0023 with patches/v8/0005: an object of interceptorFactory(handler, flags)(prototype) answers its
// indices and names through the handler's traps: a non-configurable value defined (the definer's, the property still
// configurable, as no Proxy may), an accessor refused, its keys listed, an unhandled name its prototype's; with flags 4 it
// is undetectable (typeof "undefined" though it has a named interceptor) and callable.
// patches/clearscript/0025: with flags 8 the named interceptor is non-masking, asked only for a name neither the object
// nor its prototype chain has (a document's named properties); the factory says so (nonMasking).
static unsafe class InterceptorObjects
{
    public const string Expected = "1:true:TypeError:x:true:0,1,a:proto:undefined:true:6:true:1:proto:n::named,missing";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallInterceptors", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "interceptorFactory\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate("const store = new Map(); const h = {" +
            " get: (o, k) => store.has(String(k)) ? store.get(String(k)) : undefined, has: (o, k) => store.has(String(k)) || undefined," +
            " defineProperty: (o, k, d) => 'get' in d || 'set' in d ? false : (store.set(String(k), d.value), true)," +
            " getOwnPropertyDescriptor: (o, k) => store.has(String(k)) ? { value: store.get(String(k)), writable: true, enumerable: true, configurable: true } : undefined," +
            " ownKeys: (o, indexed) => [...store.keys()].filter(k => /^[0-9]+$/.test(k) === indexed).map(k => indexed ? Number(k) : k), call: (a, b) => a * b };" +
            "const proto = { inherited: 'proto' }; const o = ssrs2.interceptorFactory(h, 3)(proto); const r = [Object.getPrototypeOf(o) === proto];" +
            "try { Object.defineProperty(o, 'a', { get() {} }); r.push('none'); } catch (e) { r.push(e.name); }" +
            "Object.defineProperty(o, 'a', { value: 'x', configurable: false }); o[0] = 1; Object.defineProperty(o, 1, { value: 2 });" +
            "r.push(o.a, Object.getOwnPropertyDescriptor(o, 'a').configurable, Object.keys(o).join(), o.inherited);" +
            "const u = ssrs2.interceptorFactory(h, 7)(proto); r.push(typeof u, u == null, u(2, 3));" +
            // (0025, flags 8: the named interceptor asked only for a name the object and its prototype chain lack)
            "const asked = []; const nm = ssrs2.interceptorFactory({ get: (o, k) => (asked.push(k), k === 'named' ? 'n' : undefined) }, 10)(proto); nm.own = 1;" +
            "r.push(ssrs2.interceptorFactory.nonMasking, nm.own, nm.inherited, nm.named, nm.missing, asked.join()); r.join(':')");
    }
}

// patches/clearscript/0024 with patches/v8/0006: between prototypeSetup(true) and prototypeSetup(false, objects) a used
// prototype's accessors are redefined (enumerable, as WebIDL's shapes make them), the prototype then made fast again (the
// count answered), its getters still found through an instance and through a subclass's.
static unsafe class PrototypeSetups
{
    public const string Expected = "1:2:true:true:41:7";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallPrototypeSetup", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "prototypeSetup\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        return installed + ":" + engine.Evaluate("let src = 'class A {'; for (let i = 0; i < 50; i++) src += `get p${i}() { return ${i}; } m${i}() { return ${i}; }`;" +
            "const A = new Function(src + '} return A;')(); class B extends A {} const a = new A(), b = new B(); let s = 0; for (let i = 0; i < 3; i++) s += a.p3 + b.p4 + a.m5();" +
            "ssrs2.prototypeSetup(true); for (const k of Object.getOwnPropertyNames(A.prototype)) { if (k === 'constructor') continue; const d = Object.getOwnPropertyDescriptor(A.prototype, k);" +
            " Object.defineProperty(A.prototype, k, d.get ? { get: d.get, enumerable: true, configurable: true } : { value: d.value, writable: true, enumerable: true, configurable: true }); }" +
            "const made = ssrs2.prototypeSetup(false, [A.prototype, A]);" +
            "[made, Object.keys(A.prototype).length === 100, Object.getOwnPropertyDescriptor(A.prototype, 'p9').enumerable, a.p41, b.m7()].join(':')");
    }
}

// patches/clearscript/0024: functionScriptId(f) answers the id of f's script: two functions of one script the same, a
// function of another script another, -1 for a bound function and for a value that is no function.
static unsafe class FunctionScriptIds
{
    public const string Expected = "1:true:true:true:-1:-1";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallFunctionScriptId", out var export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<int>(() =>
        {
            fixed (byte* holder = "ssrs2\0"u8, name = "functionScriptId\0"u8) return install(holder, name);
        }));
        var installed = Convert.ToInt32(engine.Evaluate("install()"));
        engine.Execute("globalThis.f = function f() {}; globalThis.g = () => 1;");
        return installed + ":" + engine.Evaluate("const id = ssrs2.functionScriptId; function h() {}" +
            "[id(f) > 0, id(f) === id(g), id(h) !== id(f), id(f.bind(null)), id(42)].join(':')");
    }
}

// patches/clearscript/0026: the digest of a script's text (the script and module caches' key) is XXH64 of its UTF-16
// bytes, seed 0 (a 32-bit process's: its halves folded), in place of FNV-1a a byte a step: known answers for texts of
// 0, 1, 3 and 89 characters (the last past 32 bytes: the four lanes, then a tail of 8, 4 and single bytes), a text that
// differs by one character another digest, and the same text the same.
static unsafe class TextDigests
{
    public const string Expected = "ef46db3751d8e999:e513e02c99167f96:aff0f2a2f8b32731:49bb1df8f8e706ad:True:True";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        // (ClearScript's own entry points are stdcall: a 32-bit Windows library exports them decorated, _Name@bytes of
        // arguments, which its P/Invokes find by themselves and a lookup by name does not)
        bool Export(string name, int bytes, out nint export) =>
            NativeLibrary.TryGetExport(handle, name, out export) || NativeLibrary.TryGetExport(handle, "_" + name + "@" + bytes, out export);
        if (!Export("StdString_New", 8, out var newExport) || !Export("StdString_Delete", 4, out var deleteExport)
            || !Export("V8UnitTestSupport_GetTextDigest", 4, out var digestExport)) return "export missing";
        var make = (delegate* unmanaged[Stdcall]<char*, int, nint>)newExport;
        var free = (delegate* unmanaged[Stdcall]<nint, void>)deleteExport;
        var digest = (delegate* unmanaged[Stdcall]<nint, nuint>)digestExport;
        ulong Of(string text)
        {
            fixed (char* p = text)
            {
                var s = make(p, text.Length);
                try { return digest(s); }
                finally { free(s); }
            }
        }
        // (a 32-bit process's digest is the 64 bits folded)
        string Hex(ulong d, ulong whole) => (IntPtr.Size == 8 ? d == whole : d == (uint)(whole ^ (whole >> 32))) ? whole.ToString("x16") : "other " + d.ToString("x");
        string longer = "const answer = 42; // 中文 耀￿ " + new string('x', 61);
        return Hex(Of(""), 0xef46db3751d8e999) + ":" + Hex(Of("a"), 0xe513e02c99167f96) + ":" + Hex(Of("abc"), 0xaff0f2a2f8b32731) + ":" + Hex(Of(longer), 0x49bb1df8f8e706ad)
            + ":" + (Of(longer) != Of(longer.Replace('耀', '\u0000'))) + ":" + (Of(longer) == Of(new string(longer.AsSpan())));
    }
}

// patches/clearscript/0027: a context read from a blob. Ssrs2_MakeSnapshot runs a script in a context of an isolate of
// V8's snapshot creator, whose global is made as the library makes a context's, and keeps the blob V8 writes; isolates
// made afterwards start from it, and Ssrs2_SnapshotContext(1) makes the contexts a thread creates come from the blob.
// (Its last argument names a script the host keeps to stand for the blob's where its code imports a module: none here.)
// Checked with a toy script: what it defined is there in such a context without being run again (an object, a class
// with a private field, a map holding a typed array, a closure's state), a second context of the blob has its own
// copy, host objects still work in it, a context made without the request is empty, and an isolate made before the
// blob (none here: the blob is made first) is not asked. While the script runs, the global's ssrs2PrototypeSetup is
// 0024's prototype setup function (called here as a context's script calls it), gone from a context read from the
// blob. Ssrs2_SnapshotData gives the blob's bytes out whole, and Ssrs2_UseSnapshot refuses bytes once the process has
// a blob (that a later process starts from bytes kept in a file is checked by the host that keeps them: one blob a
// process). The call must come first in the smoke test: the blob is made once a process and every later isolate starts
// from it.
static unsafe class SnapshotContexts
{
    public const string Expected = "made:True:53.5:1:2:1:host 5:undefined:False:function02:undefined:data True:use 0";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        // (ClearScript's own entry points are stdcall: a 32-bit Windows library exports them decorated, _Name@bytes of
        // arguments, which a lookup by name does not find)
        bool Export(string name, int bytes, out nint export) =>
            NativeLibrary.TryGetExport(handle, name, out export) || NativeLibrary.TryGetExport(handle, "_" + name + "@" + bytes, out export);
        if (!Export("Ssrs2_MakeSnapshot", 20, out var makeExport) || !Export("Ssrs2_SnapshotContext", 4, out var contextExport)) return "export missing";
        var make = (delegate* unmanaged[Stdcall]<char*, int, char*, int, byte*, int>)makeExport;
        var context = (delegate* unmanaged[Stdcall]<int, int>)contextExport;

        // (V8 is started by ClearScript, which gives it its flags: a runtime made and left before the blob)
        using (new V8Runtime("before")) { }
        const string name = "toy.js";
        const string code = """
            globalThis.made = { answer: 42, add(a, b) { return a + b; } };
            class Kept { #x = 7; get x() { return this.#x; } }
            globalThis.Kept = Kept;
            globalThis.table = new Map([['k', new Float64Array([1.5])]]);
            globalThis.count = (() => { let n = 0; return () => ++n; })();
            globalThis.setup = typeof ssrs2PrototypeSetup + ssrs2PrototypeSetup(true) + ssrs2PrototypeSetup(false, [Kept.prototype, {}]);
            """;
        int size;
        fixed (char* n = name)
        fixed (char* c = code)
            size = make(n, name.Length, c, code.Length, null);
        if (size <= 0) return "no blob";
        // (the blob's bytes given out whole, and not taken again: the process has its blob)
        string kept = "no data exports";
        if (Export("Ssrs2_SnapshotData", 8, out var dataExport) && Export("Ssrs2_UseSnapshot", 12, out var useExport))
        {
            var data = (delegate* unmanaged[Stdcall]<byte*, int, int>)dataExport;
            var use = (delegate* unmanaged[Stdcall]<byte*, int, byte*, int>)useExport;
            var bytes = new byte[size];
            fixed (byte* b = bytes)
                kept = "data " + (data(null, 0) == size && data(b, size) == size && bytes.Any(x => x != 0)) + ":use " + use(b, size, null);
        }

        using var runtime = new V8Runtime("after");
        context(1);
        var first = runtime.CreateScriptEngine();
        bool fromBlob = context(1) == 1;
        var second = runtime.CreateScriptEngine();
        context(0);
        var plain = runtime.CreateScriptEngine();
        bool plainFromBlob = context(0) == 1;
        try
        {
            var sum = first.Evaluate("made.answer + made.add(1, 2) + new Kept().x + table.get('k')[0]");
            var counts = first.Evaluate("count() + ':' + count()") + ":" + second.Evaluate("count()");
            first.AddHostObject("given", new { Value = 5 });
            var host = first.Evaluate("'host ' + given.Value");
            return "made:" + fromBlob + ":" + Convert.ToString(sum, System.Globalization.CultureInfo.InvariantCulture) + ":" + counts + ":" + host + ":" + plain.Evaluate("typeof made") + ":" + plainFromBlob
                + ":" + first.Evaluate("setup + ':' + typeof ssrs2PrototypeSetup") + ":" + kept;
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            plain.Dispose();
        }
    }
}

// patches/clearscript/0029: text an embedder and a context's scripts both read without copying it at each crossing.
// Ssrs2_ArenaNew makes an arena (memory that never moves, a limit of bytes); Ssrs2_ArenaAdd adds a string of UTF-16
// units and answers its number (from 1; one byte a character kept when all are Latin-1); Ssrs2_ArenaEntry gives an
// entry's characters in place; Ssrs2_InstallArena, from inside a host call, makes globalThis[holder][name] = { text,
// put, count }: text(number) is a string over the entry's own characters (a V8 external string; under 24 characters a
// copy), put(string) has V8 write the string into the arena and answers its number, count() the entries.
// Ssrs2_ArenaRelease ends the embedder's use: nothing more is added, and the memory lives until the last string over
// it and the context's functions are collected. Checked with toy strings: short and long, Latin-1 and not, empty; a
// script's own strings read back by the embedder in place; the numbers that are no entry; a limit reached; strings
// still whole after the release and a collection.
static unsafe class StringArenas
{
    public const string Expected = "1 2 3 4:short|true|true|true|:5 6:6:1 abc|2 3000 é語:undefined undefined:0 full:true 0 6";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        // (ClearScript's own entry points are stdcall: a 32-bit Windows library exports them decorated)
        bool Export(string name, int bytes, out nint export) =>
            NativeLibrary.TryGetExport(handle, name, out export) || NativeLibrary.TryGetExport(handle, "_" + name + "@" + bytes, out export);
        if (!Export("Ssrs2_ArenaNew", 8, out var newExport) || !Export("Ssrs2_ArenaAdd", 12, out var addExport) || !Export("Ssrs2_ArenaEntry", 16, out var entryExport)
            || !Export("Ssrs2_ArenaCount", 4, out var countExport) || !Export("Ssrs2_ArenaRelease", 4, out var releaseExport) || !Export("Ssrs2_InstallArena", 12, out var installExport))
            return "export missing";
        var make = (delegate* unmanaged[Stdcall]<long, nint>)newExport;
        var add = (delegate* unmanaged[Stdcall]<nint, char*, int, int>)addExport;
        var entry = (delegate* unmanaged[Stdcall]<nint, int, void**, int*, int>)entryExport;
        var count = (delegate* unmanaged[Stdcall]<nint, int>)countExport;
        var release = (delegate* unmanaged[Stdcall]<nint, void>)releaseExport;
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, nint, int>)installExport;

        int Add(nint arena, string text) { fixed (char* chars = text) return add(arena, chars, text.Length); }
        string Read(nint arena, int number)
        {
            void* data; int length;
            int kind = entry(arena, number, &data, &length);
            if (kind == 0) return "none";
            var text = kind == 2 ? new string((char*)data, 0, length) : string.Concat(new ReadOnlySpan<byte>(data, length).ToArray().Select(b => (char)b));
            return kind + " " + (length > 40 ? length.ToString() : text);
        }
        bool Install(nint arena)
        {
            var h = System.Text.Encoding.UTF8.GetBytes("__toy\0"); var n = System.Text.Encoding.UTF8.GetBytes("arena\0");
            fixed (byte* hp = h, np = n) return install(hp, np, arena) == 1;
        }

        string longLatin = new string('x', 2999) + "é", longWide = new string('語', 2999) + "a";
        var arena = make(0);
        string numbers = $"{Add(arena, "short")} {Add(arena, longLatin)} {Add(arena, longWide)} {Add(arena, "")}";

        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<bool>(() => Install(arena)));
        engine.Script.longLatin = longLatin; engine.Script.longWide = longWide;
        var texts = (string)engine.Evaluate("""
            install(); const a = __toy.arena;
            globalThis.kept = [a.text(2), a.text(3)];
            [a.text(1), a.text(2) === longLatin, a.text(3) === longWide, (a.text(2) + a.text(3)).length === 6000 && a.text(3).slice(2998) === '語a', a.text(4)].join('|')
            """);
        // (a script's own strings, written by V8 into the arena: read here in place)
        var put = (string)engine.Evaluate("[__toy.arena.put('abc'), __toy.arena.put('é'.repeat(1) + '語'.repeat(2999))].join(' ')");
        string read = $"{count(arena)}:{Read(arena, 5)}|{Read(arena, 6).Replace("3000", "3000 é語")}";
        var none = (string)engine.Evaluate("[__toy.arena.text(0), __toy.arena.text(99)].map(String).join(' ')");

        // (a limit reached: 0 answers, to the embedder and to the script)
        var small = make(64);
        string full = $"{Add(small, new string('y', 200))} {(Add(small, "fits") == 1 ? "full" : "not")}";
        release(small);

        // (released: nothing more is added; the strings made over it are whole after a collection)
        release(arena);
        engine.CollectGarbage(true);
        var after = (string)engine.Evaluate("[kept[0] === longLatin && kept[1] === longWide, __toy.arena.put('late'), __toy.arena.count()].join(' ')");
        return $"{numbers}:{texts}:{put}:{read}:{none}:{full}:{after}";
    }
}

// patches/clearscript/0030: an embedder calls a context's script functions from inside a host call, naming script
// values by numbers. Ssrs2_InstallHandles makes globalThis[holder][name] the context's handle table, a script array
// the script fills as it likes; Ssrs2_Call(function, this, arguments, count, answer) calls the function under a handle
// with arguments given as numbers, booleans, strings, null, handles, or "indexed" (a table's element at an index, made
// by a maker function where the table has none). An answer that is an object is left under handle 1; what a call
// throws is left under handle 2 and the call answers 0. Checked with toy functions: each kind of argument and of
// answer, `this`, a thrown error kept as it is, an indexed argument made once then found, a handle that is no
// function, and a call made outside a host call (refused).
static unsafe class HandlesAndCalls
{
    public const string Expected = "5:a-é語-true-null:7:object 1 42:threw 0 RangeError boom:made 9,9 1:-1:-1";

    [StructLayout(LayoutKind.Sequential)]
    struct Value { public int Kind; public int Length; public double Number; public nint Data; }
    const int Undefined = 0, Null = 1, Number = 2, Boolean = 3, String = 4, Error = 7, Handle = 8, Indexed = 9;

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        bool Export(string name, int bytes, out nint export) =>
            NativeLibrary.TryGetExport(handle, name, out export) || NativeLibrary.TryGetExport(handle, "_" + name + "@" + bytes, out export);
        if (!Export("Ssrs2_InstallHandles", 8, out var installExport) || !Export("Ssrs2_Call", 20, out var callExport)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)installExport;
        var call = (delegate* unmanaged[Stdcall]<int, int, Value*, int, Value*, int>)callExport;

        string Answer(Value v) => v.Kind switch
        {
            Number => v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Boolean => v.Number != 0 ? "true" : "false",
            String => new string((char*)v.Data, 0, v.Length),
            Null => "null", Undefined => "undefined", Handle => "object " + v.Number, Error => "error " + v.Number, _ => "kind " + v.Kind,
        };
        using var engine = new V8ScriptEngine();
        var results = new List<string>();
        engine.AddHostObject("install", new Func<bool>(() =>
        {
            var h = System.Text.Encoding.UTF8.GetBytes("__toy\0"); var n = System.Text.Encoding.UTF8.GetBytes("handles\0");
            fixed (byte* hp = h, np = n) return install(hp, np) == 1;
        }));
        engine.AddHostObject("run", new Action(() =>
        {
            Value answer;
            var args = stackalloc Value[4];
            // (numbers)
            args[0] = new Value { Kind = Number, Number = 2 }; args[1] = new Value { Kind = Number, Number = 3 };
            results.Add(call(3, 0, args, 2, &answer) == 1 ? Answer(answer) : "failed");
            // (a string, a boolean, null: joined by the script)
            fixed (char* text = "a-é語")
            {
                args[0] = new Value { Kind = String, Length = 4, Data = (nint)text }; args[1] = new Value { Kind = Boolean, Number = 1 }; args[2] = new Value { Kind = Null };
                results.Add(call(4, 0, args, 3, &answer) == 1 ? Answer(answer) : "failed");
            }
            // (this, a handle)
            results.Add(call(5, 6, args, 0, &answer) == 1 ? Answer(answer) : "failed");
            // (an object answered: left under handle 1, passed back as a handle)
            int made = call(7, 0, args, 0, &answer);
            string first = made == 1 ? Answer(answer) : "failed";
            args[0] = new Value { Kind = Handle, Number = 1 };
            results.Add(first + " " + (call(8, 0, args, 1, &answer) == 1 ? Answer(answer) : "failed"));
            // (a throw: 0, the value under handle 2, read by the script afterwards)
            int threw = call(9, 0, args, 0, &answer);
            args[0] = new Value { Kind = Handle, Number = 2 };
            results.Add("threw " + threw + " " + (call(10, 0, args, 1, &answer) == 1 ? Answer(answer) : "failed"));
            // (indexed: the table under 11, the maker under 12; index 9 made once, then found)
            args[0] = new Value { Kind = Indexed, Length = 11, Number = 9, Data = 12 }; args[1] = new Value { Kind = Indexed, Length = 11, Number = 9, Data = 12 };
            results.Add(call(13, 0, args, 2, &answer) == 1 ? Answer(answer) : "failed");
            // (a handle that is no function)
            results.Add(call(6, 0, args, 0, &answer).ToString());
        }));
        engine.Execute("""
            install(); const h = __toy.handles;
            h[3] = (a, b) => a + b;
            h[4] = (s, b, n) => s + '-' + b + '-' + n;
            h[5] = function () { return this.k; }; h[6] = { k: 7 };
            h[7] = () => ({ answer: 42 }); h[8] = o => o.answer;
            h[9] = () => { throw new RangeError('boom'); }; h[10] = e => e.constructor.name + ' ' + e.message;
            let makes = 0;
            h[11] = []; h[12] = i => { makes++; return h[11][i] = { index: i }; };
            h[13] = (a, b) => 'made ' + a.index + ',' + b.index + ' ' + makes;
            run();
            """);
        // (outside a host call: refused)
        Value outside;
        results.Add(call(3, 0, null, 0, &outside).ToString());
        return string.Join(":", results);
    }
}

// patches/clearscript/0031: the interceptor objects of 0023 read their handler's traps once, when the factory is made
// (a trap's name was made a string and looked up in the handler at every call), and a factory made with flags 16 gives
// its objects an indexed table: make.setTable(object, values, array, index, expected, ...) hands an array of values
// and up to three guards, each a cell of an Int32Array and the value it is to hold; while every cell holds its value,
// an indexed read below the array's length whose value there is not undefined is answered from the array without
// calling the handler. The factory says so: interceptorFactory.tables. Checked with a toy handler that counts its
// calls: a read from the table, one past its length, one of an undefined value, one after a guard's cell moved and
// after it moved back, the table taken away, an object of another factory refused, and a trap put on the handler
// after its factory was made, which is not seen.
static unsafe class InterceptorTables
{
    public const string Expected = "true:a0 b0 t2,1 u3,2:t1,3 b3:t1,4 false:late undefined:true";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallInterceptors", out var export) && !NativeLibrary.TryGetExport(handle, "_Ssrs2_InstallInterceptors@8", out export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<bool>(() =>
        {
            var h = System.Text.Encoding.UTF8.GetBytes("__toy\0"); var n = System.Text.Encoding.UTF8.GetBytes("interceptorFactory\0");
            fixed (byte* hp = h, np = n) return install(hp, np) == 1;
        }));
        return (string)engine.Evaluate("""
            install();
            const factory = __toy.interceptorFactory, out = [String(factory.tables === true)];
            let calls = 0;
            const handler = { get(o, i) { calls++; return 't' + i; } };
            const make = factory(handler, 1 | 16), o = make(null), cells = new Int32Array([0, 7]);
            out.push([String(make.setTable(o, ['a', 'b', undefined], cells, 1, 7) && o[0]) + calls, o[1] + calls, o[2] + ',' + calls, (o[3], 'u' + 3) + ',' + calls].join(' '));
            cells[1] = 8; const moved = o[1] + ',' + calls; cells[1] = 7;
            out.push(moved + ' ' + o[1] + calls);
            make.setTable(o, null);
            out.push(o[1] + ',' + calls + ' ' + String(make.setTable(factory(handler, 1)(null), ['x'])));
            // (a trap put on the handler later is not seen: the traps are read when the factory is made)
            handler.set = () => { throw new Error('set seen'); }; o[5] = 'late';
            out.push(o[5] === 't5' ? 'late undefined' : 'late ' + o[5]);
            // (a factory without the flag: no setTable, its reads the handler's)
            out.push(String(factory(handler, 1).setTable === undefined && factory(handler, 1)(null)[4] === 't4'));
            out.join(':');
            """);
    }
}

// patches/clearscript/0031b: the guards of an interceptor object's indexed table (0031) keep the memory of the arrays
// their cells are in, so a read stays valid whatever becomes of those arrays, and an array over a buffer a script can
// resize is refused. Checked with a toy handler that counts its calls: a table guarded by a cell of a buffer that is
// then transferred (the read still answered from the table: the memory is the same), the cell written through the
// buffer it was transferred to (the read goes to the handler), that buffer dropped and collected (the read still
// safe), a transfer to another size, an array over a resizable buffer and a detached array (no table), and two
// thousand objects with tables dropped and collected.
static unsafe class InterceptorTableGuards
{
    public const string Expected = "a0:a0:t0,1:read:read:false false:2000 collected";

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        if (!NativeLibrary.TryGetExport(handle, "Ssrs2_InstallInterceptors", out var export) && !NativeLibrary.TryGetExport(handle, "_Ssrs2_InstallInterceptors@8", out export)) return "export missing";
        var install = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)export;
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("install", new Func<bool>(() =>
        {
            fixed (byte* holder = "__toy\0"u8, name = "interceptorFactory\0"u8) return install(holder, name) == 1;
        }));
        var results = new List<string>();
        engine.Execute("""
            install();
            globalThis.calls = 0;
            const handler = { get(o, i) { calls++; return 't' + i; } };
            globalThis.make = __toy.interceptorFactory(handler, 1 | 16);
            globalThis.o = make(null);
            globalThis.buffer = new ArrayBuffer(16);
            const cells = new Int32Array(buffer); cells[1] = 7;
            make.setTable(o, ['a'], cells, 1, 7);
            """);
        results.Add((string)engine.Evaluate("o[0] + calls"));
        // (the buffer transferred: the guard's array is detached, its memory now the new buffer's)
        results.Add((string)engine.Evaluate("globalThis.moved = buffer.transfer(); o[0] + calls"));
        results.Add((string)engine.Evaluate("new Int32Array(moved)[1] = 8; o[0] + ',' + calls"));
        // (the new buffer dropped and collected: the guard still has the memory)
        engine.Execute("globalThis.moved = undefined; globalThis.buffer = undefined;");
        engine.CollectGarbage(true);
        results.Add((string)engine.Evaluate("typeof o[0] === 'string' ? 'read' : 'none'"));
        // (a transfer to another size: the guard's memory is the old one, kept)
        engine.Execute("""
            globalThis.p = make(null);
            let wide = new ArrayBuffer(16); const view = new Int32Array(wide); view[0] = 3;
            make.setTable(p, ['b'], view, 0, 3);
            wide = wide.transfer(4096); wide = undefined;
            """);
        engine.CollectGarbage(true);
        results.Add((string)engine.Evaluate("typeof p[0] === 'string' ? 'read' : 'none'"));
        // (an array over a buffer a script can resize, and a detached array: no table)
        results.Add((string)engine.Evaluate("""
            (() => {
                const resizable = new Int32Array(new ArrayBuffer(16, { maxByteLength: 64 }));
                const gone = new ArrayBuffer(16), detached = new Int32Array(gone); gone.transfer();
                return make.setTable(make(null), ['c'], resizable, 0, 0) + ' ' + make.setTable(make(null), ['c'], detached, 0, 0);
            })()
            """));
        // (objects with tables made and dropped: their guards freed with them)
        var made = Convert.ToInt32(engine.Evaluate("""
            (() => {
                let n = 0;
                for (let i = 0; i < 2000; i++) { const q = make(null); if (make.setTable(q, [i], new Int32Array([i, i]), 1, i) && q[0] === i) n++; }
                return n;
            })()
            """));
        engine.CollectGarbage(true);
        results.Add(made + " " + engine.Evaluate("'collected'"));
        return string.Join(":", results);
    }
}

// patches/clearscript/0032: Ssrs2_ContextNumber answers a number for the current context, and
// Ssrs2_SetCodeGenHostCheck(context, check, message, length) gives a context a check of code generation that is a C
// function of the host's: asked before a string is compiled (eval, the Function constructors), for a code-like object,
// for any other object given to them, and before WebAssembly is compiled from bytes; it allows, refuses (with its own
// message or the one given here) or allows with another text. Checked with toy sources: a string allowed, a direct eval
// keeping its scope, a refusal with the default message and with the check's own, a replacement, the Function
// constructor allowed and refused, a number (not asked), a plain object and a code-like one, a check that runs script
// which evals again (entered twice), WebAssembly allowed then refused, the check taken away; a second context of the
// same isolate unchecked, then given the check by its number from the first and freed of it; a number that names no
// context any more.
static unsafe class CodeGenHostChecks
{
    public const string Expected =
        "true:2:2:EvalError refused by the host:EvalError the check's own words:42:3:EvalError refused by the host:5:true:42:42:7"
        + ":[object WebAssembly.Module]:CompileError WebAssembly.Module(): no WebAssembly here:[object WebAssembly.Module]"
        + ":strings 10 code-like 1 objects 3 wasm 3 nested 1 contexts 1"
        + ":NOPE:NOPE [object WebAssembly.Module]:true EvalError Code generation from strings disallowed for this context:true NOPE:0";

    [StructLayout(LayoutKind.Sequential)]
    struct Answer { public nint Source; public int SourceLength; public nint Message; public int MessageLength; }
    [StructLayout(LayoutKind.Sequential)]
    struct Value { public int Kind; public int Length; public double Number; public nint Data; }

    static delegate* unmanaged[Stdcall]<int, int, Value*, int, Value*, int> s_call;
    static readonly int[] s_counts = new int[8];
    static readonly HashSet<int> s_contexts = new();
    static bool s_wasm = true;
    static int s_depth, s_nested;
    // (the answers' texts, the host's until its next answer: kept for the process here)
    static readonly nint s_swap = Marshal.StringToHGlobalUni("40 + 2"), s_words = Marshal.StringToHGlobalUni("the check's own words"),
        s_noWasm = Marshal.StringToHGlobalUni("no WebAssembly here");
    static nint s_made;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static int HostCheck(int context, int flags, char* source, int length, Answer* answer)
    {
        s_counts[flags]++;
        s_contexts.Add(context);
        if (s_depth > 0) s_nested++;
        if ((flags & 1) != 0)
        {
            if (s_wasm) return 1;
            answer->Message = s_noWasm; answer->MessageLength = "no WebAssembly here".Length;
            return 0;
        }
        if ((flags & 4) != 0) return source == null && length == 0 ? 1 : 0;
        var text = new string(source, 0, length);
        if (text.Contains("NOPE")) return 0;
        if (text.Contains("WORDS")) { answer->Message = s_words; answer->MessageLength = "the check's own words".Length; return 0; }
        if (text.Contains("SWAP")) { answer->Source = s_swap; answer->SourceLength = "40 + 2".Length; return 2; }
        if (text.Contains("NEST"))
        {
            // (script run from inside the check: the function under handle 3 evals a string itself, and its answer, a
            // string, is the text compiled in place of this one)
            Value made;
            s_depth++;
            int called = s_call(3, 0, null, 0, &made);
            s_depth--;
            if (called != 1 || made.Kind != 4) return 0;
            if (s_made != 0) Marshal.FreeHGlobal(s_made);
            s_made = Marshal.StringToHGlobalUni(new string((char*)made.Data, 0, made.Length));
            answer->Source = s_made; answer->SourceLength = made.Length;
            return 2;
        }
        return 1;
    }

    public static string Check(string library)
    {
        var handle = NativeLibrary.Load(library);
        bool Export(string name, int bytes, out nint export) =>
            NativeLibrary.TryGetExport(handle, name, out export) || NativeLibrary.TryGetExport(handle, "_" + name + "@" + bytes, out export);
        if (!Export("Ssrs2_ContextNumber", 0, out var numberExport) || !Export("Ssrs2_SetCodeGenHostCheck", 16, out var setExport)
            || !Export("Ssrs2_InstallHandles", 8, out var handlesExport) || !Export("Ssrs2_Call", 20, out var callExport)
            || !Export("Ssrs2_InstallCodeLike", 8, out var codeLikeExport)) return "export missing";
        var number = (delegate* unmanaged[Stdcall]<int>)numberExport;
        var set = (delegate* unmanaged[Stdcall]<int, delegate* unmanaged[Cdecl]<int, int, char*, int, Answer*, int>, char*, int, int>)setExport;
        var handles = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)handlesExport;
        var codeLike = (delegate* unmanaged[Stdcall]<byte*, byte*, int>)codeLikeExport;
        s_call = (delegate* unmanaged[Stdcall]<int, int, Value*, int, Value*, int>)callExport;

        using var runtime = new V8Runtime();
        using var engine = runtime.CreateScriptEngine();
        var other = runtime.CreateScriptEngine();
        int own = 0, others = 0;
        engine.AddHostObject("install", new Func<bool>(() =>
        {
            fixed (byte* holder = "__toy\0"u8, table = "handles\0"u8, maker = "codeLike\0"u8)
            {
                if (handles(holder, table) != 1 || codeLike(holder, maker) != 1) return false;
            }
            own = number();
            fixed (char* message = "refused by the host") return set(0, &HostCheck, message, "refused by the host".Length) == own && own > 0 && number() == own;
        }));
        engine.AddHostObject("wasm", new Action<bool>(on => s_wasm = on));
        engine.AddHostObject("remove", new Func<bool>(() => set(0, null, null, 0) == own));
        // (the other context's check set and taken away from this one, by its number)
        engine.AddHostObject("checkOther", new Func<bool>(() => set(others, &HostCheck, null, 0) == others));
        engine.AddHostObject("freeOther", new Func<bool>(() => set(others, null, null, 0) == others));
        engine.AddHostObject("deadOther", new Func<int>(() => set(others, &HostCheck, null, 0)));
        other.AddHostObject("number", new Action(() => others = number()));
        other.Execute("number()");

        var results = new List<string>();
        results.Add((string)engine.Evaluate("""
            const e = f => { try { return String(f()); } catch (x) { return x.name + ' ' + x.message; } };
            const r = [String(install())], h = __toy.handles;
            h[3] = () => String(eval('3 + 4'));
            r.push(e(() => eval('1 + 1')));
            r.push(e(() => (function () { let local = 1; eval('local = 2'); return local; })()));
            r.push(e(() => eval('"NOPE"')));
            r.push(e(() => eval('"WORDS"')));
            r.push(e(() => eval('SWAP')));
            r.push(e(() => new Function('return 3')()));
            r.push(e(() => new Function('NOPE')));
            r.push(e(() => eval(5)));
            const plain = { toString() { return 'return 6 * 7'; } };
            r.push(e(() => eval(plain) === plain && h[1] === plain));
            r.push(e(() => new Function(plain)()));
            const code = new __toy.codeLike(); code.toString = () => '6 * 7';
            r.push(e(() => eval(code)));
            r.push(e(() => eval('NEST')));
            const bytes = new Uint8Array([0, 97, 115, 109, 1, 0, 0, 0]);
            r.push(e(() => new WebAssembly.Module(bytes)));
            wasm(false); r.push(e(() => new WebAssembly.Module(bytes))); wasm(true);
            r.push(e(() => new WebAssembly.Module(bytes)));
            r.join(':')
            """));
        // (how often the check was asked: the plain object counts twice for its direct eval, which V8 resolves and then
        // calls as the global eval, and once as the Function constructor's argument)
        results.Add($"strings {s_counts[0]} code-like {s_counts[2]} objects {s_counts[4]} wasm {s_counts[1]} nested {s_nested} contexts {s_contexts.Count}");
        // (the check taken away: strings compile unchecked)
        results.Add((string)engine.Evaluate("remove() ? eval('\"NOPE\"') : 'kept'"));
        // (the second context: unchecked, then given the check by its number from the first, then freed of it)
        s_wasm = false;
        results.Add((string)other.Evaluate("eval('\"NOPE\"') + ' ' + new WebAssembly.Module(new Uint8Array([0, 97, 115, 109, 1, 0, 0, 0]))"));
        s_wasm = true;
        var given = (bool)engine.Evaluate("checkOther()");
        results.Add(given.ToString().ToLowerInvariant() + " " + (string)other.Evaluate("(() => { try { return eval('\"NOPE\"'); } catch (x) { return x.name + ' ' + x.message; } })()"));
        var freed = (bool)engine.Evaluate("freeOther()");
        results.Add(freed.ToString().ToLowerInvariant() + " " + (string)other.Evaluate("eval('\"NOPE\"')"));
        // (the second context gone: its number names nothing)
        other.Dispose();
        results.Add(Convert.ToInt32(engine.Evaluate("deadOther()")).ToString());
        return string.Join(":", results);
    }
}

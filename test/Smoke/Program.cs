// dotnet Smoke.dll <folder>: runs smoke.js with the ClearScriptV8 library in <folder> and checks its answers against
// expected.txt, stock V8's (Node's) answers; and that the date's local time is UTC whatever the machine's time zone, as
// our V8 has it (the workflow sets another zone first).
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.ClearScript.V8;

// (ClearScript loads its library from the app's folder on .NET)
foreach (var library in Directory.GetFiles(args[0], "ClearScriptV8.*"))
    File.Copy(library, Path.Combine(AppContext.BaseDirectory, Path.GetFileName(library)), overwrite: true);
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
if (answer == expected && offset == 0 && calls == HostCalls.Expected && modules == Modules.Expected && microtasks == Microtasks.Expected && realms == CreationContexts.Expected
    && scheduled == ImportSchedulers.Expected && heap == HeapLimits.Expected && referrer == KeptReferrers.Expected && codeLike == CodeLikes.Expected && undetectable == Undetectables.Expected && noReferrer == NoReferrers.Expected && incumbent == Incumbents.Expected
    && evalCallers == EvalCallers.Expected && links == ContextLinks.Expected && wasmMemory == WasmMemoryLimits.Expected) return 0;
Console.Error.WriteLine($"expected:\n{expected}\ntime zone offset 0\nhost calls: {HostCalls.Expected}\nmodules: {Modules.Expected}\nmicrotasks: {Microtasks.Expected}\ncreation contexts: {CreationContexts.Expected}\nimport scheduler: {ImportSchedulers.Expected}\nheap limit: {HeapLimits.Expected}\nkept referrer: {KeptReferrers.Expected}\ncode-like: {CodeLikes.Expected}\nundetectable: {Undetectables.Expected}\nno referrer: {NoReferrers.Expected}\nincumbent: {Incumbents.Expected}\neval callers: {EvalCallers.Expected}\ncontext links: {ContextLinks.Expected}\nwasm memory limit: {WasmMemoryLimits.Expected}");
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

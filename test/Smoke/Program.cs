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
if (answer == expected && offset == 0 && calls == HostCalls.Expected && modules == Modules.Expected && microtasks == Microtasks.Expected) return 0;
Console.Error.WriteLine($"expected:\n{expected}\ntime zone offset 0\nhost calls: {HostCalls.Expected}\nmodules: {Modules.Expected}\nmicrotasks: {Microtasks.Expected}");
return 1;

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
// module that throws rejects with its error.
static class Modules
{
    public const string Expected = "hello|loader saw ./t.txt type=text|bad module";

    sealed class Loader : Microsoft.ClearScript.DocumentLoader
    {
        public string Seen = "";

        public override Microsoft.ClearScript.Document LoadDocument(Microsoft.ClearScript.DocumentSettings settings, Microsoft.ClearScript.DocumentInfo? sourceInfo,
            string specifier, Microsoft.ClearScript.DocumentCategory category, Microsoft.ClearScript.DocumentContextCallback contextCallback)
        {
            int marker = specifier.IndexOf("\0type=", StringComparison.Ordinal);
            if (marker >= 0)
            {
                Seen = $"{specifier[..marker]} type={specifier[(marker + 6)..]}";
                return new Microsoft.ClearScript.StringDocument(new Microsoft.ClearScript.DocumentInfo(new Uri("file:///t.txt")) { Category = Microsoft.ClearScript.DocumentCategory.Json }, "\"hello\"");
            }
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
            "import t from './t.txt' with { type: 'text' }; globalThis.moduleText = t;"
            + " import('./bad.js').then(() => { globalThis.moduleError = 'resolved'; }, e => { globalThis.moduleError = e.message; });");
        return $"{engine.Script.moduleText}|loader saw {loader.Seen}|{engine.Script.moduleError}";
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

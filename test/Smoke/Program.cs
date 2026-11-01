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
if (answer == expected && offset == 0 && calls == HostCalls.Expected) return 0;
Console.Error.WriteLine($"expected:\n{expected}\ntime zone offset 0\nhost calls: {HostCalls.Expected}");
return 1;

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

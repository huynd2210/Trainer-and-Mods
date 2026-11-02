using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Disassembler;
using System.Text;

// usage: Decomp <dll> <outDir> <stateFile> [--il]
// One file per top-level type. Writes the name of the type being processed into stateFile before
// starting it so a driver can skip a type that crashes the process (stack overflow) and restart.
string dll = args[0], outDir = args[1], state = args[2];
Directory.CreateDirectory(outDir);
var skip = new HashSet<string>(File.Exists(state + ".skip") ? File.ReadAllLines(state + ".skip") : new string[0]);
var resolver = new UniversalAssemblyResolver(dll, false, null);
resolver.AddSearchDirectory(Path.GetDirectoryName(dll));
var settings = new DecompilerSettings { ThrowOnAssemblyResolveErrors = false };
var dec = new CSharpDecompiler(dll, resolver, settings);
var types = dec.TypeSystem.MainModule.TypeDefinitions.Where(t => t.DeclaringTypeDefinition == null).ToList();
int done = 0, skipped = 0, failed = 0;
var th = new Thread(() => {
foreach (var t in types)
{
    string name = t.FullName;
    string safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    string path = Path.Combine(outDir, safe + ".cs");
    if (File.Exists(path)) { done++; continue; }
    if (skip.Contains(name)) { skipped++; continue; }
    File.WriteAllText(state, name);
    try
    {
        string src = dec.DecompileTypeAsString(new FullTypeName(name));
        File.WriteAllText(path, src, Encoding.UTF8);
        done++;
    }
    catch (Exception e)
    {
        File.WriteAllText(path, "// DECOMPILE FAILED: " + e.Message);
        failed++;
    }
}
}, 64*1024*1024); th.Start(); th.Join();
File.WriteAllText(state, "DONE");
Console.WriteLine($"types={types.Count} done={done} skipped={skipped} failed={failed}");


using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using System.Text;

// usage: DecompMembers <dll> <outDir> <stateFile> <typeListFile>
// Member-by-member decompilation for types whole-type decompilation cannot finish (the decompiler loops
// forever on some methods). A member that hangs is recorded in <stateFile>.skip and emitted as IL instead.
// <stateFile> holds "<type>|<memberIndex>" before each member so a driver can kill and restart this process.
string dll = args[0], outDir = args[1], state = args[2], listFile = args[3];
Directory.CreateDirectory(outDir);
var skipMembers = new HashSet<string>(File.Exists(state + ".skip") ? File.ReadAllLines(state + ".skip") : new string[0]);
var progress = new Dictionary<string, int>();
string progressFile = state + ".progress";
if (File.Exists(progressFile))
    foreach (var l in File.ReadAllLines(progressFile)) { var p = l.Split('='); progress[p[0]] = int.Parse(p[1]); }

var resolver = new UniversalAssemblyResolver(dll, false, null);
resolver.AddSearchDirectory(Path.GetDirectoryName(dll));
var settings = new DecompilerSettings { ThrowOnAssemblyResolveErrors = false };
var dec = new CSharpDecompiler(dll, resolver, settings);
var module = dec.TypeSystem.MainModule;
var pe = new PEFile(dll);

IEnumerable<ITypeDefinition> Flatten(ITypeDefinition t)
{
    yield return t;
    foreach (var n in t.NestedTypes) foreach (var x in Flatten(n)) yield return x;
}

var th = new Thread(() =>
{
    foreach (string typeName in File.ReadAllLines(listFile).Where(s => s.Length > 0))
    {
        var type = module.TypeDefinitions.FirstOrDefault(t => t.DeclaringTypeDefinition == null && t.FullName == typeName);
        if (type == null) continue;
        string safe = string.Concat(typeName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = Path.Combine(outDir, safe + ".cs");
        var members = Flatten(type).SelectMany(t => t.Members.Where(m => m.DeclaringTypeDefinition == t || true).Select(m => (t, m)))
            .Where(x => x.m is not IMethod mm || (mm.AccessorOwner == null)).ToList();
        int start = progress.TryGetValue(typeName, out int s) ? s : 0;
        if (start >= members.Count + 1) continue;
        if (start == 0)
        {
            File.WriteAllText(path, "// MEMBER-BY-MEMBER DECOMPILE of " + typeName + " (whole-type decompile hangs). Members that hang are IL.\n// Base: " + string.Join(", ", type.DirectBaseTypes.Select(b => b.FullName)) + "\n");
        }
        for (int i = start; i < members.Count; i++)
        {
            var (t, m) = members[i];
            File.WriteAllText(state, typeName + "|" + i);
            string text;
            string key = typeName + "|" + i;
            if (skipMembers.Contains(key))
            {
                var sb = new StringBuilder();
                sb.AppendLine("// [IL fallback: C# decompile of this member hangs] " + t.FullName + "::" + m.Name);
                try
                {
                    var o = new PlainTextOutput(new StringWriter(sb));
                    var dis = new ReflectionDisassembler(o, CancellationToken.None);
                    IEnumerable<IMethod> ms = m is IMethod mm2 ? new[] { mm2 } : m is IProperty pr ? new[] { pr.Getter, pr.Setter }.Where(x => x != null) : m is IEvent ev ? new[] { ev.AddAccessor, ev.RemoveAccessor }.Where(x => x != null) : Enumerable.Empty<IMethod>();
                    foreach (var meth in ms)
                        if (meth.MetadataToken.IsNil == false)
                            dis.DisassembleMethod(pe, (MethodDefinitionHandle)meth.MetadataToken);
                }
                catch (Exception e) { sb.AppendLine("// IL failed: " + e.Message); }
                text = sb.ToString();
            }
            else
            {
                try { text = "// " + t.FullName + "::" + m.Name + "\n" + dec.DecompileAsString(new[] { m.MetadataToken }); }
                catch (Exception e) { text = "// FAILED " + t.FullName + "::" + m.Name + ": " + e.Message + "\n"; }
            }
            File.AppendAllText(path, text + "\n");
            progress[typeName] = i + 1;
            File.WriteAllLines(progressFile, progress.Select(kv => kv.Key + "=" + kv.Value));
        }
        progress[typeName] = members.Count + 1;
        File.WriteAllLines(progressFile, progress.Select(kv => kv.Key + "=" + kv.Value));
    }
});
th.Start(); th.Join();
File.WriteAllText(state, "DONE");
Console.WriteLine("done");

using Mono.Cecil;
using Mono.Cecil.Cil;

// usage: Callers <dll> <Type.Member substring>  -> every method whose IL references a member whose "Type::Member" contains the text
var asm = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { InMemory = true });
string needle = args[1];
int hits = 0;
// usage: Callers <dll> implements:<InterfaceName>  -> every type implementing an interface whose name contains the text
if (needle.StartsWith("implements:"))
{
    string iface = needle.Substring("implements:".Length);
    foreach (var type in asm.MainModule.GetTypes())
        if (type.HasInterfaces && type.Interfaces.Any(i => i.InterfaceType.Name.Contains(iface))) { Console.WriteLine(type.FullName); hits++; }
    Console.WriteLine($"-- {hits} type(s)");
    return;
}
IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts) { foreach (var t in ts) { yield return t; foreach (var n in All(t.NestedTypes)) yield return n; } }
foreach (var t in All(asm.MainModule.Types))
    foreach (var m in t.Methods)
    {
        if (!m.HasBody) continue;
        foreach (var ins in m.Body.Instructions)
        {
            string name = ins.Operand switch
            {
                MethodReference mr => mr.DeclaringType.FullName + "::" + mr.Name,
                FieldReference fr => fr.DeclaringType.FullName + "::" + fr.Name,
                _ => null
            };
            if (name != null && name.Contains(needle)) { Console.WriteLine($"{t.FullName}::{m.Name}  [{ins.OpCode}] {name}"); hits++; }
        }
    }
Console.WriteLine($"-- {hits} reference(s)");

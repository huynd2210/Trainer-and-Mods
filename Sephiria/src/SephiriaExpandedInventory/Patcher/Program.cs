using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2 || (args[0] != "install" && args[0] != "uninstall"))
{
    Console.Error.WriteLine("Usage: Patcher install|uninstall <game directory>");
    return 2;
}

string game = Path.GetFullPath(args[1]);
string managed = Path.Combine(game, "Sephiria_Data", "Managed");
string assemblyPath = Path.Combine(managed, "Assembly-CSharp.dll");
string modPath = Path.Combine(managed, "SephiriaExpandedInventory.dll");
string builtMod = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..", "..", "..", "..", "ExpandedInventory", "bin", "Release", "netstandard2.1", "SephiriaExpandedInventory.dll"));
if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Game assembly not found", assemblyPath);
if (args[0] == "install" && !File.Exists(builtMod)) throw new FileNotFoundException("Build ExpandedInventory first", builtMod);

string temp = assemblyPath + ".expandedinventory-new";
bool changed = false;
try
{
    using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true }))
    {
        ModuleDefinition module = assembly.MainModule;
        TypeDefinition inventory = module.Types.Single(t => t.Name == "GridInventory");
        TypeDefinition panel = module.Types.Single(t => t.Name == "UI_CharacterStatusPanel");
        MethodDefinition inventoryStart = inventory.Methods.Single(m => m.Name == "OnStartServer" && m.Parameters.Count == 0);
        MethodDefinition? oldPlayerStart = module.Types.Single(t => t.Name == "PlayerAvatar").Methods.Single(m => m.Name == "OnStartServer" && m.Parameters.Count == 0);
        MethodDefinition panelOpen = panel.Methods.Single(m => m.Name == "OnOpened" && m.Parameters.Count == 0);
        MethodDefinition createIcons = panel.Methods.Single(m => m.Name == "CreateItemIcons" && m.Parameters.Count == 3);

        if (args[0] == "install")
        {
            AssemblyNameReference reference = module.AssemblyReferences.FirstOrDefault(r => r.Name == "SephiriaExpandedInventory")
                ?? new AssemblyNameReference("SephiriaExpandedInventory", new Version(1, 0, 0, 0));
            if (!module.AssemblyReferences.Contains(reference)) module.AssemblyReferences.Add(reference);
            TypeReference hookType = new TypeReference("SephiriaExpandedInventory", "InventoryHooks", module, reference);
            changed |= RemoveHook(oldPlayerStart, "ExpandPlayerInventory");
            changed |= AddHook(inventoryStart, hookType, "ExpandPlayerInventory", inventory);
            changed |= AddHook(panelOpen, hookType, "ConfigurePanel", panel);
            changed |= AddHook(createIcons, hookType, "ConfigureIcons", panel);
        }
        else
        {
            changed |= RemoveHook(oldPlayerStart, "ExpandPlayerInventory");
            changed |= RemoveHook(inventoryStart, "ExpandPlayerInventory");
            changed |= RemoveHook(panelOpen, "ConfigurePanel");
            changed |= RemoveHook(createIcons, "ConfigureIcons");
            if (changed)
                foreach (AssemblyNameReference reference in module.AssemblyReferences.Where(r => r.Name == "SephiriaExpandedInventory").ToArray())
                    module.AssemblyReferences.Remove(reference);
        }

        if (changed) assembly.Write(temp);
    }

    if (changed)
    {
        File.Copy(assemblyPath, assemblyPath + ".expandedinventory-last", true);
        if (args[0] == "install") File.Copy(builtMod, modPath, true);
        File.Copy(temp, assemblyPath, true);
    }
    else if (args[0] == "install")
    {
        File.Copy(builtMod, modPath, true);
    }

    if (args[0] == "uninstall" && File.Exists(modPath)) File.Delete(modPath);
    Console.WriteLine(args[0] == "install" ? "Expanded inventory installed." : "Expanded inventory removed.");
}
finally
{
    if (File.Exists(temp)) File.Delete(temp);
}
return 0;

static bool AddHook(MethodDefinition target, TypeReference hooks, string name, TypeDefinition argument)
{
    if (FindHook(target, name) != null) return false;
    MethodReference call = new MethodReference(name, target.Module.TypeSystem.Void, hooks) { HasThis = false };
    call.Parameters.Add(new ParameterDefinition(argument));
    ILProcessor il = target.Body.GetILProcessor();
    foreach (Instruction ret in target.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
    {
        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, call));
    }
    return true;
}

static bool RemoveHook(MethodDefinition target, string name)
{
    bool changed = false;
    ILProcessor il = target.Body.GetILProcessor();
    foreach (Instruction call in target.Body.Instructions.Where(i => IsHook(i, name)).ToArray())
    {
        int index = target.Body.Instructions.IndexOf(call);
        if (index < 1 || target.Body.Instructions[index - 1].OpCode != OpCodes.Ldarg_0)
            throw new InvalidOperationException("Unexpected hook form in " + target.FullName);
        Instruction load = target.Body.Instructions[index - 1];
        if (target.Body.Instructions.Any(i => ReferenceEquals(i.Operand, load) || ReferenceEquals(i.Operand, call)))
            throw new InvalidOperationException("Hook is a branch target in " + target.FullName);
        il.Remove(call);
        il.Remove(load);
        changed = true;
    }
    return changed;
}

static Instruction? FindHook(MethodDefinition target, string name) => target.Body.Instructions.FirstOrDefault(i => IsHook(i, name));

static bool IsHook(Instruction instruction, string name) => instruction.OpCode == OpCodes.Call &&
    instruction.Operand is MethodReference method && method.Name == name &&
    method.DeclaringType.FullName == "SephiriaExpandedInventory.InventoryHooks";

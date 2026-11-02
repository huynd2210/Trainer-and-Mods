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
string modPath = Path.Combine(managed, "SephiriaStatBuff.dll");
string configPath = Path.Combine(game, "SephiriaStatBuff.cfg");
string builtMod = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..", "..", "..", "..", "StatBuff", "bin", "Release", "netstandard2.1", "SephiriaStatBuff.dll"));
if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Game assembly not found", assemblyPath);
if (args[0] == "install" && !File.Exists(builtMod)) throw new FileNotFoundException("Build StatBuff first", builtMod);

string temp = assemblyPath + ".statbuff-new";
try
{
    using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true }))
    {
        var module = assembly.MainModule;
        var player = module.Types.Single(t => t.Name == "PlayerLocalDataStorage");
        var update = player.Methods.Single(m => m.Name == "Update" && m.Parameters.Count == 0);
        var instructions = update.Body.Instructions;
        var hookCall = instructions.FirstOrDefault(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m &&
            m.DeclaringType.FullName == "SephiriaStatBuff.StatBuffHooks" && m.Name == "Tick");

        if (args[0] == "install")
        {
            if (hookCall == null)
            {
                var reference = new AssemblyNameReference("SephiriaStatBuff", new Version(1, 0, 0, 0));
                module.AssemblyReferences.Add(reference);
                var hookType = new TypeReference("SephiriaStatBuff", "StatBuffHooks", module, reference);
                var tick = new MethodReference("Tick", module.TypeSystem.Void, hookType) { HasThis = false };
                tick.Parameters.Add(new ParameterDefinition(player));
                var il = update.Body.GetILProcessor();
                var first = instructions[0];
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, il.Create(OpCodes.Call, tick));
                assembly.Write(temp);
            }
        }
        else if (hookCall != null)
        {
            int index = instructions.IndexOf(hookCall);
            if (index == 0 || instructions[index - 1].OpCode != OpCodes.Ldarg_0)
                throw new InvalidOperationException("Stat buff hook has an unexpected form; refusing to remove it.");
            var load = instructions[index - 1];
            if (instructions.Any(i => i.Operand == load || i.Operand == hookCall))
                throw new InvalidOperationException("Stat buff hook is a branch target; refusing to remove it.");
            var il = update.Body.GetILProcessor();
            il.Remove(hookCall);
            il.Remove(load);
            foreach (var reference in module.AssemblyReferences.Where(r => r.Name == "SephiriaStatBuff").ToArray())
                module.AssemblyReferences.Remove(reference);
            assembly.Write(temp);
        }
    }

    if (File.Exists(temp))
    {
        // Keep a recovery copy of exactly the version we are replacing.
        File.Copy(assemblyPath, assemblyPath + ".statbuff-last", true);
        File.Copy(temp, assemblyPath, true);
    }
    if (args[0] == "install")
    {
        File.Copy(builtMod, modPath, true);
        if (!File.Exists(configPath))
            File.Copy(Path.Combine(AppContext.BaseDirectory, "SephiriaStatBuff.cfg"), configPath);
        Console.WriteLine("Stat buff installed. F8 toggles; F9 reloads config.");
    }
    else
    {
        if (File.Exists(modPath)) File.Delete(modPath);
        Console.WriteLine("Stat buff removed. Config retained.");
    }
}
finally
{
    if (File.Exists(temp)) File.Delete(temp);
}
return 0;

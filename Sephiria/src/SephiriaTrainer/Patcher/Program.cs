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

var gameDirectory = Path.GetFullPath(args[1]);
var managed = Path.Combine(gameDirectory, "Sephiria_Data", "Managed");
var assemblyPath = Path.Combine(managed, "Assembly-CSharp.dll");
var backupPath = assemblyPath + ".trainer-original";
var trainerPath = Path.Combine(managed, "SephiriaTrainer.dll");
if (!File.Exists(assemblyPath))
    throw new FileNotFoundException("Game assembly not found", assemblyPath);

if (args[0] == "uninstall")
{
    if (!File.Exists(backupPath))
        throw new FileNotFoundException("Trainer backup not found", backupPath);
    File.Copy(backupPath, assemblyPath, true);
    if (File.Exists(trainerPath)) File.Delete(trainerPath);
    Console.WriteLine("Trainer removed; original assembly restored.");
    return 0;
}

var builtTrainer = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..", "..", "..", "..", "TrainerHooks", "bin", "Release", "netstandard2.1", "SephiriaTrainer.dll"));
if (!File.Exists(builtTrainer))
    throw new FileNotFoundException("Build TrainerHooks first", builtTrainer);

var temporary = assemblyPath + ".trainer-new";
try
{
    using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true }))
    {
        var module = assembly.MainModule;
        var playerType = module.Types.Single(t => t.Name == "PlayerLocalDataStorage");
        var update = playerType.Methods.Single(m => m.Name == "Update" && m.Parameters.Count == 0);
        if (update.Body.Instructions.Any(i => i.Operand is MethodReference m &&
            m.DeclaringType.FullName == "SephiriaTrainer.TrainerHooks"))
        {
            Console.WriteLine("Trainer already installed.");
            return 0;
        }

        if (File.Exists(backupPath))
            throw new InvalidOperationException("An existing trainer backup was found. Uninstall first before installing again.");

        var trainerReference = new AssemblyNameReference("SephiriaTrainer", new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(trainerReference);
        var hookType = new TypeReference("SephiriaTrainer", "TrainerHooks", module, trainerReference);
        var tick = new MethodReference("Tick", module.TypeSystem.Void, hookType) { HasThis = false };
        tick.Parameters.Add(new ParameterDefinition(playerType));
        var il = update.Body.GetILProcessor();
        var first = update.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, tick));
        assembly.Write(temporary);
    }

    File.Copy(assemblyPath, backupPath);
    File.Copy(builtTrainer, trainerPath);
    File.Copy(temporary, assemblyPath, true);
}
catch
{
    if (File.Exists(backupPath) && File.Exists(temporary))
        File.Copy(backupPath, assemblyPath, true);
    if (File.Exists(trainerPath) && File.Exists(temporary)) File.Delete(trainerPath);
    throw;
}
finally
{
    if (File.Exists(temporary)) File.Delete(temporary);
}

Console.WriteLine("Trainer installed. F6: +1000 saphires; F7: +10000 gold.");
return 0;

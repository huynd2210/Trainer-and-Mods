// Headless stand-in for MSL's Compile + Patch buttons:
//   pack <modSourceDir> <outDir>                  -> <outDir>/<Mod>.sml
//   patch <sml> <data.win in> <data.win out> <logDir>  -> runs the mod's PatchMod and saves
using ModShardLauncher; using ModShardLauncher.Mods; using UndertaleModLib; using UndertaleModLib.Models; using UndertaleModLib.Decompiler;
using System.Reflection; using System.IO;
var mode = args[0];
if (mode == "pack") {
    Directory.CreateDirectory(args[2]);
    var r = ModShardPackerReference.FilePacker.Pack(null, args[1], args[2], args[1], "v0.13.2.0", new Type[] { typeof(Mod), typeof(UndertaleCode) });
    Console.WriteLine($"pack result: {r}");
    foreach (var p in Directory.GetFiles(args[2], "*.sml")) Console.WriteLine($"  {p} {new FileInfo(p).Length} bytes");
    return;
}
if (mode != "patch") { Console.WriteLine($"unknown mode {mode}"); Environment.Exit(2); }
var (sml, inWin, outWin, logDir) = (args[1], args[2], args[3], args[4]); Directory.CreateDirectory(logDir);
var sw = System.Diagnostics.Stopwatch.StartNew();
using (var fs = new FileStream(inWin, FileMode.Open, FileAccess.Read, FileShare.Read))
    DataLoader.data = UndertaleIO.Read(fs, w => Console.WriteLine("WARN " + w), m => {});
Console.WriteLine($"loaded {sw.Elapsed.TotalSeconds:F0}s rooms={DataLoader.data.Rooms.Count} code={DataLoader.data.Code.Count}");
foreach (var one in sml.Split(';')) {
    var f = FileReader.Read(one) ?? throw new Exception("FileReader.Read returned null for " + one);
    var modType = f.Assembly.GetTypes().First(t => t.IsSubclassOf(typeof(Mod)));
    var mod = (Mod)Activator.CreateInstance(modType); mod.LoadAssembly(); mod.ModFiles = f; f.instance = mod;
    Console.WriteLine($"mod {mod.Name} {mod.Version} (packed with {f.Version})");
    mod.PatchMod();
}
Console.WriteLine($"patched {sw.Elapsed.TotalSeconds:F0}s rooms={DataLoader.data.Rooms.Count} code={DataLoader.data.Code.Count}");
var ctx = new GlobalDecompileContext(DataLoader.data, false);
foreach (var n in args.Skip(5)) {
    var c = DataLoader.data.Code.ByName(n);
    File.WriteAllText(Path.Combine(logDir, n + ".gml"), c == null ? "// MISSING" : Decompiler.Decompile(c, ctx, null));
}
using (var fs = new FileStream(outWin, FileMode.Create, FileAccess.Write))
    UndertaleIO.Write(fs, DataLoader.data, m => {});
Console.WriteLine($"saved {outWin} {new FileInfo(outWin).Length} bytes in {sw.Elapsed.TotalSeconds:F0}s");

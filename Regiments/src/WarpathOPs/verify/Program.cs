using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Loads the real game assembly (MainAssembly.dll) with MetadataLoadContext and asserts that
/// every member the plugin patches or touches exists with the expected shape. Members named by
/// string (the private econProvider field) compile fine but would fail at runtime inside
/// Harmony; this catches that before the mod ships.
///
/// Usage: dotnet run --project Verify.csproj -- <ManagedDir>
/// </summary>
internal static class Program
{
    private static int failures;

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: VerifySignatures <ManagedDir>");
            return 2;
        }
        string managedDir = Path.GetFullPath(args[0]);
        var resolver = new PathAssemblyResolver(Directory.GetFiles(managedDir, "*.dll"));
        using (var mlc = new MetadataLoadContext(resolver))
        {
            Assembly asm = mlc.LoadFromAssemblyPath(Path.Combine(managedDir, "MainAssembly.dll"));

            Type econ = asm.GetType("birdseye.Regiments.Menu.GUI.CampaignMenu.RegManagerEconomics");
            Check(econ != null, "RegManagerEconomics exists");
            FieldInfo provider = econ?.GetField("econProvider", All);
            Check(provider != null && provider.FieldType.Name == "IEconDataProvider", "RegManagerEconomics.econProvider : IEconDataProvider");
            Check(econ?.GetProperty("OPs", All)?.GetGetMethod() != null, "RegManagerEconomics.OPs getter");
            Check(econ?.GetMethod("IsAffordable", All) != null, "RegManagerEconomics.IsAffordable");
            Check(econ?.GetMethod("DeductOP", All) != null, "RegManagerEconomics.DeductOP");

            Type procPhase = asm.GetType("birdseye.Regiments.SaveStates.ProcOpPhaseSaveState");
            Check(procPhase?.GetField("playerSave")?.FieldType.Name == "ProcOpPlayerSaveState", "ProcOpPhaseSaveState.playerSave");
            Type procPlayer = asm.GetType("birdseye.Regiments.SaveStates.ProcOpPlayerSaveState");
            Check(procPlayer?.GetField("OPs")?.FieldType.FullName == "System.Int32", "ProcOpPlayerSaveState.OPs : int");
            Check(procPlayer?.GetMethods(All).Count(m => m.Name == "Init") == 1, "ProcOpPlayerSaveState.Init (single overload)");

            Type phase = asm.GetType("birdseye.Regiments.SaveStates.PhaseSaveState");
            Check(phase?.GetField("playerSave")?.FieldType.Name == "PlayerSaveState", "PhaseSaveState.playerSave");
            Check(asm.GetType("birdseye.Regiments.SaveStates.PlayerSaveState")?.GetField("OPs")?.FieldType.FullName == "System.Int32", "PlayerSaveState.OPs : int");

            // Every OP spend must go through RegManagerEconomics; anything else deducting from the
            // save directly would bypass the top-up.
            Type iface = asm.GetType("birdseye.Regiments.Menu.GUI.CampaignMenu.IEconDataProvider");
            Check(iface != null && procPhase.GetInterfaces().Any(i => i == iface), "ProcOpPhaseSaveState implements IEconDataProvider");
        }
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }
}

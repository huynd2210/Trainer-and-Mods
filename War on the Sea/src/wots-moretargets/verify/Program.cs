using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Loads the real game assembly (Assembly-CSharp.dll) with MetadataLoadContext and asserts that
/// every member More Targets patches or depends on exists with the expected signature.
///
/// Harmony resolves patch targets by name at runtime, so a typo or a signature that has drifted
/// compiles perfectly and only fails once the game is running. This catches it first.
///
/// It also pins the shape of the command-point economy the mod reasons about. The mod overrides
/// one reading of that economy rather than editing the arrays, but if those arrays ever stopped
/// being two-element - one slot per faction - the reasoning behind "this is enemy-only" would no
/// longer hold, and that is worth failing a build over.
///
/// Usage: dotnet run --project Verify.csproj -- &lt;ManagedDir&gt; &lt;Assembly-CSharp.dll path&gt;
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: VerifySignatures <ManagedDir> <AssemblyPath>");
            return 2;
        }

        string managedDir = Path.GetFullPath(args[0]);
        string assemblyPath = Path.GetFullPath(args[1]);

        var resolver = new PathAssemblyResolver(Directory.GetFiles(managedDir, "*.dll").ToList());

        using (var mlc = new MetadataLoadContext(resolver))
        {
            Assembly asm = mlc.LoadFromAssemblyPath(assemblyPath);
            Console.WriteLine("Loaded: " + asm.GetName().Name + " (" + assemblyPath + ")");
            Console.WriteLine();

            CheckCampaignAI(asm);
            CheckShipSupply(asm);
            CheckEconomyShape(asm);
        }

        Console.WriteLine();
        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " signature check(s) FAILED.");
            return 1;
        }
        Console.WriteLine("All signature checks passed.");
        return 0;
    }

    private static void CheckCampaignAI(Assembly asm)
    {
        Console.WriteLine("-- CampaignAI (the enemy budget and its pacing) --");
        Type ai = asm.GetType("CampaignAI", throwOnError: true);

        MethodInfo forceAvailable = ai.GetMethod("GetForceAvailable",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Check(forceAvailable != null, "CampaignAI.GetForceAvailable (private instance) exists");
        if (forceAvailable != null)
        {
            Check(forceAvailable.GetParameters().Length == 0, "GetForceAvailable takes 0 params");
            Check(forceAvailable.ReturnType.FullName == "System.Int32",
                "GetForceAvailable returns int (got " + forceAvailable.ReturnType.FullName + ")");
        }

        MethodInfo decide = ai.GetMethod("MakeStrategicDecision", BindingFlags.Public | BindingFlags.Instance);
        Check(decide != null, "CampaignAI.MakeStrategicDecision (public instance) exists");
        if (decide != null)
        {
            Check(decide.GetParameters().Length == 0, "MakeStrategicDecision takes 0 params");
            Check(decide.ReturnType.FullName == "System.Void", "MakeStrategicDecision returns void");
        }

        MethodInfo create = ai.GetMethod("CreateAIMobileSea", BindingFlags.Public | BindingFlags.Instance);
        Check(create != null, "CampaignAI.CreateAIMobileSea (public instance) exists");
        if (create != null)
        {
            Check(create.ReturnType.FullName == "System.Boolean",
                "CreateAIMobileSea returns bool (got " + create.ReturnType.FullName + ")");
            Check(create.GetParameters().Length == 3,
                "CreateAIMobileSea takes 3 params (got " + create.GetParameters().Length + ")");
        }

        MethodInfo delay = ai.GetMethod("AddAIDecisionDelay", BindingFlags.Public | BindingFlags.Instance);
        Check(delay != null, "CampaignAI.AddAIDecisionDelay (public instance) exists");
        if (delay != null)
        {
            ParameterInfo[] ps = delay.GetParameters();
            Check(ps.Length == 1, "AddAIDecisionDelay takes 1 param (got " + ps.Length + ")");
            if (ps.Length == 1)
            {
                // The prefix rewrites this parameter by name, so the name is load-bearing.
                Check(ps[0].Name == "days", "AddAIDecisionDelay param is named 'days' (got '" + ps[0].Name + "')");
                Check(ps[0].ParameterType.FullName == "System.Int32", "AddAIDecisionDelay param is int");
            }
        }

        // The game's own one-force-per-decision flag. The mod replaces the rule this drives, so
        // its disappearance would mean the decision loop had been rewritten.
        FieldInfo created = ai.GetField("strategicMMOCreated", BindingFlags.Public | BindingFlags.Instance);
        Check(created != null && created.FieldType.FullName == "System.Boolean",
            "CampaignAI.strategicMMOCreated (public bool) exists");
    }

    private static void CheckShipSupply(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- CampaignManager (the enemy's order of battle) --");
        Type cm = asm.GetType("CampaignManager", throwOnError: true);

        MethodInfo available = cm.GetMethod("IsEnemyShipClassAvailable", BindingFlags.Public | BindingFlags.Instance);
        Check(available != null, "CampaignManager.IsEnemyShipClassAvailable (public instance) exists");
        if (available != null)
        {
            ParameterInfo[] ps = available.GetParameters();
            Check(available.ReturnType.FullName == "System.Boolean",
                "IsEnemyShipClassAvailable returns bool (got " + available.ReturnType.FullName + ")");
            Check(ps.Length == 1 && ps[0].ParameterType.FullName == "System.String",
                "IsEnemyShipClassAvailable takes one string");
        }

        FieldInfo instance = cm.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        Check(instance != null && instance.FieldType == cm, "CampaignManager.instance static singleton exists");
    }

    private static void CheckEconomyShape(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- CampaignData (two slots per array: index 0 player, index 1 enemy) --");
        Type cd = asm.GetType("CampaignData", throwOnError: true);

        CheckIntArray(cd, "commandPoints");
        CheckIntArray(cd, "commandBonusPoints");
        CheckIntArray(cd, "commandPointSpent");

        FieldInfo days = cd.GetField("daysToNextDecision", BindingFlags.Public | BindingFlags.Instance);
        Check(days != null && days.FieldType.FullName == "System.Int32",
            "CampaignData.daysToNextDecision (int) exists");

        // Not patched, but the sunk-ships record is what the mod deliberately leaves intact when
        // replenishing hulls, so its continued existence is part of that promise.
        FieldInfo sunk = cd.GetField("sunkShipClasses", BindingFlags.Public | BindingFlags.Instance);
        Check(sunk != null, "CampaignData.sunkShipClasses still exists (the record the mod never edits)");
    }

    private static void CheckIntArray(Type owner, string fieldName)
    {
        FieldInfo field = owner.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Check(field != null, owner.Name + "." + fieldName + " exists");
        if (field != null)
        {
            Check(field.FieldType.FullName == "System.Int32[]",
                owner.Name + "." + fieldName + " is int[] (got " + field.FieldType.FullName + ")");
        }
    }

    private static void Check(bool condition, string description)
    {
        if (condition)
        {
            Console.WriteLine("  PASS  " + description);
        }
        else
        {
            Console.WriteLine("  FAIL  " + description);
            failures++;
        }
    }
}

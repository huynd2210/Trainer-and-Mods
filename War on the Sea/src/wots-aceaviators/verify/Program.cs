using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Loads the real game assembly (Assembly-CSharp.dll) with MetadataLoadContext and asserts
/// that every member Ace Aviators patches or reads exists with the expected signature.
///
/// Harmony resolves patch targets by name at runtime, so a typo or a signature that has
/// drifted compiles perfectly and only fails once the game is running. This catches it first.
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

            CheckAimSolutionPatch(asm);
            CheckReleasePatches(asm);
            CheckGuidancePatches(asm);
            CheckDudPatch(asm);
            CheckReadModel(asm);
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

    // --- patch targets -----------------------------------------------------------------

    private static void CheckAimSolutionPatch(Assembly asm)
    {
        Console.WriteLine("-- EngagementAI.SetAttackOffset (random aim error) --");
        Type engagementAI = asm.GetType("EngagementAI", throwOnError: true);
        MethodInfo setOffset = engagementAI.GetMethod("SetAttackOffset",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Check(setOffset != null, "EngagementAI.SetAttackOffset (private instance) exists");
        if (setOffset == null)
        {
            return;
        }
        Check(setOffset.ReturnType.FullName == "UnityEngine.Vector3",
            "SetAttackOffset returns Vector3 (got " + setOffset.ReturnType.FullName + ")");
        ParameterInfo[] ps = setOffset.GetParameters();
        Check(ps.Length == 2, "SetAttackOffset takes 2 params (got " + ps.Length + ")");
        if (ps.Length == 2)
        {
            // The postfix binds the first parameter by name to decide whether it is the player's.
            Check(ps[0].Name == "unit", "SetAttackOffset param 0 is named 'unit' (got '" + ps[0].Name + "')");
            Check(ps[0].ParameterType.Name == "Unit", "SetAttackOffset param 0 is Unit");
            Check(ps[1].ParameterType.FullName == "System.Single", "SetAttackOffset param 1 is float");
        }
    }

    private static void CheckReleasePatches(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- Weapon.FireWeapon (who is shooting) --");
        Type weapon = asm.GetType("Weapon", throwOnError: true);
        MethodInfo fire = weapon.GetMethod("FireWeapon", BindingFlags.Public | BindingFlags.Instance);
        Check(fire != null, "Weapon.FireWeapon (public instance) exists");
        if (fire != null)
        {
            Check(fire.IsVirtual, "Weapon.FireWeapon is virtual");
            Check(fire.GetParameters().Length == 0, "Weapon.FireWeapon takes 0 params");
            Check(fire.ReturnType.FullName == "System.Void", "Weapon.FireWeapon returns void");
        }
        CheckField(weapon, "parentUnit", "Unit");
        CheckField(weapon, "ammoType", "AmmoType");

        // Aircraft bomb racks are WeaponRepeat, which overrides FireWeapon and chains to the
        // base — that chaining is what lets one patch on Weapon cover them.
        Type weaponRepeat = asm.GetType("WeaponRepeat", throwOnError: true);
        Check(weaponRepeat.BaseType != null && weaponRepeat.BaseType.Name == "Weapon",
            "WeaponRepeat derives from Weapon");
        MethodInfo repeatFire = weaponRepeat.GetMethod("FireWeapon",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Check(repeatFire != null, "WeaponRepeat declares its own FireWeapon override");

        Console.WriteLine();
        Console.WriteLine("-- AmmunitionMoveBallistic.InitialiseBallistics (registration) --");
        Type ballistic = asm.GetType("AmmunitionMoveBallistic", throwOnError: true);
        MethodInfo init = ballistic.GetMethod("InitialiseBallistics", BindingFlags.Public | BindingFlags.Instance);
        Check(init != null, "AmmunitionMoveBallistic.InitialiseBallistics (public instance) exists");
        if (init != null)
        {
            Check(init.GetParameters().Length == 0, "InitialiseBallistics takes 0 params");
            Check(init.ReturnType.FullName == "System.Void", "InitialiseBallistics returns void");
        }
        CheckField(ballistic, "parentAmmunition", "Ammunition");
    }

    private static void CheckGuidancePatches(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- AmmunitionMoveBallistic.BallisticFixedUpdate (air guidance hook) --");
        Type ballistic = asm.GetType("AmmunitionMoveBallistic", throwOnError: true);
        MethodInfo step = ballistic.GetMethod("BallisticFixedUpdate", BindingFlags.Public | BindingFlags.Instance);
        Check(step != null, "AmmunitionMoveBallistic.BallisticFixedUpdate (public instance) exists");
        if (step != null)
        {
            Check(step.GetParameters().Length == 0, "BallisticFixedUpdate takes 0 params");
            Check(step.ReturnType.FullName == "System.Void", "BallisticFixedUpdate returns void");
        }

        Console.WriteLine();
        Console.WriteLine("-- AmmunitionMoveTorpedo.FixedUpdate (water guidance hook) --");
        Type torpedo = asm.GetType("AmmunitionMoveTorpedo", throwOnError: true);
        MethodInfo torpedoStep = torpedo.GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(torpedoStep != null, "AmmunitionMoveTorpedo.FixedUpdate (private instance) exists");
        if (torpedoStep != null)
        {
            Check(torpedoStep.GetParameters().Length == 0, "FixedUpdate takes 0 params");
            Check(torpedoStep.ReturnType.FullName == "System.Void", "FixedUpdate returns void");
        }
        CheckField(torpedo, "parentAmmunition", "Ammunition");
        CheckField(torpedo, "currentSpeed", "System.Single");
        CheckField(torpedo, "runSpeed", "System.Single");
        CheckField(torpedo, "gyroDone", "System.Boolean");
        CheckField(torpedo, "interceptPos", "UnityEngine.Vector3");
    }

    private static void CheckDudPatch(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- Ammunition.ExplodeAmmunition (dud suppression) --");
        Type ammo = asm.GetType("Ammunition", throwOnError: true);
        MethodInfo explode = ammo.GetMethod("ExplodeAmmunition", BindingFlags.Public | BindingFlags.Instance);
        Check(explode != null, "Ammunition.ExplodeAmmunition (public instance) exists");
        if (explode != null)
        {
            ParameterInfo[] ps = explode.GetParameters();
            Check(ps.Length == 4, "ExplodeAmmunition takes 4 params (got " + ps.Length + ")");
            Check(explode.ReturnType.FullName == "System.Void", "ExplodeAmmunition returns void");
        }
        MethodInfo destroy = ammo.GetMethod("DestroyAmmunitionObject", BindingFlags.Public | BindingFlags.Instance);
        Check(destroy != null, "Ammunition.DestroyAmmunitionObject (public instance) exists");
        if (destroy != null)
        {
            Check(destroy.GetParameters().Length == 0, "DestroyAmmunitionObject takes 0 params");
        }
        CheckField(ammo, "dudRate", "System.Single");
        CheckField(ammo, "ammoType", "AmmoType");
        CheckField(ammo, "ammoRigidbody", "UnityEngine.Rigidbody");
        CheckField(ammo, "appliedDamage", "System.Boolean");
    }

    // --- everything the guidance reads -------------------------------------------------

    private static void CheckReadModel(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- fields the guidance reads --");

        Type unit = asm.GetType("Unit", throwOnError: true);
        CheckField(unit, "faction", "Faction");
        CheckField(unit, "unitAI", "UnitAI");
        CheckField(unit, "unitAir", "UnitAir");
        CheckField(unit, "unitSea", "UnitSea");
        CheckField(unit, "unitData", "UnitData");
        CheckField(unit, "isDestroyed", "System.Boolean");
        CheckField(unit, "currentSpeed", "System.Single");
        CheckField(unit, "currentActualSpeed", "System.Single");

        Type unitAI = asm.GetType("UnitAI", throwOnError: true);
        CheckField(unitAI, "focusedUnit", "Unit");

        Type unitData = asm.GetType("UnitData", throwOnError: true);
        CheckField(unitData, "length", "System.Single");

        Type engagementManager = asm.GetType("EngagementManager", throwOnError: true);
        FieldInfo instance = engagementManager.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        Check(instance != null && instance.FieldType == engagementManager,
            "EngagementManager.instance static singleton exists");
        FieldInfo allSeaUnits = engagementManager.GetField("allSeaUnits", BindingFlags.Public | BindingFlags.Instance);
        Check(allSeaUnits != null, "EngagementManager.allSeaUnits exists");
        if (allSeaUnits != null)
        {
            Check(allSeaUnits.FieldType.Name == "List`1",
                "EngagementManager.allSeaUnits is a List<> (got " + allSeaUnits.FieldType.Name + ")");
        }

        Console.WriteLine();
        Console.WriteLine("-- enum members the registry keys on --");
        Type ammoType = asm.GetType("AmmoType", throwOnError: true);
        foreach (string name in new[] { "Bomb", "Aerial_Depth_Charge", "Aerial_Torpedo", "Rocket", "Torpedo" })
        {
            Check(Enum.GetNames(ammoType).Contains(name), "AmmoType." + name + " exists");
        }
        Type faction = asm.GetType("Faction", throwOnError: true);
        foreach (string name in new[] { "Player", "Enemy", "Neutral" })
        {
            Check(Enum.GetNames(faction).Contains(name), "Faction." + name + " exists");
        }
    }

    // --- helpers -----------------------------------------------------------------------

    private static void CheckField(Type owner, string fieldName, string expectedTypeName)
    {
        FieldInfo field = owner.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Check(field != null, owner.Name + "." + fieldName + " (public instance) exists");
        if (field == null)
        {
            return;
        }
        bool matches = field.FieldType.FullName == expectedTypeName || field.FieldType.Name == expectedTypeName;
        Check(matches, owner.Name + "." + fieldName + " is " + expectedTypeName +
                       " (got " + field.FieldType.FullName + ")");
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

using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Loads the real game assembly (Assembly-CSharp.dll) and the real UnityEngine.dll with
/// MetadataLoadContext and asserts that every member Fire Control patches, reads or writes
/// exists with the expected signature.
///
/// Harmony resolves patch targets by name at runtime, so a typo or a signature that has drifted
/// compiles perfectly and only fails once the game is running. Two things here are load-bearing
/// beyond mere existence:
///   * Director.CalculateTMAAgainstTarget's PARAMETER NAMES. The gunnery patch binds baseRate
///     and maxSolution by name; rename either and the patch silently stops doing anything.
///   * ParticleSystem.ShapeModule. The whole anti-aircraft half writes through it, and its
///     write-through behaviour is Unity-version dependent.
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
            Assembly unity = mlc.LoadFromAssemblyPath(Path.Combine(managedDir, "UnityEngine.dll"));
            Console.WriteLine("Loaded: " + asm.GetName().Name + " and " + unity.GetName().Name);
            Console.WriteLine();

            CheckAntiAir(asm);
            CheckParticleShape(unity);
            CheckGunnerySolution(asm);
            CheckSweepTargets(asm);
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

    private static void CheckAntiAir(Assembly asm)
    {
        Console.WriteLine("-- WeaponAAA (the anti-aircraft mounts) --");
        Type aaa = asm.GetType("WeaponAAA", throwOnError: true);

        MethodInfo init = aaa.GetMethod("InitialiseAAA", BindingFlags.Public | BindingFlags.Instance);
        Check(init != null, "WeaponAAA.InitialiseAAA (public instance) exists");
        if (init != null)
        {
            Check(init.IsVirtual, "InitialiseAAA is virtual");
            Check(init.GetParameters().Length == 0, "InitialiseAAA takes 0 params");
            Check(init.ReturnType.FullName == "System.Void", "InitialiseAAA returns void");
        }

        FieldInfo particles = aaa.GetField("aaaParticles", BindingFlags.Public | BindingFlags.Instance);
        Check(particles != null, "WeaponAAA.aaaParticles exists");
        if (particles != null)
        {
            Check(particles.FieldType.FullName == "UnityEngine.ParticleSystem[]",
                "aaaParticles is ParticleSystem[] (got " + particles.FieldType.FullName + ")");
        }

        // The directed mounts chain to the base InitialiseAAA, which is what lets one patch
        // cover both kinds of battery.
        Type mount = asm.GetType("WeaponAAAMount", throwOnError: true);
        Check(mount.BaseType != null && mount.BaseType.Name == "WeaponAAA",
            "WeaponAAAMount derives from WeaponAAA");
        Check(mount.GetMethod("InitialiseAAA",
                  BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null,
            "WeaponAAAMount declares its own InitialiseAAA override");

        // interval and parentUnit live on the Weapon base class.
        Type weapon = asm.GetType("Weapon", throwOnError: true);
        CheckField(weapon, "interval", "System.Single");
        CheckField(weapon, "parentUnit", "Unit");
    }

    private static void CheckParticleShape(Assembly unity)
    {
        Console.WriteLine();
        Console.WriteLine("-- UnityEngine.ParticleSystem.ShapeModule (the AA cone) --");
        Type ps = unity.GetType("UnityEngine.ParticleSystem", throwOnError: true);

        PropertyInfo shape = ps.GetProperty("shape", BindingFlags.Public | BindingFlags.Instance);
        Check(shape != null, "ParticleSystem.shape property exists");
        if (shape == null)
        {
            return;
        }

        Type shapeModule = shape.PropertyType;
        Check(shapeModule.Name == "ShapeModule", "ParticleSystem.shape is a ShapeModule");
        Check(shapeModule.IsValueType,
            "ShapeModule is a struct - the module writes through to the system, as the game's " +
            "own InitialiseAAA relies on for EmissionModule and MainModule");

        foreach (string name in new[] { "angle", "radius" })
        {
            PropertyInfo p = shapeModule.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Check(p != null, "ShapeModule." + name + " exists");
            if (p != null)
            {
                Check(p.CanRead && p.CanWrite, "ShapeModule." + name + " is readable and writable");
                Check(p.PropertyType.FullName == "System.Single",
                    "ShapeModule." + name + " is float (got " + p.PropertyType.FullName + ")");
            }
        }
    }

    private static void CheckGunnerySolution(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- Director.CalculateTMAAgainstTarget (the gunnery solution) --");
        Type director = asm.GetType("Director", throwOnError: true);

        MethodInfo tma = director.GetMethod("CalculateTMAAgainstTarget",
            BindingFlags.Public | BindingFlags.Instance);
        Check(tma != null, "Director.CalculateTMAAgainstTarget (public instance) exists");
        if (tma != null)
        {
            Check(tma.ReturnType.FullName == "System.Single",
                "CalculateTMAAgainstTarget returns float (got " + tma.ReturnType.FullName + ")");

            ParameterInfo[] ps = tma.GetParameters();
            Check(ps.Length == 3, "CalculateTMAAgainstTarget takes 3 params (got " + ps.Length + ")");
            if (ps.Length == 3)
            {
                Check(ps[0].ParameterType.Name == "MapUnit", "param 0 is MapUnit");
                // The prefix binds these two by name. A rename would leave the patch attached
                // and doing nothing, which is the worst kind of failure.
                Check(ps[1].Name == "baseRate",
                    "param 1 is named 'baseRate' (got '" + ps[1].Name + "') - the patch binds by name");
                Check(ps[1].ParameterType.FullName == "System.Single", "param 1 is float");
                Check(ps[2].Name == "maxSolution",
                    "param 2 is named 'maxSolution' (got '" + ps[2].Name + "') - the patch binds by name");
                Check(ps[2].ParameterType.FullName == "System.Single", "param 2 is float");
            }
        }

        CheckField(director, "parentUnit", "Unit");

        // Not patched, but the mod's reasoning depends on the director ticking about once a
        // second, which is what makes a gain of 1.0 per call an instant solution.
        CheckField(director, "interval2", "System.Single");
    }

    private static void CheckSweepTargets(Assembly asm)
    {
        Console.WriteLine();
        Console.WriteLine("-- what the mid-battle toggle sweep walks --");
        Type unit = asm.GetType("Unit", throwOnError: true);
        CheckField(unit, "faction", "Faction");
        FieldInfo weapons = unit.GetField("weapons", BindingFlags.Public | BindingFlags.Instance);
        Check(weapons != null && weapons.FieldType.Name == "List`1", "Unit.weapons is a List<>");

        Type em = asm.GetType("EngagementManager", throwOnError: true);
        FieldInfo instance = em.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        Check(instance != null && instance.FieldType == em, "EngagementManager.instance exists");
        FieldInfo all = em.GetField("allSeaUnits", BindingFlags.Public | BindingFlags.Instance);
        Check(all != null && all.FieldType.Name == "List`1", "EngagementManager.allSeaUnits is a List<>");

        Type faction = asm.GetType("Faction", throwOnError: true);
        Check(Enum.GetNames(faction).Contains("Player"), "Faction.Player exists");
    }

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

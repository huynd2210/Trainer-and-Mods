// Offline check, run on the Windows .NET Framework CLR (the closest thing to the game's Mono that is on every PC):
// loads the built plugin and the real game assemblies for REFLECTION ONLY, then
//   1. asks every patch to resolve its target (AccessTools, exactly as in the game), and
//   2. applies all patches with Harmony and reports which methods ended up patched.
// No game code is executed and the game is not started. Build and run with tests\PatchCheck\run.ps1.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

internal static class Program
{
    private static string _managed;
    private static string _core;

    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        _managed = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Darkest Dungeon® II\Darkest Dungeon II_Data\Managed";
        _core = Path.Combine(root, "vendor", "BepInEx", "BepInEx", "core");
        string plugin = Path.Combine(root, "src", "DD2.KillTracker", "bin", "Release", "DD2.KillTracker.dll");

        AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
        {
            string simple = new AssemblyName(e.Name).Name;
            foreach (string dir in new[] { _core, _managed })
            {
                string p = Path.Combine(dir, simple + ".dll");
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };

        Console.WriteLine("runtime: " + Environment.Version + "   plugin: " + plugin);
        Assembly asm = Assembly.LoadFrom(plugin);

        Type cfg = asm.GetType("DD2.KillTracker.Cfg", true);
        ManualLogSource log = new ManualLogSource("PatchCheck");
        int hardErrors = 0, clrLimits = 0;
        log.LogEvent += delegate (object s, LogEventArgs e)
        {
            string text = Convert.ToString(e.Data);
            Console.WriteLine("  [" + e.Level + "] " + (text.Length > 400 ? text.Substring(0, 400) + " ..." : text));
            if (e.Level != LogLevel.Error) return;
            // PrepareMethod on the desktop CLR refuses methods that touch Unity's native (ECall) code. That is a limit of
            // running outside the game's Mono runtime, not a signature or binding problem.
            if (text.Contains("ECall methods must be packaged into a system module")) clrLimits++; else hardErrors++;
        };
        cfg.GetField("Log", BindingFlags.Static | BindingFlags.Public).SetValue(null, log);

        Type patches = asm.GetType("DD2.KillTracker.Patches", true);
        Array all = (Array)patches.GetField("All", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        Console.WriteLine("patches declared: " + all.Length);
        int unresolved = 0;
        foreach (object p in all)
        {
            Type t = p.GetType();
            string name = (string)t.GetProperty("Name").GetValue(p, null);
            MethodBase target = (MethodBase)t.GetMethod("ResolveTarget").Invoke(p, null);
            Console.WriteLine((target == null ? "MISSING  " : "resolved ") + name + (target == null ? "" : "  ->  " + target.DeclaringType.FullName + "::" + target));
            if (target == null) unresolved++;
        }

        Harmony harmony = new Harmony("dd2.killtracker.patchcheck");
        patches.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { harmony });

        MethodBase[] patched = harmony.GetPatchedMethods().ToArray();
        Console.WriteLine("methods patched by Harmony: " + patched.Length + " of " + all.Length);
        foreach (MethodBase m in patched)
        {
            Patches info = Harmony.GetPatchInfo(m);
            Console.WriteLine("  " + m.DeclaringType.FullName + "::" + m.Name + "  prefixes=" + info.Prefixes.Count + " postfixes=" + info.Postfixes.Count);
        }

        bool ok = unresolved == 0 && hardErrors == 0 && patched.Length == all.Length;
        Console.WriteLine("resolved " + (all.Length - unresolved) + "/" + all.Length + ", applied cleanly " + (all.Length - hardErrors - clrLimits) + ", desktop-CLR limitation (ECall, cannot judge outside the game's Mono) " + clrLimits + ", real errors " + hardErrors);
        Console.WriteLine(ok ? "RESULT: OK" : "RESULT: PROBLEM");
        return ok ? 0 : 1;
    }
}

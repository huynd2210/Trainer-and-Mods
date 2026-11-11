using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PerfProbe
{
    // Finds the code worth timing and puts the timing patch on it.
    //
    // Mod code: every Harmony patch method of every mod, every ModHook event handler a mod
    // added. (Mods' own Update methods aren't timed: finding them takes reflection the game's
    // mod scanner refuses.) Game code: each game method a mod patches, timed as a whole, so a patch that
    // makes the game's own method slower (a transpiler, say) shows up too.
    internal static class Targets
    {
        internal const string HarmonyId = "PerfProbe.timing";
        private const string OwnModOwner = "mod.PerfProbe";

        private static readonly Harmony harmony = new Harmony(HarmonyId);
        private static readonly HashSet<MethodBase> tried = new HashSet<MethodBase>();
        private static HarmonyMethod prefix, postfix, finalizer;

        internal static int Failed;

        private sealed class Target
        {
            public MethodBase Method;
            public string Label;
            public string Mod;
            public bool Whole;
        }

        // Safe to call again: only methods not seen before are patched.
        internal static int Scan()
        {
            if (prefix == null)
            {
                prefix = new HarmonyMethod(typeof(Timing).GetMethod(nameof(Timing.Prefix))) { priority = Priority.First };
                postfix = new HarmonyMethod(typeof(Timing).GetMethod(nameof(Timing.Postfix))) { priority = Priority.Last };
                finalizer = new HarmonyMethod(typeof(Timing).GetMethod(nameof(Timing.Finalizer)));
            }

            List<Target> found = new List<Target>();
            List<MethodBase> patchedOriginals = new List<MethodBase>();
            CollectPatches(found, patchedOriginals);
            CollectEventHandlers(found);

            // Mod code first, then the game methods they patch: re-patching an original rebuilds
            // it, and the rebuilt one then calls the already-timed patch methods.
            int added = 0;
            foreach (Target t in found.Where(t => !t.Whole).Concat(found.Where(t => t.Whole)))
                if (Patch(t))
                    added++;
            return added;
        }

        private static void CollectPatches(List<Target> found, List<MethodBase> originals)
        {
            foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
            {
                Patches info = Harmony.GetPatchInfo(original);
                if (info == null)
                    continue;
                List<(Patch patch, string kind)> patches = info.Prefixes.Select(p => (p, "prefix"))
                    .Concat(info.Postfixes.Select(p => (p, "postfix")))
                    .Concat(info.Finalizers.Select(p => (p, "finalizer")))
                    .Where(p => p.p.owner != HarmonyId && p.p.owner != OwnModOwner)
                    .ToList();
                List<string> owners = patches.Select(p => p.patch.owner)
                    .Concat(info.Transpilers.Select(p => p.owner))
                    .Where(o => o != HarmonyId && o != OwnModOwner)
                    .Distinct().ToList();
                if (owners.Count == 0)
                    continue;

                string target = Name(original);
                foreach ((Patch patch, string kind) in patches)
                {
                    found.Add(new Target
                    {
                        Method = patch.PatchMethod,
                        Mod = ModName(patch.owner),
                        Label = kind + " on " + target + "  [" + Name(patch.PatchMethod) + "]",
                    });
                }
                found.Add(new Target
                {
                    Method = original,
                    Mod = "game",
                    Whole = true,
                    Label = target + " as a whole, patched by " + string.Join(", ", owners.Select(ModName)),
                });
            }
        }

        private static void CollectEventHandlers(List<Target> found)
        {
            foreach (FieldInfo field in typeof(ModHook).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType))
                    continue;
                if (!(field.GetValue(null) is Delegate handlers))
                    continue;
                foreach (Delegate d in handlers.GetInvocationList())
                {
                    string mod = ModOf(d.Method.DeclaringType?.Assembly);
                    if (mod == null)
                        continue;
                    found.Add(new Target { Method = d.Method, Mod = mod, Label = "event " + field.Name + "  [" + Name(d.Method) + "]" });
                }
            }
        }

        private static bool Patch(Target t)
        {
            MethodBase m = t.Method;
            if (m == null || !tried.Add(m))
                return false;
            if (m.IsAbstract || m.ContainsGenericParameters || (m.DeclaringType?.ContainsGenericParameters ?? false)
                || m.DeclaringType == null || m.DeclaringType.Assembly == typeof(Timing).Assembly)
                return false;
            try
            {
                Timing.Stats[m] = new Stat { Label = t.Label, Mod = t.Mod, Whole = t.Whole };
                harmony.Patch(m, prefix, postfix, null, finalizer);
                return true;
            }
            catch (Exception e)
            {
                Timing.Stats.Remove(m);
                Failed++;
                Debug.LogWarning("[PerfProbe] Can't time " + t.Label + ": " + e.Message);
                return false;
            }
        }

        // The mod an assembly belongs to (its manifest ID, from the game's mod loader), or null
        // for the game's own and framework assemblies.
        internal static string ModOf(Assembly asm)
        {
            if (asm == null || asm == typeof(Timing).Assembly || asm.IsDynamic)
                return null;
            foreach (LoadedMod mod in ModLoader.Mods)
                if (mod?.Assembly == asm)
                    return mod.Manifest?.ID ?? asm.GetName().Name;
            return null;
        }

        private static string ModName(string owner) =>
            owner != null && owner.StartsWith("mod.", StringComparison.Ordinal) ? owner.Substring(4) : owner ?? "?";

        private static string Name(MethodBase m) => (m.DeclaringType?.Name ?? "?") + "." + m.Name;
    }
}

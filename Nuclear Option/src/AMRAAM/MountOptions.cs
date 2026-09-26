using System.Collections.Generic;
using HarmonyLib;

namespace NuclearOptionAMRAAM
{
    // Keep all registered keys for existing saves/network definitions. Only the
    // chooser collapses equivalent AMRAAM counts on the same hardpoint.
    internal static class MountOptions
    {
        internal static bool IsAMRAAM(WeaponMount mount) => mount != null &&
            mount.jsonKey != null && mount.jsonKey.StartsWith(AMRAAMPlugin.Key + "_", System.StringComparison.Ordinal);

        internal static void Collapse(List<WeaponMount> options, WeaponMount equipped)
        {
            var kept = new Dictionary<int, WeaponMount>();
            var emitted = new HashSet<int>();
            if (IsAMRAAM(equipped) && options.Contains(equipped)) kept[equipped.ammo] = equipped;
            for (int i = 0; i < options.Count;)
            {
                var mount = options[i];
                if (!IsAMRAAM(mount)) { i++; continue; }
                if ((kept.TryGetValue(mount.ammo, out var existing) && existing != mount) || !emitted.Add(mount.ammo)) options.RemoveAt(i);
                else { kept[mount.ammo] = mount; i++; }
            }
        }

        [HarmonyPatch(typeof(WeaponChecker), "GetAvailableWeaponsNonAlloc")]
        static class AvailablePatch
        {
            static void Postfix(HardpointSet hardpointSet, List<WeaponMount> outAvailable) =>
                Collapse(outAvailable, hardpointSet.weaponMount);
        }

        [HarmonyPatch(typeof(WeaponSelector), "SetValue")]
        static class SelectionPatch
        {
            static readonly System.Reflection.FieldInfo Cache = AccessTools.Field(typeof(WeaponSelector), "getCache");
            static void Prefix(WeaponSelector __instance, ref WeaponMount weaponMount)
            {
                if (!IsAMRAAM(weaponMount)) return;
                var available = (List<WeaponMount>)Cache.GetValue(__instance);
                if (available.Contains(weaponMount)) return;
                foreach (var mount in available)
                    if (IsAMRAAM(mount) && mount.ammo == weaponMount.ammo) { weaponMount = mount; return; }
            }
        }
    }
}

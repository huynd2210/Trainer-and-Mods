using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace CauseOfDeath
{
    public class CauseOfDeathMod : IMod
    {
        public void Init(ModManifest manifest) { }
        public void OnEnable() { }
        public void OnDisable() { }
    }

    // The cause is a saved tag on the carcass: one or more keys joined by '+'.
    internal static class Cause
    {
        internal const string Tag = "CAUSEOFDEATH_TAG";

        private static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "old_age", "old age" },
            { "starved", "starved" },
            { "out_of_water", "suffocated outside a tank" },
            { "no_power", "unpowered tank" },
            { "no_water", "not enough water" },
            { "low_water", "low water" },
            { "toxic_water", "toxic water" },
            { "polluted_water", "polluted water" },
            { "disease", "disease" },
            { "weak_immunity", "weak immunity" },
            { "mauled", "mauled by a bigger cagemate" },
            { "blade", "killed with a blade" },
            { "auto_butcher", "auto-butcher" },
            { "poor_health", "poor health" },
        };

        internal static void Record(GameItem item, string keys)
        {
            if (item == null || string.IsNullOrEmpty(keys))
                return;
            item.ModifyTag(Tag, s => s.Enable().SetString(keys), skipValidate: true);
        }

        internal static string Describe(GameItem item)
        {
            if (item == null || !item.IsTag(Tag))
                return null;
            string keys = item.GetTagReadonly(Tag).GetString();
            if (string.IsNullOrEmpty(keys))
                return null;
            return string.Join(", ", keys.Split('+').Select(k => Text.TryGetValue(k, out string t) ? t : k));
        }
    }

    // Who is killing right now. The innermost game method wins (old age inside a tank tick).
    internal static class Context
    {
        private static readonly List<Func<GameItem, string>> stack = new List<Func<GameItem, string>>();

        internal static void Push(Func<GameItem, string> resolver) => stack.Add(resolver);

        internal static void Pop()
        {
            if (stack.Count > 0)
                stack.RemoveAt(stack.Count - 1);
        }

        internal static string Resolve(GameItem victim) => stack.Count == 0 ? null : stack[stack.Count - 1](victim);

        internal static void Run(Func<GameItem, string> resolver, Action body)
        {
            Push(resolver);
            try { body(); }
            finally { Pop(); }
        }
    }

    // What hurt each animal during the current tank tick / rat day-end.
    internal static class Harms
    {
        private sealed class RefEq : IEqualityComparer<GameItem>
        {
            public bool Equals(GameItem a, GameItem b) => ReferenceEquals(a, b);
            public int GetHashCode(GameItem o) => RuntimeHelpers.GetHashCode(o);
        }

        private static readonly Dictionary<GameItem, List<string>> tonight = new Dictionary<GameItem, List<string>>(new RefEq());

        internal static void Clear() => tonight.Clear();

        internal static void Add(GameItem item, string key)
        {
            if (!tonight.TryGetValue(item, out List<string> list))
                tonight[item] = list = new List<string>();
            if (!list.Contains(key))
                list.Add(key);
        }

        internal static string Of(GameItem item) =>
            tonight.TryGetValue(item, out List<string> list) && list.Count > 0 ? string.Join("+", list) : "poor_health";
    }

    // Game methods that kill, and what each one means. New cause = one entry.
    [HarmonyPatch]
    internal static class KillContexts
    {
        private static Dictionary<MethodBase, Func<GameItem, string>> table;

        private static Dictionary<MethodBase, Func<GameItem, string>> Table => table ?? (table = new Dictionary<MethodBase, Func<GameItem, string>>
        {
            { AccessTools.Method(typeof(AquacultureHelper), "AgeOneDay"), _ => "old_age" },
            { AccessTools.Method(typeof(AquacultureHelper), "HandleStarvation"), _ => "starved" },
            { AccessTools.Method(typeof(AquacultureHelper), "TickTank"), Harms.Of },
            { AccessTools.Method(typeof(HusbandryHelper), "CheckOldAge"), _ => "old_age" },
            { AccessTools.Method(typeof(HusbandryHelper), "HandleStarvation"), _ => "starved" },
            { AccessTools.Method(typeof(HusbandryHelper), "HandleHealth"), Harms.Of },
            { AccessTools.Method(typeof(HusbandryHelper), "HandleOvercrowding"), _ => "mauled" },
            { AccessTools.Method(typeof(MachineAutoButcher), "TryStoreAnimal"), _ => "auto_butcher" },
            { AccessTools.Method(typeof(MachineAutoButcher), "TryStoreFish"), _ => "auto_butcher" },
        });

        private static IEnumerable<MethodBase> TargetMethods() => Table.Keys.Where(m => m != null);

        private static void Prefix(MethodBase __originalMethod)
        {
            Context.Push(Table.TryGetValue(__originalMethod, out var r) ? r : (_ => null));
        }

        private static Exception Finalizer(Exception __exception)
        {
            Context.Pop();
            return __exception;
        }
    }

    // The moment of death: stamp the cause on the carcass.
    [HarmonyPatch(typeof(AquacultureHelper), nameof(AquacultureHelper.OnFishDead))]
    internal static class FishDeathPatch
    {
        private static void Prefix(GameItem item)
        {
            if (item != null && !AquacultureHelper.IsDead(item))
                Cause.Record(item, Context.Resolve(item));
        }
    }

    [HarmonyPatch(typeof(HusbandryHelper), nameof(HusbandryHelper.OnAnimalDead))]
    internal static class AnimalDeathPatch
    {
        private static void Prefix(GameItem item)
        {
            if (item != null && !HusbandryHelper.IsAnimalDead(item))
                Cause.Record(item, Context.Resolve(item));
        }
    }

    // Blades and the fish's own end-of-day check are anonymous delegates, so wrap them.
    [HarmonyPatch(typeof(HusbandryHelper), nameof(HusbandryHelper.InitBladeItem))]
    internal static class BladePatch
    {
        private static void Postfix(GameItem item)
        {
            Action<GameItem, GameItem> orig = item.onThisTargetItemFunc;
            if (orig == null)
                return;
            item.onThisTargetItemFunc = (s, t) => Context.Run(_ => "blade", () => orig(s, t));
        }
    }

    [HarmonyPatch(typeof(AquacultureHelper), nameof(AquacultureHelper.InitFishItem))]
    internal static class FishOutOfWaterPatch
    {
        private static void Postfix(GameItem item)
        {
            Action<GameItem, GameInventory, SlotMarker> orig = item.onCycleEndSlotItemFunc;
            if (orig == null)
                return;
            item.onCycleEndSlotItemFunc = (i, inv, m) => Context.Run(_ => "out_of_water", () => orig(i, inv, m));
        }
    }

    // Fish: what the tank did to each fish tonight (same checks as the game's status tags).
    [HarmonyPatch(typeof(AquacultureHelper), nameof(AquacultureHelper.TickTank))]
    internal static class TankTickPatch
    {
        private static void Prefix(GameGridInventory tankGrid, bool isPowered)
        {
            Harms.Clear();
            if (tankGrid == null || isPowered)
                return;
            foreach (GameItem stock in tankGrid.childItems.Where(AquacultureHelper.IsLiveStock))
                Harms.Add(stock, "no_power");
        }
    }

    [HarmonyPatch(typeof(AquacultureHelper), "ApplyWaterChecks")]
    internal static class WaterChecksPatch
    {
        private static void Prefix(List<GameItem> live, GameItem waterTank)
        {
            if (live == null || live.Count == 0)
                return;
            int waterMl = AquacultureHelper.GetWaterMl(waterTank);
            int need = live.Sum(AquacultureHelper.GetWaterNeedMl);
            float ratio = need <= 0 ? 1f : (float)waterMl / need;
            float waste = AquacultureHelper.GetWastePercent(waterTank);
            foreach (GameItem fish in live)
            {
                FishSpecies species = AquacultureHelper.GetSpecies(fish);
                if (species == null)
                    continue;
                if (ratio < 0.5f) Harms.Add(fish, "no_water");
                else if (ratio < 1f) Harms.Add(fish, "low_water");
                if (species.ignoresWaste)
                    continue;
                if (waste >= AquacultureHelper.GetWasteDeathPercent(species)) Harms.Add(fish, "toxic_water");
                else if (waste > AquacultureHelper.GetWasteDamagePercent(species)) Harms.Add(fish, "polluted_water");
            }
        }
    }

    // Rats: health lost to disease or weak immunity during this rat's day-end.
    [HarmonyPatch(typeof(HusbandryHelper), nameof(HusbandryHelper.OnAnimalItemDayEnd))]
    internal static class RatDayEndPatch
    {
        private static void Prefix() => Harms.Clear();
    }

    [HarmonyPatch]
    internal static class RatHealthLossPatch
    {
        private static Dictionary<MethodBase, string> table;

        private static Dictionary<MethodBase, string> Table => table ?? (table = new Dictionary<MethodBase, string>
        {
            { AccessTools.Method(typeof(HusbandryHelper), "HandleDisease"), "disease" },
            { AccessTools.Method(typeof(HusbandryHelper), "HandleRegen"), "weak_immunity" },
        });

        private static IEnumerable<MethodBase> TargetMethods() => Table.Keys.Where(m => m != null);

        private static void Prefix(GameItem item, out float __state)
        {
            __state = item.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat();
        }

        private static void Postfix(MethodBase __originalMethod, GameItem item, float __state)
        {
            if (item.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat() < __state && Table.TryGetValue(__originalMethod, out string key))
                Harms.Add(item, key);
        }
    }

    // Display: put the cause inside the game's own red [Dead] tag while that item's tooltip is built.
    [HarmonyPatch]
    internal static class TooltipItemPatch
    {
        internal static GameItem Current;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AquacultureHelper), nameof(AquacultureHelper.CreateTooltip));
            yield return AccessTools.Method(typeof(HusbandryHelper), nameof(HusbandryHelper.CreateItemTooltip));
        }

        private static void Prefix(GameItem item) => Current = item;

        private static Exception Finalizer(Exception __exception)
        {
            Current = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LocHelper), nameof(LocHelper.GetLocalizedMechanic))]
    internal static class DeadLabelPatch
    {
        private static void Postfix(string key, ref string __result)
        {
            if (TooltipItemPatch.Current == null || key != "husbandry_status_dead")
                return;
            string cause = Cause.Describe(TooltipItemPatch.Current);
            if (cause != null)
                __result += ": " + cause;
        }
    }
}

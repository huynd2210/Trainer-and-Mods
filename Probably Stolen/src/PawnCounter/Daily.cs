using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawnCounter
{
    // Builds the mod's part of each morning's queue: owners coming back, owners who gave
    // up, and maybe a new pawner.
    internal static class Daily
    {
        // Not readonly: the offline harness swaps in a seeded one (RNG needs the engine).
        internal static IRng Rng = new GameRng();

        // The live queue is not saved, so this only has to hold for the current day.
        private static int queuedDay = -1;
        private static readonly HashSet<int> queuedRedeemers = new HashSet<int>();

        // Items brought by pawners queued today; any the player never got to are thrown away
        // the next morning so they don't pile up unplaced.
        private static readonly List<GameItem> offered = new List<GameItem>();

        // A loaded save rebuilds the day's queue, so ours must be rebuilt too.
        internal static void OnGameLoaded()
        {
            queuedDay = -1;
            queuedRedeemers.Clear();
            foreach (GameItem item in offered)
                GraphUtils.DestroyIfUnplaced(item, "pawn offer dropped on load");
            offered.Clear();
        }

        internal static void AddCustomers(StoreClientManager manager)
        {
            if (!State.Ready)
                return;
            int today = State.Today;
            if (queuedDay == today)
                return;
            queuedDay = today;
            queuedRedeemers.Clear();
            foreach (GameItem item in offered)
                GraphUtils.DestroyIfUnplaced(item, "pawn offer never made");
            offered.Clear();

            List<Pawn> pawns = State.Pawns;
            foreach (Pawn pawn in pawns)
            {
                switch (Plan(pawn, today))
                {
                    case Visit.Forfeit:
                        Forfeit(pawn);
                        break;
                    case Visit.Comes:
                        manager.AddClient(Redeemers.Create(Rng, pawn));
                        queuedRedeemers.Add(pawn.Id);
                        break;
                }
            }

            TryAddPawner(manager, today, State.Pawns.Count);
        }

        internal enum Visit { Wait, Comes, Forfeit }

        // What a pawn's owner does today. An owner who won't return gives up the day after the
        // due day; one who will keeps coming until served, for a few days.
        internal static Visit Plan(Pawn pawn, int today)
        {
            if (today < pawn.DueDay)
                return Visit.Wait;
            if (!pawn.WillReturn)
                return today > pawn.DueDay ? Visit.Forfeit : Visit.Wait;
            return today > pawn.DueDay + Terms.GraceDays ? Visit.Forfeit : Visit.Comes;
        }

        private static void Forfeit(Pawn pawn)
        {
            Redeemers.Settle(pawn);
            Debug.Log("[PawnCounter] " + pawn.Owner + " never came back for " + pawn.ItemName);
            StoreUIManager.Instance?.Notify(pawn.Owner + " never came back for the " + pawn.ItemName + ". It's yours to sell.");
        }

        private static void TryAddPawner(StoreClientManager manager, int today, int active)
        {
            if (today < PawnCounterMod.FirstDay)
                return;
            // Holding your limit only makes pawners rarer, not impossible.
            int chance = active >= PawnCounterMod.MaxActive ? PawnCounterMod.ChanceWhenFull : PawnCounterMod.Chance;
            if (Rng.Range(1, 100) > chance)
                return;
            PawnerKind kind = PawnerKind.PickFor(Rng, today);
            GameItem item = Pool.Spawn(Rng, kind, PawnCounterMod.MinValue);
            if (item == null)
            {
                // Nothing of this kind passes the value floor: try any kind once.
                kind = PawnerKind.All.Where(k => today >= k.MinDay).OrderBy(_ => Rng.Range(0, 1000)).FirstOrDefault();
                item = kind == null ? null : Pool.Spawn(Rng, kind, PawnCounterMod.MinValue);
            }
            if (item == null)
            {
                Debug.Log("[PawnCounter] no item passes the value floor of " + PawnCounterMod.MinValue);
                return;
            }
            if (PlayerStore.Instance.isStolenItemIntroduced && Rng.Range(1, 100) <= kind.StolenChance)
                StolenHelper.InitStolenItem(item);
            offered.Add(item);
            manager.AddClient(Pawners.Create(Rng, kind, item));
        }
    }
}

using System.Collections.Generic;

namespace TicketBooth
{
    internal sealed class GameRng : IRng
    {
        public int Range(int minInclusive, int maxInclusive) { return RNG.GetRandomInt(minInclusive, maxInclusive); }
    }

    // Builds the mod's part of each morning's queue.
    internal static class Daily
    {
        internal const string VanillaRepId = "lotteryRep";

        // Not readonly: the offline harness swaps in a seeded one (UnityEngine.Random needs the engine).
        internal static IRng Rng = new GameRng();

        // The live queue is not saved, so this only has to hold for the current day.
        private static int queuedDay = -1;
        private static bool pitchQueued;
        private static readonly HashSet<int> queuedClaims = new HashSet<int>();

        internal static void AddCustomers(StoreClientManager manager)
        {
            if (!State.Ready)
                return;
            int today = State.Today;
            if (queuedDay != today)
            {
                queuedDay = today;
                pitchQueued = false;
                queuedClaims.Clear();
            }

            if (!State.Licensed)
            {
                if (!pitchQueued && today >= State.PitchDay)
                {
                    manager.AddClient(Rep.CreatePitch());
                    pitchQueued = true;
                    // Declined, dismissed or never reached: he is back in a week.
                    State.PitchDay = today + 7;
                }
                return;
            }

            for (int i = 0; i < TicketBoothMod.BuyersPerDay; i++)
                manager.AddClient(Buyers.Create());
            Claims.RollForgery(today);
            Claims.QueueDue(manager, today, queuedClaims);
        }

        // "Phone replaces": once licensed, the weekly vanilla visits stop. On the day our
        // rep pitches, he is the only lottery visitor.
        internal static void ReplaceVanillaRep(StoreClientManager manager)
        {
            if (!State.Ready || !(State.Licensed || pitchQueued))
                return;
            manager.clientStack.RemoveAll(c => c != null && c.identifier == VanillaRepId);
        }
    }
}

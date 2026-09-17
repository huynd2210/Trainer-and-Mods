using System.Collections.Generic;

namespace WoTSAceAviators
{
    /// <summary>
    /// The table of rounds currently under guidance.
    ///
    /// Ammunition objects come out of the game's object pool and are handed back on impact,
    /// so entries are keyed by instance id and every release path calls
    /// <see cref="Register"/> — which also clears any stale entry left by a previous life of
    /// the same pooled object. That is why registration is "register or forget" rather than
    /// "register when interesting": forgetting is what keeps a recycled enemy bomb from
    /// inheriting a player bomb's guidance.
    /// </summary>
    internal static class GuidanceTracker
    {
        private static readonly Dictionary<int, GuidedRound> rounds = new Dictionary<int, GuidedRound>();

        internal static int Count
        {
            get { return rounds.Count; }
        }

        /// <summary>
        /// Start guiding <paramref name="round"/>, or, when it is null, make sure the ammunition
        /// object carries no guidance from a previous use of the same pooled instance.
        /// </summary>
        internal static void Register(Ammunition ammo, GuidedRound round)
        {
            if (ammo == null)
            {
                return;
            }
            int key = ammo.GetInstanceID();
            if (round == null)
            {
                rounds.Remove(key);
                return;
            }
            rounds[key] = round;
        }

        internal static GuidedRound Get(Ammunition ammo)
        {
            if (ammo == null)
            {
                return null;
            }
            GuidedRound round;
            return rounds.TryGetValue(ammo.GetInstanceID(), out round) ? round : null;
        }

        internal static void Forget(Ammunition ammo)
        {
            if (ammo != null)
            {
                rounds.Remove(ammo.GetInstanceID());
            }
        }

        internal static void Clear()
        {
            rounds.Clear();
        }
    }
}

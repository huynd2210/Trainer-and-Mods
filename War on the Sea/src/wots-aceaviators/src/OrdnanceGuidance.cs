using System.Collections.Generic;
using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// Terminal guidance for one kind of ordnance.
    ///
    /// A round has at most two flight phases and the game drives each from a different
    /// place: free flight through the air comes from <c>AmmunitionMoveBallistic</c>, the
    /// underwater run comes from <c>AmmunitionMoveTorpedo</c>. A guidance implements
    /// whichever phases its ordnance actually has and leaves the rest alone.
    /// </summary>
    internal abstract class OrdnanceGuidance
    {
        /// <summary>Steer the round while it is flying through the air.</summary>
        internal virtual void GuideInAir(GuidedRound round)
        {
        }

        /// <summary>Steer the round while it is running in the water.</summary>
        internal virtual void GuideInWater(GuidedRound round, AmmunitionMoveTorpedo torpedo)
        {
        }

        /// <summary>
        /// Where the target will be <paramref name="timeOfFlight"/> seconds from now, to first
        /// order along its current heading. A turning target is not modelled, and does not need
        /// to be: the solution is recomputed every physics tick, so whatever error the turn
        /// introduces collapses as the time of flight runs down to zero.
        /// </summary>
        protected static Vector3 PredictTargetPosition(Unit target, float timeOfFlight)
        {
            float speed = target.currentActualSpeed;
            if (speed == 0f)
            {
                speed = target.currentSpeed;
            }
            return target.transform.position + target.transform.forward * (speed * timeOfFlight);
        }

        /// <summary>
        /// Shift the aim point along the target's own forward axis so a stick of bombs walks
        /// down the hull rather than landing on a single point.
        /// </summary>
        protected static Vector3 ApplyHullOffset(Vector3 aim, Unit target, float offsetAlongHull)
        {
            if (offsetAlongHull == 0f)
            {
                return aim;
            }
            return aim + target.transform.forward * offsetAlongHull;
        }
    }

    /// <summary>
    /// Which guidance flies which ammunition. Teaching the mod a new kind of ordnance is one
    /// entry here plus the class it points at; nothing that already works has to be reopened.
    ///
    /// Rockets are deliberately absent. They are ballistic, so this table would happily accept
    /// them, but a rocket is fired almost flat: solving its horizontal velocity from the time
    /// it takes to fall to deck height demands large speed changes that look wrong on screen.
    /// They want a cross-track-only guidance of their own.
    /// </summary>
    internal static class GuidanceRegistry
    {
        private static readonly BombGuidance Bomb = new BombGuidance();
        private static readonly AerialTorpedoGuidance Torpedo = new AerialTorpedoGuidance();

        private static readonly Dictionary<AmmoType, OrdnanceGuidance> byAmmoType =
            new Dictionary<AmmoType, OrdnanceGuidance>
            {
                { AmmoType.Bomb, Bomb },
                { AmmoType.Aerial_Depth_Charge, Bomb },
                { AmmoType.Aerial_Torpedo, Torpedo }
            };

        /// <summary>The guidance for this ammunition, or null if this mod does not fly it.</summary>
        internal static OrdnanceGuidance For(AmmoType ammoType)
        {
            OrdnanceGuidance guidance;
            return byAmmoType.TryGetValue(ammoType, out guidance) ? guidance : null;
        }

        internal static bool IsGuided(AmmoType ammoType)
        {
            return byAmmoType.ContainsKey(ammoType);
        }
    }
}

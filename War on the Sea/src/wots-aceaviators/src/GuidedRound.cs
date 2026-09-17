using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// One round in the air that this mod is steering, plus the small amount of state the
    /// guidance needs to carry between physics ticks.
    /// </summary>
    internal sealed class GuidedRound
    {
        internal Ammunition Ammo;
        internal Rigidbody Body;

        /// <summary>The unit the releasing aircraft was attacking when this round left the rack.</summary>
        internal Unit Target;

        /// <summary>
        /// Aim point offset along the target's own forward axis, in world units. Used to lay a
        /// stick of bombs down the length of the hull instead of stacking them on one spot.
        /// </summary>
        internal float AimOffsetAlongHull;

        /// <summary>Vertical speed seen on the previous tick, used to measure actual fall acceleration.</summary>
        internal float LastVerticalSpeed;

        internal bool HasLastVerticalSpeed;

        /// <summary>True once the target has gone, after which the round is left to fly ballistically.</summary>
        internal bool TargetLost
        {
            get { return Target == null || Target.isDestroyed; }
        }
    }
}

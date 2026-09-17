using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// Guidance for aerial torpedoes.
    ///
    /// The drop itself is left alone — the aircraft still flies its run in and puts the fish
    /// in the water where the game says. What changes is the run: a stock aerial torpedo has
    /// its gyro finished the moment it enters the water and swims dead straight, so it is
    /// beaten by any target that alters course after the release. Here the torpedo keeps
    /// steering, turning toward a lead point on the target at a rate that still reads as a
    /// torpedo rather than a missile.
    /// </summary>
    internal sealed class AerialTorpedoGuidance : OrdnanceGuidance
    {
        /// <summary>Stop steering inside this range, in world units; the last few metres are committed.</summary>
        private const float CommitRange = 0.5f;

        internal override void GuideInWater(GuidedRound round, AmmunitionMoveTorpedo torpedo)
        {
            Unit target = round.Target;
            if (target == null || torpedo == null || target.unitSea == null)
            {
                return;
            }

            Transform self = torpedo.transform;
            Vector3 position = self.position;

            float speed = torpedo.currentSpeed;
            if (speed <= 0.01f)
            {
                speed = torpedo.runSpeed;
            }
            if (speed <= 0.01f)
            {
                return;
            }

            // Two passes of lead: estimate the run time from the current range, move the target
            // along by that much, then re-estimate against the moved position. That converges
            // fast enough at torpedo speeds, and the whole thing is redone every tick anyway.
            Vector3 aim = target.transform.position;
            for (int pass = 0; pass < 2; pass++)
            {
                float runTime = Vector3.Distance(position, aim) / speed;
                aim = PredictTargetPosition(target, runTime);
            }

            Vector3 bearing = aim - position;
            bearing.y = 0f;
            if (bearing.sqrMagnitude < CommitRange * CommitRange)
            {
                return;
            }

            float maxTurn = AceAviatorsPlugin.TorpedoTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
            Vector3 heading = Vector3.RotateTowards(self.forward, bearing.normalized, maxTurn, 0f);
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-6f)
            {
                return;
            }

            self.rotation = Quaternion.LookRotation(heading.normalized);

            // The game's own gyro would otherwise fight this by turning back toward the
            // intercept point computed at launch. Retire it and keep its bookkeeping honest.
            torpedo.gyroDone = true;
            torpedo.interceptPos = aim;
        }
    }
}

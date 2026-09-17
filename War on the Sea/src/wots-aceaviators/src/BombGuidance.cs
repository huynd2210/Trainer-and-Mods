using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// Guidance for anything the aircraft simply drops: bombs and aerial depth charges,
    /// from dive bombers and level bombers alike.
    ///
    /// Each physics tick it solves how long the round still has before it reaches deck
    /// height, works out where the target will be by then, and trims the round's horizontal
    /// velocity toward the value that puts it exactly there. Vertical motion is never
    /// touched, so the round keeps falling under the game's own gravity and still looks like
    /// a bomb. Because the solution is redone every tick the residual error goes to zero as
    /// the bomb arrives — which is what makes the hit unconditional rather than merely
    /// accurate.
    /// </summary>
    internal sealed class BombGuidance : OrdnanceGuidance
    {
        /// <summary>
        /// Aim this far above the target's waterline, in world units (1 unit = 10 metres).
        /// Solving arrival at deck height rather than at the waterline puts the round over
        /// the hull at the moment it crosses the deck, which is where it should go off.
        /// </summary>
        private const float DeckHeight = 0.3f;

        /// <summary>Below this height above the aim point there is no useful time left to steer.</summary>
        private const float MinCorrectionHeight = 0.05f;

        internal override void GuideInAir(GuidedRound round)
        {
            Unit target = round.Target;
            Rigidbody body = round.Body;
            if (target == null || body == null || target.unitSea == null)
            {
                // Bombs pass straight through aircraft, so an air target is not steerable.
                return;
            }

            Transform self = round.Ammo.transform;
            Vector3 position = self.position;
            Vector3 velocity = body.velocity;

            float fallAcceleration = MeasureFallAcceleration(round, body, velocity.y);
            if (fallAcceleration <= 0.01f)
            {
                return;
            }

            float aimHeight = target.transform.position.y + DeckHeight;
            float heightToFall = position.y - aimHeight;
            if (heightToFall <= MinCorrectionHeight)
            {
                return;
            }

            // Solve heightToFall = -v.y*t + 0.5*a*t^2 for the positive root.
            float discriminant = velocity.y * velocity.y + 2f * fallAcceleration * heightToFall;
            if (discriminant <= 0f)
            {
                return;
            }
            float timeOfFlight = (velocity.y + Mathf.Sqrt(discriminant)) / fallAcceleration;
            if (timeOfFlight <= 0.02f)
            {
                return;
            }

            Vector3 aim = PredictTargetPosition(target, timeOfFlight);
            aim = ApplyHullOffset(aim, target, round.AimOffsetAlongHull);

            Vector3 required = new Vector3((aim.x - position.x) / timeOfFlight, 0f,
                                           (aim.z - position.z) / timeOfFlight);
            Vector3 current = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 change = required - current;

            float budget = AceAviatorsPlugin.BombGuidanceAuthority * Time.fixedDeltaTime;
            if (change.sqrMagnitude > budget * budget)
            {
                change = change.normalized * budget;
            }

            body.velocity = new Vector3(velocity.x + change.x, velocity.y, velocity.z + change.z);
        }

        /// <summary>
        /// The round's actual downward acceleration, measured from its own motion rather than
        /// assumed. The game applies gravity to ammunition with <c>Rigidbody.AddForce</c>, so
        /// the figure that matters depends on the prefab's mass and drag, not on
        /// <c>Physics.gravity</c> alone. Measuring keeps the solution correct whatever the
        /// prefab says. The first tick has nothing to measure from and falls back to the
        /// nominal value the game's own fall-time helper uses.
        /// </summary>
        private static float MeasureFallAcceleration(GuidedRound round, Rigidbody body, float verticalSpeed)
        {
            float measured = 0f;
            float step = Time.fixedDeltaTime;
            if (round.HasLastVerticalSpeed && step > 0f)
            {
                measured = (round.LastVerticalSpeed - verticalSpeed) / step;
            }
            round.LastVerticalSpeed = verticalSpeed;
            round.HasLastVerticalSpeed = true;

            if (measured > 0.01f)
            {
                return measured;
            }
            return -Physics.gravity.y / Mathf.Max(body.mass, 0.0001f);
        }
    }
}

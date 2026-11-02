using UnityEngine;

namespace NuclearOptionMissileControl
{
    // Commanded flight direction as world heading/elevation, so there is no roll to drift.
    // Sign conventions match the game: positive "Pitch" is nose down, positive roll/yaw turn right,
    // and look input follows the cockpit free-look (positive tilt looks down).
    internal sealed class AimState
    {
        internal const float MaxElevation = 85f;

        internal float Heading;   // degrees; 0 = +Z (north), 90 = +X (east)
        internal float Elevation; // degrees; positive = up

        internal Vector3 Direction
        {
            get
            {
                float h = Heading * Mathf.Deg2Rad, e = Elevation * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(h) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(h) * Mathf.Cos(e));
            }
        }

        internal void Set(Vector3 direction)
        {
            direction.Normalize();
            Heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg, -MaxElevation, MaxElevation);
        }

        // lookRight/lookDown are already-integrated degrees (like the cockpit view);
        // stickPitch/stickTurn are -1..1 deflections integrated at stickRate deg/s.
        internal void Steer(float lookRight, float lookDown, float stickPitch, float stickTurn, float stickRate, float dt)
        {
            Heading = Mathf.DeltaAngle(0f, Heading + lookRight + stickTurn * stickRate * dt);
            Elevation = Mathf.Clamp(Elevation - lookDown - stickPitch * stickRate * dt, -MaxElevation, MaxElevation);
        }

        // Keep the command within reach of the missile so the view never runs away from it.
        // Plain vector math (no engine calls) so the rules test runs outside the game.
        internal void LimitLead(Vector3 velocity, float maxLeadDegrees)
        {
            if (velocity.sqrMagnitude < 1f) return;
            Vector3 path = velocity.normalized, current = Direction;
            if (Vector3.Angle(path, current) <= maxLeadDegrees) return;
            Vector3 side = current - path * Vector3.Dot(path, current);
            if (side.sqrMagnitude < 1e-8f) side = Mathf.Abs(path.y) < 0.9f ? Vector3.up - path * path.y : Vector3.right - path * path.x;
            float lead = maxLeadDegrees * Mathf.Deg2Rad;
            Set(path * Mathf.Cos(lead) + side.normalized * Mathf.Sin(lead));
        }
    }
}

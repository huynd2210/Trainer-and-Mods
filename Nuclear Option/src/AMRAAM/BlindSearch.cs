using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionAMRAAM
{
    internal static class BlindSearch
    {
        static readonly FieldInfo Target = AccessTools.Field(typeof(MissileSeeker), "targetUnit");
        static readonly MethodInfo RadarReturn = AccessTools.Method(typeof(ARHSeeker), "GetRadarReturn");
        static readonly MethodInfo Chaff = AccessTools.Method(typeof(ARHSeeker), "ARHSeeker_OnChaff");
        static readonly FieldInfo LastAttempt = AccessTools.Field(typeof(ARHSeeker), "lastActiveTrackAttempt");
        static readonly FieldInfo ReturnStrength = AccessTools.Field(typeof(ARHSeeker), "returnStrength");
        static readonly FieldInfo Distance = AccessTools.Field(typeof(ARHSeeker), "targetDist");
        static readonly FieldInfo MinimumRange = AccessTools.Field(typeof(ARHSeeker), "minReacquireRange");
        static readonly FieldInfo Angle = AccessTools.Field(typeof(ARHSeeker), "maxTrackingAngle");
        static readonly FieldInfo ArmDelay = AccessTools.Field(typeof(ARHSeeker), "armDelay");
        static readonly FieldInfo GuidanceDelay = AccessTools.Field(typeof(ARHSeeker), "guidanceDelay");
        static readonly FieldInfo Guidance = AccessTools.Field(typeof(ARHSeeker), "guidance");
        static readonly FieldInfo Armed = AccessTools.Field(typeof(ARHSeeker), "armed");
        static readonly FieldInfo Jam = AccessTools.Field(typeof(ARHSeeker), "jamAccumulation");
        static readonly FieldInfo JamTolerance = AccessTools.Field(typeof(ARHSeeker), "jamTolerance");
        static readonly FieldInfo Jammed = AccessTools.Field(typeof(ARHSeeker), "isJammed");

        internal static void TrackChaffSubscription(ARHSeeker seeker, LoftState state, Unit target)
        {
            state.ChaffHandler = (Action<RadarChaff>)Delegate.CreateDelegate(typeof(Action<RadarChaff>), seeker, Chaff);
            // Initialize has already subscribed the stock handler to the launch target.
            state.ChaffTarget = target;
        }

        internal static bool Eligible(Missile missile, Unit candidate)
        {
            return candidate != null && candidate != missile && candidate != missile.owner &&
                !candidate.disabled && candidate.persistentID.IsValid &&
                (candidate is Aircraft || candidate is Missile) && candidate is IRadarReturn &&
                missile.NetworkHQ != null && candidate.NetworkHQ != null && candidate.NetworkHQ != missile.NetworkHQ;
        }

        internal static float Detect(ARHSeeker seeker, Missile missile, Unit candidate)
        {
            var radar = seeker.GetRadarParams();
            var delta = candidate.GlobalPosition() - missile.GlobalPosition();
            if (delta.sqrMagnitude < .01f || delta.sqrMagnitude > radar.maxRange * radar.maxRange ||
                Vector3.Angle(missile.transform.forward, delta) > (float)Angle.GetValue(seeker)) return 0f;
            // Reuse the real seeker's horizon, LOS, clutter, ECM and signal checks.
            // Reset its per-target cache while probing, then restore it so a miss
            // does not leak another candidate's radar return into the next scan.
            object previousTarget = Target.GetValue(seeker), previousAttempt = LastAttempt.GetValue(seeker),
                previousReturn = ReturnStrength.GetValue(seeker), previousDistance = Distance.GetValue(seeker),
                previousMinimum = MinimumRange.GetValue(seeker);
            try
            {
                Target.SetValue(seeker, candidate); LastAttempt.SetValue(seeker, float.NegativeInfinity);
                ReturnStrength.SetValue(seeker, 0f); Distance.SetValue(seeker, delta.magnitude);
                // This is first acquisition, not the stock lost-lock minimum-range restriction.
                MinimumRange.SetValue(seeker, 0f);
                return (float)RadarReturn.Invoke(seeker, null);
            }
            finally
            {
                Target.SetValue(seeker, previousTarget); LastAttempt.SetValue(seeker, previousAttempt);
                ReturnStrength.SetValue(seeker, previousReturn); Distance.SetValue(seeker, previousDistance);
                MinimumRange.SetValue(seeker, previousMinimum);
            }
        }

        internal static void Acquire(ARHSeeker seeker, Missile missile, LoftState state, Unit candidate, float signal)
        {
            if (state.ChaffTarget != null && state.ChaffHandler != null) state.ChaffTarget.onAddRadarChaff -= state.ChaffHandler;
            if (state.ChaffHandler == null) TrackChaffSubscription(seeker, state, null);
            state.ChaffTarget = candidate; candidate.onAddRadarChaff += state.ChaffHandler;
            Target.SetValue(seeker, candidate);
            var velocity = candidate.rb != null ? candidate.rb.velocity : Vector3.zero;
            AMRAAMPlugin.KnownPosition.SetValue(seeker, candidate.GlobalPosition());
            AMRAAMPlugin.KnownVelocity.SetValue(seeker, velocity);
            AMRAAMPlugin.Set(seeker, "knownVelPrev", velocity); AMRAAMPlugin.Set(seeker, "knownAccel", Vector3.zero);
            AMRAAMPlugin.Set(seeker, "radarLockEstablished", true);
            AMRAAMPlugin.Set(seeker, "timeWithoutReturn", 0f); AMRAAMPlugin.Set(seeker, "homingLockTime", 0f);
            ReturnStrength.SetValue(seeker, signal); LastAttempt.SetValue(seeker, Time.timeSinceLevelLoad);
            missile.SetTarget(candidate); missile.NetworkseekerMode = Missile.SeekerMode.activeLock;
            missile.SetAimpoint(candidate.GlobalPosition(), velocity);
            state.Searching = false; state.Progress = 0f; state.Lofting = false; state.TerminalCommitted = true;
            // The seeker found this target itself: commit directly instead of starting a new loft.
            if (candidate is Aircraft aircraft) aircraft.RecordDamage(missile.ownerID, .001f);
            AMRAAMPlugin.Set(seeker, "achievedLock", true);
            AMRAAMPlugin.Log.LogInfo($"AMRAAM acquired first enemy {candidate.persistentID} at {(candidate.GlobalPosition()-missile.GlobalPosition()).magnitude:F0}m after unlocked flight");
        }

        internal static bool BeforeSeek(ARHSeeker seeker, LoftState state)
        {
            var missile = seeker.GetComponent<Missile>();
            if (!missile.LocalSim || missile.disabled) return true;
            var current = Target.GetValue(seeker) as Unit;
            if (missile.targetID.IsValid && current != null && !current.disabled) return true;
            if (!state.Searching)
            {
                state.Searching = true; state.Lofting = false; state.TerminalCommitted = true;
                state.SearchHeading = missile.transform.forward; state.NextScan = 0f;
                if (state.ChaffTarget != null && state.ChaffHandler != null) state.ChaffTarget.onAddRadarChaff -= state.ChaffHandler;
                state.ChaffTarget = null; Target.SetValue(seeker, null); missile.SetTarget(null);
                AMRAAMPlugin.Set(seeker, "radarLockEstablished", false);
                AMRAAMPlugin.Log.LogInfo("AMRAAM searching forward without a target");
            }
            // Stock Seek returns before these steps when targetID is invalid.
            if (!(bool)Armed.GetValue(seeker) && missile.timeSinceSpawn > (float)ArmDelay.GetValue(seeker))
            { Armed.SetValue(seeker, true); missile.Arm(); missile.SetTangible(true); }
            if (!(bool)Guidance.GetValue(seeker) && missile.timeSinceSpawn > (float)GuidanceDelay.GetValue(seeker))
            { Guidance.SetValue(seeker, true); missile.DeployFins(); }
            float tolerance = (float)JamTolerance.GetValue(seeker), jam = (float)Jam.GetValue(seeker);
            jam = Mathf.Clamp01(jam - Mathf.Max(jam,.2f)*Mathf.Max(tolerance,.1f)*Time.fixedDeltaTime);
            Jam.SetValue(seeker,jam); Jammed.SetValue(seeker,jam>tolerance);
            missile.NetworkseekerMode = Missile.SeekerMode.activeSearch;
            missile.SetAimpoint(missile.GlobalPosition() + state.SearchHeading * 10000f, Vector3.zero);
            if (!(bool)Guidance.GetValue(seeker) || Time.timeSinceLevelLoad < state.NextScan) return false;
            state.NextScan = Time.timeSinceLevelLoad + .2f;
            // Registry order is the tie-breaker for simultaneous detections. No
            // nearest-target preference, and no switching while a track is valid.
            foreach (var candidate in UnitRegistry.allUnits)
            {
                if (!Eligible(missile, candidate)) continue;
                float signal = Detect(seeker, missile, candidate);
                if (!(signal > seeker.GetRadarParams().minSignal)) continue;
                Acquire(seeker, missile, state, candidate, signal); return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(ARHSeeker), "SlowChecks")]
        static class SearchLifetimePatch
        {
            static bool Prefix(ARHSeeker __instance)
            {
                var state = __instance.GetComponent<LoftState>();
                if (state == null || !state.Searching) return true;
                var missile = __instance.GetComponent<Missile>();
                if (missile.disabled) return false;
                // Keep coasting/searching after burnout while useful speed remains.
                // Retain stock cleanup for low speed; add a finite maximum search life.
                if (missile.timeSinceSpawn >= 240f)
                { missile.Detonate(missile.rb.velocity, false, false); return false; }
                return !missile.EngineOn() && missile.speed < __instance.GetMinSpeed();
            }
        }
    }
}

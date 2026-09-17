using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// Kills the random aim error the game rolls into every attack run.
    ///
    /// <c>EngagementAI.SetAttackOffset</c> returns a random horizontal offset of the given
    /// radius, and every approach — level, dive, torpedo, strafe — adds it to the point the
    /// aircraft is flying at. Zeroing it for player aircraft is what makes the run itself
    /// aimed true; guidance then only has to clean up the physics, not a deliberate error.
    /// </summary>
    [HarmonyPatch(typeof(EngagementAI), "SetAttackOffset")]
    internal static class SetAttackOffsetPatch
    {
        private static void Postfix(Unit unit, ref Vector3 __result)
        {
            if (!AceAviatorsPlugin.Enabled || unit == null || unit.faction != Faction.Player)
            {
                return;
            }
            __result = Vector3.zero;
        }
    }

    /// <summary>
    /// Records who is shooting, for the length of one weapon discharge.
    ///
    /// Ammunition is created inside <c>Weapon.FireWeapon</c> and does not learn its
    /// <c>firedByUnitID</c> until after it has been initialised, so a round cannot identify
    /// its own shooter at the moment this mod needs to decide whether to fly it. The firing
    /// unit is stashed here instead and read by <see cref="InitialiseBallisticsPatch"/>,
    /// which the same call runs synchronously.
    ///
    /// <c>WeaponRepeat</c> — the class aircraft bomb racks and torpedo gear actually use —
    /// overrides <c>FireWeapon</c> and chains to this one, so patching the base covers it.
    /// <c>WeaponTorpedoTube</c> overrides without chaining, which is why ship torpedoes never
    /// pick up guidance.
    /// </summary>
    [HarmonyPatch(typeof(Weapon), "FireWeapon")]
    internal static class FireWeaponPatch
    {
        internal static Unit FiringUnit;
        internal static Unit FiringTarget;
        internal static int RoundIndex;

        private static void Prefix(Weapon __instance)
        {
            FiringUnit = null;
            FiringTarget = null;
            RoundIndex = 0;

            // In AimOnly mode nothing is stashed, so nothing downstream registers a round and
            // no ordnance is ever steered. This one gate is what separates the two modes.
            if (!AceAviatorsPlugin.GuidanceActive || __instance == null)
            {
                return;
            }
            Unit shooter = __instance.parentUnit;
            if (shooter == null || shooter.faction != Faction.Player || shooter.unitAir == null ||
                shooter.unitAI == null)
            {
                return;
            }
            // Deliberately not gated on the weapon's declared ammoType: what gets guided is
            // decided from the round that actually comes off the rack, in the postfix below.
            Unit target = shooter.unitAI.focusedUnit;
            if (target == null || target.isDestroyed || target.unitSea == null)
            {
                target = TargetResolver.ResolveManualDropTarget(shooter);
            }
            if (target == null)
            {
                return;
            }

            FiringUnit = shooter;
            FiringTarget = target;
        }

        /// <summary>
        /// A finalizer rather than a postfix, so the stash is cleared even if the discharge
        /// throws. Returning void without touching <c>__exception</c> leaves any exception to
        /// propagate exactly as it would unpatched.
        /// </summary>
        private static void Finalizer()
        {
            FiringUnit = null;
            FiringTarget = null;
            RoundIndex = 0;
        }
    }

    /// <summary>
    /// Puts a round on the guidance list as it is launched — or takes the pooled object it is
    /// reusing off that list, so a recycled enemy bomb never inherits a player bomb's target.
    /// </summary>
    [HarmonyPatch(typeof(AmmunitionMoveBallistic), "InitialiseBallistics")]
    internal static class InitialiseBallisticsPatch
    {
        private static void Postfix(AmmunitionMoveBallistic __instance)
        {
            Ammunition ammo = __instance == null ? null : __instance.parentAmmunition;
            if (ammo == null)
            {
                return;
            }

            Unit target = FireWeaponPatch.FiringTarget;
            if (!AceAviatorsPlugin.GuidanceActive || target == null || !GuidanceRegistry.IsGuided(ammo.ammoType))
            {
                GuidanceTracker.Register(ammo, null);
                return;
            }

            int index = FireWeaponPatch.RoundIndex++;
            var round = new GuidedRound
            {
                Ammo = ammo,
                Body = ammo.ammoRigidbody,
                Target = target,
                AimOffsetAlongHull = AceAviatorsPlugin.SpreadSticksAlongHull
                    ? TargetResolver.HullSpreadOffset(index, target)
                    : 0f
            };
            GuidanceTracker.Register(ammo, round);

            if (AceAviatorsPlugin.VerboseLogging)
            {
                AceAviatorsPlugin.Log.Info("guiding " + ammo.ammoType + " #" + index + " from " +
                                           FireWeaponPatch.FiringUnit.name + " onto " + target.name);
            }
        }
    }

    /// <summary>
    /// Steers a guided round through the air, immediately before the game integrates its
    /// ballistic step. Running as a prefix on the game's own per-round update — rather than
    /// from a separate <c>FixedUpdate</c> — is what makes the ordering deterministic.
    /// </summary>
    [HarmonyPatch(typeof(AmmunitionMoveBallistic), "BallisticFixedUpdate")]
    internal static class BallisticFixedUpdatePatch
    {
        private static void Prefix(AmmunitionMoveBallistic __instance)
        {
            if (!AceAviatorsPlugin.GuidanceActive || __instance == null)
            {
                return;
            }
            Ammunition ammo = __instance.parentAmmunition;
            if (ammo == null || ammo.appliedDamage)
            {
                return;
            }
            GuidedRound round = GuidanceTracker.Get(ammo);
            if (round == null)
            {
                return;
            }
            if (round.TargetLost)
            {
                GuidanceTracker.Forget(ammo);
                return;
            }
            OrdnanceGuidance guidance = GuidanceRegistry.For(ammo.ammoType);
            if (guidance != null)
            {
                guidance.GuideInAir(round);
            }
        }
    }

    /// <summary>
    /// Steers a guided torpedo through its underwater run, immediately before the game moves
    /// it. <c>AmmunitionMoveTorpedo.FixedUpdate</c> is private, hence the explicit target.
    /// </summary>
    [HarmonyPatch]
    internal static class TorpedoFixedUpdatePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AmmunitionMoveTorpedo), "FixedUpdate");
        }

        private static void Prefix(AmmunitionMoveTorpedo __instance)
        {
            if (!AceAviatorsPlugin.GuidanceActive || __instance == null)
            {
                return;
            }
            Ammunition ammo = __instance.parentAmmunition;
            if (ammo == null || ammo.appliedDamage)
            {
                return;
            }
            GuidedRound round = GuidanceTracker.Get(ammo);
            if (round == null)
            {
                return;
            }
            if (round.TargetLost)
            {
                GuidanceTracker.Forget(ammo);
                return;
            }
            OrdnanceGuidance guidance = GuidanceRegistry.For(ammo.ammoType);
            if (guidance != null)
            {
                guidance.GuideInWater(round, __instance);
            }
        }
    }

    /// <summary>
    /// A guided round that reaches its target is not allowed to be a dud.
    ///
    /// The dud roll lives inside <c>ExplodeAmmunition</c>, so the rate is suppressed for the
    /// duration of that call and then put back. Restoring it matters: ammunition objects are
    /// pooled, and a permanently zeroed rate would follow the object into an enemy unit's
    /// hands on its next use.
    /// </summary>
    [HarmonyPatch(typeof(Ammunition), "ExplodeAmmunition")]
    internal static class ExplodeAmmunitionPatch
    {
        /// <summary>A negative <c>__state</c> means this round was left alone and needs no restore.</summary>
        private const float Untouched = -1f;

        private static void Prefix(Ammunition __instance, out float __state)
        {
            __state = Untouched;
            if (!AceAviatorsPlugin.GuidanceActive || __instance == null)
            {
                return;
            }
            if (GuidanceTracker.Get(__instance) == null)
            {
                return;
            }
            __state = __instance.dudRate;
            __instance.dudRate = 0f;
        }

        private static void Postfix(Ammunition __instance, float __state)
        {
            if (__instance == null)
            {
                return;
            }
            // Every detonation in the game passes through here, so only rounds this mod
            // actually suppressed get written back to.
            if (__state >= 0f)
            {
                __instance.dudRate = __state;
            }
            GuidanceTracker.Forget(__instance);
        }
    }

    /// <summary>
    /// Drops a round from the guidance list when it is retired without detonating — its run
    /// timer expired, or it fell outside the play area. Nothing breaks without this, because
    /// the next use of the pooled object re-registers it either way; it keeps the tracker's
    /// count, and therefore the status line, honest.
    /// </summary>
    [HarmonyPatch(typeof(Ammunition), "DestroyAmmunitionObject")]
    internal static class DestroyAmmunitionObjectPatch
    {
        private static void Prefix(Ammunition __instance)
        {
            GuidanceTracker.Forget(__instance);
        }
    }

    /// <summary>
    /// Finding a target for a release, and laying a stick of bombs across a hull.
    /// </summary>
    internal static class TargetResolver
    {
        /// <summary>How far ahead of the aircraft to look for an unstated target, in world units.</summary>
        private const float ManualDropSearchRange = 30f;

        /// <summary>Half-angle of the forward cone searched for an unstated target, in degrees.</summary>
        private const float ManualDropSearchCone = 60f;

        /// <summary>
        /// The target for a release the aircraft has no assigned focus for — the "drop now"
        /// button pressed on an unassigned bomber. Rather than invent a target, this picks the
        /// nearest enemy ship the aircraft is already flying at, inside a forward cone, and
        /// returns null when there is no such ship. A release with no answer here simply falls
        /// ballistically, exactly as it does without this mod.
        /// </summary>
        internal static Unit ResolveManualDropTarget(Unit shooter)
        {
            if (EngagementManager.instance == null || EngagementManager.instance.allSeaUnits == null)
            {
                return null;
            }

            List<Unit> candidates = EngagementManager.instance.allSeaUnits;
            Vector3 origin = shooter.transform.position;
            Vector3 heading = shooter.transform.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-6f)
            {
                return null;
            }
            heading = heading.normalized;

            float coneDot = Mathf.Cos(ManualDropSearchCone * Mathf.Deg2Rad);
            Unit best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Unit candidate = candidates[i];
                if (candidate == null || candidate.isDestroyed || candidate.unitSea == null)
                {
                    continue;
                }
                if (candidate.faction == shooter.faction || candidate.faction == Faction.Neutral)
                {
                    continue;
                }
                Vector3 toTarget = candidate.transform.position - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance > ManualDropSearchRange || distance < 0.01f)
                {
                    continue;
                }
                if (Vector3.Dot(heading, toTarget / distance) < coneDot)
                {
                    continue;
                }
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// Where the n-th bomb of a stick should aim along the hull, in world units, measured
        /// from the target's centre. Offsets alternate outward from the middle — 0, +1, -1,
        /// +2, -2 — so a stick of any length stays centred on the ship without the caller
        /// needing to know how many bombs are coming. The result is clamped to the hull so
        /// every bomb of a long stick still lands on the ship.
        /// </summary>
        internal static float HullSpreadOffset(int index, Unit target)
        {
            if (target == null || target.unitData == null)
            {
                return 0f;
            }
            // unitData.length is in metres; the game world is 1 unit = 10 metres.
            float hull = target.unitData.length * 0.1f;
            if (hull <= 0f || index <= 0)
            {
                return 0f;
            }

            int step = (index + 1) / 2;
            float sign = (index % 2 == 1) ? 1f : -1f;
            float offset = sign * step * (hull * 0.12f);
            float limit = hull * 0.35f;
            return Mathf.Clamp(offset, -limit, limit);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace WoTSFireControl
{
    /// <summary>
    /// Tightens a ship's close-in anti-aircraft fire, and can put it back.
    ///
    /// AA here is not a shot that hits or misses. WeaponAAA points a mount at an exact intercept
    /// point - Utilities.GetInterceptPoint, which carries no randomness - and emits a particle
    /// stream; a hit is a particle physically intersecting the aircraft. So the random miss is
    /// exactly one thing: particles that leave inside the emitter's cone and do not intersect.
    ///
    /// Two settings fix that, and they only work together. Collapsing the cone alone makes AA
    /// WORSE, because the cone is quietly covering for a second error: the mount re-aims only
    /// once per WeaponAAA.interval and snaps there with LookAt, so between updates every
    /// particle is fired at a point going stale at the aircraft's own speed. A tight cone on a
    /// stale aim point is a laser pointed where the aircraft used to be. The logic harness
    /// measures this - at the stock 0.5 s update a zero cone lands under two thirds of its
    /// particles, while a tight cone plus a 0.1 s update lands all of them.
    ///
    /// No guidance is added. The rounds still fly where the mount points them.
    /// </summary>
    internal static class AaaTuning
    {
        /// <summary>What a mount looked like before this mod touched it.</summary>
        private sealed class Original
        {
            internal float Interval;
            internal float[] ConeAngles;
            internal float[] ConeRadii;
        }

        private static readonly Dictionary<int, Original> originals = new Dictionary<int, Original>();

        /// <summary>
        /// Applies the configured cone and aim rate to one mount, remembering what it replaced.
        /// Enemy ships are never touched.
        /// </summary>
        internal static void Apply(WeaponAAA mount)
        {
            if (!IsEligible(mount))
            {
                return;
            }

            Original original = Remember(mount);
            if (original == null)
            {
                return;
            }

            mount.interval = FireControlPlugin.AaaAimInterval;

            for (int i = 0; i < mount.aaaParticles.Length; i++)
            {
                ParticleSystem particles = mount.aaaParticles[i];
                if (particles == null)
                {
                    continue;
                }
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.angle = FireControlPlugin.AaaConeDegrees;
                shape.radius = original.ConeRadii[i] * FireControlPlugin.AaaMuzzleSpreadScale;
            }
        }

        /// <summary>Puts a mount back exactly as it was, so toggling the mod off is complete.</summary>
        internal static void Restore(WeaponAAA mount)
        {
            if (mount == null)
            {
                return;
            }
            Original original;
            if (!originals.TryGetValue(mount.GetInstanceID(), out original))
            {
                return;
            }

            mount.interval = original.Interval;
            for (int i = 0; i < mount.aaaParticles.Length && i < original.ConeAngles.Length; i++)
            {
                ParticleSystem particles = mount.aaaParticles[i];
                if (particles == null)
                {
                    continue;
                }
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.angle = original.ConeAngles[i];
                shape.radius = original.ConeRadii[i];
            }
        }

        /// <summary>
        /// Walks every player ship afloat and applies or restores. Used when the switch is
        /// thrown mid-battle, so the change takes effect without waiting for a respawn.
        /// </summary>
        internal static void ApplyToEveryPlayerShip(bool apply)
        {
            if (EngagementManager.instance == null || EngagementManager.instance.allSeaUnits == null)
            {
                return;
            }
            List<Unit> units = EngagementManager.instance.allSeaUnits;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || unit.weapons == null)
                {
                    continue;
                }
                for (int w = 0; w < unit.weapons.Count; w++)
                {
                    WeaponAAA mount = unit.weapons[w] as WeaponAAA;
                    if (mount == null)
                    {
                        continue;
                    }
                    if (apply)
                    {
                        Apply(mount);
                    }
                    else
                    {
                        Restore(mount);
                    }
                }
            }
        }

        /// <summary>Drops every remembered mount, for a battle ending or a scene change.</summary>
        internal static void Forget()
        {
            originals.Clear();
        }

        private static bool IsEligible(WeaponAAA mount)
        {
            return mount != null
                   && mount.parentUnit != null
                   && mount.parentUnit.faction == Faction.Player
                   && mount.aaaParticles != null
                   && mount.aaaParticles.Length > 0;
        }

        /// <summary>
        /// Records a mount's stock settings the first time it is seen. Reading them before the
        /// first write is what makes Restore honest; capturing them later would just save this
        /// mod's own values back over the originals.
        /// </summary>
        private static Original Remember(WeaponAAA mount)
        {
            int key = mount.GetInstanceID();
            Original original;
            if (originals.TryGetValue(key, out original))
            {
                return original;
            }

            original = new Original
            {
                Interval = mount.interval,
                ConeAngles = new float[mount.aaaParticles.Length],
                ConeRadii = new float[mount.aaaParticles.Length]
            };
            for (int i = 0; i < mount.aaaParticles.Length; i++)
            {
                ParticleSystem particles = mount.aaaParticles[i];
                if (particles == null)
                {
                    continue;
                }
                ParticleSystem.ShapeModule shape = particles.shape;
                original.ConeAngles[i] = shape.angle;
                original.ConeRadii[i] = shape.radius;
            }
            originals[key] = original;
            return original;
        }
    }
}

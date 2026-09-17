using System;
using UnityEngine;
using WoTSAceAviators;

/// <summary>
/// Flies the real guidance code (src\BombGuidance.cs and src\AerialTorpedoGuidance.cs are
/// compiled into this program, not copied) and reports where the ordnance actually ends up.
///
/// Three columns everywhere, because the mod now has two modes and both need a control:
///
///   vanilla    the game as shipped, including the random aim offset it rolls into the run
///   aim only   that offset forced to zero, ordnance otherwise untouched  (the mod's default)
///   guided     aim offset zeroed and the round steered onto the target
///
/// WHAT IS AND IS NOT MODELLED. Level bombing and the aerial torpedo run are reproduced from
/// the game's own release logic - the lead times, standoff distances, release radii and AI
/// tick rate are the literal values in EngagementAI and UnitAIAir - so their vanilla and
/// aim-only columns mean something. Dive bombing is NOT: its release depends on
/// Config.diveBombManeuvreRate and the aircraft flight model, neither of which can be read
/// out of the assembly, so no vanilla baseline is claimed for it and it appears only in the
/// guidance-capability section below.
///
/// Units follow the game: 1 world unit = 10 metres, gravity applied by AddForce at mass 1,
/// physics stepped at 50 Hz, unit AI stepped at 10 Hz.
/// </summary>
internal static class Sim
{
    private const float Tick = 0.02f;
    private const float AiTick = 0.1f;
    private const float WorldUnitsPerMetre = 0.1f;

    /// <summary>Samples per case. The aim offset and release height are redrawn for each.</summary>
    private const int Samples = 400;

    private static readonly Random Rng = new Random(20260916);

    private static int failures;

    private static int Main()
    {
        Console.WriteLine("Ace Aviators guidance simulation");
        Console.WriteLine("1 world unit = 10 m; gravity " + (-Physics.gravity.y) +
                          " u/s^2; physics " + Tick + " s; unit AI " + AiTick + " s; " +
                          Samples + " samples/case");
        Console.WriteLine();

        AceAviatorsPlugin.BombGuidanceAuthority = 40f;
        AceAviatorsPlugin.TorpedoTurnRate = 25f;

        Console.WriteLine("=== Level bombing from 150 m, game's own release logic ===");
        Console.WriteLine("    (release point per UnitAIAir.LevelBombApproach; aim offset radius 7 = 70 m)");
        Header();
        LevelBombCase("Light cruiser 180x18 m, 20 kn steady", 180f, 18f, 20f, 0f);
        LevelBombCase("Light cruiser 180x18 m, 30 kn turning", 180f, 18f, 30f, -3f);
        LevelBombCase("Destroyer   110x11 m, 33 kn turning", 110f, 11f, 33f, -4f);
        LevelBombCase("Battleship  250x33 m, 27 kn turning", 250f, 33f, 27f, -2f);
        Note("aim only is deterministic - one geometry, so its column is all-or-nothing, not a rate.");
        Note("it leaves a systematic 10-20 m short bias from the 2-unit release radius and the");
        Note("0.1 s AI tick, plus a few metres across from the straight-line lead. Comfortably");
        Note("inside a cruiser or battleship; comparable to a destroyer's beam, hence marginal there.");

        Console.WriteLine();
        Console.WriteLine("=== Aerial torpedo, game's own release logic ===");
        Console.WriteLine("    (28 s lead, 605 m standoff per UnitAIAir.TorpedoApproach; aim offset radius 8 = 80 m)");
        Header();
        TorpedoCase("Light cruiser 180x18 m, 25 kn steady", 180f, 18f, 25f, 0f);
        TorpedoCase("Light cruiser 180x18 m, 30 kn turning", 180f, 18f, 30f, -3f);
        TorpedoCase("Destroyer   110x11 m, 33 kn combing", 110f, 11f, 33f, 5f);
        TorpedoCase("Destroyer   110x11 m, 33 kn turning", 110f, 11f, 33f, -5f);
        Note("removing the aim offset barely moves an aerial torpedo: a straight-running fish is");
        Note("beaten by the course change, not by the aim error. Guidance is what fixes torpedoes.");

        Console.WriteLine();
        Console.WriteLine("=== Guidance capability: how bad a release it can still salvage ===");
        Console.WriteLine("    (synthetic release displaced across the target's track, guidance on)");
        EnvelopeSweep("Released from 150 m", 15f);
        EnvelopeSweep("Released from  60 m", 6f);
        EnvelopeSweep("Released from 350 m", 35f);

        Console.WriteLine();
        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " guided case(s) fell short of a clean sweep.");
            return 1;
        }
        Console.WriteLine("Guided mode hit on every sample of every modelled case.");
        return 0;
    }

    private static void Note(string text)
    {
        Console.WriteLine("    note: " + text);
    }

    private static void Header()
    {
        Console.WriteLine("    " + "".PadRight(34) + "vanilla   aim only  guided");
    }

    // --- level bombing ------------------------------------------------------------------

    private static void LevelBombCase(string label, float hullMetres, float beamMetres,
                                      float targetKnots, float turnDegPerSec)
    {
        var vanilla = new Tally(hullMetres, beamMetres);
        var aimOnly = new Tally(hullMetres, beamMetres);
        var guided = new Tally(hullMetres, beamMetres);

        for (int i = 0; i < Samples; i++)
        {
            // EngagementAI.LevelBombTarget: attackOffset = SetAttackOffset(unit, 7f).
            Vector3 offset = RandomAttackOffset(7f);
            vanilla.Add(FlyLevelBombRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, offset, guided: false));
            aimOnly.Add(FlyLevelBombRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, Vector3.zero, guided: false));
            guided.Add(FlyLevelBombRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, Vector3.zero, guided: true));
        }

        ReportRow(label, vanilla, aimOnly, guided);
    }

    /// <summary>
    /// One level bombing run, from the bomber's approach to the bomb's impact. Returns the
    /// impact point in the target's own frame at that instant, so the hull test is exact.
    ///
    /// Release logic is UnitAIAir.LevelBombApproach as written: the aim point is the target
    /// led by the bomb's fall time, pulled back along the bomber's heading by the distance the
    /// bomber covers during that fall, plus the attack offset; the bomber releases the moment
    /// it is within attackRange (2 units) of that point, tested once per 0.1 s AI tick.
    /// </summary>
    private static Impact FlyLevelBombRun(float hullMetres, float beamMetres, float targetKnots,
                                          float turnDegPerSec, Vector3 attackOffset, bool guided)
    {
        Unit target = MakeTarget(targetKnots, hullMetres, beamMetres);

        float releaseHeight = 15f;
        float bomberSpeed = Knots(200f);

        // The bomber runs in up the target's wake, starting a kilometre behind the release point.
        Vector3 heading = target.transform.forward;
        Vector3 bomber = target.transform.position - heading * 100f;
        bomber.y = releaseHeight;

        var ammo = new Ammunition();
        Rigidbody body = ammo.ammoRigidbody;
        bool released = false;
        float aiTimer = 0f;

        for (int step = 0; step < 20000 && !released; step++)
        {
            aiTimer += Tick;
            if (aiTimer >= AiTick)
            {
                aiTimer = 0f;

                // Utilities.GetFallTimeToTarget ignores the bomb's initial vertical speed,
                // which is right for level flight.
                float lead = Mathf.Sqrt(2f * bomber.y / -Physics.gravity.y);
                Vector3 aim = target.transform.position + target.transform.forward * (target.currentActualSpeed * lead);
                aim.y = bomber.y;
                aim = aim - heading * (lead * bomberSpeed);
                aim = aim + attackOffset;

                if (HorizontalDistance(bomber, aim) < 2f)
                {
                    released = true;
                    ammo.transform.position = bomber;
                    body.velocity = heading * bomberSpeed;
                    break;
                }
                heading = SteerToward(heading, aim - bomber);
            }

            bomber = bomber + heading * (bomberSpeed * Tick);
            AdvanceTarget(target, Tick, turnDegPerSec);
        }

        if (!released)
        {
            return Impact.Missed;
        }
        return FallToTarget(ammo, body, target, turnDegPerSec, guided, new GuidedRound
        {
            Ammo = ammo,
            Body = body,
            Target = target
        });
    }

    // --- aerial torpedo -----------------------------------------------------------------

    private static void TorpedoCase(string label, float hullMetres, float beamMetres,
                                    float targetKnots, float turnDegPerSec)
    {
        var vanilla = new Tally(hullMetres, beamMetres);
        var aimOnly = new Tally(hullMetres, beamMetres);
        var guided = new Tally(hullMetres, beamMetres);

        for (int i = 0; i < Samples; i++)
        {
            // EngagementAI.AerialTorpedoTarget: attackOffset = SetAttackOffset(unit, 8f).
            Vector3 offset = RandomAttackOffset(8f);
            vanilla.Add(SwimTorpedoRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, offset, guided: false));
            aimOnly.Add(SwimTorpedoRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, Vector3.zero, guided: false));
            guided.Add(SwimTorpedoRun(hullMetres, beamMetres, targetKnots, turnDegPerSec, Vector3.zero, guided: true));
        }

        ReportRow(label, vanilla, aimOnly, guided);
    }

    /// <summary>
    /// One aerial torpedo run, from the drop point the game computes to closest approach.
    ///
    /// The drop point is solved at its fixed point rather than by flying the approach. In
    /// UnitAIAir.TorpedoApproach the aircraft steers at
    ///     attackPosition = target led by 28 s  -  aircraftHeading * (28 * 2.16)  +  attackOffset
    /// where 2.16 u/s is the torpedo's own speed, so the standoff term depends on the very
    /// heading the aircraft is converging to. Simulating that loop needs the aircraft flight
    /// model, which cannot be read out of the assembly; solving it instead is exact and
    /// invents nothing. An aircraft flying heading h arrives at
    ///     R = leadPoint + approachBearing * 60.5 + attackOffset
    /// and drops a torpedo running along h - so the fish ends its 28 s run at leadPoint plus
    /// the attack offset, while the ship is at leadPoint. That is the error mechanism: for an
    /// aerial torpedo the offset translates straight into miss distance.
    /// </summary>
    private static Impact SwimTorpedoRun(float hullMetres, float beamMetres, float targetKnots,
                                         float turnDegPerSec, Vector3 attackOffset, bool guided)
    {
        Unit target = MakeTarget(targetKnots, hullMetres, beamMetres);

        const float LeadTime = 28f;
        const float TorpedoSpeed = 2.16f;
        float standoff = LeadTime * TorpedoSpeed;

        Vector3 forward = target.transform.forward;
        Vector3 beam = new Vector3(forward.z, 0f, -forward.x);

        Vector3 leadPoint = target.transform.position + forward * (target.currentActualSpeed * LeadTime);
        Vector3 entry = leadPoint + beam * standoff + attackOffset;
        entry.y = -0.2f;

        var torpedo = new AmmunitionMoveTorpedo();
        torpedo.runSpeed = TorpedoSpeed;
        torpedo.currentSpeed = TorpedoSpeed;
        torpedo.transform.position = entry;
        torpedo.transform.forward = (beam * -1f).normalized;

        var round = new GuidedRound { Ammo = torpedo.parentAmmunition, Target = target };
        var guidance = new AerialTorpedoGuidance();

        Impact closest = Impact.Missed;
        float closestRange = float.MaxValue;
        for (int step = 0; step < 20000; step++)
        {
            if (guided)
            {
                guidance.GuideInWater(round, torpedo);
            }
            torpedo.transform.position = torpedo.transform.position +
                                         torpedo.transform.forward * (torpedo.currentSpeed * Tick);
            AdvanceTarget(target, Tick, turnDegPerSec);

            Impact here = ToTargetFrame(torpedo.transform.position, target);
            if (here.OnHull(hullMetres, beamMetres))
            {
                return here;
            }

            float range = HorizontalDistance(torpedo.transform.position, target.transform.position);
            if (range < closestRange)
            {
                closestRange = range;
                closest = here;
            }
            else if (range > closestRange + 2f)
            {
                // Opening the range again: the run is decided.
                break;
            }
        }
        return closest;
    }

    // --- guidance capability ------------------------------------------------------------

    /// <summary>
    /// Walks a synthetic release further and further off the correct point and reports the
    /// last displacement guidance still turns into a hit. This is the figure that says whether
    /// the configured authority is enough, and it is measured rather than assumed.
    /// </summary>
    private static void EnvelopeSweep(string label, float releaseHeight)
    {
        float lastHit = 0f;
        float firstMiss = float.MaxValue;
        for (float displacement = 0f; displacement <= 80f; displacement += 1f)
        {
            Impact impact = FlySyntheticDrop(releaseHeight, 30f, -3f, new Vector3(displacement, 0f, 0f));
            if (impact.OnHull(180f, 18f))
            {
                lastHit = displacement;
            }
            else
            {
                firstMiss = displacement;
                break;
            }
        }
        string limit = firstMiss >= float.MaxValue * 0.5f
            ? "salvages anything up to the " + Metres(80f) + " sweep limit"
            : "salvages up to " + Metres(lastHit) + ", misses at " + Metres(firstMiss);
        Console.WriteLine("  " + label.PadRight(34) + limit);
    }

    /// <summary>
    /// A bomb dropped from level flight at the given height, displaced from the correct
    /// release point by <paramref name="releaseError"/>, always guided. Used only to measure
    /// guidance authority, never to claim anything about the game's own accuracy.
    /// </summary>
    private static Impact FlySyntheticDrop(float releaseHeight, float targetKnots,
                                           float turnDegPerSec, Vector3 releaseError)
    {
        Unit target = MakeTarget(targetKnots, 180f, 18f);

        var ammo = new Ammunition();
        Rigidbody body = ammo.ammoRigidbody;

        float gravity = -Physics.gravity.y / body.mass;
        float fallTime = Mathf.Sqrt(2f * releaseHeight / gravity);
        float bomberSpeed = Knots(200f);

        Vector3 heading = target.transform.forward;
        Vector3 aim = target.transform.position +
                      target.transform.forward * (target.currentActualSpeed * fallTime) + releaseError;
        Vector3 release = aim - heading * (bomberSpeed * fallTime);
        release.y = releaseHeight;

        ammo.transform.position = release;
        body.velocity = heading * bomberSpeed;

        return FallToTarget(ammo, body, target, turnDegPerSec, guided: true, round: new GuidedRound
        {
            Ammo = ammo,
            Body = body,
            Target = target
        });
    }

    // --- shared bomb flight -------------------------------------------------------------

    /// <summary>
    /// Integrates a released bomb the way the game does - guidance first, then the ballistic
    /// step - and returns the impact point in the target's frame, interpolated to the exact
    /// deck crossing so the answer is not quantised by the physics tick.
    /// </summary>
    private static Impact FallToTarget(Ammunition ammo, Rigidbody body, Unit target,
                                       float turnDegPerSec, bool guided, GuidedRound round)
    {
        var guidance = new BombGuidance();
        float gravity = -Physics.gravity.y / body.mass;
        float deckHeight = target.transform.position.y + 0.3f;

        for (int step = 0; step < 20000; step++)
        {
            if (guided)
            {
                guidance.GuideInAir(round);
            }

            body.velocity = new Vector3(body.velocity.x, body.velocity.y - gravity * Tick, body.velocity.z);
            Vector3 next = ammo.transform.position + body.velocity * Tick;

            if (next.y <= deckHeight)
            {
                float span = ammo.transform.position.y - next.y;
                float fraction = span <= 0f ? 1f : (ammo.transform.position.y - deckHeight) / span;
                Vector3 point = ammo.transform.position + (next - ammo.transform.position) * fraction;
                AdvanceTarget(target, Tick * fraction, turnDegPerSec);
                return ToTargetFrame(point, target);
            }

            ammo.transform.position = next;
            AdvanceTarget(target, Tick, turnDegPerSec);
        }
        return Impact.Missed;
    }

    // --- scaffolding --------------------------------------------------------------------

    /// <summary>Where a round landed, in the target's own frame, in world units.</summary>
    private struct Impact
    {
        public float AlongTrack;
        public float CrossTrack;
        public bool Valid;

        public static Impact Missed
        {
            get { return new Impact { Valid = false }; }
        }

        public float Range
        {
            get { return Mathf.Sqrt(AlongTrack * AlongTrack + CrossTrack * CrossTrack); }
        }

        public bool OnHull(Unit target)
        {
            return OnHull(target.unitData.length, target.unitData.width);
        }

        public bool OnHull(float hullMetres, float beamMetres)
        {
            if (!Valid)
            {
                return false;
            }
            float halfLength = hullMetres * WorldUnitsPerMetre * 0.5f;
            float halfBeam = beamMetres * WorldUnitsPerMetre * 0.5f;
            return Math.Abs(AlongTrack) <= halfLength && Math.Abs(CrossTrack) <= halfBeam;
        }
    }

    private sealed class Tally
    {
        private readonly float hullMetres;
        private readonly float beamMetres;
        private int hits;
        private int total;
        private float missSum;
        private float alongSum;
        private float crossSum;

        public Tally(float hullMetres, float beamMetres)
        {
            this.hullMetres = hullMetres;
            this.beamMetres = beamMetres;
        }

        public void Add(Impact impact)
        {
            total++;
            if (impact.Valid)
            {
                missSum += impact.Range;
                alongSum += impact.AlongTrack;
                crossSum += impact.CrossTrack;
            }
            else
            {
                missSum += 100f;
            }
            if (impact.OnHull(hullMetres, beamMetres))
            {
                hits++;
            }
        }

        public float HitRate
        {
            get { return total == 0 ? 0f : 100f * hits / total; }
        }

        public float MeanMiss
        {
            get { return total == 0 ? 0f : missSum / total; }
        }

        public float MeanAlongTrack
        {
            get { return total == 0 ? 0f : alongSum / total; }
        }

        public float MeanCrossTrack
        {
            get { return total == 0 ? 0f : crossSum / total; }
        }
    }

    private static Unit MakeTarget(float knots, float hullMetres, float beamMetres)
    {
        var target = new Unit();
        target.transform.position = Vector3.zero;
        target.transform.forward = new Vector3(0f, 0f, 1f);
        target.currentSpeed = Knots(knots);
        target.currentActualSpeed = target.currentSpeed;
        target.unitData.length = hullMetres;
        target.unitData.width = beamMetres;
        return target;
    }

    private static void AdvanceTarget(Unit target, float dt, float turnDegPerSec)
    {
        if (turnDegPerSec != 0f)
        {
            double radians = turnDegPerSec * Mathf.Deg2Rad * dt;
            Vector3 f = target.transform.forward;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            target.transform.forward = new Vector3(f.x * cos + f.z * sin, 0f, -f.x * sin + f.z * cos);
        }
        target.transform.position = target.transform.position +
                                    target.transform.forward * (target.currentActualSpeed * dt);
    }

    /// <summary>
    /// Points the aircraft straight at where the game tells it to go. The real turn rate comes
    /// from GetRequiredTurnRateFlat and the aircraft flight model, neither of which can be read
    /// out of the assembly; idealising it is the conservative choice, because it flatters the
    /// game's own accuracy rather than this mod's.
    /// </summary>
    private static Vector3 SteerToward(Vector3 heading, Vector3 desired)
    {
        desired.y = 0f;
        if (desired.sqrMagnitude < 1e-6f)
        {
            return heading;
        }
        return desired.normalized;
    }

    /// <summary>
    /// Reproduces EngagementAI.SetAttackOffset: Random.insideUnitSphere * radius with the
    /// vertical component discarded. Note that this is a point inside the ball, not on it, so
    /// the typical offset is well under the radius - which is why the vanilla column is not
    /// simply "always misses by 70 m".
    /// </summary>
    private static Vector3 RandomAttackOffset(float radius)
    {
        while (true)
        {
            float x = (float)(Rng.NextDouble() * 2.0 - 1.0);
            float y = (float)(Rng.NextDouble() * 2.0 - 1.0);
            float z = (float)(Rng.NextDouble() * 2.0 - 1.0);
            if (x * x + y * y + z * z <= 1f)
            {
                return new Vector3(x * radius, 0f, z * radius);
            }
        }
    }

    private static Impact ToTargetFrame(Vector3 point, Unit target)
    {
        Vector3 delta = point - target.transform.position;
        delta.y = 0f;
        Vector3 forward = target.transform.forward;
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        return new Impact
        {
            AlongTrack = Vector3.Dot(delta, forward),
            CrossTrack = Vector3.Dot(delta, right),
            Valid = true
        };
    }

    private static float Knots(float knots)
    {
        // Utilities.ConvertKnotToMetrePerSecond: knots -> m/s -> world units.
        return knots * 0.514444f * WorldUnitsPerMetre;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x - b.x, 0f, a.z - b.z).magnitude;
    }

    private static void ReportRow(string label, Tally vanilla, Tally aimOnly, Tally guided)
    {
        if (guided.HitRate < 100f)
        {
            failures++;
        }
        Console.WriteLine("  " + label.PadRight(34) +
                          Column(vanilla) + Column(aimOnly) + Column(guided) +
                          "   aim-only bias " + Signed(aimOnly.MeanAlongTrack) + " along, " +
                          Signed(aimOnly.MeanCrossTrack) + " across");
    }

    private static string Column(Tally tally)
    {
        return (tally.HitRate.ToString("F0") + "%").PadLeft(6) + "    ";
    }

    private static string Signed(float worldUnits)
    {
        float metres = worldUnits / WorldUnitsPerMetre;
        return (metres >= 0f ? "+" : "") + metres.ToString("F0") + " m";
    }

    private static string Metres(float worldUnits)
    {
        return (worldUnits / WorldUnitsPerMetre).ToString("F0") + " m";
    }
}

namespace WoTSAceAviators
{
    /// <summary>
    /// Stand-in for the plugin's tuning statics. The real class carries BepInEx types that
    /// have no business in a headless simulation; only these values reach the guidance.
    /// </summary>
    internal static class AceAviatorsPlugin
    {
        internal static float BombGuidanceAuthority = 40f;
        internal static float TorpedoTurnRate = 25f;
    }
}

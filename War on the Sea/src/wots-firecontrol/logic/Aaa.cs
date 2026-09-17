using System;

/// <summary>
/// Picks the anti-aircraft defaults from numbers instead of intuition.
///
/// Close-in AA in this game is not a shot that hits or misses. WeaponAAA points a mount at an
/// exact intercept point (Utilities.GetInterceptPoint, no randomness in it) and emits a particle
/// stream; a hit is a particle physically intersecting the aircraft. So "random miss" means
/// exactly one thing here: particles that leave inside the emitter's cone and do not intersect.
///
/// The obvious fix - collapse the cone to zero - is the thing this harness exists to check,
/// because there is a second error the cone is quietly covering for. The mount re-aims only
/// once per WeaponAAA.interval (0.5 s on the hull mounts, 0.2 s on the directed mounts) and
/// snaps to the intercept with LookAt. Between those updates every particle is fired at a point
/// that is going stale at the target's own speed. A zero cone with a stale aim point is a laser
/// pointed where the aircraft used to be.
///
/// So the two knobs have to be chosen together, and that is what this prints.
///
/// MODELLING ASSUMPTIONS, stated because they set the answer:
///   * particle speed 80 u/s, read off WeaponAAA.InitialiseAAA (startLifetime = range / 80).
///   * effective aircraft radius 0.6 u (6 m) - a strike aircraft's presented size. The absolute
///     hit fractions scale with this; the comparison between settings does not.
///   * the aircraft flies a constant-rate turn, which is what an attack run and a break away
///     both look like. The intercept solve assumes a straight line, so the turn is what it
///     cannot predict - the same limitation the game's own solve has.
///   * the vanilla cone angle lives in the particle prefab and cannot be read out of the
///     assembly, so no vanilla baseline is claimed. The table is for choosing settings.
/// </summary>
internal static class Aaa
{
    private const float ParticleSpeed = 80f;
    private const float AircraftRadius = 0.6f;
    private const float Tick = 0.005f;

    private static readonly Random Rng = new Random(20260917);

    private static int Main()
    {
        Console.WriteLine("Fire Control - anti-aircraft cone and aim-rate study");
        Console.WriteLine("particle speed " + ParticleSpeed + " u/s; aircraft radius " +
                          AircraftRadius + " u (" + (AircraftRadius * 10f) + " m)");
        Console.WriteLine();

        float[] cones = { 0f, 0.25f, 0.5f, 1f, 2f, 4f };
        float[] intervals = { 0.5f, 0.2f, 0.1f, 0.05f };

        Console.WriteLine("Hit fraction, attacking aircraft: 220 kn, 2 deg/s turn, 15 u (150 m) out");
        Table(cones, intervals, knots: 220f, turnDegPerSec: 2f, range: 15f);

        Console.WriteLine();
        Console.WriteLine("Hit fraction, jinking aircraft: 260 kn, 8 deg/s turn, 20 u (200 m) out");
        Table(cones, intervals, knots: 260f, turnDegPerSec: 8f, range: 20f);

        Console.WriteLine();
        Console.WriteLine("Reading it: down a column, tightening the cone only helps once the aim");
        Console.WriteLine("is fresh enough to be worth converging on. Across a row, a faster aim");
        Console.WriteLine("update does nothing on its own if the cone is still spraying.");

        Console.WriteLine();
        Gunnery();
        return 0;
    }

    /// <summary>
    /// What maxing the fire-control solution is actually worth.
    ///
    /// Director.CalculateTMAAgainstTarget accumulates currentSolution by baseRate once per
    /// second and clamps it to 0.99. The solution is then spent as an aiming error: the method
    /// computes errorFraction = 1 - currentSolution and perturbs the range it reports to the
    /// guns by lastRange * tmaMaxRangeError * Random.Range(-errorFraction, +errorFraction).
    ///
    /// tmaMaxRangeError is set from campaign JSON and cannot be read out of the assembly, so
    /// the error below is quoted as a fraction OF IT rather than in metres. That is enough to
    /// compare settings, which is what this is for.
    /// </summary>
    private static void Gunnery()
    {
        Console.WriteLine("Fire-control solution: seconds to converge, and the aiming error left");
        Console.WriteLine("    (error is the expected |range error| as a fraction of tmaMaxRangeError)");
        Console.WriteLine();
        Console.WriteLine("    gain/tick      solution after 1s   5s    15s    60s    error at 60s");

        foreach (float gain in new[] { 0.02f, 0.05f, 0.1f, 0.5f, 1f })
        {
            Console.Write(("    " + gain.ToString("0.00")).PadRight(19));
            float after60 = 0f;
            foreach (int seconds in new[] { 1, 5, 15, 60 })
            {
                float solution = Converge(gain, seconds);
                after60 = solution;
                Console.Write(solution.ToString("0.00").PadLeft(6));
            }
            // Random.Range(-e, +e) has expected magnitude e/2.
            float error = (1f - after60) / 2f;
            Console.Write(("      " + error.ToString("0.000")).PadLeft(16));
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("    A gain of 1.00 is the mod's default: full solution on the first tick.");
        Console.WriteLine("    The residual 0.005 at a maxed 0.99 solution is the game's own clamp,");
        Console.WriteLine("    not something the mod can remove - and shell dispersion is untouched");
        Console.WriteLine("    on purpose, so the guns still scatter, they just stop aiming at a guess.");
    }

    /// <summary>The game's accumulate-and-clamp, run for the given number of one-second ticks.</summary>
    private static float Converge(float gainPerTick, int seconds)
    {
        float solution = 0f;
        for (int t = 0; t < seconds; t++)
        {
            solution += gainPerTick;
            if (solution > 0.99f)
            {
                solution = 0.99f;
            }
        }
        return solution;
    }

    private static void Table(float[] cones, float[] intervals, float knots, float turnDegPerSec,
                              float range)
    {
        Console.Write("    cone\\aim ");
        foreach (float interval in intervals)
        {
            Console.Write((interval + "s").PadLeft(9));
        }
        Console.WriteLine();

        foreach (float cone in cones)
        {
            Console.Write(("    " + cone + " deg").PadRight(13));
            foreach (float interval in intervals)
            {
                float hits = HitFraction(cone, interval, knots, turnDegPerSec, range);
                Console.Write((hits * 100f).ToString("F0").PadLeft(8) + "%");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Fraction of emitted particles that intersect the aircraft, over one second of firing.
    /// </summary>
    private static float HitFraction(float coneDegrees, float aimInterval, float knots,
                                     float turnDegPerSec, float range)
    {
        // Aircraft starts abeam the mount and flies across it, which is the geometry a mount
        // sees for most of an attack run.
        float speed = knots * 0.514444f * 0.1f;
        var target = new Mover
        {
            X = range,
            Z = 0f,
            Y = 3f,
            HeadingRad = (float)Math.PI * 0.5f,
            Speed = speed,
            TurnRadPerSec = turnDegPerSec * (float)Math.PI / 180f
        };

        float aimTimer = 0f;
        float aimX = 0f, aimY = 0f, aimZ = 0f;
        int fired = 0;
        int hit = 0;

        for (float t = 0f; t < 1f; t += Tick)
        {
            if (aimTimer <= 0f)
            {
                // The game's exact intercept solve, against a straight-line prediction.
                SolveIntercept(target, out aimX, out aimY, out aimZ);
                aimTimer = aimInterval;
            }
            aimTimer -= Tick;

            // One particle per tick is enough: the emission rate cancels out of a fraction.
            fired++;
            if (ParticleHits(aimX, aimY, aimZ, coneDegrees, target))
            {
                hit++;
            }

            target.Advance(Tick);
        }
        return fired == 0 ? 0f : (float)hit / fired;
    }

    /// <summary>
    /// Where a particle leaving now at <see cref="ParticleSpeed"/> meets the target's current
    /// straight-line course. This mirrors Utilities.GetInterceptPoint, which is exact given that
    /// assumption and carries no randomness of its own.
    /// </summary>
    private static void SolveIntercept(Mover target, out float x, out float y, out float z)
    {
        float vx = (float)Math.Sin(target.HeadingRad) * target.Speed;
        float vz = (float)Math.Cos(target.HeadingRad) * target.Speed;

        float a = vx * vx + vz * vz - ParticleSpeed * ParticleSpeed;
        float b = 2f * (vx * target.X + vz * target.Z);
        float c = target.X * target.X + target.Z * target.Z + target.Y * target.Y;
        float disc = b * b - 4f * a * c;
        float flight = disc < 0f ? 0f : (-b - (float)Math.Sqrt(disc)) / (2f * a);
        if (flight < 0f)
        {
            flight = 0f;
        }

        x = target.X + vx * flight;
        z = target.Z + vz * flight;
        y = target.Y;
    }

    /// <summary>
    /// Fires one particle at the (possibly stale) aim point, scattered inside the cone, and
    /// reports whether it passes within the aircraft's radius of where the aircraft actually is
    /// when the particle arrives.
    /// </summary>
    private static bool ParticleHits(float aimX, float aimY, float aimZ, float coneDegrees,
                                     Mover target)
    {
        float len = (float)Math.Sqrt(aimX * aimX + aimY * aimY + aimZ * aimZ);
        if (len < 1e-4f)
        {
            return false;
        }
        float dx = aimX / len, dy = aimY / len, dz = aimZ / len;

        if (coneDegrees > 0f)
        {
            // Uniform inside a cone of half-angle coneDegrees about the aim direction.
            double maxAngle = coneDegrees * Math.PI / 180.0;
            double angle = maxAngle * Math.Sqrt(Rng.NextDouble());
            double spin = Rng.NextDouble() * 2.0 * Math.PI;

            // Any two vectors perpendicular to the aim direction.
            float ux = -dz, uy = 0f, uz = dx;
            float ulen = (float)Math.Sqrt(ux * ux + uz * uz);
            if (ulen < 1e-4f)
            {
                ux = 1f; uy = 0f; uz = 0f; ulen = 1f;
            }
            ux /= ulen; uz /= ulen;
            float wx = dy * uz - dz * uy;
            float wy = dz * ux - dx * uz;
            float wz = dx * uy - dy * ux;

            float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);
            float cs = (float)Math.Cos(spin), sn = (float)Math.Sin(spin);
            float nx = cos * dx + sin * (cs * ux + sn * wx);
            float ny = cos * dy + sin * (cs * uy + sn * wy);
            float nz = cos * dz + sin * (cs * uz + sn * wz);
            float nlen = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            dx = nx / nlen; dy = ny / nlen; dz = nz / nlen;
        }

        // Step the particle and the aircraft forward together and take the closest approach.
        var flying = target.Clone();
        float closest = float.MaxValue;
        float px = 0f, py = 0f, pz = 0f;
        for (float t = 0f; t < 1.5f; t += Tick)
        {
            px += dx * ParticleSpeed * Tick;
            py += dy * ParticleSpeed * Tick;
            pz += dz * ParticleSpeed * Tick;
            flying.Advance(Tick);

            float sx = px - flying.X, sy = py - flying.Y, sz = pz - flying.Z;
            float sep = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
            if (sep < closest)
            {
                closest = sep;
            }
            else if (sep > closest + AircraftRadius)
            {
                break;
            }
        }
        return closest <= AircraftRadius;
    }

    private sealed class Mover
    {
        public float X, Y, Z;
        public float HeadingRad;
        public float Speed;
        public float TurnRadPerSec;

        public void Advance(float dt)
        {
            HeadingRad += TurnRadPerSec * dt;
            X += (float)Math.Sin(HeadingRad) * Speed * dt;
            Z += (float)Math.Cos(HeadingRad) * Speed * dt;
        }

        public Mover Clone()
        {
            return new Mover
            {
                X = X, Y = Y, Z = Z,
                HeadingRad = HeadingRad,
                Speed = Speed,
                TurnRadPerSec = TurnRadPerSec
            };
        }
    }
}

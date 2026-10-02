using System;
using NuclearOptionAutoMissileDefense;

class Program
{
    static int count;
    static void Check(string name, bool expected, bool on = true, bool alive = true, bool targeted = true,
        bool radar = true, bool engaged = false, float dot = 1, float distanceSquared = 24990001, float range = 5000)
    {
        bool actual = DefenseRules.Eligible(on, alive, targeted, radar, engaged, dot, distanceSquared, range);
        if (actual != expected) throw new Exception(name + ": expected " + expected + ", got " + actual);
        count++;
    }
    static void Main()
    {
        Check("incoming radar threat inside range", true);
        Check("off", false, on: false);
        Check("destroyed", false, alive: false);
        Check("targeting someone else", false, targeted: false);
        Check("IR and unguided threat excluded", false, radar: false);
        Check("already engaged", false, engaged: true);
        Check("behind", false, dot: -0.001f);
        Check("exact side plane", false, dot: 0);
        Check("just ahead at wide angle", true, dot: 0.001f);
        Check("exact range", false, distanceSquared: 25000000);
        Check("beyond range", false, distanceSquared: 25000001);
        Check("user configured short range", false, range: 1000);
        Check("user configured long range", true, range: 10000);
        Check("zero range", false, range: 0);
        Check("negative range", false, range: -5000);
        Check("NaN range", false, range: float.NaN);
        Check("infinite range", false, range: float.PositiveInfinity);
        Check("NaN distance", false, distanceSquared: float.NaN);
        Check("infinite distance", false, distanceSquared: float.PositiveInfinity);
        Check("negative distance squared", false, distanceSquared: -1);
        Check("NaN direction", false, dot: float.NaN);
        // Sweep a full sphere: elevation is not ignored and camera bearing is not an input.
        for (int elevation = -80; elevation <= 80; elevation += 10)
        for (int azimuth = -175; azimuth <= 175; azimuth += 10)
        {
            float z = (float)(Math.Cos(elevation * Math.PI / 180) * Math.Cos(azimuth * Math.PI / 180));
            Check($"sphere {elevation}/{azimuth}", Math.Abs(azimuth) < 90, dot: z, distanceSquared: 100);
        }
        Console.WriteLine($"PASS: {count} defense eligibility checks.");
    }
}

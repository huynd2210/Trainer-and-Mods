using System;
using NuclearOptionMissileControl;
using UnityEngine;

class Program
{
    static int count;
    static void Check(string name, bool ok, string detail = "")
    {
        if (!ok) throw new Exception(name + " failed " + detail);
        count++;
    }
    static bool Near(float a, float b, float tolerance = 0.01f) => Mathf.Abs(a - b) <= tolerance;
    static bool Near(Vector3 a, Vector3 b) => (a - b).magnitude < 1e-3f;
    static AimState At(float heading, float elevation) => new AimState { Heading = heading, Elevation = elevation };

    static void Main()
    {
        var aim = new AimState();
        foreach (var dir in new[] { Vector3.forward, Vector3.right, -Vector3.forward, new Vector3(1, 0.5f, -2).normalized, new Vector3(-0.3f, -0.8f, 0.2f).normalized })
        {
            aim.Set(dir);
            Check("round trip " + dir, Near(aim.Direction, dir), aim.Direction.ToString());
        }
        Check("north is heading 0", Near(At(0, 0).Direction, Vector3.forward));
        Check("east is heading 90", Near(At(90, 0).Direction, Vector3.right));

        // Game conventions: +Pitch input is nose down; +Roll/+Yaw turn right; +Tilt View looks down.
        aim = At(0, 0); aim.Steer(0, 0, 1, 0, 30, 0.5f);
        Check("positive stick pitch is nose down", Near(aim.Elevation, -15f), aim.Elevation.ToString());
        aim = At(0, 0); aim.Steer(0, 0, 0, 1, 30, 0.5f);
        Check("positive stick turn goes right", aim.Direction.x > 0 && Near(aim.Heading, 15f), aim.Heading.ToString());
        aim = At(0, 0); aim.Steer(10, 4, 0, 0, 30, 0.02f);
        Check("look right and down", Near(aim.Heading, 10f) && Near(aim.Elevation, -4f));
        aim = At(0, 0); aim.Steer(0, 0, 0, 0, 30, 1f);
        Check("no input holds", Near(aim.Heading, 0f) && Near(aim.Elevation, 0f));

        aim = At(170, 80); aim.Steer(20, -30, 0, 0, 30, 0.02f);
        Check("heading wraps", Near(aim.Heading, -170f), aim.Heading.ToString());
        Check("elevation clamps up", Near(aim.Elevation, AimState.MaxElevation));
        aim = At(0, -80); aim.Steer(0, 0, 1, 0, 90, 1f);
        Check("elevation clamps down", Near(aim.Elevation, -AimState.MaxElevation));

        aim = At(90, 0); aim.LimitLead(Vector3.forward * 300f, 40f);
        Check("lead limited toward command", Near(Vector3.Angle(Vector3.forward, aim.Direction), 40f, 0.05f) && aim.Direction.x > 0, aim.Direction.ToString());
        aim = At(20, 5); aim.LimitLead(Vector3.forward * 300f, 40f);
        Check("lead inside limit untouched", Near(aim.Heading, 20f) && Near(aim.Elevation, 5f));
        aim = At(0, -60); aim.LimitLead(new Vector3(0, -0.2f, 1) * 250f, 30f);
        Check("dive lead limited", Near(Vector3.Angle(new Vector3(0, -0.2f, 1), aim.Direction), 30f, 0.05f) && aim.Elevation < -11f);
        aim = At(180, 0); aim.LimitLead(Vector3.forward * 300f, 45f);
        Check("reversed command still limited", Near(Vector3.Angle(Vector3.forward, aim.Direction), 45f, 0.05f));
        aim = At(0, 0); aim.LimitLead(Vector3.zero, 10f);
        Check("stationary missile leaves command", Near(aim.Heading, 0f));

        Console.WriteLine(count + " aim checks passed");
    }
}

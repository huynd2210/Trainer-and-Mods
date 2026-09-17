using System;
using UnityEngine;

// Minimal stand-ins for the Unity and game types the guidance touches, so the REAL guidance
// source files can be compiled and flown here without the game. Only the members the
// guidance actually uses are implemented; anything else is deliberately absent so that a
// guidance change reaching for new game state fails to compile instead of silently going
// untested.
//
// Conventions copied from the game: the world is 1 unit = 10 metres, and ammunition falls
// under Rigidbody.AddForce(0, Physics.gravity.y, 0) in ForceMode.Force, so its downward
// acceleration is Physics.gravity.y / mass.

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero
        {
            get { return new Vector3(0f, 0f, 0f); }
        }

        public float magnitude
        {
            get { return (float)Math.Sqrt(x * x + y * y + z * z); }
        }

        public float sqrMagnitude
        {
            get { return x * x + y * y + z * z; }
        }

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m < 1e-9f ? zero : new Vector3(x / m, y / m, z / m);
            }
        }

        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vector3 operator *(Vector3 a, float s)
        {
            return new Vector3(a.x * s, a.y * s, a.z * s);
        }

        public static Vector3 operator *(float s, Vector3 a)
        {
            return a * s;
        }

        public static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            return (a - b).magnitude;
        }

        /// <summary>
        /// Rotates <paramref name="from"/> toward <paramref name="to"/> by at most
        /// <paramref name="maxRadiansDelta"/>, keeping the source magnitude when
        /// <paramref name="maxMagnitudeDelta"/> is zero — the behaviour the guidance relies on.
        /// </summary>
        public static Vector3 RotateTowards(Vector3 from, Vector3 to, float maxRadiansDelta,
                                            float maxMagnitudeDelta)
        {
            float fromLength = from.magnitude;
            float targetLength = maxMagnitudeDelta == 0f ? fromLength : to.magnitude;
            Vector3 a = from.normalized;
            Vector3 b = to.normalized;
            float dot = Math.Max(-1f, Math.Min(1f, Dot(a, b)));
            float angle = (float)Math.Acos(dot);
            if (angle <= maxRadiansDelta || angle < 1e-6f)
            {
                return b * targetLength;
            }
            float t = maxRadiansDelta / angle;
            float sinAngle = (float)Math.Sin(angle);
            float wa = (float)Math.Sin((1f - t) * angle) / sinAngle;
            float wb = (float)Math.Sin(t * angle) / sinAngle;
            return (a * wa + b * wb).normalized * targetLength;
        }

        public override string ToString()
        {
            return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ", " + z.ToString("F2") + ")";
        }
    }

    public struct Quaternion
    {
        public Vector3 Forward;

        public static Quaternion LookRotation(Vector3 forward)
        {
            return new Quaternion { Forward = forward.normalized };
        }
    }

    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f;

        public static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        public static float Sqrt(float f)
        {
            return (float)Math.Sqrt(f);
        }

        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    public static class Time
    {
        public static float fixedDeltaTime = 0.02f;
    }

    public static class Physics
    {
        public static Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    }

    public class Transform
    {
        public Vector3 position;
        private Vector3 forwardDirection = new Vector3(0f, 0f, 1f);

        public Vector3 forward
        {
            get { return forwardDirection; }
            set { forwardDirection = value.normalized; }
        }

        public Quaternion rotation
        {
            get { return Quaternion.LookRotation(forwardDirection); }
            set { forwardDirection = value.Forward; }
        }
    }

    public class Rigidbody
    {
        public Vector3 velocity;
        public float mass = 1f;
    }
}

// --- game types -----------------------------------------------------------------------

public class UnitData
{
    /// <summary>Hull length in metres; the world is 1 unit = 10 metres.</summary>
    public float length;

    /// <summary>Beam in metres.</summary>
    public float width;
}

public class UnitSea
{
}

public class Unit
{
    public Transform transform = new Transform();
    public UnitData unitData = new UnitData();
    public UnitSea unitSea = new UnitSea();
    public float currentSpeed;
    public float currentActualSpeed;
    public bool isDestroyed;
    public string name = "Target";
}

public class Ammunition
{
    public Transform transform = new Transform();
    public Rigidbody ammoRigidbody = new Rigidbody();
}

public class AmmunitionMoveTorpedo
{
    public Transform transform = new Transform();
    public Ammunition parentAmmunition = new Ammunition();
    public float currentSpeed;
    public float runSpeed;
    public bool gyroDone;
    public Vector3 interceptPos;
}

/// <summary>Mirrors the game's AmmoType enum; the registry keys on these values.</summary>
public enum AmmoType
{
    Shell,
    Torpedo,
    Aerial_Torpedo,
    Depth_Charge,
    Aerial_Depth_Charge,
    Bomb,
    Rocket,
    Air_Gun,
    Air_Turret
}

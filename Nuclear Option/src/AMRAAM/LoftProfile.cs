using System;

namespace NuclearOptionAMRAAM
{
    // Pure trajectory math, shared by the plugin and the verification executable.
    public static class LoftProfile
    {
        public static float Height(float progress, float startAltitude, float endAltitude, float rise)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            return startAltitude + (endAltitude - startAltitude) * t + 4f * rise * t * (1f - t);
        }
        public static float Rise(float distance, float threshold, float maximumRise)
        {
            if (distance <= threshold) return 0f;
            return Math.Min(maximumRise, (distance - threshold) * 0.22f);
        }
        public static float Advance(float previous, float initialRange, float remaining)
        {
            return Math.Max(previous, Math.Max(0f, Math.Min(1f, 1f - remaining / Math.Max(initialRange, 1f))));
        }
    }
}

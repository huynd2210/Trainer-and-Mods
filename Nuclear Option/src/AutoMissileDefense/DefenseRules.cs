namespace NuclearOptionAutoMissileDefense
{
    internal static class DefenseRules
    {
        internal static bool Eligible(bool enabled, bool alive, bool targetsPlayer,
            bool radarGuided, bool alreadyEngaged, float forwardDot, float distanceSquared, float range)
        {
            // Strict range boundary; the dividing plane belongs to neither forward hemisphere.
            return enabled && alive && targetsPlayer && radarGuided && !alreadyEngaged &&
                forwardDot > 0f && distanceSquared >= 0f && range > 0f &&
                !float.IsInfinity(range) && distanceSquared < range * range;
        }
    }
}

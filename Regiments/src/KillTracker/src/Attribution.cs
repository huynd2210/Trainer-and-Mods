using System.Collections.Generic;
using birdseye.Regiments.Commanders;
using birdseye.Regiments.Platoons;

namespace KillTracker
{
    /// <summary>
    /// Who is firing right now, for death events that do not carry it (soldier deaths). A stack,
    /// because a casualty can nest: the last soldier of an infantry squad dies inside
    /// ApplyCasualty, which then calls KillUnit for the squad.
    /// </summary>
    internal static class Attribution
    {
        internal struct Shooter
        {
            public PlatoonCore killer;
            public Commander cmdr;
        }

        private static readonly List<Shooter> stack = new List<Shooter>();

        public static Shooter Current => stack.Count > 0 ? stack[stack.Count - 1] : default(Shooter);

        public static void Push(PlatoonCore killer, Commander cmdr)
        {
            stack.Add(new Shooter { killer = killer, cmdr = cmdr });
        }

        public static void Pop()
        {
            if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        public static void Reset()
        {
            stack.Clear();
        }
    }
}

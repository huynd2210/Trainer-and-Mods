using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace PerfProbe
{
    // Time and calls spent in one timed method.
    internal sealed class Stat
    {
        public string Label;
        public string Mod;           // which mod's code it is ("game" for a patched game method as a whole)
        public bool Whole;           // a game method timed with every mod patch on it included

        public long Calls, Ticks, MaxTicks, Alloc;              // since start
        public long WindowCalls, WindowTicks, WindowMax, WindowAlloc; // since the last summary
        public long FrameCalls, FrameTicks;                     // this frame
        public bool TouchedThisFrame;
        public int Depth;            // recursion: only the outermost call is counted
        public long Start, StartAlloc;
    }

    // The timing patch put on every timed method. Main thread only: the counters aren't locked,
    // and code other threads run is skipped.
    internal static class Timing
    {
        internal static readonly Dictionary<MethodBase, Stat> Stats = new Dictionary<MethodBase, Stat>();
        internal static readonly List<Stat> TouchedThisFrame = new List<Stat>(256);
        internal static int MainThread;
        internal static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        // Bytes allocated by this thread so far, where the runtime can tell (null where it can't).
        internal static Func<long> AllocatedBytes;

        internal static void Init()
        {
            MainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            try
            {
                MethodInfo m = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m == null)
                    return;
                Func<long> f = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), m);
                long before = f();
                byte[] probe = new byte[64 * 1024];
                long after = f();
                if (after - before >= probe.Length)
                    AllocatedBytes = f;
            }
            catch
            {
                AllocatedBytes = null;
            }
        }

        public static void Prefix(MethodBase __originalMethod)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != MainThread)
                return;
            if (!Stats.TryGetValue(__originalMethod, out Stat s) || s.Depth++ > 0)
                return;
            s.StartAlloc = AllocatedBytes != null ? AllocatedBytes() : 0;
            s.Start = Stopwatch.GetTimestamp();
        }

        public static void Postfix(MethodBase __originalMethod)
        {
            long end = Stopwatch.GetTimestamp();
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != MainThread)
                return;
            if (!Stats.TryGetValue(__originalMethod, out Stat s) || s.Depth == 0 || --s.Depth > 0)
                return;
            long t = end - s.Start;
            long alloc = AllocatedBytes != null ? AllocatedBytes() - s.StartAlloc : 0;
            s.Calls++;
            s.Ticks += t;
            s.Alloc += alloc;
            if (t > s.MaxTicks) s.MaxTicks = t;
            s.WindowCalls++;
            s.WindowTicks += t;
            s.WindowAlloc += alloc;
            if (t > s.WindowMax) s.WindowMax = t;
            s.FrameCalls++;
            s.FrameTicks += t;
            if (!s.TouchedThisFrame)
            {
                s.TouchedThisFrame = true;
                TouchedThisFrame.Add(s);
            }
        }

        // An exception skips the postfix; unwind the depth so the method keeps being timed.
        public static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            if (__exception != null && System.Threading.Thread.CurrentThread.ManagedThreadId == MainThread
                && Stats.TryGetValue(__originalMethod, out Stat s) && s.Depth > 0)
                s.Depth = 0;
            return __exception;
        }
    }
}

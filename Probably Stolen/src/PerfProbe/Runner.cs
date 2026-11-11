using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PerfProbe
{
    // Watches frame times. A frame over SpikeMs is written down with the timed code that ran in
    // it; once a minute a summary of where mod time went is appended to the log file.
    internal sealed class Runner : MonoBehaviour
    {
        private const double SpikeMs = 50;
        private const double SlowMs = 33;
        private const float SummaryEverySeconds = 60;
        private const int MaxSpikesPerWindow = 40;

        // The game lets a mod write whole files in its own data folder only, so the log is kept
        // in memory and rewritten; past MaxLogChars a new part starts.
        private const int MaxLogChars = 1_000_000;
        private static string logName, logPath;
        private static int logPart = 1;
        private static readonly StringBuilder logText = new StringBuilder();
        private bool installed;
        private long lastFrameStamp;
        private float nextSummary;
        private int lastGc;
        private long lastHeap;

        // This window.
        private int frames, slowFrames, spikeFrames, gcCount;
        private double frameMsSum, worstFrameMs;
        private long heapGrowth;
        private readonly List<string> spikes = new List<string>();
        private int spikesDropped;

        private void Update()
        {
            long now = Stopwatch.GetTimestamp();
            if (!installed)
            {
                // First frame: every mod has loaded and applied its patches by now.
                installed = true;
                Install();
                lastFrameStamp = now;
                lastGc = GC.CollectionCount(0);
                lastHeap = GC.GetTotalMemory(false);
                nextSummary = Time.realtimeSinceStartup + SummaryEverySeconds;
                return;
            }

            double frameMs = (now - lastFrameStamp) * Timing.TicksToMs;
            lastFrameStamp = now;
            int gc = GC.CollectionCount(0);
            int gcDelta = gc - lastGc;
            lastGc = gc;
            long heap = GC.GetTotalMemory(false);
            if (gcDelta == 0 && heap > lastHeap)
                heapGrowth += heap - lastHeap;
            lastHeap = heap;

            frames++;
            gcCount += gcDelta;
            frameMsSum += frameMs;
            if (frameMs > worstFrameMs) worstFrameMs = frameMs;
            if (frameMs > SlowMs) slowFrames++;
            if (frameMs > SpikeMs)
            {
                spikeFrames++;
                if (spikes.Count < MaxSpikesPerWindow)
                    spikes.Add(DescribeSpike(frameMs, gcDelta));
                else
                    spikesDropped++;
            }

            foreach (Stat s in Timing.TouchedThisFrame)
            {
                s.FrameCalls = 0;
                s.FrameTicks = 0;
                s.TouchedThisFrame = false;
            }
            Timing.TouchedThisFrame.Clear();

            if (Time.realtimeSinceStartup >= nextSummary)
            {
                nextSummary = Time.realtimeSinceStartup + SummaryEverySeconds;
                WriteSummary();
                // Mods can patch more later (rare); time those too.
                int added = Targets.Scan();
                if (added > 0)
                    Append("Now also timing " + added + " more method(s).\n");
            }
        }

        private static void Install()
        {
            try
            {
                logName = "perf_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                logPath = Path.Combine(ModHelper.GetModDataFolder(ModId), logName + ".log");
                Timing.Init();
                int count = Targets.Scan();
                string msg = "Timing " + count + " method(s) of mod code and the game methods mods patch"
                    + (Targets.Failed > 0 ? " (" + Targets.Failed + " could not be timed)" : "")
                    + ". Allocation per method: " + (Timing.AllocatedBytes != null ? "measured" : "not available on this runtime") + ".";
                Debug.Log("[PerfProbe] " + msg + " Log: " + logPath);
                Append("PerfProbe started " + DateTime.Now + ". " + msg + "\nFrames over " + SpikeMs + " ms are listed with the timed code that ran in them; "
                    + "'whole' rows are game methods timed with all mod patches on them, so they overlap the mod rows.\n\n");
            }
            catch (Exception e)
            {
                Debug.LogError("[PerfProbe] Could not start: " + e);
            }
        }

        // What ran in a slow frame: the biggest timed items, mod code and whole methods apart.
        private static string DescribeSpike(double frameMs, int gcDelta)
        {
            double modMs = Timing.TouchedThisFrame.Where(s => !s.Whole).Sum(s => s.FrameTicks) * Timing.TicksToMs;
            IEnumerable<string> top = Timing.TouchedThisFrame
                .OrderByDescending(s => s.FrameTicks).Take(6)
                .Where(s => s.FrameTicks * Timing.TicksToMs >= 0.5)
                .Select(s => (s.Whole ? "[whole] " : "[" + s.Mod + "] ") + s.Label + " " + Ms(s.FrameTicks) + " ms x" + s.FrameCalls);
            string topText = string.Join("\n      ", top);
            return DateTime.Now.ToString("HH:mm:ss") + "  frame " + frameMs.ToString("0") + " ms"
                + (gcDelta > 0 ? ", garbage collection ran" : "")
                + ", timed mod code " + modMs.ToString("0.0") + " ms"
                + (topText.Length > 0 ? "\n      " + topText : "");
        }

        private void WriteSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== " + DateTime.Now.ToString("HH:mm:ss") + "  last " + SummaryEverySeconds + " s: " + frames + " frames, avg "
                + (frames > 0 ? (frameMsSum / frames).ToString("0.0") : "-") + " ms, worst " + worstFrameMs.ToString("0") + " ms, over "
                + SlowMs + " ms: " + slowFrames + ", over " + SpikeMs + " ms: " + spikeFrames + ", garbage collections: " + gcCount
                + ", heap growth ~" + (heapGrowth / (1024 * 1024.0)).ToString("0.0") + " MB");

            List<Stat> all = Timing.Stats.Values.ToList();
            double windowSeconds = Math.Max(1, frameMsSum / 1000);
            sb.AppendLine("  Time per mod (own code only), ms per second of play:");
            foreach (var g in all.Where(s => !s.Whole && s.WindowCalls > 0).GroupBy(s => s.Mod)
                .Select(g => new { Mod = g.Key, Ticks = g.Sum(s => s.WindowTicks), Alloc = g.Sum(s => s.WindowAlloc) })
                .OrderByDescending(g => g.Ticks))
            {
                sb.AppendLine("    " + g.Mod.PadRight(28) + (g.Ticks * Timing.TicksToMs / windowSeconds).ToString("0.00").PadLeft(8)
                    + (Timing.AllocatedBytes != null ? "   alloc " + Kb(g.Alloc) + " KB" : ""));
            }

            sb.AppendLine("  Top code this window (total ms | calls | avg us | max ms" + (Timing.AllocatedBytes != null ? " | alloc KB" : "") + "):");
            foreach (Stat s in all.Where(s => s.WindowCalls > 0).OrderByDescending(s => s.WindowTicks).Take(25))
            {
                sb.AppendLine("    " + Ms(s.WindowTicks).PadLeft(9) + " | " + s.WindowCalls.ToString().PadLeft(8) + " | "
                    + (s.WindowTicks * Timing.TicksToMs * 1000 / s.WindowCalls).ToString("0.0").PadLeft(8) + " | " + Ms(s.WindowMax).PadLeft(7)
                    + (Timing.AllocatedBytes != null ? " | " + Kb(s.WindowAlloc).PadLeft(8) : "")
                    + "  " + (s.Whole ? "[whole] " : "[" + s.Mod + "] ") + s.Label);
            }

            if (spikes.Count > 0)
            {
                sb.AppendLine("  Slow frames:");
                foreach (string spike in spikes)
                    sb.AppendLine("    " + spike);
                if (spikesDropped > 0)
                    sb.AppendLine("    ... and " + spikesDropped + " more");
            }
            sb.AppendLine();
            Append(sb.ToString());
            Debug.Log("[PerfProbe] Summary written: avg " + (frames > 0 ? (frameMsSum / frames).ToString("0.0") : "-") + " ms, " + spikeFrames + " slow frame(s). " + logPath);

            foreach (Stat s in all)
            {
                s.WindowCalls = 0;
                s.WindowTicks = 0;
                s.WindowMax = 0;
                s.WindowAlloc = 0;
            }
            frames = slowFrames = spikeFrames = gcCount = 0;
            frameMsSum = worstFrameMs = 0;
            heapGrowth = 0;
            spikes.Clear();
            spikesDropped = 0;
        }

        private static string Ms(long ticks) => (ticks * Timing.TicksToMs).ToString("0.00");
        private static string Kb(long bytes) => (bytes / 1024).ToString();

        private const string ModId = "PerfProbe";

        private static void Append(string text)
        {
            if (logName == null)
                return;
            try
            {
                if (logText.Length > 0 && logText.Length + text.Length > MaxLogChars)
                {
                    logText.Clear();
                    logPart++;
                }
                logText.Append(text);
                string file = logName + (logPart > 1 ? "_part" + logPart : "") + ".log";
                ModHelper.WriteModDataFileText(ModId, file, logText.ToString());
                logPath = Path.Combine(ModHelper.GetModDataFolder(ModId), file);
            }
            catch (Exception e) { Debug.LogWarning("[PerfProbe] Could not write the log: " + e.Message); }
        }
    }
}

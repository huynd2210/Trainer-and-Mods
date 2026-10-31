using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;
using birdseye.Localization;
using birdseye.Regiments;
using birdseye.Regiments.Commanders;
using birdseye.Regiments.Platoons;
using birdseye.Regiments.SceneManagement;

namespace KillTracker
{
    /// <summary>
    /// One battle's text file. Lines go out as the battle runs, so the file is readable mid-battle
    /// and survives a crash; the summary is appended when the battle ends. The file is only
    /// created once something happens, so menus and empty scenes leave nothing behind.
    ///
    /// Repeated hits by the same shooter on the same target (a machine gun working through a
    /// squad) merge into one line while they keep coming, up to MaxMergeSeconds. Lines are
    /// written in the order their first hit happened.
    /// </summary>
    internal sealed class BattleLog
    {
        private const float QuietSeconds = 8f;
        private const float MaxMergeSeconds = 30f;

        private sealed class Entry
        {
            public string key;
            public string time;
            public float first;
            public float last;
            public int count;
            public Func<int, string> text;

            public bool IsClosed(float now)
            {
                return now - last > QuietSeconds || now - first > MaxMergeSeconds;
            }
        }

        // Real time, not battle time: pausing must still let merged lines go out. Swappable so
        // the verify harness can run outside Unity.
        internal static Func<float> Clock = () => Time.unscaledTime;

        private readonly DateTime started;
        private readonly List<Entry> pending = new List<Entry>();
        private readonly Tally kills = new Tally();
        private readonly Tally losses = new Tally();
        private int friendlyFire;
        private StreamWriter writer;
        private bool finished;

        public BattleLog(DateTime started)
        {
            this.started = started;
        }

        public static string ResolveFolder()
        {
            string folder = KillTrackerPlugin.Folder.Value;
            if (string.IsNullOrEmpty(folder))
            {
                folder = "KillTracker";
            }
            return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(Paths.GameRootPath, folder));
        }

        private static bool Is(Commander a, Commander b)
        {
            return Names.Exists(a) && ReferenceEquals(a, b);
        }

        /// <summary>
        /// A unit or soldier died. Only two cases are kept: you lost it, or you killed an enemy.
        /// Allied commanders' losses and kills are left out; you hitting an ally is noted as
        /// friendly fire but not counted as a kill.
        /// </summary>
        public void Record(Kind kind, Commander victimCmdr, string type, string victim, PlatoonCore killer, Commander killingCmdr)
        {
            Commander me = GameQueries.activePlayer;
            if (finished || !Names.Exists(me) || !Names.Exists(victimCmdr))
            {
                return;
            }
            bool byMe = Is(killingCmdr, me) || (Names.Exists(killer) && Is(killer.commander, me));
            string by = Names.Killer(killer, killingCmdr);
            string verb = kind == Kind.Soldier ? "killed" : "destroyed";
            if (Is(victimCmdr, me))
            {
                losses.Add(kind, type, victim);
                string cause = by == null ? " (cause unknown)" : " by " + by + (byMe ? " (friendly fire)" : "");
                Add("LOSS", kind, victim, by, n => Phrase(kind, victim, n) + " " + verb + cause);
            }
            else if (byMe && victimCmdr.side != me.side)
            {
                kills.Add(kind, type, by);
                Add("KILL", kind, victim, by, n => by + " " + verb + " " + Phrase(kind, victim, n));
            }
            else if (byMe)
            {
                friendlyFire++;
                Add("FF", kind, victim, by, n => by + " " + verb + " allied " + Phrase(kind, victim, n));
            }
        }

        public void RecordWipe(Commander victimCmdr, string victim, PlatoonCore killer, Commander killingCmdr)
        {
            Commander me = GameQueries.activePlayer;
            if (finished || !Names.Exists(me) || !Names.Exists(victimCmdr))
            {
                return;
            }
            bool byMe = Is(killingCmdr, me) || (Names.Exists(killer) && Is(killer.commander, me));
            string by = Names.Killer(killer, killingCmdr);
            if (Is(victimCmdr, me))
            {
                losses.platoonsWiped++;
                Add("LOSS", null, victim, by, n => victim + " WIPED OUT" + (by == null ? "" : " by " + by));
            }
            else if (byMe && victimCmdr.side != me.side)
            {
                kills.platoonsWiped++;
                Add("KILL", null, victim, by, n => by + " WIPED OUT " + victim);
            }
        }

        private static string Phrase(Kind kind, string victim, int n)
        {
            if (kind == Kind.Soldier)
            {
                return Tally.Count(Kind.Soldier, n) + " of " + victim;
            }
            return n == 1 ? victim : n + "x " + victim;
        }

        // A null kind marks a one-off line (a platoon wiped out) that never merges.
        private void Add(string tag, Kind? kind, string victim, string by, Func<int, string> text)
        {
            float now = Clock();
            string key = kind == null ? null : tag + "|" + kind + "|" + victim + "|" + by;
            Entry open = key == null ? null : pending.FirstOrDefault(e => e.key == key && !e.IsClosed(now));
            if (open != null)
            {
                open.count++;
                open.last = now;
                return;
            }
            pending.Add(new Entry { key = key, time = GameClock(), first = now, last = now, count = 1, text = n => tag.PadRight(5) + text(n) });
        }

        public void Tick()
        {
            Flush(false);
        }

        private void Flush(bool all)
        {
            float now = Clock();
            while (pending.Count > 0 && (all || pending[0].IsClosed(now)))
            {
                Entry e = pending[0];
                pending.RemoveAt(0);
                WriteLine("[" + e.time + "] " + e.text(e.count));
            }
        }

        public void Finish(eSides winningSide, eGameResult result, eSessionEndType endType)
        {
            if (finished)
            {
                return;
            }
            Commander me = GameQueries.activePlayer;
            string outcome;
            if (endType == eSessionEndType.EarlyExit)
            {
                outcome = "Left before the end";
            }
            else if (result == eGameResult.Draw)
            {
                outcome = "Draw";
            }
            else
            {
                outcome = (Names.Exists(me) && winningSide == me.side ? "Victory" : "Defeat") + " (" + result + ")";
            }
            WriteSummary(outcome);
        }

        public void Close()
        {
            WriteSummary("Battle closed");
            writer?.Dispose();
            writer = null;
        }

        private void WriteSummary(string outcome)
        {
            if (finished)
            {
                return;
            }
            Flush(true);
            finished = true;
            if (writer == null)
            {
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("==================== SUMMARY ====================");
            sb.AppendLine("Result:  " + outcome + ", battle time " + GameClock());
            sb.AppendLine();
            kills.Write(sb, "YOUR KILLS:   ", "wiped out", "By your platoon");
            sb.AppendLine();
            losses.Write(sb, "YOUR LOSSES:  ", "wiped out", "By your platoon");
            if (friendlyFire > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Friendly fire on allies: " + friendlyFire + " (not counted as kills)");
            }
            writer.Write(sb.ToString());
        }

        private void WriteLine(string line)
        {
            if (writer == null)
            {
                Open();
            }
            writer.WriteLine(line);
        }

        private void Open()
        {
            string folder = ResolveFolder();
            Directory.CreateDirectory(folder);
            string map = MapName();
            string stem = started.ToString("yyyy-MM-dd HH-mm") + " " + map;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(c, '_');
            }
            string path = Path.Combine(folder, stem + ".txt");
            for (int i = 2; File.Exists(path); i++)
            {
                path = Path.Combine(folder, stem + " (" + i + ").txt");
            }
            // Shared read so the file can be open in an editor while the battle runs.
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            writer = new StreamWriter(stream, new UTF8Encoding(true)) { AutoFlush = true };
            Commander me = GameQueries.activePlayer;
            writer.WriteLine("Regiments Kill Tracker " + KillTrackerPlugin.Version);
            writer.WriteLine("Battle:    " + map + "  |  " + ModeName() + "  |  started " + started.ToString("yyyy-MM-dd HH:mm"));
            writer.WriteLine("Commander: " + (Names.Exists(me) ? Names.Commander(me) + " (" + me.side + ")" : "?"));
            writer.WriteLine("Your own units only; allied commanders' kills and losses are left out. Times are battle time.");
            writer.WriteLine();
            KillTrackerPlugin.Log.LogInfo("Logging battle to " + path);
        }

        private static string GameClock()
        {
            try
            {
                return GameTime.active.GetPrintableGameSpan();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static string MapName()
        {
            try
            {
                MapDescriptor desc = GameController.active.mapDesc;
                string name = LocalizationManager.GetString(desc.nameLocID);
                return string.IsNullOrEmpty(name) || name.StartsWith("LS_", StringComparison.Ordinal) ? desc.internalName : name;
            }
            catch (Exception)
            {
                return "battle";
            }
        }

        private static string ModeName()
        {
            try
            {
                string name = GameController.active.gameMode.GetType().Name;
                return name.EndsWith("GameMode", StringComparison.Ordinal) ? name.Substring(0, name.Length - "GameMode".Length) : name;
            }
            catch (Exception)
            {
                return "?";
            }
        }
    }
}

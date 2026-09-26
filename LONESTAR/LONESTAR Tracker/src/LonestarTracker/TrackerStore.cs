using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LonestarTracker
{
    /// <summary>
    /// The logs on disk. Both files are append-only and never rewritten: a row that has
    /// been written is a permanent record, so a crash, a bad parse or a later version of
    /// this mod can only ever fail to add data, never destroy it.
    ///
    /// Rows are read back by header name rather than by position, so columns added in a
    /// later version leave older files readable.
    /// </summary>
    internal sealed class TrackerStore
    {
        public const string BattlesFile = "battles.csv";
        public const string RunsFile = "runs.csv";
        public const string CheckpointFile = "checkpoint.csv";

        public readonly string Directory;
        public readonly List<BattleRecord> Battles = new List<BattleRecord>();
        public readonly List<RunRecord> Runs = new List<RunRecord>();

        public string LastError { get; private set; }

        public TrackerStore(string directory)
        {
            Directory = directory;
        }

        public string BattlesPath { get { return Path.Combine(Directory, BattlesFile); } }
        public string RunsPath { get { return Path.Combine(Directory, RunsFile); } }
        public string CheckpointPath { get { return Path.Combine(Directory, CheckpointFile); } }

        public void Load()
        {
            Battles.Clear();
            Runs.Clear();
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    System.IO.Directory.CreateDirectory(Directory);

                ReadInto(BattlesPath, BattleRecord.Schema, Battles, () => new BattleRecord());
                ReadInto(RunsPath, RunRecord.Schema, Runs, () => new RunRecord());
                DedupeRuns();
                ReconcileOrphanRuns();
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[LonestarTracker] could not read the logs: " + e);
            }
        }

        /// <summary>
        /// The log is append-only, so a voyage written once as "Incomplete" and again
        /// when it finished has two rows. The later row is the truth.
        /// </summary>
        private void DedupeRuns()
        {
            Dictionary<string, int> lastIndex = new Dictionary<string, int>();
            for (int i = 0; i < Runs.Count; i++)
            {
                string id = Runs[i].RunId;
                if (!string.IsNullOrEmpty(id)) lastIndex[id] = i;
            }

            List<RunRecord> kept = new List<RunRecord>();
            for (int i = 0; i < Runs.Count; i++)
            {
                string id = Runs[i].RunId;
                if (string.IsNullOrEmpty(id) || lastIndex[id] == i) kept.Add(Runs[i]);
            }

            if (kept.Count == Runs.Count) return;
            Runs.Clear();
            Runs.AddRange(kept);
        }

        /// <summary>
        /// A run whose battles were logged but whose summary row never arrived - the game
        /// was closed mid-voyage, or crashed. The battles themselves carry enough context
        /// to rebuild the summary, so show it rather than dropping the run. Memory only:
        /// nothing is written back, because the run may still be resumed.
        /// </summary>
        private void ReconcileOrphanRuns()
        {
            HashSet<string> known = new HashSet<string>();
            foreach (RunRecord run in Runs)
                if (!string.IsNullOrEmpty(run.RunId)) known.Add(run.RunId);

            Dictionary<string, RunRecord> rebuilt = new Dictionary<string, RunRecord>();
            List<RunRecord> order = new List<RunRecord>();

            foreach (BattleRecord battle in Battles)
            {
                if (string.IsNullOrEmpty(battle.RunId) || known.Contains(battle.RunId)) continue;

                RunRecord run;
                if (!rebuilt.TryGetValue(battle.RunId, out run))
                {
                    run = new RunRecord
                    {
                        RunId = battle.RunId,
                        Started = battle.When,
                        StartedUnix = battle.WhenUnix,
                        Outcome = "Incomplete",
                        PlayerShip = battle.PlayerShip,
                        PlayerPilot = battle.PlayerPilot,
                        Seed = battle.Seed,
                        Mode = battle.Mode
                    };
                    rebuilt[battle.RunId] = run;
                    order.Add(run);
                }

                run.Ended = battle.When;
                run.Days = battle.Day;
                run.PhaseReached = battle.Phase;
                Tally.Add(run, battle);
            }

            Runs.AddRange(order);
        }

        public void AppendBattle(BattleRecord record)
        {
            Battles.Add(record);
            Append(BattlesPath, BattleRecord.Schema, record);
        }

        /// <summary>
        /// With <paramref name="persist"/> false the row is kept for this session only -
        /// what "Log Voyages" being off means. Rows already on disk are left alone.
        /// </summary>
        public void AppendRun(RunRecord record, bool persist)
        {
            // A run that was left open in an earlier session is now closed for real.
            for (int i = Runs.Count - 1; i >= 0; i--)
            {
                if (Runs[i].RunId == record.RunId) { Runs.RemoveAt(i); break; }
            }
            Runs.Add(record);
            if (persist) Append(RunsPath, RunRecord.Schema, record);
        }

        // ----- checkpoint -------------------------------------------------------

        /// <summary>The voyage in progress as of the last write, or null if there was none.</summary>
        public Checkpoint ReadCheckpoint()
        {
            try
            {
                List<Checkpoint> read = new List<Checkpoint>();
                ReadInto(CheckpointPath, Checkpoint.Schema, read, () => new Checkpoint());
                if (read.Count == 0) return null;

                Checkpoint checkpoint = read[read.Count - 1];
                return checkpoint.HasRun ? checkpoint : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LonestarTracker] could not read the checkpoint: " + e);
                return null;
            }
        }

        public void WriteCheckpoint(Checkpoint checkpoint)
        {
            Write(CheckpointPath, Checkpoint.Schema, checkpoint);
        }

        /// <summary>Called once the voyage has its own row in runs.csv and is no longer live.</summary>
        public void ClearCheckpoint()
        {
            Write<Checkpoint>(CheckpointPath, Checkpoint.Schema, default(Checkpoint));
        }

        /// <summary>
        /// Replaces a file with a header and at most one row. Writes to a temporary file
        /// and moves it into place, so a crash part way through leaves the previous
        /// checkpoint intact rather than half of a new one.
        /// </summary>
        private void Write<T>(string path, List<Column<T>> schema, T record) where T : class
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    System.IO.Directory.CreateDirectory(Directory);

                string[] header = new string[schema.Count];
                for (int i = 0; i < schema.Count; i++) header[i] = schema[i].Name;

                StringBuilder sb = new StringBuilder();
                sb.Append(Csv.Line(header)).Append('\n');

                if (record != null)
                {
                    string[] row = new string[schema.Count];
                    for (int i = 0; i < schema.Count; i++) row[i] = schema[i].Get(record);
                    sb.Append(Csv.Line(row)).Append('\n');
                }

                string temporary = path + ".tmp";
                File.WriteAllText(temporary, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                LastError = null;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[LonestarTracker] could not write " + path + ": " + e);
            }
        }

        private void Append<T>(string path, List<Column<T>> schema, T record)
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    System.IO.Directory.CreateDirectory(Directory);

                StringBuilder sb = new StringBuilder();
                if (!File.Exists(path))
                {
                    string[] header = new string[schema.Count];
                    for (int i = 0; i < schema.Count; i++) header[i] = schema[i].Name;
                    sb.Append(Csv.Line(header)).Append('\n');
                }

                string[] row = new string[schema.Count];
                for (int i = 0; i < schema.Count; i++) row[i] = schema[i].Get(record);
                sb.Append(Csv.Line(row)).Append('\n');

                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                LastError = null;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[LonestarTracker] could not write " + path + ": " + e);
            }
        }

        private static void ReadInto<T>(string path, List<Column<T>> schema, List<T> into, Func<T> create)
        {
            if (!File.Exists(path)) return;

            List<string[]> rows = Csv.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count < 2) return;

            // Map each file column to a schema column by name, so a file written by an
            // older or newer version of the mod still loads.
            string[] header = rows[0];
            Column<T>[] byPosition = new Column<T>[header.Length];
            for (int i = 0; i < header.Length; i++)
            {
                string name = header[i].Trim();
                for (int c = 0; c < schema.Count; c++)
                {
                    if (schema[c].Name == name) { byPosition[i] = schema[c]; break; }
                }
            }

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                T record = create();
                for (int i = 0; i < row.Length && i < byPosition.Length; i++)
                {
                    Column<T> column = byPosition[i];
                    if (column != null) column.Set(record, row[i]);
                }
                into.Add(record);
            }
        }
    }

    internal static class Tally
    {
        /// <summary>Folds one battle into a run's counters. Used live and when rebuilding.</summary>
        public static void Add(RunRecord run, BattleRecord battle)
        {
            if (!battle.IsKill) { run.Defeats++; return; }

            run.Kills++;
            if (battle.Tier == Tier.Boss) run.KillsBoss++;
            else if (battle.Tier == Tier.Elite) run.KillsElite++;
            else run.KillsMinion++;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KillTracker
{
    /// <summary>One kill, as recorded. Fields the game cannot provide are left null rather than guessed.</summary>
    public sealed class KillRecord
    {
        public string Utc;
        public string Kind;
        public string Mode;
        public string Scene;
        public int? Level;
    }

    public sealed class RunTally
    {
        public string StartedUtc;
        public Dictionary<string, int> Counts = new Dictionary<string, int>();
    }

    public sealed class LedgerData
    {
        public int Version = 1;
        public Dictionary<string, int> Lifetime = new Dictionary<string, int>();
        // Keyed by mode ("story", "endless"): the game keeps a separate run for each.
        public Dictionary<string, RunTally> Runs = new Dictionary<string, RunTally>();
        public List<KillRecord> Records = new List<KillRecord>();
    }

    /// <summary>
    /// Kill counts for this level, the current run and the lifetime, kept in a JSON file.
    /// Writes replace the file through a temp copy and keep the previous version as .bak.
    /// </summary>
    internal sealed class Ledger
    {
        private readonly string path;
        private LedgerData data = new LedgerData();
        private readonly Dictionary<string, int> level = new Dictionary<string, int>();
        private bool readOnly;
        private bool dirty;

        public Ledger(string path)
        {
            this.path = path;
        }

        public int LifetimeTotal => data.Lifetime.Values.Sum();

        public void Record(string kind, string mode, string scene, int? levelNumber)
        {
            Increment(level, kind);
            Increment(data.Lifetime, kind);
            Increment(RunFor(mode).Counts, kind);

            int max = Plugin.MaxRawRecords.Value;
            if (max > 0)
            {
                data.Records.Add(new KillRecord
                {
                    Utc = DateTime.UtcNow.ToString("o"),
                    Kind = kind,
                    Mode = mode,
                    Scene = scene,
                    Level = levelNumber,
                });
            }

            int excess = data.Records.Count - Math.Max(0, max);
            if (excess > 0)
            {
                data.Records.RemoveRange(0, excess);
            }

            dirty = true;
        }

        public void StartLevel()
        {
            level.Clear();
        }

        public void StartRun(string mode)
        {
            data.Runs[mode] = new RunTally { StartedUtc = DateTime.UtcNow.ToString("o") };
            dirty = true;
        }

        public int Level(string kind) => Get(level, kind);

        public int Run(string mode, string kind) => Get(RunFor(mode).Counts, kind);

        public int Lifetime(string kind) => Get(data.Lifetime, kind);

        public int LevelTotal => level.Values.Sum();

        public int RunTotal(string mode) => RunFor(mode).Counts.Values.Sum();

        /// <summary>Every kind ever killed, most-killed first.</summary>
        public IEnumerable<string> Kinds =>
            data.Lifetime.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Select(pair => pair.Key);

        public void Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak"))
            {
                return;
            }

            if (TryRead(path, out LedgerData loaded) || TryRead(path + ".bak", out loaded))
            {
                data = loaded;
                data.Lifetime ??= new Dictionary<string, int>();
                data.Runs ??= new Dictionary<string, RunTally>();
                data.Records ??= new List<KillRecord>();
            }
        }

        public void SaveIfDirty()
        {
            if (!dirty || readOnly)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(data, Formatting.Indented));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, path + ".bak");
                }
                else
                {
                    File.Move(temp, path);
                }

                dirty = false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not save kills to {path}: {e.Message}. Retrying on the next kill.");
            }
        }

        private bool TryRead(string file, out LedgerData loaded)
        {
            loaded = null;
            if (!File.Exists(file))
            {
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (Exception e)
            {
                // Saving now would overwrite kills we could not read, so stop saving for this session.
                readOnly = true;
                Plugin.Log.LogError($"Could not read {file}: {e.Message}. Kills will not be saved this session.");
                return false;
            }

            try
            {
                loaded = JsonConvert.DeserializeObject<LedgerData>(json);
                if (loaded != null)
                {
                    return true;
                }
            }
            catch (JsonException e)
            {
                Plugin.Log.LogWarning($"{file} is not valid JSON ({e.Message}).");
            }

            string aside = $"{file}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(file, aside);
            Plugin.Log.LogWarning($"Moved the unreadable ledger aside to {aside}.");
            return false;
        }

        private RunTally RunFor(string mode)
        {
            if (!data.Runs.TryGetValue(mode, out RunTally run))
            {
                run = new RunTally { StartedUtc = DateTime.UtcNow.ToString("o") };
                data.Runs[mode] = run;
            }

            return run;
        }

        private static void Increment(Dictionary<string, int> counts, string kind)
        {
            counts[kind] = Get(counts, kind) + 1;
        }

        private static int Get(Dictionary<string, int> counts, string kind)
        {
            return counts.TryGetValue(kind, out int count) ? count : 0;
        }
    }
}

using System;
using System.Collections.Generic;
using Lean.Localization;
using Tool.Database;
using UnityEngine;

namespace LonestarTracker
{
    /// <summary>
    /// The live bookkeeping. The game tells it when a voyage begins, when a battle
    /// starts and how each battle ended; it turns that into permanent rows.
    ///
    /// Every entry point is called from inside the game's own event chain or from a
    /// Harmony patch, so every one of them swallows its exceptions: a tracker that
    /// throws would break the delegate chain it is attached to and take the battle
    /// with it. Losing a row is acceptable, losing the player's run is not.
    /// </summary>
    internal static class Tracker
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static TrackerStore Store { get; private set; }

        /// <summary>The voyage in progress, or null between voyages.</summary>
        public static RunRecord CurrentRun { get; private set; }

        /// <summary>Battles of the voyage in progress, oldest first.</summary>
        public static readonly List<BattleRecord> CurrentRunBattles = new List<BattleRecord>();

        private static float _battleStartedAt = -1f;
        private static int _battleElapsedBefore;
        private static int _battleStartHp;
        private static int _battleEnemyId;
        private static bool _ready;
        private static Stats _stats;

        /// <summary>
        /// A voyage that was live when the game last stopped. Held aside rather than
        /// made current: the player is at the menu now, and it only becomes the current
        /// voyage again if they actually load that save.
        /// </summary>
        private static Checkpoint _pending;

        public static event Action Changed;

        /// <summary>
        /// The folded-up totals. Cached because the panel asks for them every frame and
        /// building them walks the whole history.
        /// </summary>
        public static Stats Snapshot
        {
            get { return _stats ?? (_stats = Stats.Build(Store)); }
        }

        public static void Init(string directory)
        {
            Store = new TrackerStore(directory);
            Store.Load();

            _pending = Store.ReadCheckpoint();
            if (_pending != null && IsClosed(_pending.Run.RunId)) _pending = null;
            if (_pending != null) ShowPendingInHistory();

            _ready = true;
            Debug.Log("[LonestarTracker] " + Store.Battles.Count + " battles and " + Store.Runs.Count +
                      " runs loaded from " + directory +
                      (_pending != null ? "; a voyage was still in progress (" + _pending.Run.Kills + " kills)" : ""));
        }

        /// <summary>
        /// The unfinished voyage shows up in the history straight away, with its real
        /// counters rather than the thinner version rebuilt from its battle rows. If it
        /// is resumed, this is the same object the voyage carries on writing into.
        /// </summary>
        private static void ShowPendingInHistory()
        {
            _pending.Run.Outcome = "Incomplete";
            for (int i = 0; i < Store.Runs.Count; i++)
            {
                if (Store.Runs[i].RunId != _pending.Run.RunId) continue;
                Store.Runs[i] = _pending.Run;
                return;
            }
            Store.Runs.Add(_pending.Run);
        }

        private static bool IsClosed(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            foreach (RunRecord run in Store.Runs)
                if (run.RunId == runId && (run.IsWin || run.IsLoss)) return true;
            return false;
        }

        /// <summary>
        /// A checkpointed voyage that is definitely not being resumed. Give it its own
        /// row so its clock, coins and counters survive, then let go of it.
        /// </summary>
        private static void RetirePending()
        {
            if (_pending == null) return;

            RunRecord run = _pending.Run;
            _pending = null;

            if (IsClosed(run.RunId)) return;

            run.Outcome = "Incomplete";
            if (string.IsNullOrEmpty(run.Ended)) run.Ended = Stamp(DateTime.Now);

            TrackerConfig config = TrackerMod.Config;
            Store.AppendRun(run, config == null || config.logRuns);
        }

        // ----- voyage lifecycle ------------------------------------------------

        /// <summary>A brand new voyage: ship picked, pilot picked, seed rolled.</summary>
        public static void OnRunStart()
        {
            if (!_ready) return;
            try
            {
                WantedProcess process = Process();
                if (process == null) return;

                CloseOpenRun("Incomplete");
                RetirePending();

                DateTime now = DateTime.Now;
                CurrentRun = new RunRecord
                {
                    RunId = NewRunId(now, Seed(process)),
                    Started = Stamp(now),
                    StartedUnix = Unix(now),
                    Outcome = RunRecord.InProgress,
                    PlayerShip = PlayerShipName(),
                    PlayerPilot = PlayerPilotName(),
                    Seed = Seed(process),
                    Seeded = process.seeded_run,
                    Mode = Mode(process)
                };
                CurrentRunBattles.Clear();
                ResetBattleState();
                Save();
                Raise();
            }
            catch (Exception e) { Warn("run start", e); }
        }

        /// <summary>
        /// A saved voyage was loaded. Re-adopt the run row it was writing into, matching
        /// on the voyage seed, so resuming does not split one voyage into two.
        /// </summary>
        public static void OnRunResume()
        {
            if (!_ready) return;
            try
            {
                WantedProcess process = Process();
                if (process == null) return;

                string seed = Seed(process);
                if (CurrentRun != null && CurrentRun.Seed == seed) return;

                CloseOpenRun("Incomplete");
                ResetBattleState();

                // Best source first: the checkpoint written while this very voyage was
                // being played. It carries the counters, the clock and, if the game
                // stopped during a battle, that battle's starting hull.
                if (_pending != null && _pending.Run.Seed == seed)
                {
                    CurrentRun = _pending.Run;
                    CurrentRun.Outcome = RunRecord.InProgress;
                    _battleElapsedBefore = _pending.BattleElapsedSeconds;
                    _battleStartHp = _pending.BattleStartHp;
                    _battleEnemyId = _pending.BattleEnemyId;
                    if (_battleEnemyId != 0) _battleStartedAt = Time.realtimeSinceStartup;
                    _pending = null;
                    RebuildCurrentRunBattles();
                }
                else
                {
                    // A checkpoint for some other voyage is not coming back now.
                    RetirePending();

                    RunRecord adopted = FindOpenRunBySeed(seed);
                    if (adopted != null)
                    {
                        CurrentRun = adopted;
                        CurrentRun.Outcome = RunRecord.InProgress;
                        RebuildCurrentRunBattles();
                    }
                    else
                    {
                        // First load after installing the mod mid-voyage: record from here.
                        DateTime now = DateTime.Now;
                        CurrentRun = new RunRecord
                        {
                            RunId = NewRunId(now, seed),
                            Started = Stamp(now),
                            StartedUnix = Unix(now),
                            Outcome = RunRecord.InProgress,
                            PlayerShip = PlayerShipName(),
                            PlayerPilot = PlayerPilotName(),
                            Seed = seed,
                            Seeded = process.seeded_run,
                            Mode = Mode(process)
                        };
                        CurrentRunBattles.Clear();
                    }
                }

                Save();
                Raise();
            }
            catch (Exception e) { Warn("run resume", e); }
        }

        private static void RebuildCurrentRunBattles()
        {
            CurrentRunBattles.Clear();
            if (CurrentRun == null) return;
            foreach (BattleRecord battle in Store.Battles)
                if (battle.RunId == CurrentRun.RunId) CurrentRunBattles.Add(battle);
        }

        /// <summary>The results screen: the voyage is over for good.</summary>
        public static void OnRunEnd(WantedProcessStatus status)
        {
            if (!_ready || CurrentRun == null) return;
            try
            {
                string outcome = status == WantedProcessStatus.Win ? "Win"
                               : status == WantedProcessStatus.Lose ? "Loss"
                               : "Incomplete";
                CloseOpenRun(outcome);
                Raise();
            }
            catch (Exception e) { Warn("run end", e); }
        }

        /// <summary>
        /// The voyage left memory without reaching the results screen - quit to menu, or
        /// a new voyage started over it. Written as Incomplete; if it is resumed later
        /// and finishes, that row supersedes this one.
        /// </summary>
        public static void OnRunDetached()
        {
            if (!_ready || CurrentRun == null) return;
            try
            {
                CloseOpenRun("Incomplete");
                Raise();
            }
            catch (Exception e) { Warn("run detach", e); }
        }

        private static void CloseOpenRun(string outcome)
        {
            if (CurrentRun == null) return;

            RunRecord run = CurrentRun;
            CurrentRun = null;

            run.Outcome = outcome;
            run.Ended = Stamp(DateTime.Now);
            SnapshotProcessInto(run);

            TrackerConfig config = TrackerMod.Config;
            Store.AppendRun(run, config == null || config.logRuns);
            CurrentRunBattles.Clear();
            ResetBattleState();

            // The voyage has its own row now, so the checkpoint has nothing left to hold.
            Store.ClearCheckpoint();
        }

        private static void ResetBattleState()
        {
            _battleStartedAt = -1f;
            _battleElapsedBefore = 0;
            _battleStartHp = 0;
            _battleEnemyId = 0;
        }

        /// <summary>
        /// Writes the voyage in progress to disk. Called whenever something about it
        /// changes and on a timer, so an unclean exit costs at most the last few seconds.
        /// </summary>
        public static void Save()
        {
            if (!_ready || Store == null) return;
            try
            {
                if (CurrentRun == null) return;

                SnapshotProcessInto(CurrentRun);
                Store.WriteCheckpoint(new Checkpoint
                {
                    Run = CurrentRun,
                    BattleElapsedSeconds = BattleSeconds(),
                    BattleStartHp = _battleStartHp,
                    BattleEnemyId = _battleEnemyId
                });
            }
            catch (Exception e) { Warn("checkpoint", e); }
        }

        private static void SnapshotProcessInto(RunRecord run)
        {
            WantedProcess process = Process();
            if (process == null) return;

            run.Days = process.currentDays;
            run.PhaseReached = PhaseLabel(process);

            ProcessStatistics stats = process.processStatistics;
            if (stats == null) return;

            run.RunSeconds = (int)stats.gameTime;
            run.RunTime = string.IsNullOrEmpty(stats.gamteTimeString) ? Duration(run.RunSeconds) : stats.gamteTimeString;
            run.CoinsEarned = stats.addCoin;
            run.MaxLinePower = stats.maxLinePower;
            run.MaxTotalPower = stats.maxTotalPower;
            run.Retries = stats.reCount;
        }

        // ----- battles ---------------------------------------------------------

        /// <summary>Stamped when the battle grid is built, to measure the battle itself.</summary>
        public static void OnBattleStart()
        {
            if (!_ready) return;
            try
            {
                _battleStartedAt = Time.realtimeSinceStartup;
                _battleElapsedBefore = 0;
                _battleStartHp = PlayerHp();
                _battleEnemyId = EnemyId();
                Save();
            }
            catch (Exception e) { Warn("battle start", e); }
        }

        private static int EnemyId()
        {
            BattleManager battle = Singleton<BattleManager>.Instance();
            if (battle == null || battle.enemyShip == null) return 0;
            EnemyShipData data = battle.enemyShip.shipData as EnemyShipData;
            return data != null && data.dataShip != null ? data.dataShip.ID : 0;
        }

        public static void OnVictory(DataEnemyShip enemy) { Record(enemy, won: true); }

        public static void OnDefeat(DataEnemyShip enemy) { Record(enemy, won: false); }

        private static void Record(DataEnemyShip enemy, bool won)
        {
            if (!_ready || enemy == null) return;
            try
            {
                WantedProcess process = Process();
                DateTime now = DateTime.Now;

                int hpMax = PlayerMaxHp();
                int hpLeft = won ? PlayerHp() : 0;

                BattleRecord record = new BattleRecord
                {
                    When = Stamp(now),
                    WhenUnix = Unix(now),
                    Result = won ? "Win" : "Loss",
                    EnemyPilot = Translate(enemy.PilotName),
                    EnemyShip = Translate(enemy.Name),
                    Tier = TierInfo.FromEnemyType(enemy.EnemyType),
                    Danger = DangerLabel(enemy),
                    EnemyId = enemy.ID,
                    Phase = PhaseLabel(process),
                    PhaseId = PhaseId(process),
                    Day = process != null ? process.currentDays : 0,
                    Rounds = Rounds(),
                    HpLeft = hpLeft,
                    HpMax = hpMax,
                    HpLost = Mathf.Max(0, _battleStartHp - hpLeft),
                    DurationSeconds = BattleSeconds(),
                    PlayerShip = PlayerShipName(),
                    PlayerPilot = PlayerPilotName(),
                    StarCoins = process != null ? process.starCoin : 0,
                    RunId = CurrentRun != null ? CurrentRun.RunId : "",
                    Seed = process != null ? Seed(process) : "",
                    Mode = process != null ? Mode(process) : "",
                    RunKillIndex = won && CurrentRun != null ? CurrentRun.Kills + 1 : 0
                };

                Store.AppendBattle(record);

                if (CurrentRun != null)
                {
                    Tally.Add(CurrentRun, record);
                    CurrentRunBattles.Add(record);
                    if (!won) CurrentRun.KilledBy = record.EnemyPilot;
                }

                // The battle row is already on disk; the checkpoint follows it, never the
                // other way round, so a crash between the two loses nothing that matters.
                ResetBattleState();
                Save();
                Raise();
            }
            catch (Exception e) { Warn("battle result", e); }
        }

        // ----- reading the game safely -----------------------------------------

        private static WantedProcess Process()
        {
            WantedManager manager = Singleton<WantedManager>.Instance();
            return manager != null ? manager.wantedProcess : null;
        }

        private static ShipData PlayerShipData()
        {
            BattleManager battle = Singleton<BattleManager>.Instance();
            if (battle != null && battle.playerShip != null && battle.playerShip.shipData != null)
                return battle.playerShip.shipData;

            ShipDatasManager ships = Singleton<ShipDatasManager>.Instance();
            return ships != null ? ships.currentPlayerShipData : null;
        }

        private static int PlayerHp()
        {
            ShipData data = PlayerShipData();
            return data != null ? data.currentHP : 0;
        }

        private static int PlayerMaxHp()
        {
            ShipData data = PlayerShipData();
            return data != null ? data.maxHP : 0;
        }

        private static string PlayerShipName()
        {
            ShipDatasManager ships = Singleton<ShipDatasManager>.Instance();
            if (ships == null || ships.currentPlayerShipData == null) return "";
            DataShip data = ships.currentPlayerShipData.dataShip;
            return data != null ? Translate(data.Name) : "";
        }

        private static string PlayerPilotName()
        {
            ShipDatasManager ships = Singleton<ShipDatasManager>.Instance();
            if (ships == null || ships.currentPilotData == null) return "";
            return ships.currentPilotData.GetTranslatedName();
        }

        private static int Rounds()
        {
            BattleManager battle = Singleton<BattleManager>.Instance();
            return battle != null ? battle.round : 0;
        }

        /// <summary>
        /// Time in the current battle, including any counted before the game was last
        /// closed. Measured off realtimeSinceStartup rather than the wall clock, so a
        /// voyage left paused overnight does not come back as a twelve-hour battle.
        /// </summary>
        private static int BattleSeconds()
        {
            if (_battleStartedAt < 0f) return _battleElapsedBefore;
            return _battleElapsedBefore + Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _battleStartedAt));
        }

        private static string PhaseLabel(WantedProcess process)
        {
            if (process == null || process.wantedPhases == null) return "";
            return (process.wantedPhases.currentBigPhase + 1) + "-" + (process.wantedPhases.currentSmallPhase + 1);
        }

        private static int PhaseId(WantedProcess process)
        {
            if (process == null || process.wantedPhases == null) return 0;
            try { return process.wantedPhases.GetCurrentPhase(); }
            catch { return 0; }
        }

        private static string Seed(WantedProcess process)
        {
            if (process == null) return "";
            string seed = process.full_seed;
            return seed ?? "";
        }

        private static string Mode(WantedProcess process)
        {
            if (process == null) return "";
            if (process.isCusMod) return "Custom";
            return process.onBossSuccessive ? "Boss rush" : "Standard";
        }

        /// <summary>The game's own danger wording for the tier, in the player's language.</summary>
        private static string DangerLabel(DataEnemyShip enemy)
        {
            if (enemy.ID == 999) return Translate("WantedPanel/Danger/Final");
            if (enemy.EnemyType == 2) return Translate("WantedPanel/Danger/High");
            if (enemy.EnemyType == 1) return Translate("WantedPanel/Danger/Mid");
            return Translate("WantedPanel/Danger/Low");
        }

        private static string Translate(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            try
            {
                string text = LeanLocalization.GetTranslationText(key);
                return string.IsNullOrEmpty(text) ? key : text;
            }
            catch { return key; }
        }

        private static RunRecord FindOpenRunBySeed(string seed)
        {
            if (string.IsNullOrEmpty(seed)) return null;
            for (int i = Store.Runs.Count - 1; i >= 0; i--)
            {
                RunRecord run = Store.Runs[i];
                if (run.Seed != seed) continue;
                if (run.Outcome == RunRecord.InProgress || run.Outcome == "Incomplete") return run;
            }
            return null;
        }

        // ----- small helpers ---------------------------------------------------

        private static string NewRunId(DateTime when, string seed)
        {
            string tail = string.IsNullOrEmpty(seed) ? "noseed" : seed.Replace(",", "").Replace(" ", "");
            return when.ToString("yyyyMMdd-HHmmss") + "-" + tail;
        }

        public static string Stamp(DateTime when) { return when.ToString("yyyy-MM-dd HH:mm:ss"); }

        public static long Unix(DateTime when) { return (long)(when.ToUniversalTime() - Epoch).TotalSeconds; }

        public static string Duration(int seconds)
        {
            if (seconds <= 0) return "-";
            int h = seconds / 3600;
            int m = (seconds - h * 3600) / 60;
            int s = seconds - h * 3600 - m * 60;
            if (h > 0) return h + "h " + m.ToString("D2") + "m";
            if (m > 0) return m + "m " + s.ToString("D2") + "s";
            return s + "s";
        }

        private static void Raise()
        {
            _stats = null;
            Action changed = Changed;
            if (changed != null)
            {
                try { changed(); } catch (Exception e) { Warn("refresh", e); }
            }
        }

        private static void Warn(string what, Exception e)
        {
            Debug.LogWarning("[LonestarTracker] " + what + " failed: " + e);
        }
    }
}

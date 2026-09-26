using System;
using System.Collections.Generic;
using System.Globalization;

namespace LonestarTracker
{
    /// <summary>Enemy danger tier. Mirrors the game's own EnemyType (Nomal/Elite/Boss).</summary>
    internal enum Tier
    {
        Minion = 0,
        Elite = 1,
        Boss = 2
    }

    internal static class TierInfo
    {
        public static Tier FromEnemyType(int enemyType)
        {
            if (enemyType == 1) return Tier.Elite;
            if (enemyType == 2) return Tier.Boss;
            return Tier.Minion;
        }

        public static string Label(Tier tier)
        {
            if (tier == Tier.Elite) return "Elite";
            if (tier == Tier.Boss) return "Boss";
            return "Minion";
        }

        public static Tier Parse(string s)
        {
            if (s == "Elite") return Tier.Elite;
            if (s == "Boss") return Tier.Boss;
            return Tier.Minion;
        }
    }

    /// <summary>
    /// One column of a CSV log. A column owns both directions of the mapping, so adding
    /// a field to a record is a single entry in that record's schema - the header, the
    /// writer and the parser all follow from it and cannot drift apart.
    /// </summary>
    internal sealed class Column<T>
    {
        public readonly string Name;
        public readonly Func<T, string> Get;
        public readonly Action<T, string> Set;

        public Column(string name, Func<T, string> get, Action<T, string> set)
        {
            Name = name;
            Get = get;
            Set = set;
        }
    }

    internal static class Field
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Num(int v) { return v.ToString(Inv); }
        public static string Num(long v) { return v.ToString(Inv); }
        public static string Flag(bool v) { return v ? "yes" : "no"; }

        public static int Int(string s)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : 0;
        }

        public static long Long(string s)
        {
            long v;
            return long.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : 0L;
        }

        public static bool Bool(string s)
        {
            return s == "yes" || s == "true" || s == "True" || s == "1";
        }
    }

    /// <summary>One completed battle - a kill (result Win) or a defeat (result Loss).</summary>
    internal sealed class BattleRecord
    {
        public string When = "";
        public long WhenUnix;
        public string Result = "Win";
        public string EnemyPilot = "";
        public string EnemyShip = "";
        public Tier Tier = Tier.Minion;
        public string Danger = "";
        public int EnemyId;
        public string Phase = "";
        public int PhaseId;
        public int Day;
        public int Rounds;
        public int HpLeft;
        public int HpMax;
        public int HpLost;
        public int DurationSeconds;
        public string PlayerShip = "";
        public string PlayerPilot = "";
        public int StarCoins;
        public string RunId = "";
        public string Seed = "";
        public string Mode = "";
        public int RunKillIndex;

        public bool IsKill { get { return Result == "Win"; } }

        public static readonly List<Column<BattleRecord>> Schema = new List<Column<BattleRecord>>
        {
            new Column<BattleRecord>("when",         r => r.When,                       (r, s) => r.When = s),
            new Column<BattleRecord>("when_unix",    r => Field.Num(r.WhenUnix),        (r, s) => r.WhenUnix = Field.Long(s)),
            new Column<BattleRecord>("result",       r => r.Result,                     (r, s) => r.Result = s),
            new Column<BattleRecord>("enemy_pilot",  r => r.EnemyPilot,                 (r, s) => r.EnemyPilot = s),
            new Column<BattleRecord>("enemy_ship",   r => r.EnemyShip,                  (r, s) => r.EnemyShip = s),
            new Column<BattleRecord>("tier",         r => TierInfo.Label(r.Tier),       (r, s) => r.Tier = TierInfo.Parse(s)),
            new Column<BattleRecord>("danger",       r => r.Danger,                     (r, s) => r.Danger = s),
            new Column<BattleRecord>("enemy_id",     r => Field.Num(r.EnemyId),         (r, s) => r.EnemyId = Field.Int(s)),
            new Column<BattleRecord>("phase",        r => r.Phase,                      (r, s) => r.Phase = s),
            new Column<BattleRecord>("phase_id",     r => Field.Num(r.PhaseId),         (r, s) => r.PhaseId = Field.Int(s)),
            new Column<BattleRecord>("day",          r => Field.Num(r.Day),             (r, s) => r.Day = Field.Int(s)),
            new Column<BattleRecord>("rounds",       r => Field.Num(r.Rounds),          (r, s) => r.Rounds = Field.Int(s)),
            new Column<BattleRecord>("hp_left",      r => Field.Num(r.HpLeft),          (r, s) => r.HpLeft = Field.Int(s)),
            new Column<BattleRecord>("hp_max",       r => Field.Num(r.HpMax),           (r, s) => r.HpMax = Field.Int(s)),
            new Column<BattleRecord>("hp_lost",      r => Field.Num(r.HpLost),          (r, s) => r.HpLost = Field.Int(s)),
            new Column<BattleRecord>("duration_s",   r => Field.Num(r.DurationSeconds), (r, s) => r.DurationSeconds = Field.Int(s)),
            new Column<BattleRecord>("player_ship",  r => r.PlayerShip,                 (r, s) => r.PlayerShip = s),
            new Column<BattleRecord>("player_pilot", r => r.PlayerPilot,                (r, s) => r.PlayerPilot = s),
            new Column<BattleRecord>("star_coins",   r => Field.Num(r.StarCoins),       (r, s) => r.StarCoins = Field.Int(s)),
            new Column<BattleRecord>("run_id",       r => r.RunId,                      (r, s) => r.RunId = s),
            new Column<BattleRecord>("seed",         r => r.Seed,                       (r, s) => r.Seed = s),
            new Column<BattleRecord>("mode",         r => r.Mode,                       (r, s) => r.Mode = s),
            new Column<BattleRecord>("run_kill_no",  r => Field.Num(r.RunKillIndex),    (r, s) => r.RunKillIndex = Field.Int(s)),
        };
    }

    /// <summary>One voyage, from ship select to the results screen.</summary>
    internal sealed class RunRecord
    {
        public const string InProgress = "In progress";

        public string RunId = "";
        public string Started = "";
        public long StartedUnix;
        public string Ended = "";
        public string Outcome = InProgress;
        public string PlayerShip = "";
        public string PlayerPilot = "";
        public string Seed = "";
        public bool Seeded;
        public string Mode = "";
        public int Kills;
        public int KillsMinion;
        public int KillsElite;
        public int KillsBoss;
        public int Defeats;
        public int Days;
        public string PhaseReached = "";
        public int RunSeconds;
        public string RunTime = "";
        public int CoinsEarned;
        public int MaxLinePower;
        public int MaxTotalPower;
        public int Retries;
        public string KilledBy = "";

        public bool IsWin { get { return Outcome == "Win"; } }
        public bool IsLoss { get { return Outcome == "Loss"; } }

        public static readonly List<Column<RunRecord>> Schema = new List<Column<RunRecord>>
        {
            new Column<RunRecord>("run_id",          r => r.RunId,                    (r, s) => r.RunId = s),
            new Column<RunRecord>("started",         r => r.Started,                  (r, s) => r.Started = s),
            new Column<RunRecord>("started_unix",    r => Field.Num(r.StartedUnix),   (r, s) => r.StartedUnix = Field.Long(s)),
            new Column<RunRecord>("ended",           r => r.Ended,                    (r, s) => r.Ended = s),
            new Column<RunRecord>("outcome",         r => r.Outcome,                  (r, s) => r.Outcome = s),
            new Column<RunRecord>("player_ship",     r => r.PlayerShip,               (r, s) => r.PlayerShip = s),
            new Column<RunRecord>("player_pilot",    r => r.PlayerPilot,              (r, s) => r.PlayerPilot = s),
            new Column<RunRecord>("seed",            r => r.Seed,                     (r, s) => r.Seed = s),
            new Column<RunRecord>("seeded",          r => Field.Flag(r.Seeded),       (r, s) => r.Seeded = Field.Bool(s)),
            new Column<RunRecord>("mode",            r => r.Mode,                     (r, s) => r.Mode = s),
            new Column<RunRecord>("kills",           r => Field.Num(r.Kills),         (r, s) => r.Kills = Field.Int(s)),
            new Column<RunRecord>("kills_minion",    r => Field.Num(r.KillsMinion),   (r, s) => r.KillsMinion = Field.Int(s)),
            new Column<RunRecord>("kills_elite",     r => Field.Num(r.KillsElite),    (r, s) => r.KillsElite = Field.Int(s)),
            new Column<RunRecord>("kills_boss",      r => Field.Num(r.KillsBoss),     (r, s) => r.KillsBoss = Field.Int(s)),
            new Column<RunRecord>("defeats",         r => Field.Num(r.Defeats),       (r, s) => r.Defeats = Field.Int(s)),
            new Column<RunRecord>("days",            r => Field.Num(r.Days),          (r, s) => r.Days = Field.Int(s)),
            new Column<RunRecord>("phase_reached",   r => r.PhaseReached,             (r, s) => r.PhaseReached = s),
            new Column<RunRecord>("run_seconds",     r => Field.Num(r.RunSeconds),    (r, s) => r.RunSeconds = Field.Int(s)),
            new Column<RunRecord>("run_time",        r => r.RunTime,                  (r, s) => r.RunTime = s),
            new Column<RunRecord>("coins_earned",    r => Field.Num(r.CoinsEarned),   (r, s) => r.CoinsEarned = Field.Int(s)),
            new Column<RunRecord>("max_line_power",  r => Field.Num(r.MaxLinePower),  (r, s) => r.MaxLinePower = Field.Int(s)),
            new Column<RunRecord>("max_total_power", r => Field.Num(r.MaxTotalPower), (r, s) => r.MaxTotalPower = Field.Int(s)),
            new Column<RunRecord>("retries",         r => Field.Num(r.Retries),       (r, s) => r.Retries = Field.Int(s)),
            new Column<RunRecord>("killed_by",       r => r.KilledBy,                 (r, s) => r.KilledBy = s),
        };
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LonestarTracker
{
    internal interface ITrackerTab
    {
        string Title { get; }
        bool Available { get; }
        string Hint { get; }
        int RowCount { get; }
        int RowsPerPage(Rect area, UiSkin skin);
        void HandleKey(Key key);
        void Invalidate();
        void Draw(Rect area, UiSkin skin, int scroll);
    }

    /// <summary>A named way of narrowing or ordering a list. Adding one is adding an entry.</summary>
    internal sealed class Choice<T>
    {
        public readonly string Name;
        public readonly T Value;
        public Choice(string name, T value) { Name = name; Value = value; }
    }

    // ---------------------------------------------------------------- overview

    internal sealed class OverviewTab : ITrackerTab
    {
        public string Title { get { return "OVERVIEW"; } }
        public bool Available { get { return true; } }
        public string Hint { get { return "everything logged so far"; } }
        public int RowCount { get { return 0; } }
        public int RowsPerPage(Rect area, UiSkin skin) { return 1; }
        public void HandleKey(Key key) { }
        public void Invalidate() { }

        public void Draw(Rect area, UiSkin skin, int scroll)
        {
            Stats s = Tracker.Snapshot;
            float y = area.y;
            float step = skin.RowHeight;
            float half = area.width * 0.5f - skin.Pad;

            y = Heading(new Rect(area.x, y, area.width, step), skin, "LIFETIME");

            y = Stat(area.x, y, half, step, skin, "Kills", s.Kills.ToString(),
                new[] { s.KillsMinion + " minion", s.KillsElite + " elite", s.KillsBoss + " boss" },
                new[] { Palette.Minion, Palette.Elite, Palette.Boss });

            y = Stat(area.x, y, half, step, skin, "Battles", s.Battles.ToString(),
                new[] { s.Kills + " won", s.Defeats + " lost", s.WinRatePercent + "% win rate" },
                new[] { Palette.Win, Palette.Lose, Palette.Text });

            y = Stat(area.x, y, half, step, skin, "Voyages", s.RunsTotal.ToString(),
                new[] { s.RunsWon + " won", s.RunsLost + " lost", s.RunsIncomplete + " unfinished" },
                new[] { Palette.Win, Palette.Lose, Palette.Muted });

            y = Stat(area.x, y, half, step, skin, "Best kill streak", s.BestKillStreak + " battles",
                new[] { "current " + s.CurrentKillStreak }, new[] { Palette.Muted });

            y = Stat(area.x, y, half, step, skin, "Most kills in a voyage", s.MostKillsInRun.ToString(),
                new string[0], new Color[0]);

            y = Stat(area.x, y, half, step, skin, "Fastest winning voyage",
                s.FastestWinSeconds < 0 ? "-" : Tracker.Duration(s.FastestWinSeconds),
                new string[0], new Color[0]);

            y = Stat(area.x, y, half, step, skin, "Time logged", Tracker.Duration(s.TrackedSeconds),
                new string[0], new Color[0]);

            y += skin.Pad;

            float columnWidth = area.width * 0.5f - skin.Pad;
            float listTop = y;
            float left = area.x;
            float right = area.x + columnWidth + skin.Pad * 2f;

            DrawTop(new Rect(left, listTop, columnWidth, area.height - (listTop - area.y)), skin,
                "MOST KILLED", s.MostKilled, Palette.Win, "No kills logged yet.");

            DrawTop(new Rect(right, listTop, columnWidth, area.height - (listTop - area.y)), skin,
                "TOOK YOU DOWN", s.Nemesis, Palette.Lose, "Never been destroyed. Yet.");
        }

        private static float Heading(Rect rect, UiSkin skin, string text)
        {
            Ui.Label(rect, text, Palette.Heading, skin.Text);
            Ui.Fill(new Rect(rect.x, rect.y + rect.height - 1f, rect.width, 1f), Palette.Rule);
            return rect.y + rect.height + skin.Pad * 0.4f;
        }

        private static float Stat(float x, float y, float labelWidth, float step, UiSkin skin,
            string label, string value, string[] extras, Color[] extraColors)
        {
            Ui.Label(new Rect(x, y, labelWidth, step), label, Palette.Muted, skin.Text);

            float vx = x + labelWidth * 0.62f;
            float vw = skin.Text.CalcSize(new GUIContent(value)).x;
            Ui.Label(new Rect(vx, y, vw + 4f, step), value, Palette.Text, skin.Text);

            if (extras.Length > 0)
            {
                List<string> parts = new List<string>();
                List<Color> colors = new List<Color>();
                for (int i = 0; i < extras.Length; i++)
                {
                    parts.Add(extras[i]);
                    colors.Add(extraColors[i]);
                }
                Ui.Run(new Rect(vx + Mathf.Max(vw, 70f * skin.Scale) + skin.Pad, y, 600f, step),
                    skin, parts, colors, skin.Pad);
            }
            return y + step;
        }

        private static void DrawTop(Rect area, UiSkin skin, string title, List<NameCount> entries,
            Color countColor, string empty)
        {
            float y = Heading(new Rect(area.x, area.y, area.width, skin.RowHeight), skin, title);

            if (entries.Count == 0)
            {
                Ui.Label(new Rect(area.x, y, area.width, skin.RowHeight), empty, Palette.Muted, skin.Text);
                return;
            }

            // Three lanes, so a long pilot name cannot run over the ship name or the count.
            float countWidth = Mathf.Round(56f * skin.Scale);
            float gap = skin.Pad * 0.6f;
            float textWidth = area.width - countWidth - gap;
            float nameWidth = Mathf.Round(textWidth * 0.52f);
            float detailWidth = textWidth - nameWidth - gap;

            for (int i = 0; i < entries.Count; i++)
            {
                NameCount entry = entries[i];
                if ((i & 1) == 1) Ui.Fill(new Rect(area.x, y, area.width, skin.RowHeight), Palette.RowOdd);

                Ui.Label(new Rect(area.x, y, nameWidth, skin.RowHeight),
                    Ui.Clip(entry.Name, nameWidth, skin), Palette.Text, skin.Text);
                Ui.Label(new Rect(area.x + nameWidth + gap, y, detailWidth, skin.RowHeight),
                    Ui.Clip(entry.Detail, detailWidth, skin), Palette.Muted, skin.Text);
                Ui.Label(new Rect(area.x + area.width - countWidth, y, countWidth, skin.RowHeight),
                    "×" + entry.Count, countColor, skin.TextRight);
                y += skin.RowHeight;
            }
        }
    }

    // ----------------------------------------------------------------- battles

    internal sealed class BattlesTab : ITrackerTab
    {
        private readonly List<Choice<Func<BattleRecord, bool>>> _filters =
            new List<Choice<Func<BattleRecord, bool>>>
            {
                new Choice<Func<BattleRecord, bool>>("all battles", b => true),
                new Choice<Func<BattleRecord, bool>>("kills", b => b.IsKill),
                new Choice<Func<BattleRecord, bool>>("minions", b => b.IsKill && b.Tier == Tier.Minion),
                new Choice<Func<BattleRecord, bool>>("elites", b => b.IsKill && b.Tier == Tier.Elite),
                new Choice<Func<BattleRecord, bool>>("bosses", b => b.IsKill && b.Tier == Tier.Boss),
                new Choice<Func<BattleRecord, bool>>("defeats", b => !b.IsKill),
            };

        private readonly List<Choice<Comparison<BattleRecord>>> _sorts =
            new List<Choice<Comparison<BattleRecord>>>
            {
                new Choice<Comparison<BattleRecord>>("newest", (a, b) => b.WhenUnix.CompareTo(a.WhenUnix)),
                new Choice<Comparison<BattleRecord>>("oldest", (a, b) => a.WhenUnix.CompareTo(b.WhenUnix)),
                new Choice<Comparison<BattleRecord>>("toughest tier", (a, b) =>
                {
                    int byTier = ((int)b.Tier).CompareTo((int)a.Tier);
                    return byTier != 0 ? byTier : b.WhenUnix.CompareTo(a.WhenUnix);
                }),
                new Choice<Comparison<BattleRecord>>("longest fight", (a, b) =>
                {
                    int byRounds = b.Rounds.CompareTo(a.Rounds);
                    return byRounds != 0 ? byRounds : b.WhenUnix.CompareTo(a.WhenUnix);
                }),
                new Choice<Comparison<BattleRecord>>("closest call", (a, b) =>
                {
                    int byHp = a.HpLeft.CompareTo(b.HpLeft);
                    return byHp != 0 ? byHp : b.WhenUnix.CompareTo(a.WhenUnix);
                }),
                new Choice<Comparison<BattleRecord>>("enemy name", (a, b) =>
                {
                    int byName = string.Compare(a.EnemyPilot, b.EnemyPilot, StringComparison.CurrentCulture);
                    return byName != 0 ? byName : b.WhenUnix.CompareTo(a.WhenUnix);
                }),
            };

        private readonly List<TableColumn<BattleRecord>> _columns;
        private List<BattleRecord> _rows;
        private int _filter;
        private int _sort;

        public BattlesTab()
        {
            _columns = Columns.ForBattles();
        }

        public string Title { get { return "BATTLES"; } }
        public bool Available { get { return true; } }
        public int RowCount { get { return Rows.Count; } }

        public string Hint
        {
            get { return "F filter: " + _filters[_filter].Name + "   S order: " + _sorts[_sort].Name; }
        }

        public int RowsPerPage(Rect area, UiSkin skin) { return Table.RowsThatFit(area, skin); }

        public void Invalidate() { _rows = null; }

        public void HandleKey(Key key)
        {
            if (key == Key.F) { _filter = (_filter + 1) % _filters.Count; _rows = null; }
            else if (key == Key.S) { _sort = (_sort + 1) % _sorts.Count; _rows = null; }
        }

        /// <summary>The filter and order by name, so they survive a restart.</summary>
        public string FilterName
        {
            get { return _filters[_filter].Name; }
            set { _filter = IndexOf(_filters, value, _filter); _rows = null; }
        }

        public string OrderName
        {
            get { return _sorts[_sort].Name; }
            set { _sort = IndexOf(_sorts, value, _sort); _rows = null; }
        }

        private static int IndexOf<T>(List<Choice<T>> choices, string name, int fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            for (int i = 0; i < choices.Count; i++)
                if (choices[i].Name == name) return i;
            return fallback;
        }

        private List<BattleRecord> Rows
        {
            get
            {
                if (_rows != null) return _rows;

                _rows = new List<BattleRecord>();
                if (Tracker.Store != null)
                {
                    Func<BattleRecord, bool> keep = _filters[_filter].Value;
                    foreach (BattleRecord battle in Tracker.Store.Battles)
                        if (keep(battle)) _rows.Add(battle);
                }
                _rows.Sort(_sorts[_sort].Value);
                return _rows;
            }
        }

        public void Draw(Rect area, UiSkin skin, int scroll)
        {
            Table.Draw(area, skin, _columns, Rows, scroll);
        }
    }

    // -------------------------------------------------------------------- runs

    internal sealed class RunsTab : ITrackerTab
    {
        private readonly List<TableColumn<RunRecord>> _columns = Columns.ForRuns();
        private List<RunRecord> _rows;

        public string Title { get { return "VOYAGES"; } }
        public bool Available { get { return true; } }
        public string Hint { get { return "newest first"; } }
        public int RowCount { get { return Rows.Count; } }
        public int RowsPerPage(Rect area, UiSkin skin) { return Table.RowsThatFit(area, skin); }
        public void HandleKey(Key key) { }
        public void Invalidate() { _rows = null; }

        private List<RunRecord> Rows
        {
            get
            {
                if (_rows != null) return _rows;
                _rows = new List<RunRecord>();
                if (Tracker.Store != null) _rows.AddRange(Tracker.Store.Runs);
                _rows.Sort((a, b) => b.StartedUnix.CompareTo(a.StartedUnix));
                return _rows;
            }
        }

        public void Draw(Rect area, UiSkin skin, int scroll)
        {
            Table.Draw(area, skin, _columns, Rows, scroll);
        }
    }

    // ------------------------------------------------------------ current run

    internal sealed class CurrentRunTab : ITrackerTab
    {
        private readonly List<TableColumn<BattleRecord>> _columns = Columns.ForBattles();
        private List<BattleRecord> _rows;

        public string Title { get { return "THIS VOYAGE"; } }
        public bool Available { get { return Tracker.CurrentRun != null; } }
        public string Hint { get { return "newest first"; } }
        public int RowCount { get { return Rows.Count; } }
        public void HandleKey(Key key) { }
        public void Invalidate() { _rows = null; }

        private float HeaderHeight(UiSkin skin) { return skin.RowHeight * 2f + skin.Pad; }

        public int RowsPerPage(Rect area, UiSkin skin)
        {
            Rect table = area;
            table.yMin += HeaderHeight(skin);
            return Table.RowsThatFit(table, skin);
        }

        private List<BattleRecord> Rows
        {
            get
            {
                if (_rows != null) return _rows;
                _rows = new List<BattleRecord>(Tracker.CurrentRunBattles);
                _rows.Reverse();
                return _rows;
            }
        }

        public void Draw(Rect area, UiSkin skin, int scroll)
        {
            RunRecord run = Tracker.CurrentRun;
            if (run == null)
            {
                Ui.Label(new Rect(area.x, area.y, area.width, skin.RowHeight),
                    "No voyage in progress.", Palette.Muted, skin.Text);
                return;
            }

            Ui.Run(new Rect(area.x, area.y, area.width, skin.RowHeight), skin,
                new[] { run.PlayerShip, "·", run.PlayerPilot, "·", run.Mode, "·", "seed " + run.Seed },
                new[] { Palette.Text, Palette.Muted, Palette.Text, Palette.Muted, Palette.Muted, Palette.Muted, Palette.Muted },
                skin.Pad * 0.5f);

            Ui.Run(new Rect(area.x, area.y + skin.RowHeight, area.width, skin.RowHeight), skin,
                new[]
                {
                    run.Kills + " kills", run.KillsMinion + " minion", run.KillsElite + " elite",
                    run.KillsBoss + " boss", "day " + run.Days, "phase " + run.PhaseReached
                },
                new[] { Palette.Text, Palette.Minion, Palette.Elite, Palette.Boss, Palette.Muted, Palette.Muted },
                skin.Pad);

            Rect table = area;
            table.yMin += HeaderHeight(skin);
            Table.Draw(table, skin, _columns, Rows, scroll);
        }
    }

    // ----------------------------------------------------------------- columns

    internal static class Columns
    {
        public static List<TableColumn<BattleRecord>> ForBattles()
        {
            return new List<TableColumn<BattleRecord>>
            {
                new TableColumn<BattleRecord>("WHEN", 1.7f, b => Short(b.When), b => Palette.Muted),
                new TableColumn<BattleRecord>("RESULT", 1.0f, b => b.IsKill ? "Kill" : "Destroyed",
                    b => Palette.ForResult(b.IsKill)),
                new TableColumn<BattleRecord>("PILOT", 2.6f, b => b.EnemyPilot, b => Palette.Text),
                new TableColumn<BattleRecord>("SHIP", 2.4f, b => b.EnemyShip, b => Palette.Muted),
                new TableColumn<BattleRecord>("TIER", 1.0f, b => TierInfo.Label(b.Tier),
                    b => Palette.ForTier(b.Tier)),
                new TableColumn<BattleRecord>("PHASE", 0.8f, b => b.Phase, b => Palette.Muted),
                new TableColumn<BattleRecord>("DAY", 0.6f, b => b.Day.ToString(), b => Palette.Muted, true),
                new TableColumn<BattleRecord>("RNDS", 0.7f, b => b.Rounds.ToString(), b => Palette.Muted, true),
                new TableColumn<BattleRecord>("HULL", 1.0f, b => b.HpLeft + "/" + b.HpMax,
                    b => b.HpMax > 0 && b.HpLeft * 4 <= b.HpMax ? Palette.Lose : Palette.Text, true),
                new TableColumn<BattleRecord>("TIME", 0.8f, b => Tracker.Duration(b.DurationSeconds),
                    b => Palette.Muted, true),
            };
        }

        public static List<TableColumn<RunRecord>> ForRuns()
        {
            return new List<TableColumn<RunRecord>>
            {
                new TableColumn<RunRecord>("STARTED", 1.7f, r => Short(r.Started), r => Palette.Muted),
                new TableColumn<RunRecord>("OUTCOME", 1.2f, r => r.Outcome, r => Palette.ForOutcome(r.Outcome)),
                new TableColumn<RunRecord>("SHIP", 1.9f, r => r.PlayerShip, r => Palette.Text),
                new TableColumn<RunRecord>("PILOT", 1.9f, r => r.PlayerPilot, r => Palette.Muted),
                new TableColumn<RunRecord>("KILLS", 0.8f, r => r.Kills.ToString(), r => Palette.Text, true),
                new TableColumn<RunRecord>("MIN", 0.6f, r => r.KillsMinion.ToString(), r => Palette.Minion, true),
                new TableColumn<RunRecord>("ELI", 0.6f, r => r.KillsElite.ToString(), r => Palette.Elite, true),
                new TableColumn<RunRecord>("BOSS", 0.7f, r => r.KillsBoss.ToString(), r => Palette.Boss, true),
                new TableColumn<RunRecord>("DAYS", 0.7f, r => r.Days.ToString(), r => Palette.Muted, true),
                new TableColumn<RunRecord>("PHASE", 0.8f, r => r.PhaseReached, r => Palette.Muted),
                new TableColumn<RunRecord>("TIME", 1.0f, r => string.IsNullOrEmpty(r.RunTime) ? "-" : r.RunTime,
                    r => Palette.Muted, true),
            };
        }

        /// <summary>"2026-09-20 14:33:01" as "09-20 14:33" - the year is rarely the question.</summary>
        private static string Short(string stamp)
        {
            if (string.IsNullOrEmpty(stamp)) return "";
            if (stamp.Length >= 16) return stamp.Substring(5, 11);
            return stamp;
        }
    }
}

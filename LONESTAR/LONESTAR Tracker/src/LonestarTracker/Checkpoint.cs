using System.Collections.Generic;

namespace LonestarTracker
{
    /// <summary>
    /// The voyage in progress, on disk. battles.csv and runs.csv only ever gain a row
    /// once something has finished; this is the other half - the state that would
    /// otherwise exist only in memory, so a crash, an alt-F4 or a power cut costs
    /// nothing but the seconds since the last write.
    ///
    /// Unlike the two logs this file IS rewritten, because it is a checkpoint rather
    /// than a history. It can never hold the only copy of anything that happened: a
    /// battle is appended to battles.csv before the checkpoint is rewritten, so the
    /// worst a lost checkpoint costs is the voyage's running clock and coin total,
    /// both of which the game's own save still has.
    /// </summary>
    internal sealed class Checkpoint
    {
        public RunRecord Run = new RunRecord();

        /// <summary>Seconds already spent in the current battle in earlier sessions.</summary>
        public int BattleElapsedSeconds;

        /// <summary>Player hull when the current battle started, for hp_lost.</summary>
        public int BattleStartHp;

        /// <summary>Enemy the current battle is against, or 0 when not in one.</summary>
        public int BattleEnemyId;

        public bool HasRun { get { return !string.IsNullOrEmpty(Run.RunId); } }

        /// <summary>
        /// The voyage's own columns, projected straight off RunRecord's schema, plus the
        /// few fields only a checkpoint needs. Projecting rather than restating means a
        /// field added to RunRecord is carried here automatically.
        /// </summary>
        public static readonly List<Column<Checkpoint>> Schema = BuildSchema();

        private static List<Column<Checkpoint>> BuildSchema()
        {
            List<Column<Checkpoint>> schema = new List<Column<Checkpoint>>();

            foreach (Column<RunRecord> column in RunRecord.Schema)
            {
                Column<RunRecord> captured = column;
                schema.Add(new Column<Checkpoint>(
                    captured.Name,
                    c => captured.Get(c.Run),
                    (c, s) => captured.Set(c.Run, s)));
            }

            schema.Add(new Column<Checkpoint>("battle_elapsed_s",
                c => Field.Num(c.BattleElapsedSeconds), (c, s) => c.BattleElapsedSeconds = Field.Int(s)));
            schema.Add(new Column<Checkpoint>("battle_start_hp",
                c => Field.Num(c.BattleStartHp), (c, s) => c.BattleStartHp = Field.Int(s)));
            schema.Add(new Column<Checkpoint>("battle_enemy_id",
                c => Field.Num(c.BattleEnemyId), (c, s) => c.BattleEnemyId = Field.Int(s)));

            return schema;
        }
    }
}

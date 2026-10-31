using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    // Plain data (public fields and Lists) so the theater saves as JSON beside the War Path slot.

    public enum Side { Blue, Red }

    public enum TerrainKind { Open, Forest, Urban, Water }

    public enum UnitKind { Mechanized, Armour, Infantry, Recon, Airborne }

    public enum UnitSize { Battalion, Regiment, Brigade, Division }

    public enum BattleResolution { Pending, Fought, AutoResolved }

    /// <summary>Your units move, your attacks are decided, then the enemy moves and its attacks are decided.</summary>
    public enum Phase { PlayerMoves, PlayerBattles, EnemyBattles }

    /// <summary>One square of the theater grid.</summary>
    [Serializable]
    public class Cell
    {
        public int id;
        public int col;
        public int row;
        public TerrainKind terrain;
        public bool river;
        public bool road;
        public Side owner;
        // Index into Theater.towns when a town stands here, else -1.
        public int town = -1;
        public List<int> neighbors = new List<int>();
    }

    [Serializable]
    public class Town
    {
        public int id;
        public string name;
        public int cell;
        // Position in map units (x 0..aspect, y 0..1, y down).
        public float x;
        public float y;
        // 0 village, 1 town, 2 city.
        public int size;
        public int vp;
        // The headquarters of this side stands here; losing it loses the campaign.
        public bool hq;
        public Side hqSide;
        // Index into the game's War Path map list: battles near this town are fought there.
        public int mapIndex = -1;
    }

    [Serializable]
    public class Formation
    {
        public int id;
        public Side side;
        public string name;
        public int cell;
        public UnitKind kind;
        public UnitSize size;
        public float strength;
        public float maxStrength;
        public int entrench;
        public int experience;
        public bool supplied = true;
        public bool destroyed;
        // Movement points: maxMp at the start of each of its side's moves.
        public float mp;
        public float maxMp;
        public bool moved;
        public bool attacked;
        // Took part in a battle this turn, as attacker or defender.
        public bool fought;
        public bool refit;
        // Blue: index of the War Path force record it was raised from. Red: battlegroup index.
        public int forceRef;

        public float strength01 => maxStrength <= 0f ? 0f : strength / maxStrength;
    }

    [Serializable]
    public class Battle
    {
        public int id;
        // The defenders' cell.
        public int cell;
        public Side attackerSide;
        public List<int> attackers = new List<int>();
        public List<int> defenders = new List<int>();
        public BattleResolution resolution;
        public bool attackerWon;
        public float attackerLoss;
        public float defenderLoss;
        // The player's formation that fought it in game; its real losses are already in its roster.
        public int foughtBy = -1;
    }

    /// <summary>A move made this phase, kept so it can be taken back.</summary>
    [Serializable]
    public class MoveRecord
    {
        public int formation;
        public int from;
        public float mp;
        public bool moved;
        public int entrench;
        public bool refit;
        public bool attacked;
        public List<int> flipped = new List<int>();
        // The battle this step joined as an attacker, and whether the step created it.
        public int battle = -1;
        public bool createdBattle;
    }

    [Serializable]
    public class Theater
    {
        public int seed;
        public int turn;
        public int maxTurns = 12;
        public Side playerSide = Side.Blue;
        public int cols;
        public int rows;
        public float aspect;
        public Phase phase;
        // The side whose formations may move right now.
        public Side acting = Side.Blue;
        // How hard the computer opponent pushes: 1 holds, higher attacks at worse odds.
        public float aiAggression = 1.4f;
        public List<Cell> cells = new List<Cell>();
        public List<Town> towns = new List<Town>();
        public List<Formation> formations = new List<Formation>();
        public List<Battle> battles = new List<Battle>();
        public List<MoveRecord> moves = new List<MoveRecord>();
        // Where the enemy's formations went in its last move: [from, to] cells.
        public List<int[]> enemyMoves = new List<int[]>();
        public List<string> log = new List<string>();
        public int blueVP;
        public int redVP;
        public bool finished;
        public Side winner;
        public string endReason = "";
        public int nextFormationId;
        public int nextBattleId;
        // Reinforcements arrive at each side's HQ on these turns, if the HQ is still held.
        public List<int> reinforcementTurns = new List<int> { 4, 8 };

        public Cell Cell(int id) => cells[id];

        public Cell CellAt(int col, int row) => col < 0 || row < 0 || col >= cols || row >= rows ? null : cells[row * cols + col];

        public Formation Formation(int id) => formations.First(f => f.id == id);

        public IEnumerable<Formation> Alive(Side side) => formations.Where(f => !f.destroyed && f.side == side);

        public Formation At(int cell) => formations.FirstOrDefault(f => !f.destroyed && f.cell == cell);

        public Battle BattleAt(int cell) => battles.FirstOrDefault(b => b.cell == cell && b.resolution == BattleResolution.Pending);

        public bool Adjacent(int a, int b) => Distance(a, b) == 1;

        public bool EnemyAt(int cell, Side side) { Formation f = At(cell); return f != null && f.side != side; }

        public Town HQ(Side side) => towns.FirstOrDefault(x => x.hq && x.hqSide == side);

        public Town TownAt(int cell) { int i = cells[cell].town; return i >= 0 ? towns[i] : null; }

        // Cell centre in map units.
        public float X(int cell) => (cells[cell].col + 0.5f) / cols * aspect;

        public float Y(int cell) => (cells[cell].row + 0.5f) / rows;

        public int Distance(int a, int b) => Math.Abs(cells[a].col - cells[b].col) + Math.Abs(cells[a].row - cells[b].row);

        public void Log(string line) => log.Add("T" + (turn + 1) + ": " + line);

        public static Side Other(Side s) => s == Side.Blue ? Side.Red : Side.Blue;

        /// <summary>The town a battle at this cell is named after and fought at: the nearest one.</summary>
        public Town NearestTown(int cell)
        {
            float x = X(cell), y = Y(cell);
            return towns.OrderBy(t => (t.x - x) * (t.x - x) + (t.y - y) * (t.y - y)).First();
        }
    }

    /// <summary>Deterministic randomness keyed by what is being decided, so replays agree.</summary>
    public static class Dice
    {
        public static Random For(Theater t, int salt, int salt2 = 0)
        {
            unchecked
            {
                int h = t.seed;
                h = h * 486187739 + t.turn;
                h = h * 486187739 + salt;
                h = h * 486187739 + salt2;
                return new Random(h);
            }
        }
    }
}

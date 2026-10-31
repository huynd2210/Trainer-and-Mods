using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TheaterCommand.Core;

/// <summary>
/// Theater Command off-line.
///   dotnet run -- render [seed] [out.png] [width]   paint a generated theater with its units
///   dotnet run -- stats [campaigns]                 AI vs AI campaigns, every battle auto-resolved
///   dotnet run -- trace [seed] [dir]                one campaign, a PNG per turn
/// </summary>
internal static class Program
{
    private static float BlueAggression = 1f;
    private static float RedAggression = 1.4f;

    private static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "stats";
        int Arg(int i, int d) => args.Length > i ? int.Parse(args[i]) : d;
        if (args.Length > 3 && mode == "stats")
        {
            BlueAggression = float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            RedAggression = float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        }
        switch (mode)
        {
            case "render":
                Render(Generate(Arg(1, 1)), args.Length > 2 ? args[2] : "map.png", Arg(3, 2048));
                return 0;
            case "trace":
                return Trace(Arg(1, 1), args.Length > 2 ? args[2] : "trace");
            default:
                return Stats(Arg(1, 200));
        }
    }

    private static Theater Generate(int seed) => TheaterGenerator.Generate(seed, new GeneratorSettings());

    // The AI plays NATO's moves, then the rules play the Pact's, battles auto-resolved.
    private static void PlayTurn(Theater t, IForceHooks hooks)
    {
        StrategicAI.Play(t, Side.Blue, BlueAggression);
        while (!t.finished)
        {
            Battle b = Rules.Pending(t).FirstOrDefault();
            if (b != null)
            {
                Rules.AutoResolve(t, b, hooks);
                continue;
            }
            int turn = t.turn;
            Phase before = t.phase;
            Rules.Advance(t, hooks);
            if (t.turn != turn) break;
            if (t.phase == before && !Rules.Pending(t).Any()) throw new Exception("stuck in " + before);
        }
    }

    private static int Stats(int n)
    {
        var hooks = new AbstractForceHooks();
        int blueWins = 0, errors = 0, battles = 0, turns = 0, lostBlue = 0, lostRed = 0, flips = 0, redAttacks = 0, blueAttacks = 0;
        var reasons = new Dictionary<string, int>();
        for (int seed = 1; seed <= n; seed++)
        {
            try
            {
                Theater t = Generate(seed);
                Validate(t);
                var start = t.cells.Select(c => c.owner).ToList();
                while (!t.finished)
                {
                    int before = t.nextBattleId;
                    PlayTurn(t, hooks);
                    Validate(t);
                }
                battles += t.nextBattleId;
                turns += t.turn;
                flips += t.cells.Count(c => c.owner != start[c.id]);
                lostBlue += t.formations.Count(f => f.side == Side.Blue && f.destroyed);
                lostRed += t.formations.Count(f => f.side == Side.Red && f.destroyed);
                if (t.winner == Side.Blue) blueWins++;
                string r = t.endReason.Contains("fallen") ? (t.endReason.StartsWith("NATO") ? "NATO HQ fell" : "Pact HQ fell") : t.endReason.Contains("no formations") ? "annihilated" : "time";
                reasons[r] = (reasons.TryGetValue(r, out int c0) ? c0 : 0) + 1;
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3) Console.WriteLine("seed " + seed + ": " + ex);
            }
        }
        int ok = Math.Max(1, n - errors);
        Console.WriteLine("campaigns " + n + ", errors " + errors + "  (NATO aggression " + BlueAggression + ", Pact " + RedAggression + ")");
        Console.WriteLine("NATO win rate " + (100.0 * blueWins / ok).ToString("0.0") + "%");
        Console.WriteLine("end reasons: " + string.Join(", ", reasons.Select(kv => kv.Key + " " + kv.Value)));
        Console.WriteLine("avg turns " + ((double)turns / ok).ToString("0.0") + ", battles/turn " + ((double)battles / Math.Max(1, turns)).ToString("0.00"));
        Console.WriteLine("avg cells changing hands " + ((double)flips / ok).ToString("0") + " of 800");
        Console.WriteLine("avg formations destroyed: NATO " + ((double)lostBlue / ok).ToString("0.0") + ", Pact " + ((double)lostRed / ok).ToString("0.0"));
        return errors == 0 ? 0 : 1;
    }

    private static void Validate(Theater t)
    {
        var seen = new HashSet<int>();
        foreach (Formation f in t.formations.Where(f => !f.destroyed))
        {
            if (f.strength <= 0f || f.strength > f.maxStrength + 0.01f) throw new Exception(f.name + " strength " + f.strength);
            if (t.Cell(f.cell).owner != f.side) throw new Exception(f.name + " sits on enemy land at " + t.Cell(f.cell).col + "," + t.Cell(f.cell).row);
            if (t.Cell(f.cell).terrain == TerrainKind.Water) throw new Exception(f.name + " in water");
            if (!seen.Add(f.cell)) throw new Exception("two formations in one cell");
        }
    }

    private static int Trace(int seed, string dir)
    {
        Directory.CreateDirectory(dir);
        Theater t = Generate(seed);
        var hooks = new AbstractForceHooks();
        Render(t, Path.Combine(dir, "turn00.png"), 1600);
        while (!t.finished)
        {
            int logStart = t.log.Count;
            PlayTurn(t, hooks);
            Validate(t);
            foreach (string l in t.log.Skip(logStart)) Console.WriteLine("  " + l);
            Console.WriteLine("turn " + t.turn + ": VP NATO " + t.blueVP + " Pact " + t.redVP + ", units " + t.Alive(Side.Blue).Count() + " v " + t.Alive(Side.Red).Count());
            Render(t, Path.Combine(dir, "turn" + t.turn.ToString("00") + ".png"), 1600);
        }
        Console.WriteLine("END: " + Rules.SideName(t.winner) + " - " + t.endReason);
        return 0;
    }

    // --- Picture: the painted land, plus owner tint, front line and unit blocks for checking ---

    private static void Render(Theater t, string path, int width)
    {
        int height = (int)(width / t.aspect);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        byte[] px = MapPainter.Paint(Terrain.Of(t), t, width, height);
        Console.WriteLine("painted " + width + "x" + height + " in " + watch.ElapsedMilliseconds + " ms");
        float cell = (float)width / t.cols;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Cell c = t.CellAt(Math.Min(t.cols - 1, (int)(x / cell)), Math.Min(t.rows - 1, (int)(y / cell)));
                int i = (y * width + x) * 4;
                if (c.owner == Side.Red)
                {
                    Tint(px, i, 200, 40, 40, 0.18f);
                }
                else
                {
                    Tint(px, i, 40, 90, 200, 0.12f);
                }
                // Front line: red border where ownership changes.
                float fx = x / cell - c.col, fy = y / cell - c.row;
                bool edge = (fx < 0.06f && c.col > 0 && t.CellAt(c.col - 1, c.row).owner != c.owner)
                    || (fy < 0.06f && c.row > 0 && t.CellAt(c.col, c.row - 1).owner != c.owner);
                if (edge) Tint(px, i, 220, 20, 20, 0.9f);
            }
        }
        foreach (Formation f in t.formations.Where(f => !f.destroyed))
        {
            Cell c = t.Cell(f.cell);
            int x0 = (int)((c.col + 0.15f) * cell), x1 = (int)((c.col + 0.85f) * cell);
            int y0 = (int)((c.row + 0.25f) * cell), y1 = (int)((c.row + 0.75f) * cell);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    bool border = x == x0 || y == y0 || x == x1 - 1 || y == y1 - 1;
                    bool bar = y >= y1 - 3 && (x - x0) < (x1 - x0) * f.strength01;
                    int i = (y * width + x) * 4;
                    if (border) Tint(px, i, 20, 20, 20, 1f);
                    else if (bar) Tint(px, i, 30, 30, 30, 1f);
                    else if (f.side == Side.Blue) Tint(px, i, 92, 160, 227, 1f);
                    else Tint(px, i, 214, 70, 60, 1f);
                }
            }
        }
        foreach (Town town in t.towns)
        {
            int cx = (int)(town.x / t.aspect * width), cy = (int)(town.y * height);
            int r = town.hq ? 6 : 3;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && y >= 0 && x < width && y < height) Tint(px, (y * width + x) * 4, town.hq ? 252 : 30, town.hq ? 179 : 30, town.hq ? 65 : 30, 1f);
        }
        WritePng(path, px, width, height);
    }

    private static void Tint(byte[] px, int i, int r, int g, int b, float a)
    {
        px[i] = (byte)(px[i] + (r - px[i]) * a);
        px[i + 1] = (byte)(px[i + 1] + (g - px[i + 1]) * a);
        px[i + 2] = (byte)(px[i + 2] + (b - px[i + 2]) * a);
    }

    private static void WritePng(string path, byte[] rgba, int w, int h)
    {
        using (var fs = File.Create(path))
        {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            BE(ihdr, 0, w);
            BE(ihdr, 4, h);
            ihdr[8] = 8;
            ihdr[9] = 6;
            Chunk(fs, "IHDR", ihdr);
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * (w * 4 + 1)] = 0;
                Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            }
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", new byte[0]);
        }
    }

    private static void BE(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BE(len, 0, data.Length);
        s.Write(len, 0, 4);
        byte[] td = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(td, 0, td.Length);
        var crc = new byte[4];
        BE(crc, 0, (int)Crc(td));
        s.Write(crc, 0, 4);
    }

    private static uint Crc(byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            c ^= b;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFF;
    }
}

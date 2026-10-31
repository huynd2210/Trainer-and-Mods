using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>
    /// Paints the theater as a topographic map: field patchwork, forests, lakes, hill shading,
    /// rivers, roads, built-up areas and the grid. Pure C# so the simulator can write the same
    /// picture to a PNG; the game uploads the pixels to a texture once per campaign.
    /// Pixels are RGBA, row 0 at the top.
    /// </summary>
    public static class MapPainter
    {
        private struct Rgb
        {
            public float r, g, b;

            public Rgb(float r, float g, float b)
            {
                this.r = r;
                this.g = g;
                this.b = b;
            }

            public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);

            public Rgb Scale(float k) => new Rgb(r * k, g * k, b * k);
        }

        private static readonly Rgb[] FieldTones =
        {
            new Rgb(204, 201, 163),
            new Rgb(190, 194, 148),
            new Rgb(212, 205, 172),
            new Rgb(181, 189, 141),
            new Rgb(199, 190, 152),
        };

        private static readonly Rgb Forest = new Rgb(92, 116, 74);
        private static readonly Rgb ForestDark = new Rgb(70, 94, 58);
        private static readonly Rgb Water = new Rgb(122, 162, 191);
        private static readonly Rgb Shore = new Rgb(98, 138, 170);
        private static readonly Rgb Urban = new Rgb(186, 176, 164);
        private static readonly Rgb Building = new Rgb(128, 114, 104);
        private static readonly Rgb River = new Rgb(98, 142, 186);
        private static readonly Rgb MotorwayCasing = new Rgb(104, 66, 34);
        private static readonly Rgb MotorwayFill = new Rgb(238, 168, 64);
        private static readonly Rgb RoadCasing = new Rgb(120, 110, 98);
        private static readonly Rgb RoadFill = new Rgb(246, 241, 226);
        private static readonly Rgb GridLine = new Rgb(40, 44, 36);

        public static byte[] Paint(Terrain terr, Theater t, int width, int height)
        {
            var px = new float[width * height * 3];
            float aspect = terr.aspect;
            float sx = aspect / width, sy = 1f / height;

            // Height once per pixel, for both water and hill shading.
            var h = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    h[y * width + x] = terr.Height((x + 0.5f) * sx, (y + 0.5f) * sy);
                }
            }

            float edge = 0.012f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float mx = (x + 0.5f) * sx, my = (y + 0.5f) * sy;

                    Rgb c = Field(terr, mx, my);

                    float fv = terr.ForestValue(mx, my);
                    float forest = Noise.SmoothStep(terr.forestLevel - edge, terr.forestLevel + edge, fv);
                    if (forest > 0f)
                    {
                        float tex = Noise.Fbm(mx * 90f, my * 90f, terr.seed + 501, 2);
                        Rgb fc = Rgb.Lerp(Forest, ForestDark, Noise.SmoothStep(0.35f, 0.75f, tex));
                        c = Rgb.Lerp(c, fc, forest);
                    }

                    // Hill shading from the height gradient, lit from the north-west.
                    int xl = Math.Max(0, x - 1), xr = Math.Min(width - 1, x + 1);
                    int yu = Math.Max(0, y - 1), yd = Math.Min(height - 1, y + 1);
                    float dx = (h[y * width + xr] - h[y * width + xl]) / ((xr - xl) * sx);
                    float dy = (h[yd * width + x] - h[yu * width + x]) / ((yd - yu) * sy);
                    float shade = -(dx + dy) * 0.055f;
                    c = c.Scale(1f + Math.Max(-0.22f, Math.Min(0.16f, shade)));

                    float wh = h[i];
                    float water = Noise.SmoothStep(terr.waterLevel + 0.004f, terr.waterLevel - 0.004f, wh);
                    if (water > 0f)
                    {
                        float shore = Noise.SmoothStep(terr.waterLevel - 0.004f, terr.waterLevel - 0.02f, wh);
                        c = Rgb.Lerp(c, Rgb.Lerp(Shore, Water, shore), water);
                    }

                    px[i * 3] = c.r;
                    px[i * 3 + 1] = c.g;
                    px[i * 3 + 2] = c.b;
                }
            }

            var canvas = new Canvas(px, width, height, width / aspect, t.rows);
            foreach (Town town in t.towns)
            {
                PaintTown(canvas, terr, town, t);
            }
            foreach (Line river in terr.rivers)
            {
                canvas.Stroke(river.points, river.major ? 0.085f : 0.065f, River, 1f);
            }
            foreach (Line road in terr.roads.Where(r => !r.major))
            {
                canvas.Stroke(road.points, 0.09f, RoadCasing, 1f);
            }
            foreach (Line road in terr.roads.Where(r => !r.major))
            {
                canvas.Stroke(road.points, 0.055f, RoadFill, 1f);
            }
            foreach (Line road in terr.roads.Where(r => r.major))
            {
                canvas.Stroke(road.points, 0.15f, MotorwayCasing, 1f);
                canvas.Stroke(road.points, 0.1f, MotorwayFill, 1f);
            }

            // Grid: thin dark lines on the cell borders.
            float cellPx = (float)width / t.cols;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float gx = x + 0.5f, gy = (y + 0.5f) * t.rows / (float)height * cellPx;
                    float dxl = Math.Abs(gx - (float)Math.Round(gx / cellPx) * cellPx);
                    float dyl = Math.Abs(gy - (float)Math.Round(gy / cellPx) * cellPx);
                    float a = Math.Max(0f, 1f - Math.Min(dxl, dyl) / 0.75f) * 0.22f;
                    if (a > 0f)
                    {
                        canvas.Blend(y * width + x, GridLine, a);
                    }
                }
            }

            var rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                rgba[i * 4] = Clamp(px[i * 3]);
                rgba[i * 4 + 1] = Clamp(px[i * 3 + 1]);
                rgba[i * 4 + 2] = Clamp(px[i * 3 + 2]);
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }

        // Fields: an irregular patchwork. Each parcel is the region nearest one jittered point
        // (stretched so parcels run long and thin, in a direction that drifts across the map),
        // painted one flat tone, with a faint seam where two parcels meet.
        private static Rgb Field(Terrain terr, float x, float y)
        {
            // A fixed direction per district; turning the plane continuously would make whorls.
            float ang = (int)(Noise.Value(x * 2.5f, y * 2.5f, terr.seed + 227) * 4f) * 0.785f + 0.3f;
            float ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang);
            float u = (x * ca - y * sa) * 30f, v = (x * sa + y * ca) * 55f;
            int iu = (int)Math.Floor(u), iv = (int)Math.Floor(v);
            float d1 = float.MaxValue, d2 = float.MaxValue;
            int bu = 0, bv = 0;
            for (int du = -1; du <= 1; du++)
            {
                for (int dv = -1; dv <= 1; dv++)
                {
                    int cu = iu + du, cv = iv + dv;
                    float pu = cu + Hash(cu, cv, terr.seed + 1), pv = cv + Hash(cu, cv, terr.seed + 2);
                    float d = (pu - u) * (pu - u) + (pv - v) * (pv - v);
                    if (d < d1)
                    {
                        d2 = d1;
                        d1 = d;
                        bu = cu;
                        bv = cv;
                    }
                    else if (d < d2)
                    {
                        d2 = d;
                    }
                }
            }
            float k = Hash(bu, bv, terr.seed + 3);
            Rgb tone = FieldTones[Math.Min(FieldTones.Length - 1, (int)(k * FieldTones.Length))];
            float seam = (float)Math.Sqrt(d2) - (float)Math.Sqrt(d1);
            return tone.Scale(seam < 0.06f ? 0.94f : 1f);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
        private static void PaintTown(Canvas canvas, Terrain terr, Town town, Theater t)
        {
            var rng = new Random(terr.seed * 31 + town.id);
            float cell = 1f / t.rows;
            float radius = (0.3f + 0.22f * town.size) * cell;
            // Built-up area: a soft blob, then blocks of buildings within it.
            int blocks = 26 + 40 * town.size + 30 * town.size * town.size;
            canvas.Disc(town.x, town.y, radius * 1.2f, Urban, 0.9f);
            for (int i = 0; i < blocks; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2, d = Math.Sqrt(rng.NextDouble()) * radius;
                float bx = town.x + (float)(Math.Cos(a) * d), by = town.y + (float)(Math.Sin(a) * d);
                float bw = cell * (0.04f + 0.05f * (float)rng.NextDouble());
                float bh = cell * (0.035f + 0.04f * (float)rng.NextDouble());
                canvas.Rect(bx, by, bw, bh, Building, 0.9f);
            }
        }

        private static byte Clamp(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v + 0.5f)));

        private class Canvas
        {
            private readonly float[] px;
            private readonly int w;
            private readonly int h;
            // Pixels per map unit.
            private readonly float scale;
            private readonly float[] coverage;
            private readonly float cellPx;

            public Canvas(float[] px, int w, int h, float scale, int rows)
            {
                cellPx = (float)h / rows;
                this.px = px;
                this.w = w;
                this.h = h;
                this.scale = scale;
                coverage = new float[w * h];
            }

            public void Blend(int i, Rgb c, float a)
            {
                px[i * 3] += (c.r - px[i * 3]) * a;
                px[i * 3 + 1] += (c.g - px[i * 3 + 1]) * a;
                px[i * 3 + 2] += (c.b - px[i * 3 + 2]) * a;
            }

            // Width is in cells (1/rows of the map height), so lines keep their weight at any size.
            public void Stroke(List<P2> pts, float widthCells, Rgb c, float alpha)
            {
                float halfPx = Math.Max(0.6f, widthCells * cellPx * 0.5f);
                int x0 = w, y0 = h, x1 = 0, y1 = 0;
                for (int s = 0; s + 1 < pts.Count; s++)
                {
                    float ax = pts[s].x * scale, ay = pts[s].y * scale, bx = pts[s + 1].x * scale, by = pts[s + 1].y * scale;
                    int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - halfPx - 1));
                    int maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(ax, bx) + halfPx + 1));
                    int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - halfPx - 1));
                    int maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(ay, by) + halfPx + 1));
                    x0 = Math.Min(x0, minX);
                    y0 = Math.Min(y0, minY);
                    x1 = Math.Max(x1, maxX);
                    y1 = Math.Max(y1, maxY);
                    float vx = bx - ax, vy = by - ay;
                    float len2 = Math.Max(1e-6f, vx * vx + vy * vy);
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            float qx = x + 0.5f - ax, qy = y + 0.5f - ay;
                            float tt = Math.Max(0f, Math.Min(1f, (qx * vx + qy * vy) / len2));
                            float ex = qx - vx * tt, ey = qy - vy * tt;
                            float d = (float)Math.Sqrt(ex * ex + ey * ey);
                            float cov = Math.Max(0f, Math.Min(1f, halfPx - d + 0.5f));
                            int i = y * w + x;
                            if (cov > coverage[i])
                            {
                                coverage[i] = cov;
                            }
                        }
                    }
                }
                // One blend per pixel per line, so joints between segments don't darken.
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = y * w + x;
                        if (coverage[i] > 0f)
                        {
                            Blend(i, c, coverage[i] * alpha);
                            coverage[i] = 0f;
                        }
                    }
                }
            }

            public void Disc(float cx, float cy, float r, Rgb c, float alpha)
            {
                float pcx = cx * scale, pcy = cy * scale, pr = r * scale;
                for (int y = Math.Max(0, (int)(pcy - pr - 2)); y <= Math.Min(h - 1, (int)(pcy + pr + 2)); y++)
                {
                    for (int x = Math.Max(0, (int)(pcx - pr - 2)); x <= Math.Min(w - 1, (int)(pcx + pr + 2)); x++)
                    {
                        float d = (float)Math.Sqrt((x + 0.5f - pcx) * (x + 0.5f - pcx) + (y + 0.5f - pcy) * (y + 0.5f - pcy));
                        float a = Noise.SmoothStep(pr, pr * 0.7f, d);
                        if (a > 0f)
                        {
                            Blend(y * w + x, c, a * alpha);
                        }
                    }
                }
            }

            public void Rect(float cx, float cy, float rw, float rh, Rgb c, float alpha)
            {
                float x0 = (cx - rw / 2) * scale, x1 = (cx + rw / 2) * scale, y0 = (cy - rh / 2) * scale, y1 = (cy + rh / 2) * scale;
                for (int y = Math.Max(0, (int)Math.Floor(y0)); y <= Math.Min(h - 1, (int)Math.Ceiling(y1)); y++)
                {
                    for (int x = Math.Max(0, (int)Math.Floor(x0)); x <= Math.Min(w - 1, (int)Math.Ceiling(x1)); x++)
                    {
                        float cov = Math.Max(0f, Math.Min(1f, Math.Min(x + 1 - x0, x1 - x))) * Math.Max(0f, Math.Min(1f, Math.Min(y + 1 - y0, y1 - y)));
                        if (cov > 0f)
                        {
                            Blend(y * w + x, c, cov * alpha);
                        }
                    }
                }
            }

        }
    }
}

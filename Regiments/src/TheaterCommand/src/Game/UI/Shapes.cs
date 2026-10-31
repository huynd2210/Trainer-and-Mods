using System;
using System.Collections.Generic;
using TheaterCommand.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TheaterCommand.Game.UI
{
    /// <summary>A filled convex polygon; clicks only count inside it.</summary>
    public class PolyGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        public List<Vector2> points = new List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 3)
            {
                return;
            }
            Color32 c = color;
            foreach (Vector2 p in points)
            {
                vh.AddVert(p, c, Vector2.zero);
            }
            for (int i = 1; i < points.Count - 1; i++)
            {
                vh.AddTriangle(0, i, i + 1);
            }
        }

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 local);
            return Contains(points, local);
        }

        public static bool Contains(List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y)
                    && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }

    /// <summary>Straight line segments of one width; arrowheads optional. Never catches clicks.</summary>
    public class LineGraphic : MaskableGraphic
    {
        public struct Segment
        {
            public Vector2 a;
            public Vector2 b;
            public bool arrow;
        }

        public List<Segment> segments = new List<Segment>();
        public float width = 3f;
        public float headSize = 22f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color32 c = color;
            foreach (Segment s in segments)
            {
                Vector2 dir = s.b - s.a;
                float len = dir.magnitude;
                if (len < 0.01f)
                {
                    continue;
                }
                dir /= len;
                Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
                Vector2 end = s.arrow ? s.b - dir * headSize * 0.8f : s.b;
                Quad(vh, s.a + n, end + n, end - n, s.a - n, c);
                if (s.arrow)
                {
                    Vector2 hn = new Vector2(-dir.y, dir.x) * (headSize * 0.6f);
                    int i = vh.currentVertCount;
                    vh.AddVert(s.b, c, Vector2.zero);
                    vh.AddVert(s.b - dir * headSize + hn, c, Vector2.zero);
                    vh.AddVert(s.b - dir * headSize - hn, c, Vector2.zero);
                    vh.AddTriangle(i, i + 1, i + 2);
                }
            }
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c2, Vector2 d, Color32 c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, c, Vector2.zero);
            vh.AddVert(b, c, Vector2.zero);
            vh.AddVert(c2, c, Vector2.zero);
            vh.AddVert(d, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>
    /// A NATO unit symbol: a friendly rectangle or hostile diamond in the side's colour, with the
    /// branch drawn inside in ink. Branches are a table of drawing steps, one entry per kind.
    /// </summary>
    public class CounterGraphic : MaskableGraphic
    {
        public bool hostile;
        public UnitKind kind;
        public Color fill = Color.white;
        public Color ink = Color.black;
        public Color frame = Color.black;
        public float frameWidth = 2f;

        // Each draws inside the symbol's inner box (centre, half-size), in ink.
        private static readonly Dictionary<UnitKind, Action<CounterGraphic, VertexHelper, Vector2, Vector2>> Branch =
            new Dictionary<UnitKind, Action<CounterGraphic, VertexHelper, Vector2, Vector2>>
            {
                { UnitKind.Infantry, (g, vh, c, h) => g.Cross(vh, c, h) },
                { UnitKind.Armour, (g, vh, c, h) => g.Ellipse(vh, c, new Vector2(h.x * 0.62f, h.y * 0.5f), 0f, 360f) },
                { UnitKind.Mechanized, (g, vh, c, h) => { g.Cross(vh, c, h); g.Ellipse(vh, c, new Vector2(h.x * 0.62f, h.y * 0.5f), 0f, 360f); } },
                { UnitKind.Recon, (g, vh, c, h) => g.Line(vh, c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, h.y), g.frameWidth) },
                { UnitKind.Airborne, (g, vh, c, h) => { g.Cross(vh, c, h); g.Ellipse(vh, c + new Vector2(0f, -h.y * 0.62f), new Vector2(h.x * 0.3f, h.y * 0.26f), -180f, 0f); } },
            };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            if (hostile)
            {
                float s = Mathf.Min(r.width, r.height) * 0.5f;
                Vector2[] d = { c + new Vector2(0f, s), c + new Vector2(s, 0f), c + new Vector2(0f, -s), c + new Vector2(-s, 0f) };
                Poly(vh, d, fill);
                Outline(vh, d);
                Branch[kind](this, vh, c, new Vector2(s * 0.56f, s * 0.56f));
            }
            else
            {
                Vector2 h = r.size * 0.5f;
                Vector2[] q = { c + new Vector2(-h.x, h.y), c + new Vector2(h.x, h.y), c + new Vector2(h.x, -h.y), c + new Vector2(-h.x, -h.y) };
                Poly(vh, q, fill);
                Outline(vh, q);
                Branch[kind](this, vh, c, h - new Vector2(frameWidth, frameWidth) * 0.5f);
            }
        }

        private void Cross(VertexHelper vh, Vector2 c, Vector2 h)
        {
            Line(vh, c + new Vector2(-h.x, h.y), c + new Vector2(h.x, -h.y), frameWidth);
            Line(vh, c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, h.y), frameWidth);
        }

        private void Ellipse(VertexHelper vh, Vector2 c, Vector2 radius, float fromDeg, float toDeg)
        {
            const int steps = 28;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Deg2Rad * (180f + fromDeg + (toDeg - fromDeg) * i / steps);
                Vector2 p = c + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
                if (i > 0)
                {
                    Line(vh, prev, p, frameWidth);
                }
                prev = p;
            }
        }

        private void Outline(VertexHelper vh, Vector2[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                Vector2 dir = (b - a).normalized * (frameWidth * 0.5f);
                Line(vh, a - dir, b + dir, frameWidth, frame);
            }
        }

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float w) => Line(vh, a, b, w, ink);

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color col)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            Poly(vh, new[] { a + n, b + n, b - n, a - n }, col);
        }

        private void Poly(VertexHelper vh, Vector2[] pts, Color col)
        {
            Color32 c32 = col * color;
            int i0 = vh.currentVertCount;
            foreach (Vector2 p in pts)
            {
                vh.AddVert(p, c32, Vector2.zero);
            }
            for (int i = 1; i < pts.Length - 1; i++)
            {
                vh.AddTriangle(i0, i0 + i, i0 + i + 1);
            }
        }
    }}

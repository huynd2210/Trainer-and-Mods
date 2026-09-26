using System;
using System.Collections.Generic;
using UnityEngine;

namespace LonestarTracker
{
    /// <summary>Styles and metrics for one frame, sized to the screen and the text-size setting.</summary>
    internal sealed class UiSkin
    {
        public GUIStyle Text;
        public GUIStyle TextRight;
        public GUIStyle Title;
        public float Scale;
        public float Line;
        public float Pad;
        public float RowHeight;

        private static Texture2D _white;

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        public void Refresh(float textScalePercent)
        {
            Scale = Mathf.Max(1f, Screen.height / 1080f) * Mathf.Clamp(textScalePercent, 70f, 160f) / 100f;

            if (Text == null)
            {
                Text = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false, alignment = TextAnchor.MiddleLeft };
                Text.normal.textColor = Color.white;
                Text.padding = new RectOffset(0, 0, 0, 0);

                TextRight = new GUIStyle(Text) { alignment = TextAnchor.MiddleRight };

                Title = new GUIStyle(Text) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            }

            Text.fontSize = Mathf.RoundToInt(14f * Scale);
            TextRight.fontSize = Text.fontSize;
            Title.fontSize = Mathf.RoundToInt(17f * Scale);

            Line = Text.lineHeight;
            Pad = Mathf.Round(12f * Scale);
            RowHeight = Mathf.Round(Line * 1.55f);
        }
    }

    internal static class Ui
    {
        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, UiSkin.White);
            GUI.color = previous;
        }

        public static void Label(Rect rect, string text, Color color, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        /// <summary>
        /// Shortens text with an ellipsis until it fits. Binary search, because this runs
        /// for every cell of every visible row on every frame.
        /// </summary>
        public static string Clip(string text, float width, UiSkin skin)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (skin.Text.CalcSize(new GUIContent(text)).x <= width) return text;

            int low = 0;
            int high = text.Length - 1;
            int best = 0;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                string candidate = text.Substring(0, mid) + "…";
                if (skin.Text.CalcSize(new GUIContent(candidate)).x <= width) { best = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return best == 0 ? "…" : text.Substring(0, best) + "…";
        }

        /// <summary>Draws left-to-right segments on one line and returns where it ended.</summary>
        public static float Run(Rect rect, UiSkin skin, IList<string> parts, IList<Color> colors, float gap)
        {
            float x = rect.x;
            for (int i = 0; i < parts.Count; i++)
            {
                float w = skin.Text.CalcSize(new GUIContent(parts[i])).x;
                Label(new Rect(x, rect.y, w, rect.height), parts[i], colors[i], skin.Text);
                x += w + gap;
            }
            return x - gap;
        }
    }

    /// <summary>One column of a table: its heading, its share of the width and its cell.</summary>
    internal sealed class TableColumn<T>
    {
        public readonly string Header;
        public readonly float Weight;
        public readonly bool RightAligned;
        public readonly Func<T, string> Cell;
        public readonly Func<T, Color> Tint;

        public TableColumn(string header, float weight, Func<T, string> cell, Func<T, Color> tint = null, bool rightAligned = false)
        {
            Header = header;
            Weight = weight;
            Cell = cell;
            Tint = tint;
            RightAligned = rightAligned;
        }
    }

    /// <summary>
    /// The one table renderer every list view uses, so the kill log, the voyage log and
    /// the current voyage all line up and colour the same way.
    /// </summary>
    internal static class Table
    {
        public static int RowsThatFit(Rect area, UiSkin skin)
        {
            float body = area.height - skin.RowHeight - skin.Pad * 0.5f;
            return Mathf.Max(1, Mathf.FloorToInt(body / skin.RowHeight));
        }

        public static void Draw<T>(Rect area, UiSkin skin, IList<TableColumn<T>> columns, IList<T> rows, int scroll)
        {
            float totalWeight = 0f;
            for (int i = 0; i < columns.Count; i++) totalWeight += columns[i].Weight;

            float gap = Mathf.Round(10f * skin.Scale);
            float usable = area.width - gap * (columns.Count - 1);

            float[] widths = new float[columns.Count];
            for (int i = 0; i < columns.Count; i++) widths[i] = usable * columns[i].Weight / totalWeight;

            // Header
            float y = area.y;
            float x = area.x;
            for (int i = 0; i < columns.Count; i++)
            {
                Rect cell = new Rect(x, y, widths[i], skin.RowHeight);
                Ui.Label(cell, columns[i].Header, Palette.Muted,
                    columns[i].RightAligned ? skin.TextRight : skin.Text);
                x += widths[i] + gap;
            }
            y += skin.RowHeight;
            Ui.Fill(new Rect(area.x, y - 1f, area.width, 1f), Palette.Rule);
            y += skin.Pad * 0.5f;

            int fits = RowsThatFit(area, skin);
            for (int r = 0; r < fits; r++)
            {
                int index = scroll + r;
                if (index < 0 || index >= rows.Count) break;

                T row = rows[index];
                Rect line = new Rect(area.x, y, area.width, skin.RowHeight);
                if ((index & 1) == 1) Ui.Fill(line, Palette.RowOdd);

                x = area.x;
                for (int i = 0; i < columns.Count; i++)
                {
                    TableColumn<T> column = columns[i];
                    Rect cell = new Rect(x, y, widths[i], skin.RowHeight);
                    Color tint = column.Tint != null ? column.Tint(row) : Palette.Text;
                    Ui.Label(cell, Ui.Clip(column.Cell(row), cell.width, skin), tint,
                        column.RightAligned ? skin.TextRight : skin.Text);
                    x += widths[i] + gap;
                }
                y += skin.RowHeight;
            }

            if (rows.Count == 0)
            {
                Ui.Label(new Rect(area.x, y, area.width, skin.RowHeight),
                    "Nothing logged yet.", Palette.Muted, skin.Text);
            }
        }
    }
}

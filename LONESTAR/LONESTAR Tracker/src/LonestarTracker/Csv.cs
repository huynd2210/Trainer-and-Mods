using System.Collections.Generic;
using System.Text;

namespace LonestarTracker
{
    /// <summary>
    /// RFC 4180 CSV. The logs are append-only files the player is expected to open in
    /// a spreadsheet, so quoting has to be correct for names that contain commas or
    /// quotes - and the parser has to read back exactly what the writer produced.
    /// </summary>
    internal static class Csv
    {
        public static string Escape(string field)
        {
            if (field == null) return "";
            bool needsQuotes = false;
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (c == ',' || c == '"' || c == '\n' || c == '\r') { needsQuotes = true; break; }
            }
            if (!needsQuotes) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        public static string Line(IList<string> fields)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(fields[i]));
            }
            return sb.ToString();
        }

        /// <summary>Parses a whole CSV document into rows of fields.</summary>
        public static List<string[]> Parse(string text)
        {
            List<string[]> rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;

            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            bool rowHasContent = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                    continue;
                }

                if (c == '"') { inQuotes = true; rowHasContent = true; }
                else if (c == ',') { row.Add(field.ToString()); field.Length = 0; rowHasContent = true; }
                else if (c == '\r') { /* handled by the \n that follows, or ignored */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                    if (rowHasContent || row.Count > 1) rows.Add(row.ToArray());
                    row.Clear();
                    rowHasContent = false;
                }
                else { field.Append(c); rowHasContent = true; }
            }

            if (rowHasContent || field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (rowHasContent || row.Count > 1) rows.Add(row.ToArray());
            }

            return rows;
        }
    }
}

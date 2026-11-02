using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DD2.KillTracker.Core
{
	/// <summary>
	/// Minimal JSON reader/writer, no dependencies, so the ledger can be tested without Unity or the game's
	/// Newtonsoft. Values map to: object -> Dictionary&lt;string,object&gt;, array -> List&lt;object&gt;, string,
	/// bool, null, integers -> long, other numbers -> double.
	/// </summary>
	internal static class Json
	{
		// ------------------------------------------------------------------ writing

		public static string Write(object value, bool indent = false)
		{
			var sb = new StringBuilder();
			WriteValue(sb, value, indent, 0);
			return sb.ToString();
		}

		private static void WriteValue(StringBuilder sb, object v, bool indent, int depth)
		{
			if (v == null) { sb.Append("null"); return; }
			if (v is string s) { WriteString(sb, s); return; }
			if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
			if (v is int i) { sb.Append(i.ToString(CultureInfo.InvariantCulture)); return; }
			if (v is long l) { sb.Append(l.ToString(CultureInfo.InvariantCulture)); return; }
			if (v is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
			if (v is IDictionary<string, object> obj) { WriteObject(sb, obj, indent, depth); return; }
			if (v is IList<object> arr) { WriteArray(sb, arr, indent, depth); return; }
			throw new ArgumentException("Unsupported JSON value type: " + v.GetType().FullName);
		}

		private static void WriteObject(StringBuilder sb, IDictionary<string, object> obj, bool indent, int depth)
		{
			if (obj.Count == 0) { sb.Append("{}"); return; }
			sb.Append('{');
			bool first = true;
			foreach (KeyValuePair<string, object> kv in obj)
			{
				if (!first) sb.Append(',');
				first = false;
				NewLine(sb, indent, depth + 1);
				WriteString(sb, kv.Key);
				sb.Append(indent ? ": " : ":");
				WriteValue(sb, kv.Value, indent, depth + 1);
			}
			NewLine(sb, indent, depth);
			sb.Append('}');
		}

		private static void WriteArray(StringBuilder sb, IList<object> arr, bool indent, int depth)
		{
			if (arr.Count == 0) { sb.Append("[]"); return; }
			sb.Append('[');
			for (int i = 0; i < arr.Count; i++)
			{
				if (i > 0) sb.Append(',');
				NewLine(sb, indent, depth + 1);
				WriteValue(sb, arr[i], indent, depth + 1);
			}
			NewLine(sb, indent, depth);
			sb.Append(']');
		}

		private static void NewLine(StringBuilder sb, bool indent, int depth)
		{
			if (!indent) return;
			sb.Append('\n');
			sb.Append('\t', depth);
		}

		private static void WriteString(StringBuilder sb, string s)
		{
			sb.Append('"');
			foreach (char c in s)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
						else sb.Append(c);
						break;
				}
			}
			sb.Append('"');
		}

		// ------------------------------------------------------------------ reading

		/// <summary>Parses a complete JSON document. Returns false (never throws) on any malformed input.</summary>
		public static bool TryParse(string text, out object value)
		{
			value = null;
			if (string.IsNullOrWhiteSpace(text)) return false;
			try
			{
				var p = new Parser(text);
				value = p.ParseValue(0);
				p.SkipWs();
				return p.AtEnd;
			}
			catch (FormatException)
			{
				value = null;
				return false;
			}
		}

		private sealed class Parser
		{
			private readonly string _t;
			private int _i;
			private const int MaxDepth = 64;

			public Parser(string t) { _t = t; }

			public bool AtEnd => _i >= _t.Length;

			public void SkipWs()
			{
				while (_i < _t.Length && (_t[_i] == ' ' || _t[_i] == '\t' || _t[_i] == '\r' || _t[_i] == '\n')) _i++;
			}

			public object ParseValue(int depth)
			{
				if (depth > MaxDepth) throw new FormatException("too deep");
				SkipWs();
				if (_i >= _t.Length) throw new FormatException("eof");
				char c = _t[_i];
				if (c == '{') return ParseObject(depth);
				if (c == '[') return ParseArray(depth);
				if (c == '"') return ParseString();
				if (Match("true")) return true;
				if (Match("false")) return false;
				if (Match("null")) return null;
				return ParseNumber();
			}

			private bool Match(string word)
			{
				if (string.CompareOrdinal(_t, _i, word, 0, word.Length) != 0) return false;
				_i += word.Length;
				return true;
			}

			private Dictionary<string, object> ParseObject(int depth)
			{
				_i++;
				var obj = new Dictionary<string, object>();
				SkipWs();
				if (_i < _t.Length && _t[_i] == '}') { _i++; return obj; }
				while (true)
				{
					SkipWs();
					if (_i >= _t.Length || _t[_i] != '"') throw new FormatException("key");
					string key = ParseString();
					SkipWs();
					if (_i >= _t.Length || _t[_i] != ':') throw new FormatException(":");
					_i++;
					obj[key] = ParseValue(depth + 1);
					SkipWs();
					if (_i >= _t.Length) throw new FormatException("eof");
					if (_t[_i] == ',') { _i++; continue; }
					if (_t[_i] == '}') { _i++; return obj; }
					throw new FormatException("object");
				}
			}

			private List<object> ParseArray(int depth)
			{
				_i++;
				var arr = new List<object>();
				SkipWs();
				if (_i < _t.Length && _t[_i] == ']') { _i++; return arr; }
				while (true)
				{
					arr.Add(ParseValue(depth + 1));
					SkipWs();
					if (_i >= _t.Length) throw new FormatException("eof");
					if (_t[_i] == ',') { _i++; continue; }
					if (_t[_i] == ']') { _i++; return arr; }
					throw new FormatException("array");
				}
			}

			private string ParseString()
			{
				_i++;
				var sb = new StringBuilder();
				while (_i < _t.Length)
				{
					char c = _t[_i++];
					if (c == '"') return sb.ToString();
					if (c != '\\') { sb.Append(c); continue; }
					if (_i >= _t.Length) break;
					char e = _t[_i++];
					switch (e)
					{
						case '"': sb.Append('"'); break;
						case '\\': sb.Append('\\'); break;
						case '/': sb.Append('/'); break;
						case 'n': sb.Append('\n'); break;
						case 'r': sb.Append('\r'); break;
						case 't': sb.Append('\t'); break;
						case 'b': sb.Append('\b'); break;
						case 'f': sb.Append('\f'); break;
						case 'u':
							if (_i + 4 > _t.Length) throw new FormatException("\\u");
							sb.Append((char)int.Parse(_t.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
							_i += 4;
							break;
						default: throw new FormatException("escape");
					}
				}
				throw new FormatException("unterminated string");
			}

			private object ParseNumber()
			{
				int start = _i;
				bool isFloat = false;
				while (_i < _t.Length)
				{
					char c = _t[_i];
					if ((c >= '0' && c <= '9') || c == '-' || c == '+') { _i++; continue; }
					if (c == '.' || c == 'e' || c == 'E') { isFloat = true; _i++; continue; }
					break;
				}
				if (_i == start) throw new FormatException("value");
				string s = _t.Substring(start, _i - start);
				if (!isFloat && long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
				if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
				throw new FormatException("number");
			}
		}

		// ------------------------------------------------------------------ typed accessors

		public static string Str(IDictionary<string, object> o, string key) =>
			o != null && o.TryGetValue(key, out object v) ? v as string : null;

		public static long Long(IDictionary<string, object> o, string key, long fallback = 0)
		{
			if (o == null || !o.TryGetValue(key, out object v)) return fallback;
			if (v is long l) return l;
			if (v is double d) return (long)d;
			return fallback;
		}

		public static bool Bool(IDictionary<string, object> o, string key, bool fallback = false) =>
			o != null && o.TryGetValue(key, out object v) && v is bool b ? b : fallback;

		public static Dictionary<string, object> Obj(IDictionary<string, object> o, string key) =>
			o != null && o.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;

		public static List<object> Arr(IDictionary<string, object> o, string key) =>
			o != null && o.TryGetValue(key, out object v) ? v as List<object> : null;
	}
}

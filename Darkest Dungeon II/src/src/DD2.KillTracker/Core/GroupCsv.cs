using System;
using System.Collections.Generic;
using System.IO;

namespace DD2.KillTracker.Core
{
	/// <summary>
	/// Reads the element headers of DD2 data files the same way the game does, so we learn which ids a folder
	/// defines without depending on the game's private structures.
	/// Mirrors <c>ResourceGroupCsvDatabase.GatherResourcesFromFolder</c> (decomp: Assets.Code.Resource.ResourceGroupCsvDatabase).
	/// </summary>
	internal static class GroupCsv
	{
		private const string ElementStart = "element_start";
		private const string ElementEnd = "element_end";

		/// <summary>
		/// Calls <paramref name="onElement"/>(type, name) for every <c>element_start,name,type</c> block in the text.
		/// Same scan as the game: find "element_start", then the next "element_end", split the header line on commas
		/// dropping empty entries, take entry 1 as the name and entry 2 as the type.
		/// </summary>
		public static void ScanText(string text, Action<string, string> onElement)
		{
			if (string.IsNullOrEmpty(text)) return;
			int pos = 0;
			while (true)
			{
				int start = text.IndexOf(ElementStart, pos, StringComparison.Ordinal);
				if (start < 0) break;
				int end = text.IndexOf(ElementEnd, start, StringComparison.Ordinal);
				if (end <= start) break;

				int eol = text.IndexOf('\n', start);
				string header = eol < 0 || eol > end ? text.Substring(start, end - start) : text.Substring(start, eol - start);
				string[] parts = header.TrimEnd('\r').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 3)
				{
					onElement(parts[2].Trim(), parts[1].Trim());
				}
				pos = end;
			}
		}

		/// <summary>Scans every <c>*.Group.csv</c> directly inside the folder (the game does not recurse).</summary>
		public static void ScanFolder(string folder, Action<string, string> onElement)
		{
			if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
			foreach (string file in Directory.GetFiles(folder, "*.Group.csv", SearchOption.TopDirectoryOnly))
			{
				ScanText(File.ReadAllText(file), onElement);
			}
		}

		/// <summary>
		/// The single-type files <c>name.Type.csv</c> that <c>ResourceDatabaseText.AddResourcesFromFolder</c> picks up:
		/// the element name is the file name up to its first dot.
		/// </summary>
		public static void ScanSingleTypeFiles(string folder, string fileType, string extension, Action<string, string> onElement)
		{
			if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
			foreach (string file in Directory.GetFiles(folder, "*." + fileType + "." + extension, SearchOption.TopDirectoryOnly))
			{
				string name = Path.GetFileName(file);
				int dot = name.IndexOf('.');
				if (dot > 0) onElement(fileType, name.Substring(0, dot));
			}
		}
	}

	/// <summary>A keyboard shortcut written as text, e.g. "F11" or "Ctrl+Shift+K". Parsed here, mapped to Unity keys elsewhere.</summary>
	internal sealed class KeyChord
	{
		public string Key;
		public bool Ctrl;
		public bool Shift;
		public bool Alt;

		public static KeyChord Parse(string text, string fallbackKey)
		{
			var chord = new KeyChord { Key = fallbackKey };
			if (string.IsNullOrWhiteSpace(text)) return chord;

			var parsed = new KeyChord();
			foreach (string raw in text.Split('+'))
			{
				string token = raw.Trim();
				if (token.Length == 0) continue;
				if (Modifiers.TryGetValue(token, out Action<KeyChord> apply)) apply(parsed);
				else parsed.Key = token;
			}
			return string.IsNullOrEmpty(parsed.Key) ? chord : parsed;
		}

		private static readonly Dictionary<string, Action<KeyChord>> Modifiers =
			new Dictionary<string, Action<KeyChord>>(StringComparer.OrdinalIgnoreCase)
			{
				["ctrl"] = c => c.Ctrl = true,
				["control"] = c => c.Ctrl = true,
				["shift"] = c => c.Shift = true,
				["alt"] = c => c.Alt = true,
			};

		public override string ToString() => (Ctrl ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + Key;
	}
}

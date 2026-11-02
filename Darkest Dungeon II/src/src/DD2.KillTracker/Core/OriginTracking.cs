using System;
using System.Collections.Generic;

namespace DD2.KillTracker.Core
{
	/// <summary>One mod the game knows about: where it lives and what to call it.</summary>
	internal sealed class ModEntry
	{
		public string Path;
		public string Title;
		/// <summary>Steam Workshop id; 0 / null for local mods.</summary>
		public string WorkshopId;
	}

	/// <summary>One official DLC data folder, e.g. folder name "dlc_catacombs" shown as "Catacombs".</summary>
	internal sealed class DlcEntry
	{
		public string FolderName;
		public string Title;
	}

	/// <summary>
	/// Maps a folder the game loads data from to the source that owns it. Pure string logic over the lists the
	/// game bridge hands in, so it is unit-testable. Mod folders win over base folders when both match.
	/// </summary>
	internal sealed class OriginClassifier
	{
		private readonly List<ModEntry> _mods = new List<ModEntry>();
		private readonly List<string> _baseRoots = new List<string>();
		private readonly Dictionary<string, DlcEntry> _dlcs = new Dictionary<string, DlcEntry>(StringComparer.OrdinalIgnoreCase);

		public void SetMods(IEnumerable<ModEntry> mods)
		{
			_mods.Clear();
			if (mods != null) _mods.AddRange(mods);
		}

		public void SetBaseRoots(IEnumerable<string> roots)
		{
			_baseRoots.Clear();
			if (roots == null) return;
			foreach (string r in roots)
			{
				if (!string.IsNullOrEmpty(r)) _baseRoots.Add(Normalize(r));
			}
		}

		public void SetDlcs(IEnumerable<DlcEntry> dlcs)
		{
			_dlcs.Clear();
			if (dlcs == null) return;
			foreach (DlcEntry d in dlcs)
			{
				if (d != null && !string.IsNullOrEmpty(d.FolderName)) _dlcs[d.FolderName] = d;
			}
		}

		public static string Normalize(string path)
		{
			if (string.IsNullOrEmpty(path)) return string.Empty;
			string p = path.Replace('\\', '/');
			while (p.Length > 1 && p.EndsWith("/", StringComparison.Ordinal)) p = p.Substring(0, p.Length - 1);
			return p;
		}

		/// <summary>True when <paramref name="path"/> is <paramref name="root"/> or lies below it (case-insensitive).</summary>
		public static bool IsUnder(string path, string root)
		{
			if (path.Length < root.Length) return false;
			if (string.Compare(path, 0, root, 0, root.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
			return path.Length == root.Length || path[root.Length] == '/';
		}

		public static string Leaf(string path)
		{
			string p = Normalize(path);
			int i = p.LastIndexOf('/');
			return i >= 0 ? p.Substring(i + 1) : p;
		}

		public SourceInfo Classify(string folder)
		{
			string f = Normalize(folder);
			if (f.Length == 0) return SourceInfo.Unknown;

			foreach (ModEntry m in _mods)
			{
				string mp = Normalize(m.Path);
				if (mp.Length > 0 && IsUnder(f, mp))
				{
					// A local mod without a manifest title is named "mods" by the game (the shared parent folder's name):
					// its own folder name is the better label.
					bool usable = !string.IsNullOrWhiteSpace(m.Title) && !string.Equals(m.Title.Trim(), "mods", StringComparison.OrdinalIgnoreCase);
					string title = usable ? m.Title : Leaf(mp);
					string id = string.IsNullOrEmpty(m.WorkshopId) || m.WorkshopId == "0" ? null : m.WorkshopId;
					return new SourceInfo(SourceKind.Mod, title, id, Leaf(mp));
				}
			}

			foreach (string root in _baseRoots)
			{
				if (!IsUnder(f, root)) continue;
				if (f.Length > root.Length)
				{
					string first = f.Substring(root.Length + 1);
					int slash = first.IndexOf('/');
					if (slash >= 0) first = first.Substring(0, slash);
					if (_dlcs.TryGetValue(first, out DlcEntry dlc))
					{
						return new SourceInfo(SourceKind.Dlc, string.IsNullOrWhiteSpace(dlc.Title) ? PrettyDlcName(dlc.FolderName) : dlc.Title, null, dlc.FolderName);
					}
				}
				return SourceInfo.BaseGame;
			}

			return SourceInfo.Unknown;
		}

		/// <summary>"dlc_dul_cru" becomes "Dul Cru". Used when the game offers no localized DLC title.</summary>
		public static string PrettyDlcName(string folder)
		{
			string s = folder ?? string.Empty;
			if (s.StartsWith("dlc_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(4);
			var parts = s.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++)
			{
				parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
			}
			return string.Join(" ", parts);
		}
	}

	/// <summary>
	/// Replays the game's own data-merge rules to decide who owns each definition id. The game loads folders in a
	/// fixed order (base, DLC, then each enabled mod) and merges elements with the same id in
	/// <c>Library.AddLibraryElement(element, overrideCSV)</c>: a first sighting adds the element, a repeat with the
	/// override flag replaces it, and a repeat without the flag only extends it (or is rejected). So:
	/// <list type="bullet">
	/// <item>first sighting: that source owns the id (a mod -> Mod, base/DLC -> as is)</item>
	/// <item>repeat with override flag: the later source owns it; a mod replacing something -> Override</item>
	/// <item>repeat without override flag: ownership does not change (the id is extended, not replaced)</item>
	/// </list>
	/// </summary>
	internal sealed class OriginTracker
	{
		private sealed class Sighting
		{
			public SourceInfo Origin;
			public bool OverrideFlag;
		}

		// category (e.g. "ActorDataClass") -> definition id -> sightings in load order
		private readonly Dictionary<string, Dictionary<string, List<Sighting>>> _byCategory =
			new Dictionary<string, Dictionary<string, List<Sighting>>>(StringComparer.Ordinal);

		/// <summary>Number of sightings stored (duplicates are not stored, see <see cref="Note"/>).</summary>
		public int Count { get; private set; }

		/// <summary>Sightings that came from the base game or a DLC (not from a mod).</summary>
		public int BaseCount { get; private set; }

		public void Reset()
		{
			_byCategory.Clear();
			Count = 0;
			BaseCount = 0;
		}

		/// <summary>
		/// Records that a source defined an element, in load order. An identical earlier sighting (same source and
		/// override flag) is not stored again: the game re-reads its folders several times per load, and replaying
		/// the same pass twice cannot change the outcome.
		/// </summary>
		public void Note(string category, string id, SourceInfo origin, bool overrideFlag) => Add(category, id, origin, overrideFlag, atFront: false);

		/// <summary>Like <see cref="Note"/> but placed before everything seen so far; used to backfill the base game.</summary>
		public void NoteFirst(string category, string id, SourceInfo origin, bool overrideFlag) => Add(category, id, origin, overrideFlag, atFront: true);

		private void Add(string category, string id, SourceInfo origin, bool overrideFlag, bool atFront)
		{
			if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(id) || origin == null) return;

			if (!_byCategory.TryGetValue(category, out Dictionary<string, List<Sighting>> ids))
			{
				ids = new Dictionary<string, List<Sighting>>(StringComparer.Ordinal);
				_byCategory[category] = ids;
			}
			if (!ids.TryGetValue(id, out List<Sighting> list))
			{
				list = new List<Sighting>(1);
				ids[id] = list;
			}
			foreach (Sighting existing in list)
			{
				if (existing.OverrideFlag == overrideFlag && existing.Origin.SameAs(origin)) return;
			}

			var sighting = new Sighting { Origin = origin, OverrideFlag = overrideFlag };
			if (atFront) list.Insert(0, sighting); else list.Add(sighting);
			Count++;
			if (!origin.IsMod) BaseCount++;
		}

		/// <summary>The owner of a definition id after replaying every sighting, or null if it was never seen.</summary>
		public SourceInfo Resolve(string category, string id)
		{
			if (category == null || id == null) return null;
			if (!_byCategory.TryGetValue(category, out Dictionary<string, List<Sighting>> ids)) return null;
			if (!ids.TryGetValue(id, out List<Sighting> list) || list.Count == 0) return null;

			SourceInfo owner = list[0].Origin;
			for (int i = 1; i < list.Count; i++)
			{
				Sighting s = list[i];
				if (!s.OverrideFlag) continue; // extends the existing element; ownership unchanged

				if (s.Origin.Kind == SourceKind.Mod)
				{
					// A mod replaced an element that already existed: it is not a new definition.
					owner = new SourceInfo(SourceKind.Override, s.Origin.Name, s.Origin.ModId, s.Origin.Folder);
				}
				else
				{
					owner = s.Origin; // base/DLC replacing itself (game-type folders) stays base/DLC
				}
			}
			return owner;
		}

		/// <summary>Ids that were sighted at least once for a category (diagnostics and tests).</summary>
		public IEnumerable<string> Ids(string category) =>
			_byCategory.TryGetValue(category, out Dictionary<string, List<Sighting>> ids) ? ids.Keys : (IEnumerable<string>)new string[0];
	}
}

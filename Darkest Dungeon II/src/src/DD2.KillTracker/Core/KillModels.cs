using System;
using System.Collections.Generic;

namespace DD2.KillTracker.Core
{
	/// <summary>Where a piece of content came from.</summary>
	internal enum SourceKind
	{
		/// <summary>Shipped with the base game.</summary>
		Base = 0,
		/// <summary>Shipped with an official DLC (the game keeps DLC data in its own folders).</summary>
		Dlc = 1,
		/// <summary>Added by a mod: the id did not exist before the mod loaded.</summary>
		Mod = 2,
		/// <summary>A mod replaced a definition that already existed in the base game or a DLC.</summary>
		Override = 3,
		/// <summary>Could not be determined.</summary>
		Unknown = 4,
	}

	/// <summary>Immutable description of where a monster definition came from.</summary>
	internal sealed class SourceInfo
	{
		public static readonly SourceInfo BaseGame = new SourceInfo(SourceKind.Base, "Base game", null, null);
		public static readonly SourceInfo Unknown = new SourceInfo(SourceKind.Unknown, "Unknown", null, null);

		public readonly SourceKind Kind;

		/// <summary>"Base game", the DLC name, or the mod title.</summary>
		public readonly string Name;

		/// <summary>Steam Workshop id (or any numeric mod id) as text; null for local mods and non-mods.</summary>
		public readonly string ModId;

		/// <summary>Mod folder name (leaf of the mod path); null for non-mods.</summary>
		public readonly string Folder;

		public SourceInfo(SourceKind kind, string name, string modId, string folder)
		{
			Kind = kind;
			Name = name ?? string.Empty;
			ModId = string.IsNullOrEmpty(modId) ? null : modId;
			Folder = string.IsNullOrEmpty(folder) ? null : folder;
		}

		public bool IsMod => Kind == SourceKind.Mod || Kind == SourceKind.Override;

		/// <summary>Short label for the UI: "Base game", "Catacombs (DLC)", "Cool Mod", "Cool Mod (override)".</summary>
		public string Label
		{
			get
			{
				switch (Kind)
				{
					case SourceKind.Override: return Name + " (override)";
					case SourceKind.Dlc: return Name + " (DLC)";
					default: return Name;
				}
			}
		}

		public bool SameAs(SourceInfo other) =>
			other != null && other.Kind == Kind && other.Name == Name && other.ModId == ModId && other.Folder == Folder;

		public Dictionary<string, object> ToJson()
		{
			var o = new Dictionary<string, object> { ["kind"] = Kind.ToString().ToLowerInvariant(), ["name"] = Name };
			if (ModId != null) o["modId"] = ModId;
			if (Folder != null) o["folder"] = Folder;
			return o;
		}

		public static SourceInfo FromJson(Dictionary<string, object> o)
		{
			if (o == null) return Unknown;
			SourceKind kind = SourceKind.Unknown;
			string k = Json.Str(o, "kind");
			if (k != null) Enum.TryParse(k, true, out kind);
			return new SourceInfo(kind, Json.Str(o, "name"), Json.Str(o, "modId"), Json.Str(o, "folder"));
		}
	}

	/// <summary>One kill, as stored in the raw log. Any field the game did not provide stays null.</summary>
	internal sealed class KillRecord
	{
		public string TimeUtc;
		public string MonsterId;
		public string MonsterName;
		public string KillerClassId;
		public string KillerHeroName;
		/// <summary>The game's source type: attack, dot, etc. (lower-case name of the game's enum).</summary>
		public string SourceType;
		/// <summary>Skill id for an attack, or the DoT id (blight, bleed, burn) for damage over time.</summary>
		public string SourceId;
		public string Region;
		public string RunId;
		/// <summary>Label of the mod/DLC/base source of the monster at the time of the kill.</summary>
		public string ContentSource;

		public Dictionary<string, object> ToJson()
		{
			var o = new Dictionary<string, object>();
			Put(o, "t", TimeUtc);
			Put(o, "monster", MonsterId);
			Put(o, "name", MonsterName);
			Put(o, "killerClass", KillerClassId);
			Put(o, "killerHero", KillerHeroName);
			Put(o, "sourceType", SourceType);
			Put(o, "sourceId", SourceId);
			Put(o, "region", Region);
			Put(o, "run", RunId);
			Put(o, "content", ContentSource);
			return o;
		}

		public static KillRecord FromJson(Dictionary<string, object> o)
		{
			if (o == null) return null;
			string monster = Json.Str(o, "monster");
			if (string.IsNullOrEmpty(monster)) return null;
			return new KillRecord
			{
				TimeUtc = Json.Str(o, "t"),
				MonsterId = monster,
				MonsterName = Json.Str(o, "name"),
				KillerClassId = Json.Str(o, "killerClass"),
				KillerHeroName = Json.Str(o, "killerHero"),
				SourceType = Json.Str(o, "sourceType"),
				SourceId = Json.Str(o, "sourceId"),
				Region = Json.Str(o, "region"),
				RunId = Json.Str(o, "run"),
				ContentSource = Json.Str(o, "content"),
			};
		}

		private static void Put(Dictionary<string, object> o, string key, string value)
		{
			if (value != null) o[key] = value;
		}
	}

	/// <summary>Per-monster aggregate. Exact for the lifetime of the profile; never trimmed with the raw log.</summary>
	internal sealed class MonsterStats
	{
		public string Id;
		public string Name;
		public SourceInfo Source = SourceInfo.Unknown;
		public long Lifetime;
		public long Run;
		public string FirstKillUtc;
		public string LastKillUtc;

		public long CountFor(bool lifetime) => lifetime ? Lifetime : Run;
	}
}

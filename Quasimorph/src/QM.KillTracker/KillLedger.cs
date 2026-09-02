using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using MGSC;

namespace QM.KillTracker;

/// <summary>
/// One bucket of kill counters for a single scope (raid / campaign / lifetime), keyed three ways:
/// by unit type (<c>MobClassId</c>), by faction id, and by the game's own <c>CreatureClass</c>
/// bucket. Every recorded kill increments exactly one key in each dictionary plus the total, so the
/// three sums always agree with <see cref="Total"/>.
/// </summary>
internal sealed class KillScope
{
	public readonly Dictionary<string, int> ByMobClass = new Dictionary<string, int>();
	public readonly Dictionary<string, int> ByFaction = new Dictionary<string, int>();
	public readonly Dictionary<string, int> ByCreatureClass = new Dictionary<string, int>();

	public int Total;

	public void Record(string mobClass, string faction, string creatureClass)
	{
		Increment(ByMobClass, mobClass);
		Increment(ByFaction, faction);
		Increment(ByCreatureClass, creatureClass);
		Total++;
	}

	public void Reset()
	{
		ByMobClass.Clear();
		ByFaction.Clear();
		ByCreatureClass.Clear();
		Total = 0;
	}

	public void ReplaceFrom(KillScope other)
	{
		Reset();
		foreach (KeyValuePair<string, int> kv in other.ByMobClass)
		{
			ByMobClass[kv.Key] = kv.Value;
		}
		foreach (KeyValuePair<string, int> kv in other.ByFaction)
		{
			ByFaction[kv.Key] = kv.Value;
		}
		foreach (KeyValuePair<string, int> kv in other.ByCreatureClass)
		{
			ByCreatureClass[kv.Key] = kv.Value;
		}
		Total = other.Total;
	}

	private static void Increment(Dictionary<string, int> map, string key)
	{
		map.TryGetValue(key, out int current);
		map[key] = current + 1;
	}
}

/// <summary>
/// The counting model: three <see cref="KillScope"/>s (current raid, current campaign, lifetime)
/// plus the mob-class → localization-id map used to resolve display names, and hand-rolled JSON
/// persistence under <c>BepInEx/config/QM.KillTracker/</c>. One file per campaign slot plus one
/// lifetime file; the game's own save files are never touched.
/// </summary>
internal sealed class KillLedger
{
	/// <summary>Set once in <see cref="KillTrackerPlugin.Awake"/>, before any patch can fire.</summary>
	public static KillLedger Instance;

	public readonly KillScope Raid = new KillScope();
	public readonly KillScope Campaign = new KillScope();
	public readonly KillScope Lifetime = new KillScope();

	private readonly Dictionary<string, string> _mobLocalization = new Dictionary<string, string>();

	/// <summary>The save slot the campaign scope currently belongs to; -1 means no campaign.</summary>
	private int _slot = -1;

	private bool _dirty;

	public void Record(CreatureData data)
	{
		string mobClass = string.IsNullOrEmpty(data.MobClassId) ? "unknown" : data.MobClassId;
		string faction = string.IsNullOrEmpty(data.FactionId) ? "none" : data.FactionId;
		string creatureClass = data.CreatureClass.ToString();

		Raid.Record(mobClass, faction, creatureClass);
		Campaign.Record(mobClass, faction, creatureClass);
		Lifetime.Record(mobClass, faction, creatureClass);

		if (!string.IsNullOrEmpty(data.LocalizationId))
		{
			_mobLocalization[mobClass] = data.LocalizationId;
		}

		_dirty = true;
	}

	public void ResetRaid()
	{
		Raid.Reset();
	}

	/// <summary>
	/// Switches the campaign scope to another save slot. The outgoing slot is flushed first — via the
	/// normal flush, so the lifetime totals go out with it rather than being dropped along with the
	/// dirty flag — then the new slot's data is loaded in. Slot -1 means "no campaign": it accumulates
	/// into a scratch scope that is never written.
	/// </summary>
	public void SwitchCampaign(int newSlot)
	{
		if (newSlot == _slot)
		{
			return;
		}

		try
		{
			Flush();

			Campaign.Reset();
			_slot = newSlot;

			if (newSlot >= 0)
			{
				LoadSlot(newSlot);
			}

			_dirty = false;
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Campaign switch failed: " + e);
		}
	}

	/// <summary>Writes the campaign and lifetime files, but only if anything changed since the last flush.</summary>
	public void Flush()
	{
		if (!_dirty)
		{
			return;
		}

		try
		{
			if (_slot >= 0)
			{
				WriteJson(SlotPath(_slot), SerializeScope(Campaign, _mobLocalization));
			}

			WriteJson(LifetimePath, SerializeScope(Lifetime, _mobLocalization));
			_dirty = false;
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Failed to flush kill stats: " + e);
		}
	}

	public void LoadLifetime()
	{
		string path = LifetimePath;
		if (!File.Exists(path))
		{
			return;
		}

		try
		{
			if (TryParse(File.ReadAllText(path), out KillScope loaded, out Dictionary<string, string> localization))
			{
				Lifetime.ReplaceFrom(loaded);
				foreach (KeyValuePair<string, string> kv in localization)
				{
					_mobLocalization[kv.Key] = kv.Value;
				}
				Cfg.Log.LogInfo($"Loaded lifetime kill stats ({Lifetime.Total} kills).");
			}
			else
			{
				Cfg.Log.LogWarning($"Lifetime kill stats failed to parse; starting from empty. "
					+ "File left in place: " + path);
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("Could not read lifetime kill stats: " + e);
		}
	}

	/// <summary>Display name for a mob-class bucket, resolved from the localization id we last saw for it.</summary>
	public string GetMobDisplayName(string mobClass)
	{
		if (!_mobLocalization.TryGetValue(mobClass, out string localizationId) || string.IsNullOrEmpty(localizationId))
		{
			return mobClass;
		}
		return ResolveLocalizedName("monster." + localizationId + ".name", mobClass);
	}

	/// <summary>
	/// Reads a localized string, falling back to the raw id. Keys are guarded with HasKey so a
	/// missing translation never spams the game log with warnings.
	/// </summary>
	public static string ResolveLocalizedName(string key, string fallback)
	{
		try
		{
			if (Localization.HasKey(key))
			{
				string name = Localization.Get(key, false);
				if (!string.IsNullOrEmpty(name))
				{
					return name;
				}
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("Localization lookup failed for '" + key + "': " + e);
		}
		return fallback;
	}

	private void LoadSlot(int slot)
	{
		string path = SlotPath(slot);
		if (!File.Exists(path))
		{
			return;
		}

		try
		{
			if (TryParse(File.ReadAllText(path), out KillScope loaded, out Dictionary<string, string> localization))
			{
				Campaign.ReplaceFrom(loaded);
				foreach (KeyValuePair<string, string> kv in localization)
				{
					_mobLocalization[kv.Key] = kv.Value;
				}
				Cfg.Log.LogInfo($"Loaded kill stats for slot {slot} ({Campaign.Total} kills).");
			}
			else
			{
				Cfg.Log.LogWarning($"Kill stats for slot {slot} failed to parse; starting from empty. "
					+ "File left in place: " + path);
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("Could not read kill stats for slot " + slot + ": " + e);
		}
	}

	private static string DataDirectory => Path.Combine(BepInEx.Paths.ConfigPath, "QM.KillTracker");

	private static string SlotPath(int slot) => Path.Combine(DataDirectory, "slot_" + slot + ".json");

	private static string LifetimePath => Path.Combine(DataDirectory, "lifetime.json");

	/// <summary>Writes to a temp file first and then moves it, so a crash mid-write can never leave a truncated file behind.</summary>
	private static void WriteJson(string path, string json)
	{
		Directory.CreateDirectory(DataDirectory);
		string temp = path + ".tmp";
		File.WriteAllText(temp, json);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		File.Move(temp, path);
	}

	// ------------------------------------------------------------------ persistence format
	// Hand-rolled JSON for exactly the shape we write; the only value types are strings and ints.
	//
	//   {"total":5,"mob_class":{"ensign":3},"mob_class_localization":{"ensign":"Ensign"},
	//    "faction":{"unchained_belt":4,"none":1},"creature_class":{"Human":5}}

	private static string SerializeScope(KillScope scope, IReadOnlyDictionary<string, string> localization)
	{
		var sb = new StringBuilder();
		sb.Append('{');
		sb.Append("\"total\":").Append(scope.Total);
		sb.Append(",\"mob_class\":").Append(WriteIntMap(scope.ByMobClass));
		sb.Append(",\"mob_class_localization\":").Append(WriteStringMap(localization));
		sb.Append(",\"faction\":").Append(WriteIntMap(scope.ByFaction));
		sb.Append(",\"creature_class\":").Append(WriteIntMap(scope.ByCreatureClass));
		sb.Append('}');
		return sb.ToString();
	}

	private static string WriteIntMap(Dictionary<string, int> map)
	{
		var sb = new StringBuilder();
		sb.Append('{');
		bool first = true;
		foreach (KeyValuePair<string, int> kv in map)
		{
			if (!first)
			{
				sb.Append(',');
			}
			first = false;
			sb.Append('"').Append(Escape(kv.Key)).Append("\":").Append(kv.Value);
		}
		sb.Append('}');
		return sb.ToString();
	}

	private static string WriteStringMap(IReadOnlyDictionary<string, string> map)
	{
		var sb = new StringBuilder();
		sb.Append('{');
		bool first = true;
		foreach (KeyValuePair<string, string> kv in map)
		{
			if (!first)
			{
				sb.Append(',');
			}
			first = false;
			sb.Append('"').Append(Escape(kv.Key)).Append("\":\"").Append(Escape(kv.Value)).Append('"');
		}
		sb.Append('}');
		return sb.ToString();
	}

	private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

	/// <summary>
	/// Parses a stats file. On any failure returns false and leaves <paramref name="scope"/> untouched,
	/// so a corrupt file is skipped rather than throwing. The caller logs and starts from empty.
	/// </summary>
	private static bool TryParse(string json, out KillScope scope, out Dictionary<string, string> localization)
	{
		scope = null;
		localization = new Dictionary<string, string>();

		Dictionary<string, object> root = JsonReader.Parse(json);
		if (root == null)
		{
			return false;
		}

		var parsed = new KillScope();

		if (root.TryGetValue("mob_class", out object mobClass) && mobClass is Dictionary<string, object> mobClassMap)
		{
			foreach (KeyValuePair<string, object> kv in mobClassMap)
			{
				if (kv.Value is int n)
				{
					parsed.ByMobClass[kv.Key] = n;
				}
			}
		}

		if (root.TryGetValue("mob_class_localization", out object mobLocalization)
			&& mobLocalization is Dictionary<string, object> localizationMap)
		{
			foreach (KeyValuePair<string, object> kv in localizationMap)
			{
				if (kv.Value is string s)
				{
					localization[kv.Key] = s;
				}
			}
		}

		if (root.TryGetValue("faction", out object faction) && faction is Dictionary<string, object> factionMap)
		{
			foreach (KeyValuePair<string, object> kv in factionMap)
			{
				if (kv.Value is int n)
				{
					parsed.ByFaction[kv.Key] = n;
				}
			}
		}

		if (root.TryGetValue("creature_class", out object creatureClass)
			&& creatureClass is Dictionary<string, object> creatureClassMap)
		{
			foreach (KeyValuePair<string, object> kv in creatureClassMap)
			{
				if (kv.Value is int n)
				{
					parsed.ByCreatureClass[kv.Key] = n;
				}
			}
		}

		// Trust the stored total, but fall back to summing the creature-class buckets for files
		// written before the total field existed (or hand-edited files that dropped it).
		if (root.TryGetValue("total", out object total) && total is int totalValue)
		{
			parsed.Total = totalValue;
		}
		else
		{
			foreach (int n in parsed.ByCreatureClass.Values)
			{
				parsed.Total += n;
			}
		}

		scope = parsed;
		return true;
	}

	/// <summary>
	/// Minimal JSON reader for exactly the shape above: an object of string keys whose values are
	/// strings, ints, or nested objects of the same. Anything else returns null (parse failure).
	/// </summary>
	private static class JsonReader
	{
		public static Dictionary<string, object> Parse(string text)
		{
			int i = 0;
			return ParseValue(text, ref i) as Dictionary<string, object>;
		}

		private static object ParseValue(string text, ref int i)
		{
			SkipWhitespace(text, ref i);
			if (i >= text.Length)
			{
				return null;
			}

			char c = text[i];
			if (c == '{')
			{
				return ParseObject(text, ref i);
			}
			if (c == '"')
			{
				return ParseString(text, ref i);
			}
			return ParseNumber(text, ref i);
		}

		private static Dictionary<string, object> ParseObject(string text, ref int i)
		{
			i++; // '{'
			var obj = new Dictionary<string, object>();
			SkipWhitespace(text, ref i);

			if (i < text.Length && text[i] == '}')
			{
				i++;
				return obj;
			}

			while (true)
			{
				SkipWhitespace(text, ref i);
				if (i >= text.Length || text[i] != '"')
				{
					return null;
				}

				string key = ParseString(text, ref i);
				SkipWhitespace(text, ref i);
				if (i >= text.Length || text[i] != ':')
				{
					return null;
				}

				i++; // ':'
				object value = ParseValue(text, ref i);
				if (value == null)
				{
					return null;
				}

				obj[key] = value;
				SkipWhitespace(text, ref i);

				if (i >= text.Length)
				{
					return null;
				}
				if (text[i] == ',')
				{
					i++;
					continue;
				}
				if (text[i] == '}')
				{
					i++;
					return obj;
				}
				return null;
			}
		}

		private static string ParseString(string text, ref int i)
		{
			i++; // opening quote
			var sb = new StringBuilder();
			while (i < text.Length)
			{
				char c = text[i];
				if (c == '\\')
				{
					i++;
					if (i >= text.Length)
					{
						return null;
					}

					char escaped = text[i];
					if (escaped == '"' || escaped == '\\')
					{
						sb.Append(escaped);
					}
					else
					{
						return null; // we never emit other escape sequences
					}
					i++;
				}
				else if (c == '"')
				{
					i++;
					return sb.ToString();
				}
				else
				{
					sb.Append(c);
					i++;
				}
			}

			return null; // unterminated string
		}

		private static object ParseNumber(string text, ref int i)
		{
			int start = i;
			while (i < text.Length && text[i] >= '0' && text[i] <= '9')
			{
				i++;
			}

			if (i == start)
			{
				return null;
			}

			return int.TryParse(text.Substring(start, i - start), out int value) ? value : null;
		}

		private static void SkipWhitespace(string text, ref int i)
		{
			while (i < text.Length && (text[i] == ' ' || text[i] == '\t' || text[i] == '\r' || text[i] == '\n'))
			{
				i++;
			}
		}
	}
}

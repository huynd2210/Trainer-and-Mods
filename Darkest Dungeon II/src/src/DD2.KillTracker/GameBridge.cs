using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.DLC;
using Assets.Code.Library;
using Assets.Code.Locale;
using Assets.Code.Map;
using Assets.Code.Map.Generation.Biome;
using Assets.Code.Mod;
using Assets.Code.Profile;
using Assets.Code.Resource;
using Assets.Code.Utils;
using Assets.Code.Utils.Serialization;
using DD2.KillTracker.Core;
using HarmonyLib;

namespace DD2.KillTracker
{
	/// <summary>
	/// Every read of game state goes through here, each one returning null (never throwing) when the game is not in
	/// a state that can answer. A field the game does not provide stays null in the ledger.
	/// </summary>
	internal static class GameBridge
	{
		// ModMgr keeps its mod list private (Assets.Code.Mod.ModMgr.mods); GetAllMods() would reset every mod's "on" flag.
		private static readonly FieldInfo ModsField = AccessTools.Field(typeof(ModMgr), "mods");

		// SaveUtils.RunSavePath is a private static string set once per run (SaveUtils.SetRunSavePath).
		private static readonly FieldInfo RunSavePathField = AccessTools.Field(typeof(SaveUtils), "RunSavePath");

		/// <summary>"profile_3" or "mod_profile_3": the game keeps modded play in separate profiles (ProfileBhv.SAVE_PROFILE_PREFIX).</summary>
		public static string ProfileKey()
		{
			if (!SingletonMonoBehaviour<ProfileBhv>.HasInstance()) return null;
			ProfileBhv bhv = SingletonMonoBehaviour<ProfileBhv>.Instance;
			uint guid = bhv.GetCurrentProfileGuid();
			if (guid == 0) return null;
			return bhv.SAVE_PROFILE_PREFIX + guid;
		}

		/// <summary>
		/// The game's own run identifier: the folder name of the run save (yyMMdd_HHmmss, taken when the run is first saved
		/// and kept when the run is continued). Null until the game has created that folder.
		/// </summary>
		public static string GameRunId()
		{
			if (RunSavePathField == null) return null;
			string path = RunSavePathField.GetValue(null) as string;
			if (string.IsNullOrEmpty(path)) return null;
			string leaf = OriginClassifier.Leaf(path);
			return string.IsNullOrEmpty(leaf) ? null : leaf;
		}

		/// <summary>Biome of the loaded map (the region the party is travelling through), e.g. "crossroads".</summary>
		public static string Region()
		{
			if (!SingletonMonoBehaviour<MapMgrBhv>.HasInstance()) return null;
			BiomeType biome = MapMgrBhv.LoadedBiome;
			if (biome == null || !biome.IsValid()) return null;
			return biome.GetName();
		}

		public static ActorInstance FindActor(uint guid)
		{
			if (!SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance()) return null;
			return SingletonMonoBehaviour<Library<uint, ActorInstance>>.Instance.GetLibraryElement(guid);
		}

		/// <summary>Localized name of a monster class. The game keys it by the class id itself (see BossKillLogEntry).</summary>
		public static string LocalizedName(string classId)
		{
			if (string.IsNullOrEmpty(classId) || !Singleton<Localization>.HasInstance()) return null;
			string text = Singleton<Localization>.Instance.TryGetString(classId);
			return string.IsNullOrEmpty(text) ? null : text;
		}

		public static string LocalizedOrNull(string key) => Guard.Run("localize " + key, () => LocalizedName(key));

		// ------------------------------------------------------------------ mod / DLC / base folders for attribution

		public static List<ModEntry> Mods()
		{
			var result = new List<ModEntry>();
			if (ModsField == null || !Singleton<ModMgr>.HasInstance()) return result;
			if (!(ModsField.GetValue(Singleton<ModMgr>.Instance) is List<ModInfo> mods)) return result;

			foreach (ModInfo m in mods)
			{
				if (m == null || string.IsNullOrEmpty(m.path)) continue;
				result.Add(new ModEntry { Path = m.path, Title = m.modName, WorkshopId = m.id == 0 ? null : m.id.ToString() });
			}
			return result;
		}

		public static List<DlcEntry> Dlcs()
		{
			var result = new List<DlcEntry>();
			if (DLCManager.Instance == null) return result;
			IReadOnlyList<DLCConfig> configs = DLCManager.Instance.DLCConfigs;
			if (configs == null) return result;
			foreach (DLCConfig c in configs)
			{
				if (c == null || string.IsNullOrEmpty(c.DLCName)) continue;
				string title = LocalizedName(c.DLCLocKey);
				result.Add(new DlcEntry { FolderName = c.DLCName, Title = title ?? OriginClassifier.PrettyDlcName(c.DLCName) });
			}
			return result;
		}

		/// <summary>The folders holding the base game's data (the game's ResourceTextFolder list, normally StreamingAssets/Excel).</summary>
		public static List<string> BaseRoots()
		{
			var result = new List<string>();
			foreach (ResourceTextFolder folder in CustomEnum<ResourceTextFolder>.GetInstances())
			{
				if (!string.IsNullOrEmpty(folder.m_Folder)) result.Add(folder.m_Folder);
			}
			return result;
		}

		/// <summary>Names of the game-type sub-folders (kingdom, expedition) that the game also reads under each base root.</summary>
		public static List<string> GameTypeFolderNames()
		{
			var result = new List<string>();
			foreach (Assets.Code.Game.GameType type in CustomEnum<Assets.Code.Game.GameType>.GetInstances())
			{
				string name = type.GetName();
				if (!string.IsNullOrEmpty(name)) result.Add(name);
			}
			return result;
		}
	}
}

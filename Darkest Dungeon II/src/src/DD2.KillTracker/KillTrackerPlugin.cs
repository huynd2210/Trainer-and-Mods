using System;
using System.Reflection;
using BepInEx;
using DD2.KillTracker.Core;
using HarmonyLib;

namespace DD2.KillTracker
{
	[BepInPlugin(Guid, "Darkest Dungeon II Kill Tracker", "1.0.0")]
	public class KillTrackerPlugin : BaseUnityPlugin
	{
		public const string Guid = "com.claude.dd2.killtracker";

		private Harmony _harmony;

		private void Awake()
		{
			Cfg.Log = Logger;

			Cfg.ToggleKey = Config.Bind("General", "ToggleKey", "F11",
				"Key that opens and closes the kill tracker window. Unity Input System key names (F11, Backquote, K, ...), "
				+ "optionally with modifiers: Ctrl+F11, Shift+Alt+K.");

			Cfg.SaveIntervalSeconds = Config.Bind("General", "SaveIntervalSeconds", 15f,
				"How often (seconds) unsaved kills are written to disk. The ledger is also written when combat ends, "
				+ "when you switch profile, and when the game quits.");

			Cfg.MaxRawRecords = Config.Bind("General", "MaxRawRecords", KillLedger.DefaultMaxRecords,
				"How many individual kill records to keep in the ledger file (oldest are dropped first). "
				+ "Per-monster totals are exact regardless of this limit. 0 keeps totals only.");

			Cfg.CountNonEnemyDeaths = Config.Bind("General", "CountNonEnemyDeaths", false,
				"Also count deaths of heroes, allies and props. Off by default. Corpses are never counted either way.");

			string dataDir = System.IO.Path.Combine(Paths.ConfigPath, "DD2.KillTracker");
			var store = new LedgerStore(dataDir, Cfg.Info, Cfg.Warn);
			Cfg.Ledger = new LedgerManager(store, Math.Max(0, Cfg.MaxRawRecords.Value));
			Cfg.MaxRawRecords.SettingChanged += (s, e) => Cfg.Ledger.SetMaxRecords(Math.Max(0, Cfg.MaxRawRecords.Value));

			Cfg.Ready = true;

			LogEnvironment();
			try
			{
				_harmony = new Harmony(Guid);
				Patches.Apply(_harmony);
			}
			catch (Exception e)
			{
				Cfg.Log.LogError("Patching failed, the mod will do nothing: " + e);
			}

			KillTrackerHost.Spawn();
			Cfg.Log.LogInfo("Kill Tracker 1.0.0 loaded. Press " + Cfg.ParsedToggleKey() + " to open. Ledgers: " + dataDir);
		}

		/// <summary>The game may ship its own 0Harmony; record which one we are bound to in case patches silently fail.</summary>
		private static void LogEnvironment()
		{
			try
			{
				Assembly harmony = typeof(Harmony).Assembly;
				Cfg.Log.LogInfo("Harmony in use: " + harmony.GetName().Version + " at " + harmony.Location);
			}
			catch (Exception e)
			{
				Cfg.Log.LogWarning("Could not identify the Harmony assembly: " + e.Message);
			}
		}
	}
}

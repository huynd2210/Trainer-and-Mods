using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using DD2.KillTracker.Core;

namespace DD2.KillTracker
{
	/// <summary>
	/// Plain static holder for settings, the logger and the shared services. Everything the patches and the UI
	/// need hangs off here, so none of them depend on the plugin MonoBehaviour (which the game may destroy).
	/// </summary>
	internal static class Cfg
	{
		public static ManualLogSource Log;

		public static ConfigEntry<string> ToggleKey;
		public static ConfigEntry<float> SaveIntervalSeconds;
		public static ConfigEntry<int> MaxRawRecords;
		public static ConfigEntry<bool> CountNonEnemyDeaths;

		/// <summary>Owns the ledger of the active DD2 profile and writes it to disk.</summary>
		public static LedgerManager Ledger;

		public static bool Ready;

		public static KeyChord ParsedToggleKey() => KeyChord.Parse(ToggleKey?.Value, "F11");

		public static void Warn(string message) => Log?.LogWarning(message);
		public static void Info(string message) => Log?.LogInfo(message);
	}

	/// <summary>
	/// Runs patch and UI code so that an exception is logged once per site and never reaches the game.
	/// A handler that fails every frame or every kill would otherwise flood the log.
	/// </summary>
	internal static class Guard
	{
		private static readonly System.Collections.Generic.HashSet<string> Reported = new System.Collections.Generic.HashSet<string>();

		public static void Run(string site, Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Report(site, e);
			}
		}

		public static T Run<T>(string site, Func<T> func, T fallback = default(T))
		{
			try
			{
				return func();
			}
			catch (Exception e)
			{
				Report(site, e);
				return fallback;
			}
		}

		public static void Report(string site, Exception e)
		{
			bool first;
			lock (Reported)
			{
				first = Reported.Add(site);
			}
			if (first)
			{
				Cfg.Log?.LogError(site + " failed (further failures at this site are not logged): " + e);
			}
		}
	}
}

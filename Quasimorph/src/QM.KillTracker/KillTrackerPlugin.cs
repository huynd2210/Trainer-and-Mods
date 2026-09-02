using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QM.KillTracker;

[BepInPlugin(Guid, "Quasimorph Kill Tracker", "1.0.0")]
public class KillTrackerPlugin : BaseUnityPlugin
{
	public const string Guid = "com.claude.quasimorph.killtracker";

	private Harmony _harmony;

	private void Awake()
	{
		Cfg.Log = Logger;

		Cfg.ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F11),
			"Opens and closes the kill tracker window.");

		Cfg.SaveIntervalSeconds = Config.Bind("General", "SaveIntervalSeconds", 10f,
			"How often, in seconds, dirty kill counts are written to disk. This throttles file "
			+ "writes; lower values write more often and lose less on a crash.");

		Cfg.TrackAllyKills = Config.Bind("General", "TrackAllyKills", true,
			"Also count creatures that were allied to the player at the moment of death. Turn off "
			+ "to count only hostiles.");

		KillLedger.Instance = new KillLedger();
		KillLedger.Instance.LoadLifetime();

		Cfg.Ready = true;

		LogEnvironment();
		ApplyPatches();
		KillTrackerHost.Spawn();

		Cfg.Log.LogInfo($"Kill Tracker 1.0.0 loaded. Press {Cfg.ToggleKey.Value} to open.");
	}

	/// <summary>
	/// The game ships its own 0Harmony in Quasimorph_Data/Managed alongside the BepInEx one. If we
	/// ever bind against the wrong copy, patches can silently fail, so record which one we got.
	/// </summary>
	private void LogEnvironment()
	{
		try
		{
			Assembly harmony = typeof(Harmony).Assembly;
			Cfg.Log.LogInfo($"[diag] Harmony in use: {harmony.GetName().Version} at {harmony.Location}");
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("[diag] Could not identify the Harmony assembly: " + e);
		}
	}

	private void ApplyPatches()
	{
		try
		{
			_harmony = new Harmony(Guid);
			_harmony.PatchAll(typeof(KillPatches));

			var patched = new List<MethodBase>(_harmony.GetPatchedMethods());
			if (patched.Count == 0)
			{
				Cfg.Log.LogError("[diag] Harmony reported ZERO patched methods. The mod cannot work; "
					+ "CreatureSystem.KillMonster did not match what this build expects.");
				return;
			}

			Cfg.Log.LogInfo($"[diag] Harmony patched {patched.Count} method(s):");
			foreach (MethodBase m in patched)
			{
				Cfg.Log.LogInfo($"[diag]   {m.DeclaringType?.FullName}.{m.Name}");
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Patching failed, the mod will do nothing: " + e);
		}
	}

	private void OnDestroy()
	{
		Cfg.Log?.LogWarning("[diag] plugin component was DESTROYED (expected; the host survives).");
	}
}

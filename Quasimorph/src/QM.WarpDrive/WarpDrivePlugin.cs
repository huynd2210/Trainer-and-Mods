using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QM.WarpDrive;

[BepInPlugin(Guid, "Quasimorph Warp Drive", "1.0.4")]
public class WarpDrivePlugin : BaseUnityPlugin
{
	public const string Guid = "com.claude.quasimorph.warpdrive";

	private Harmony _harmony;
	private float _nextHeartbeat;

	private void Awake()
	{
		Cfg.Log = Logger;

		Cfg.Enabled = Config.Bind("1. General", "Enabled", true,
			"Master switch. When off, travel behaves exactly as in the unmodded game.");

		Cfg.ToggleKey = Config.Bind("1. General", "ToggleKey", new KeyboardShortcut(KeyCode.F9),
			"Turns the mod on and off while you play.");

		Cfg.ShowStatusOverlay = Config.Bind("1. General", "ShowStatusOverlay", true,
			"Show a small status readout in the bottom-left corner.");

		Cfg.InstantTravel = Config.Bind("2. Speed", "InstantTravel", false,
			"Skip the flight animation entirely and arrive almost immediately. Overrides "
			+ "RealTimeSpeedMultiplier while it is on.");

		Cfg.RealTimeSpeedMultiplier = Config.Bind("2. Speed", "RealTimeSpeedMultiplier", 5f,
			new ConfigDescription(
				"How much faster the flight animation plays out in real seconds. "
				+ "Purely how long you sit and watch; it does not change the in-game cost of the trip.",
				new AcceptableValueRange<float>(1f, 50f)));

		Cfg.InGameTimeMultiplier = Config.Bind("2. Speed", "InGameTimeMultiplier", 0f,
			new ConfigDescription(
				"Scales how many in-game hours a trip costs. 0.0 = the calendar does not move at "
				+ "all while you travel, 0.1 = a tenth of the usual time, 1.0 = unmodded.",
				new AcceptableValueRange<float>(0f, 1f)));

		Cfg.AffectShippingDeliveries = Config.Bind("3. Side effects", "AffectShippingDeliveries", false,
			"The game reuses its travel-distance maths to schedule station-to-station item "
			+ "deliveries. Off by default so this mod only changes your own ship's travel.");

		Cfg.VerboseLogging = Config.Bind("4. Diagnostics", "VerboseLogging", true,
			"Log what the mod is actually doing on every trip. Leave on until you have confirmed "
			+ "the mod works, then turn it off.");

		Cfg.Ready = true;

		LogEnvironment();
		ApplyPatches();
		WarpDriveHost.Spawn();

		Cfg.Log.LogInfo($"Warp Drive 1.0.4 loaded. Enabled={Cfg.Enabled.Value}, "
			+ $"real x{Cfg.RealTimeSpeedMultiplier.Value:0.##}, in-game x{Cfg.InGameTimeMultiplier.Value:0.##}, "
			+ $"toggle={Cfg.ToggleKey.Value}");
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
			_harmony.PatchAll(typeof(TravelPatches));

			var patched = new List<MethodBase>(_harmony.GetPatchedMethods());
			if (patched.Count == 0)
			{
				Cfg.Log.LogError("[diag] Harmony reported ZERO patched methods. The mod cannot work; "
					+ "the game's travel code did not match what this build expects.");
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

	// --- lifecycle diagnostics -------------------------------------------------
	// These exist to prove whether BepInEx's plugin component survives gameplay. The mod's actual
	// behaviour no longer depends on it either way.

	private void Update()
	{
		if (Cfg.Verbose && Time.realtimeSinceStartup >= _nextHeartbeat)
		{
			_nextHeartbeat = Time.realtimeSinceStartup + 30f;
			Cfg.Log.LogInfo("[diag] plugin component alive.");
		}
	}

	private void OnDestroy()
	{
		Cfg.Log?.LogWarning("[diag] plugin component was DESTROYED.");
	}
}

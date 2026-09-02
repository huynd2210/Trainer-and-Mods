using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QM.MapReveal;

[BepInPlugin(Guid, "Quasimorph Map Reveal", "1.0.0")]
public class MapRevealPlugin : BaseUnityPlugin
{
	public const string Guid = "com.claude.quasimorph.mapreveal";

	/// <summary>
	/// Distinct game methods <see cref="MapRevealPatches"/> is expected to patch:
	/// MinimapScreen.OnEnable, ObjectiveObstacle.get_ShowMark and FogOfWar.RefreshMinimap.
	/// Checking the exact count rather than merely "more than zero" is what catches a single
	/// target having been renamed by a game update while the others still match.
	/// </summary>
	private const int ExpectedPatchedMethods = 3;

	private Harmony _harmony;

	private void Awake()
	{
		Cfg.Log = Logger;

		Cfg.Enabled = Config.Bind("1. General", "Enabled", true,
			"Master switch. When off, the map behaves exactly as in the unmodded game.");

		Cfg.ToggleKey = Config.Bind("1. General", "ToggleKey", new KeyboardShortcut(KeyCode.F10),
			"Turns the mod on and off while you play. Reopen the map to see the change.");

		Cfg.ShowExits = Config.Bind("2. Reveal", "ShowExits", true,
			"Always draw elevators and inter-floor ladders on the map, including ones you have not "
			+ "walked to yet. This is the same reveal a Scanner station gives you when you pay it "
			+ "to scan for exits.");

		Cfg.ShowObjectives = Config.Bind("2. Reveal", "ShowObjectives", true,
			"Also reveal mission objectives that were placed with their map marker switched off. "
			+ "Objectives with a marker are already shown by the unmodded game, so on most missions "
			+ "this setting changes nothing — hence 'if applicable'. Scripted story triggers are "
			+ "left alone either way so staged reveals are not spoiled.");

		Cfg.VerboseLogging = Config.Bind("3. Diagnostics", "VerboseLogging", true,
			"Log what the mod reveals each time you open the map. Leave on until you have confirmed "
			+ "the mod works, then turn it off.");

		Cfg.Ready = true;

		LogEnvironment();
		ApplyPatches();
		MapRevealHost.Spawn();

		Cfg.Log.LogInfo($"Map Reveal 1.0.0 loaded. Enabled={Cfg.Enabled.Value}, "
			+ $"exits={Cfg.ShowExits.Value}, objectives={Cfg.ShowObjectives.Value}, "
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
			_harmony.PatchAll(typeof(MapRevealPatches));

			var patched = new List<MethodBase>(_harmony.GetPatchedMethods());

			Cfg.Log.LogInfo($"[diag] Harmony patched {patched.Count} method(s):");
			foreach (MethodBase m in patched)
			{
				Cfg.Log.LogInfo($"[diag]   {m.DeclaringType?.FullName}.{m.Name}");
			}

			if (patched.Count != ExpectedPatchedMethods)
			{
				Cfg.Log.LogError($"[diag] Expected to patch {ExpectedPatchedMethods} method(s) but "
					+ $"patched {patched.Count}. Some of the game's map code no longer matches what "
					+ "this build expects, so the mod is at best partly working.");
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Patching failed, the mod will do nothing: " + e);
		}
	}

	private void OnDestroy()
	{
		// Expected: the game destroys BepInEx plugin components shortly after startup. The mod's
		// behaviour does not depend on this component surviving.
		Cfg.Log?.LogWarning("[diag] plugin component was DESTROYED.");
	}
}

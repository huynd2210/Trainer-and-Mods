using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QM.OperatorBoost;

[BepInPlugin(Guid, "Quasimorph Operator Boost", Version)]
public class OperatorBoostPlugin : BaseUnityPlugin
{
	public const string Guid = "com.claude.quasimorph.operatorboost";
	public const string Version = "1.0.0";

	private Harmony _harmony;
	private float _nextHeartbeat;

	private void Awake()
	{
		Cfg.Log = Logger;

		Cfg.Enabled = Config.Bind("1. General", "Enabled", true,
			"Master switch. When off, every boost reports zero and your operators have exactly "
			+ "their unmodded stats.");

		Cfg.ToggleKey = Config.Bind("1. General", "ToggleKey", new KeyboardShortcut(KeyCode.F10),
			"Turns the mod on and off while you play.");

		Cfg.ShowStatusOverlay = Config.Bind("1. General", "ShowStatusOverlay", true,
			"Show the boosts currently in effect in the bottom-left corner.");

		Cfg.OnlyOperatorInRaid = Config.Bind("1. General", "OnlyOperatorInRaid", false,
			"When on, only the operator you have deployed is boosted and the rest of the roster is "
			+ "left alone. When off, every operator you own is boosted.");

		Cfg.GrantHealthOnMaxIncrease = Config.Bind("1. General", "GrantHealthOnMaxIncrease", true,
			"When the maximum health boost raises the ceiling, hand the operator the extra hit "
			+ "points too. With this off you gain the headroom but have to heal into it.");

		Cfg.VerboseLogging = Config.Bind("6. Diagnostics", "VerboseLogging", false,
			"Log what the mod is doing. Turn this on if a boost does not appear to work.");

		Boosts.Bind(Config);

		Cfg.Ready = true;

		LogEnvironment();
		ApplyPatches();
		OperatorBoostHost.Spawn();

		List<string> active = Boosts.ActiveSummary();
		Cfg.Log.LogInfo($"Operator Boost {Version} loaded. Enabled={Cfg.Enabled.Value}, "
			+ $"toggle={Cfg.ToggleKey.Value}, boosts set: "
			+ (active.Count == 0 ? "none yet — edit BepInEx/config/" + Guid + ".cfg"
				: string.Join(", ", active.ToArray())));
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

			// Each boost is a nested class inside StatPatches, and PatchAll(Type) only looks at
			// the annotations on the type it is handed — it does not descend into nested types,
			// which silently patches nothing at all. Walk them explicitly instead.
			var patchClasses = new List<Type>();
			foreach (Type container in new[] { typeof(StatPatches), typeof(InventoryPatches) })
			{
				patchClasses.AddRange(container.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));
			}
			foreach (Type patchClass in patchClasses)
			{
				_harmony.CreateClassProcessor(patchClass).Patch();
			}

			var patched = new List<MethodBase>(_harmony.GetPatchedMethods());
			if (patched.Count == 0)
			{
				Cfg.Log.LogError("[diag] Harmony reported ZERO patched methods. The mod cannot work; "
					+ "the game's stat code did not match what this build expects.");
				return;
			}

			if (patched.Count != patchClasses.Count)
			{
				Cfg.Log.LogError($"[diag] Only {patched.Count} of {patchClasses.Count} boosts attached. "
					+ "The boosts missing from the list below will do nothing.");
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
	// The mod's behaviour does not depend on this component surviving; these only record whether
	// it does. Everything that has to keep running lives on OperatorBoostHost.

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

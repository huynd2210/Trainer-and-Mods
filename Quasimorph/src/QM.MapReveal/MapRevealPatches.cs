using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QM.MapReveal;

/// <summary>
/// Forces the map screen to show the two things you most want to know and normally have to walk
/// into the dark to find: the way out, and the mission objective.
///
/// Both are done by reusing the game's own rendering rather than drawing anything new, so the
/// icons, colours and positions are exactly the ones the game already uses when a scanner reveals
/// them. Nothing here touches gameplay — see the notes on each patch.
/// </summary>
[HarmonyPatch]
internal static class MapRevealPatches
{
	// --- exits ---------------------------------------------------------------------------------
	//
	// FogOfWar.RefreshMinimapExits skips any ladder or elevator whose cell is not yet explored,
	// unless it is passed forceShowExits. MinimapScreen.OnEnable sources that argument from
	// LocationMetadata.ScanExit — the flag a Scanner station sets when you pay it to scan for
	// exits — and calls RefreshMinimap as its very first statement. Setting the flag in a prefix
	// therefore reveals the exits through the stock code path.
	//
	// This also switches on the hover labels: MinimapScreen.RefreshLabelUnderCursor gates the
	// "Elevator" / "Ladder up" / "Ladder down" text on the same flag, so the markers get names
	// rather than being anonymous icons.
	//
	// The flag is [Save], but MinimapScreen.OnDisable clears all three scan flags when the map
	// closes, so the mod leaves nothing behind in the save file.

	[HarmonyPatch(typeof(MinimapScreen), "OnEnable")]
	[HarmonyPrefix]
	private static void ForceExitScanOnMapOpen(MinimapScreen __instance)
	{
		try
		{
			if (!Cfg.Active || !Cfg.ShowExits.Value)
			{
				return;
			}

			LocationMetadata location = ReadLocationMetadata(__instance);
			if (location == null)
			{
				// [Inject(false)] — absent outside a raid. Nothing to reveal.
				return;
			}

			location.ScanExit = true;

			if (Cfg.Verbose)
			{
				Cfg.Log.LogInfo("[diag] map opened; forced ScanExit so elevators and ladders are drawn.");
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("ForceExitScanOnMapOpen failed: " + e);
		}
	}

	// --- objectives ----------------------------------------------------------------------------
	//
	// Mission objectives are already drawn regardless of exploration, but only when the obstacle's
	// ObjectiveObstacle.ShowMark is true. That is a per-prefab authored field, so objectives placed
	// with the mark switched off stay invisible for the whole raid. Forcing it true reveals them.
	//
	// This is safe: ShowMark is read in exactly two places in the entire game — the minimap bake in
	// FogOfWar.RefreshMinimap and the hover label in MinimapScreen.RefreshLabelUnderCursor. It
	// feeds no win condition, no AI and no scoring, so overriding it cannot change how a mission
	// plays out, only what you can see on the map.
	//
	// Deliberately NOT extended to TriggerCell.showMark, which looks similar and is drawn by the
	// same loop. Those marks are driven by mission scripts as the story progresses (the Interplan
	// portal room, for instance, un-marks itself once you have opened the portal). Forcing them all
	// on would put a marker over every scripted trigger zone on the map and spoil staged reveals.

	[HarmonyPatch(typeof(ObjectiveObstacle), nameof(ObjectiveObstacle.ShowMark), MethodType.Getter)]
	[HarmonyPostfix]
	private static void AlwaysShowObjectiveMark(ref bool __result)
	{
		try
		{
			if (Cfg.Active && Cfg.ShowObjectives.Value)
			{
				__result = true;
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("AlwaysShowObjectiveMark failed: " + e);
		}
	}

	/// <summary>
	/// Objectives whose backing field this run temporarily set to true, and must set back.
	/// </summary>
	private static readonly List<ObjectiveObstacle> _forcedMarks = new List<ObjectiveObstacle>();

	private static FieldInfo _showMarkField;
	private static FieldInfo _locationMetadataField;
	private static FieldInfo _mapObstaclesField;

	/// <summary>
	/// Backstop for <see cref="AlwaysShowObjectiveMark"/>.
	///
	/// ShowMark is a one-line auto-property, exactly the shape Mono is free to inline into its
	/// caller — and where that happens the postfix on the getter never runs, so the objective would
	/// silently stay hidden. Writing the backing field directly cannot be inlined away. The value
	/// is restored in a finalizer immediately afterwards, so nothing observes the change except the
	/// bake itself, and the field never reaches the save file in a modified state.
	/// </summary>
	[HarmonyPatch(typeof(FogOfWar), nameof(FogOfWar.RefreshMinimap))]
	[HarmonyPrefix]
	private static void RevealHiddenObjectives(FogOfWar __instance)
	{
		_forcedMarks.Clear();

		try
		{
			if (!Cfg.Active || !Cfg.ShowObjectives.Value)
			{
				return;
			}

			MapObstacles obstacles = ReadMapObstacles(__instance);
			if (obstacles == null)
			{
				return;
			}

			_showMarkField ??= AccessTools.Field(typeof(ObjectiveObstacle), "_showMark");
			if (_showMarkField == null)
			{
				Cfg.Log.LogError("Could not find ObjectiveObstacle._showMark; "
					+ "hidden objectives will only be revealed if the getter patch applies.");
				return;
			}

			foreach (ObjectiveObstacle objective in obstacles.Objectives)
			{
				// Unity null check, intended: skips objectives whose GameObject has been destroyed.
				if (objective == null || (bool)_showMarkField.GetValue(objective))
				{
					continue;
				}

				_showMarkField.SetValue(objective, true);
				_forcedMarks.Add(objective);
			}

			if (_forcedMarks.Count > 0 && Cfg.Verbose)
			{
				Cfg.Log.LogInfo($"[diag] revealed {_forcedMarks.Count} objective(s) that were "
					+ "authored with the map mark switched off.");
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("RevealHiddenObjectives failed: " + e);
		}
	}

	[HarmonyPatch(typeof(FogOfWar), nameof(FogOfWar.RefreshMinimap))]
	[HarmonyFinalizer]
	private static void RestoreHiddenObjectives()
	{
		// A finalizer also runs when the original method throws, so a forced mark cannot get stuck
		// on and be written out by a later save.
		try
		{
			foreach (ObjectiveObstacle objective in _forcedMarks)
			{
				if (objective != null)
				{
					_showMarkField.SetValue(objective, false);
				}
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("RestoreHiddenObjectives failed: " + e);
		}
		finally
		{
			_forcedMarks.Clear();
		}
	}

	// --- reflection helpers --------------------------------------------------------------------
	// Both fields are [Inject(false)] privates, so they are null until a raid is running.

	private static LocationMetadata ReadLocationMetadata(MinimapScreen screen)
	{
		_locationMetadataField ??= AccessTools.Field(typeof(MinimapScreen), "_locationMetadata");
		return _locationMetadataField?.GetValue(screen) as LocationMetadata;
	}

	private static MapObstacles ReadMapObstacles(FogOfWar fogOfWar)
	{
		_mapObstaclesField ??= AccessTools.Field(typeof(FogOfWar), "_mapObstacles");
		return _mapObstaclesField?.GetValue(fogOfWar) as MapObstacles;
	}
}

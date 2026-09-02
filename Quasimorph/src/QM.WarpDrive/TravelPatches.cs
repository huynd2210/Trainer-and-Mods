using System;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QM.WarpDrive;

/// <summary>
/// Two independent knobs on the same journey:
///
/// * <see cref="ScaleFlightTime"/> shortens <c>TravelMetadata.FlightTime</c>, the number of REAL
///   seconds the flight animation runs for.
/// * <see cref="ScaleTravelHours"/> scales the result of
///   <c>TravelSystem.GetTravelHoursBetweenPoints</c>, the number of IN-GAME hours the trip costs.
///
/// The in-game clock during a flight is driven by
/// <c>TravelStartTime.AddHours(TravelHoursDuration * progress)</c>, so the two really are
/// independent: making the animation shorter does not refund any calendar time, and vice versa.
///
/// Everything here reads <see cref="Cfg"/> rather than the plugin MonoBehaviour, deliberately —
/// see the note on that class.
/// </summary>
[HarmonyPatch]
internal static class TravelPatches
{
	/// <summary>
	/// Travel progress is computed as <c>(TravelTimer.Time - 5) / (FlightTime - 5)</c>, and the
	/// preceding ExitingOrbit state does not end until <c>TravelTimer.Time > 5</c>. A FlightTime at
	/// or below 5 therefore divides by zero (or goes negative) and the ship never arrives, so the
	/// value is hard-floored here regardless of what the config says.
	/// </summary>
	private const float MinFlightTimeSeconds = 6f;

	/// <summary>Set while the game is scheduling a station-to-station delivery rather than our own travel.</summary>
	private static bool _inShippingCalculation;

	/// <summary>
	/// Whether <see cref="ScaleTravelHours"/> actually ran during the current call to
	/// StartSpaceshipTravel. Mono is free to inline a small static method like
	/// GetTravelHoursBetweenPoints into its caller, in which case Harmony's patch on the standalone
	/// method never executes for the travel path and the trip would cost full price. When that
	/// happens we fall back to scaling the field directly.
	/// </summary>
	private static bool _hoursPatchFired;

	private static float HoursMultiplier => Mathf.Clamp01(Cfg.InGameTimeMultiplier.Value);

	[HarmonyPatch(typeof(TravelSystem), nameof(TravelSystem.StartSpaceshipTravel))]
	[HarmonyPrefix]
	private static void BeforeTravelStarts()
	{
		_hoursPatchFired = false;
	}

	[HarmonyPatch(typeof(TravelSystem), nameof(TravelSystem.StartSpaceshipTravel))]
	[HarmonyPostfix]
	private static void ScaleFlightTime(TravelMetadata travelData)
	{
		try
		{
			if (travelData == null)
			{
				return;
			}

			if (!Cfg.Active)
			{
				Cfg.Log?.LogInfo("[diag] Travel started but Warp Drive is disabled; leaving it alone.");
				return;
			}

			// --- real seconds spent watching the flight ---
			float originalFlight = travelData.FlightTime;
			float scaledFlight;

			if (Cfg.InstantTravel.Value)
			{
				scaledFlight = MinFlightTimeSeconds;
				travelData.FlightTime = scaledFlight;
				SkipFlightAnimation(travelData);
			}
			else
			{
				float multiplier = Mathf.Clamp(Cfg.RealTimeSpeedMultiplier.Value, 1f, 50f);
				scaledFlight = Mathf.Max(originalFlight / multiplier, MinFlightTimeSeconds);
				travelData.FlightTime = scaledFlight;
			}

			// --- in-game hours the trip costs ---
			double hoursNow = travelData.TravelHoursDuration;
			if (!_hoursPatchFired)
			{
				// The postfix on GetTravelHoursBetweenPoints did not run, so the game used an
				// inlined copy. Scale the stored value instead.
				travelData.TravelHoursDuration = hoursNow * HoursMultiplier;
				Cfg.Log.LogWarning(
					"[diag] GetTravelHoursBetweenPoints was inlined by Mono; scaled TravelHoursDuration "
					+ $"directly ({hoursNow:0.##}h -> {travelData.TravelHoursDuration:0.##}h). Orbit "
					+ "positions were already advanced using the unscaled figure.");
			}

			string mode = Cfg.InstantTravel.Value
				? "INSTANT (animation skipped)"
				: $"animation {originalFlight:0.#}s -> {scaledFlight:0.#}s";

			Cfg.Log.LogInfo(
				$"Travel to '{travelData.TargetSpaceObject}': {mode}, "
				+ $"cost {travelData.TravelHoursDuration:0.##} in-game hours "
				+ $"at time multiplier x{HoursMultiplier:0.##} "
				+ $"(satellite hop: {travelData.IsSateliteFlight}).");
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("ScaleFlightTime failed: " + e);
		}
	}

	/// <summary>
	/// Fast-forwards the travel timer so the whole flight is over within a few frames.
	///
	/// The state machine is gated on elapsed time, not on distance: ExitingOrbit ends once
	/// <c>TravelTimer.Time > 5</c>, and Travelling finishes when
	/// <c>(Time - 5) / (FlightTime - 5)</c> reaches 1. Winding the timer forward to just short of
	/// arrival lets every stage run in order — path building, orbit exit, arrival, the space event
	/// roll — without waiting out the animation. Stopping a hair short rather than at the end
	/// matters: it leaves the ship positioned at ~95% along its path, so the final "smooth move"
	/// onto the orbit point has almost no distance to cover. Jumping straight to 100% would strand
	/// the ship at its departure point and make that move crawl the entire way at fixed speed.
	/// </summary>
	private static void SkipFlightAnimation(TravelMetadata travelData)
	{
		float desired = MinFlightTimeSeconds - 0.05f;

		// Timer.AddTime works by moving _startTime backwards, and Timer.Started is `_startTime > 0`.
		// Winding back past the current Time.time would make the timer report itself as never
		// started, ProcessSpaceshipTravel would skip it entirely, and the trip would hang forever.
		float safe = Mathf.Min(desired, Time.time - 1f);

		if (safe <= 0f)
		{
			Cfg.Log.LogWarning("[diag] Too early in the session to skip the flight safely; "
				+ "playing it at minimum length instead.");
			return;
		}

		travelData.TravelTimer.AddTime(safe);
	}

	[HarmonyPatch(typeof(TravelSystem), nameof(TravelSystem.GetTravelHoursBetweenPoints))]
	[HarmonyPostfix]
	private static void ScaleTravelHours(ref double __result)
	{
		try
		{
			_hoursPatchFired = true;

			if (!Cfg.Active)
			{
				return;
			}

			if (_inShippingCalculation && !Cfg.AffectShippingDeliveries.Value)
			{
				return;
			}

			__result *= HoursMultiplier;
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("ScaleTravelHours failed: " + e);
		}
	}

	// ShippingSystem.ShipToStation reuses GetTravelHoursBetweenPoints to pick a delivery date for
	// goods moving between stations. That is not the player's ship travelling, so it is fenced off
	// behind its own config option instead of being swept up by the multiplier.

	[HarmonyPatch(typeof(ShippingSystem), nameof(ShippingSystem.ShipToStation))]
	[HarmonyPrefix]
	private static void EnterShippingCalculation()
	{
		_inShippingCalculation = true;
	}

	[HarmonyPatch(typeof(ShippingSystem), nameof(ShippingSystem.ShipToStation))]
	[HarmonyFinalizer]
	private static void ExitShippingCalculation()
	{
		// A finalizer also runs when the original method throws, so the flag cannot get stuck on.
		_inShippingCalculation = false;
	}
}

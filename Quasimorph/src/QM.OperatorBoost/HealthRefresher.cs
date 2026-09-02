using System;
using MGSC;
using UnityEngine;

namespace QM.OperatorBoost;

/// <summary>
/// Maximum health is the one boosted stat the game does not recompute on demand: it caches the
/// result in <c>HealthInfo.MaxValue</c> and only refreshes it when something structural changes,
/// such as installing an augmentation. So this recomputes it on a timer, using exactly the
/// formula <c>AugmentationSystem.UpdateMaxHealth</c> uses.
///
/// Two things follow from doing it this way. <c>BaseHealth</c> is never written, so the operator's
/// real health is still in the save untouched. And because the formula reads the patched
/// <c>GetMaxHealthBonus</c>, which reports zero while the mod is off, this same pass is what puts
/// maximum health back to stock when the boost is lowered, cleared or the mod is disabled.
/// </summary>
internal static class HealthRefresher
{
	private const float IntervalSeconds = 1f;

	private static float _nextRun;

	public static void Tick()
	{
		if (!Cfg.Ready || Time.realtimeSinceStartup < _nextRun)
		{
			return;
		}
		_nextRun = Time.realtimeSinceStartup + IntervalSeconds;

		try
		{
			foreach (Mercenary mercenary in OperatorRoster.Current)
			{
				Apply(mercenary.CreatureData);
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Max health refresh failed: " + e);
		}
	}

	private static void Apply(CreatureData creatureData)
	{
		HealthInfo health = creatureData?.Health;
		if (health == null || health.Dead)
		{
			return;
		}

		float woundPenalty = creatureData.EffectsController
			.SumEffectsValue((WoundEffectMaxHealth w) => w.MaxHealthPenalty);
		int target = Mathf.Max(1, Mathf.RoundToInt(
			creatureData.BaseHealth + woundPenalty + creatureData.GetMaxHealthBonus()));

		int delta = target - health.MaxValue;
		if (delta == 0)
		{
			return;
		}

		// Raising the ceiling on its own leaves current health where it was, which reads as the
		// boost having done nothing. Hand over the difference so the extra hit points are usable.
		health.ReinitializePreservingCurrent(target);
		if (delta > 0 && Cfg.GrantHealthOnMaxIncrease.Value)
		{
			health.Restore(delta, ignoreHealMultiplier: true);
		}

		if (Cfg.Verbose)
		{
			Cfg.Log.LogInfo($"[health] {creatureData} max health {health.MaxValue - delta} -> {target}");
		}
	}
}

using HarmonyLib;

namespace MagicalPrincessTrainer
{
	/// <summary>
	/// Battle hooks. In this game the damage methods run *on the victim* - `__instance` is who
	/// gets hurt, `_target` is the attacker - so the side check reads off `__instance`.
	/// </summary>
	[HarmonyPatch(typeof(BattleCharacter), nameof(BattleCharacter.SetPhysicalDamage))]
	internal static class PhysicalDamagePatch
	{
		private static bool Prefix(BattleCharacter __instance, ref float _atk)
		{
			return DamageGate.Allow(__instance, ref _atk);
		}
	}

	[HarmonyPatch(typeof(BattleCharacter), nameof(BattleCharacter.SetMagicalDamage))]
	internal static class MagicalDamagePatch
	{
		private static bool Prefix(BattleCharacter __instance, ref float _batk)
		{
			return DamageGate.Allow(__instance, ref _batk);
		}
	}

	internal static class DamageGate
	{
		/// <summary>False cancels the hit entirely; the attack power is boosted for enemies.</summary>
		internal static bool Allow(BattleCharacter victim, ref float power)
		{
			if (victim == null)
			{
				return true;
			}
			if (Plugin.GodMode && !victim.isEnemySide)
			{
				return false;
			}
			if (Plugin.OneHitKill && victim.isEnemySide)
			{
				power = 999999f;
			}
			return true;
		}
	}

	/// <summary>
	/// Damage is not the only way to lose HP - burn, poison and scripted drain all tick inside
	/// TimeProceed. Topping the party back up here covers every one of them.
	/// </summary>
	[HarmonyPatch(typeof(BattleCharacter), nameof(BattleCharacter.TimeProceed))]
	internal static class TimeProceedPatch
	{
		private static void Postfix(BattleCharacter __instance)
		{
			if (!Plugin.GodMode || __instance == null || __instance.isEnemySide || __instance.isDead)
			{
				return;
			}
			if (__instance.hpMax <= 0f || __instance.hp >= __instance.hpMax)
			{
				return;
			}
			__instance.hp = __instance.hpMax;
			try
			{
				__instance.UpdateStatusInfo();
			}
			catch (System.Exception)
			{
				// The HP bar just stays stale for a frame; not worth failing the hook over.
			}
		}
	}

	/// <summary>Forces every activity roll (work, training, dates, crafting) to succeed.</summary>
	[HarmonyPatch(typeof(ActivityData), nameof(ActivityData.IsActivitySucceed))]
	internal static class ActivitySuccessPatch
	{
		private static void Postfix(ref bool __result)
		{
			if (Plugin.AlwaysSucceed)
			{
				__result = true;
			}
		}
	}
}

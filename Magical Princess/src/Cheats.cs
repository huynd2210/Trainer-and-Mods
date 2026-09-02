using System;
using UnityEngine;

namespace MagicalPrincessTrainer
{
	/// <summary>
	/// The actual edits. Each method is a no-op unless a run is loaded, and each one ends by
	/// asking the game to recalculate so the on-screen values update immediately.
	/// </summary>
	internal static class Cheats
	{
		/// <summary>The 16 sub-attributes, in the order the status screen groups them.</summary>
		internal static readonly string[] Attributes =
		{
			"phyKinryoku", "phySeimei", "phyKonjyo", "phyBinsho",
			"intBungaku", "intSanjyutsu", "intMajyutsu", "intShinkou",
			"chaBibou", "chaShakou", "chaReigi", "chaDoutoku",
			"senSouzou", "senSousaku", "senOnkan", "senBikan"
		};

		private const int AttributeCap = 9999;

		internal static bool AddMoney(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.money = Mathf.Max(0, s.money + amount);
			if (amount > 0)
			{
				s.moneyGetTotal += amount;
			}
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_MONEY);
			return true;
		}

		internal static bool AddBlackCoin(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.blackCoin = Mathf.Max(0, s.blackCoin + amount);
			try
			{
				// The shop reads the coin count off item 126; keep the two in sync.
				ItemData coin = Game.Data.GetItemDataFromItemId(126);
				if (coin != null && coin.data != null)
				{
					coin.data.count = s.blackCoin;
				}
			}
			catch (Exception)
			{
				// Item table not loaded - status.blackCoin is still the authority.
			}
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_MONEY);
			return true;
		}

		internal static bool ClearStress()
		{
			if (!Game.Ready)
			{
				return false;
			}
			Game.Status.stress = 0;
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool RefillActivePower()
		{
			if (!Game.Ready)
			{
				return false;
			}
			Game.Status.activePower = Game.Data.GetActivePowerMax();
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool AddAllAttributes(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			foreach (string field in Attributes)
			{
				int value = (int)s[field];
				s[field] = Mathf.Clamp(value + amount, 0, AttributeCap);
			}
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool AddSkillPoints(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.skillPoint = Mathf.Max(0, s.skillPoint + amount);
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool AddBattleExp(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.btlExp = Mathf.Max(0, s.btlExp + amount);
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool AddFatherFavour(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.fatherFavarite = Mathf.Max(0, s.fatherFavarite + amount);
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		internal static bool AddReputation(int amount)
		{
			if (!Game.Ready)
			{
				return false;
			}
			BasicStatusData s = Game.Status;
			s.reputation = Mathf.Max(0, s.reputation + amount);
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		/// <summary>Raises affection for every friend you have already met.</summary>
		internal static bool AddFriendFavour(int amount)
		{
			if (!Game.Ready || Game.Data.friendDataList == null)
			{
				return false;
			}
			int touched = 0;
			foreach (FriendData friend in Game.Data.friendDataList)
			{
				if (friend == null || friend.data == null || friend.data.fMeet <= 0)
				{
					continue;
				}
				friend.data.fFavarite = Mathf.Clamp(friend.data.fFavarite + amount, 0, 100);
				touched++;
			}
			if (touched == 0)
			{
				return false;
			}
			Game.Recalculate();
			Game.Beep(SoundType.UI_STATUS_UP);
			return true;
		}

		/// <summary>
		/// Achievement points - the meta-currency you spend on new-run gifts. Works on the title
		/// screen (where the gift menu lives), so it deliberately does not go through Recalculate,
		/// which needs a loaded run.
		/// </summary>
		internal static bool AddAchievementPoints(int amount)
		{
			if (!Game.MetaReady)
			{
				return false;
			}
			GrobalStatusData g = Game.Global;
			g.acvPoint = Mathf.Max(0, g.acvPoint + amount);
			RefreshGiftMenu();
			Game.Beep(SoundType.UI_STATUS_MONEY);
			return true;
		}

		/// <summary>Repaints the new-run gift menu so the budget and affordable items update live.</summary>
		private static void RefreshGiftMenu()
		{
			try
			{
				MenuAchievementGift menu = UnityEngine.Object.FindFirstObjectByType<MenuAchievementGift>();
				if (menu != null && menu.isOpen)
				{
					menu.Refresh();
					menu.CheckGiftCalc();
				}
			}
			catch (Exception)
			{
				// Menu closed or mid-transition; the new total shows when it next opens.
			}
		}

		/// <summary>Called every frame by the freeze toggle; deliberately does no recalculation.</summary>
		internal static void HoldStressAndPower()
		{
			if (!Game.Ready)
			{
				return;
			}
			BasicStatusData s = Game.Status;
			int max = Game.Data.GetActivePowerMax();
			bool changed = false;
			if (s.stress != 0)
			{
				s.stress = 0;
				changed = true;
			}
			if (s.activePower < max)
			{
				s.activePower = max;
				changed = true;
			}
			if (changed)
			{
				Game.RefreshUI();
			}
		}
	}
}

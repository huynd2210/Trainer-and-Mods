using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MagicalPrincessTrainer
{
	[BepInPlugin(Guid, "Magical Princess Trainer", "1.1.0")]
	public class Plugin : BaseUnityPlugin
	{
		internal const string Guid = "com.magicalprincess.trainer";

		internal static ManualLogSource Log;

		private static ConfigEntry<bool> _cfgGodMode;
		private static ConfigEntry<bool> _cfgOneHitKill;
		private static ConfigEntry<bool> _cfgAlwaysSucceed;
		private static ConfigEntry<bool> _cfgFreeze;
		private static ConfigEntry<bool> _cfgOverlay;

		internal static bool GodMode => _cfgGodMode != null && _cfgGodMode.Value;
		internal static bool OneHitKill => _cfgOneHitKill != null && _cfgOneHitKill.Value;
		internal static bool AlwaysSucceed => _cfgAlwaysSucceed != null && _cfgAlwaysSucceed.Value;

		private readonly List<ITrainerCommand> _commands = new List<ITrainerCommand>();
		private ConfigEntry<float> _cfgUiScale;

		private static string _toast = "";
		private static float _toastUntil;

		private Overlay _overlay;

		private void Awake()
		{
			Log = Logger;

			_cfgUiScale = Config.Bind("Overlay", "UiScale", 1f,
				"Extra size multiplier for the on-screen panel (the panel already scales with resolution).");
			_cfgOverlay = Config.Bind("Overlay", "Visible", true, "Panel visible on startup.");
			_cfgGodMode = Config.Bind("Toggles", "GodMode", false, "Your party takes no damage in battle.");
			_cfgOneHitKill = Config.Bind("Toggles", "OneHitKill", false, "Any hit you land kills the enemy.");
			_cfgAlwaysSucceed = Config.Bind("Toggles", "AlwaysSucceedActivities", false,
				"Every activity roll succeeds.");
			_cfgFreeze = Config.Bind("Toggles", "FreezeStressAndPower", false,
				"Holds stress at 0 and action points at maximum, every frame.");

			ConfigEntry<int> money = Config.Bind("Amounts", "Money", 10000, "Added per press.");
			ConfigEntry<int> blackCoin = Config.Bind("Amounts", "BlackCoin", 100, "Added per press.");
			ConfigEntry<int> attributes = Config.Bind("Amounts", "Attributes", 25,
				"Added to each of the 16 sub-attributes per press.");
			ConfigEntry<int> skillPoints = Config.Bind("Amounts", "SkillPoints", 10, "Added per press.");
			ConfigEntry<int> battleExp = Config.Bind("Amounts", "BattleExp", 1000, "Added per press.");
			ConfigEntry<int> fatherFavour = Config.Bind("Amounts", "FatherFavour", 50, "Added per press.");
			ConfigEntry<int> reputation = Config.Bind("Amounts", "Reputation", 50, "Added per press.");
			ConfigEntry<int> friendFavour = Config.Bind("Amounts", "FriendFavour", 20,
				"Added to every friend you have met (capped at 100).");
			ConfigEntry<int> achievementPoints = Config.Bind("Amounts", "AchievementPoints", 250,
				"Added per press. 990 buys the most expensive gift in all 23 categories.");

			_commands.Add(new ToggleCommand("Show panel", Key("ToggleOverlay", KeyCode.F1), _cfgOverlay));
			_commands.Add(new ActionCommand("+" + money.Value.ToString("N0") + " money",
				Key("Money", KeyCode.F2), () => Cheats.AddMoney(money.Value)));
			_commands.Add(new ActionCommand("+" + blackCoin.Value + " black coin",
				Key("BlackCoin", KeyCode.F3), () => Cheats.AddBlackCoin(blackCoin.Value)));
			_commands.Add(new ActionCommand("Clear stress",
				Key("ClearStress", KeyCode.F4), Cheats.ClearStress));
			_commands.Add(new ActionCommand("Refill action points",
				Key("RefillActivePower", KeyCode.F5), Cheats.RefillActivePower));
			_commands.Add(new ActionCommand("+" + attributes.Value + " every attribute",
				Key("Attributes", KeyCode.F6), () => Cheats.AddAllAttributes(attributes.Value)));
			_commands.Add(new ActionCommand("+" + skillPoints.Value + " skill points",
				Key("SkillPoints", KeyCode.F7), () => Cheats.AddSkillPoints(skillPoints.Value)));
			_commands.Add(new ActionCommand("+" + battleExp.Value.ToString("N0") + " battle EXP",
				Key("BattleExp", KeyCode.F8), () => Cheats.AddBattleExp(battleExp.Value)));
			_commands.Add(new ActionCommand("+" + fatherFavour.Value + " father affection",
				Key("FatherFavour", KeyCode.F9), () => Cheats.AddFatherFavour(fatherFavour.Value)));
			_commands.Add(new ActionCommand("+" + reputation.Value + " reputation",
				Key("Reputation", KeyCode.F10), () => Cheats.AddReputation(reputation.Value)));
			_commands.Add(new ActionCommand("+" + friendFavour.Value + " affection, all friends",
				Key("FriendFavour", KeyCode.End), () => Cheats.AddFriendFavour(friendFavour.Value)));
			_commands.Add(new ActionCommand("+" + achievementPoints.Value + " achievement points",
				Key("AchievementPoints", KeyCode.PageUp),
				() => Cheats.AddAchievementPoints(achievementPoints.Value)));
			_commands.Add(new ToggleCommand("God mode", Key("GodMode", KeyCode.F11), _cfgGodMode));
			_commands.Add(new ToggleCommand("One-hit kill", Key("OneHitKill", KeyCode.F12), _cfgOneHitKill));
			_commands.Add(new ToggleCommand("No stress / full AP", Key("Freeze", KeyCode.Insert), _cfgFreeze));
			_commands.Add(new ToggleCommand("Activities always succeed",
				Key("AlwaysSucceed", KeyCode.Home), _cfgAlwaysSucceed));

			_overlay = new Overlay(_commands, _cfgOverlay, _cfgUiScale);

			new Harmony(Guid).PatchAll();
			Log.LogInfo("Magical Princess Trainer loaded - F1 shows the panel.");
		}

		private ConfigEntry<KeyboardShortcut> Key(string name, KeyCode key)
		{
			return Config.Bind("Keys", name, new KeyboardShortcut(key));
		}

		private void Update()
		{
			for (int i = 0; i < _commands.Count; i++)
			{
				if (_commands[i].Key.Value.IsDown())
				{
					_commands[i].Invoke();
				}
			}

			if (_cfgFreeze.Value)
			{
				Cheats.HoldStressAndPower();
			}
		}

		private void OnGUI()
		{
			_overlay.Draw(ToastText);
		}

		internal static void Toast(string message)
		{
			_toast = message;
			_toastUntil = Time.unscaledTime + 2.5f;
			Log.LogInfo(message);
		}

		private static string ToastText => Time.unscaledTime < _toastUntil ? _toast : null;
	}
}

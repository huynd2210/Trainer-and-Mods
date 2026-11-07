using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using PotatoPeeler;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StartingCash
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "dev.potatoplease.startingcash";
        public const string Name = "Potato Please - Starting Cash";
        public const string Version = "2.0.0";

        internal static new ManualLogSource Log { get; private set; }
        internal static ConfigEntry<string> Hotkey { get; private set; }
        internal static ConfigEntry<int> Amount { get; private set; }

        public override void Load()
        {
            Log = base.Log;

            Hotkey = Config.Bind("StartingCash", "Hotkey", "F8",
                "Key that adds the money. Unity Input System key names, e.g. F8, F9, Insert, Backquote.");
            Amount = Config.Bind("StartingCash", "Amount", 10000,
                "Rubles added each time you press the key.");

            AddComponent<CashKey>();
            Log.LogInfo($"{Name} v{Version} loaded. Press {Hotkey.Value} in a game for {Amount.Value:N0} rubles.");
        }
    }

    public sealed class CashKey : MonoBehaviour
    {
        public CashKey(IntPtr pointer) : base(pointer) { }

        private Key _key = Key.F8;

        private void Start()
        {
            if (Enum.TryParse(Plugin.Hotkey.Value, true, out Key parsed) && parsed != Key.None)
                _key = parsed;
            else
                Plugin.Log.LogWarning($"Unknown key '{Plugin.Hotkey.Value}', using F8.");
        }

        private void Update()
        {
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard == null || !keyboard[_key].wasPressedThisFrame) return;

                var economy = EconomyManager.Instance;
                if (economy == null)
                {
                    Plugin.Log.LogInfo($"{_key} pressed outside a game; load a save first.");
                    return;
                }

                long before = economy.Balance;
                // SetBalance rather than AddIncome: the money should not count as earnings in the stats.
                economy.SetBalance(before + Plugin.Amount.Value * Currency.UnitsPerRuble);
                Plugin.Log.LogInfo($"Added {Currency.Format(economy.Balance - before)}: {Currency.Format(before)} -> {Currency.Format(economy.Balance)}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Adding money failed: {e}");
            }
        }
    }
}

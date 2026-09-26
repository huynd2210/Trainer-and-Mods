using System;
using BepInEx;
using BepInEx.Configuration;
using Restory.Gameplay.Inventory;
using UnityEngine;

namespace MoreMoney;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.restory.moremoney";
    public const string Name = "MoreMoney";
    public const string Version = "1.0.0";

    private ConfigEntry<KeyboardShortcut> addKey;
    private ConfigEntry<int> amount;

    // The game's Wallet is a scene MonoBehaviour bound as a Zenject singleton;
    // it is recreated when a save is loaded, so the cache is re-resolved when Unity nulls it.
    private Wallet wallet;

    private void Awake()
    {
        addKey = Config.Bind("General", "AddMoneyKey", new KeyboardShortcut(KeyCode.F8),
            "Press to add money to your wallet.");
        amount = Config.Bind("General", "Amount", 10000,
            new ConfigDescription("Yen added per key press.", new AcceptableValueRange<int>(1, 100_000_000)));

        Logger.LogInfo($"{Name} {Version} loaded. Press {addKey.Value} in game to add ¥{amount.Value:N0}.");
    }

    private void Update()
    {
        if (!addKey.Value.IsDown()) return;

        if (!wallet) wallet = FindFirstObjectByType<Wallet>();
        if (!wallet)
        {
            Logger.LogWarning("No wallet yet - load a save or start a new game first.");
            return;
        }

        // Wallet stores an int; clamp so repeated presses cannot overflow into negative money.
        int before = wallet.MoneyAvailable;
        int after = (int)Math.Min((long)before + amount.Value, int.MaxValue);

        // Init sets the balance and fires OnMoneyAmountChanged, so every money display refreshes.
        // TryToAdd would also fire OnMoneyAdded, which books the cheat as earned income in the
        // game's statistics and earnings achievements - Init keeps those honest.
        wallet.Init(after);
        Logger.LogInfo($"Wallet: ¥{before:N0} -> ¥{after:N0}");
    }
}

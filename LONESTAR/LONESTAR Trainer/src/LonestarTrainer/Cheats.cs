using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace LonestarTrainer
{
    /// <summary>One entry in the trainer. New cheats are added by registering, never by editing a dispatcher.</summary>
    internal interface ICheat
    {
        string Label { get; }
        Key Hotkey { get; }
        /// <summary>Fired when the hotkey is pressed.</summary>
        void Activate();
        /// <summary>Text for the overlay, or null to stay out of it.</summary>
        string StatusText { get; }
        /// <summary>True when this cheat is currently changing the game.</summary>
        bool IsActive { get; }
    }

    /// <summary>A cheat that is either on or off, backed by a field on <see cref="TrainerConfig"/>.</summary>
    internal sealed class ToggleCheat : ICheat
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;

        public string Label { get; }
        public Key Hotkey { get; }

        public ToggleCheat(string label, Key hotkey, Func<bool> get, Action<bool> set)
        {
            Label = label;
            Hotkey = hotkey;
            _get = get;
            _set = set;
        }

        public bool IsActive => _get();
        public string StatusText => Label;

        public void Activate()
        {
            bool next = !_get();
            _set(next);
            TrainerMod.Instance?.SaveModSettings();
            Overlay.Flash(Label + ": " + (next ? "ON" : "OFF"));
        }
    }

    /// <summary>A cheat that does something once when its hotkey is pressed.</summary>
    internal sealed class ActionCheat : ICheat
    {
        private readonly Action _action;

        public string Label { get; }
        public Key Hotkey { get; }

        public ActionCheat(string label, Key hotkey, Action action)
        {
            Label = label;
            Hotkey = hotkey;
            _action = action;
        }

        public bool IsActive => false;
        public string StatusText => null;

        public void Activate() => _action();
    }

    internal static class CheatRegistry
    {
        public static readonly List<ICheat> All = new List<ICheat>();

        /// <summary>
        /// Toggles read and write through TrainerMod.Config rather than capturing a
        /// TrainerConfig: the game's settings panel swaps the instance out whenever the
        /// player hits Reload or Reset, and a captured one would go stale.
        /// </summary>
        public static void Build()
        {
            All.Clear();

            All.Add(new ToggleCheat("God Mode", Key.F1,
                () => Cfg()?.godMode == true, v => { TrainerConfig c = Cfg(); if (c != null) c.godMode = v; }));

            All.Add(new ToggleCheat("One-Hit Kill", Key.F2,
                () => Cfg()?.oneHitKill == true, v => { TrainerConfig c = Cfg(); if (c != null) c.oneHitKill = v; }));

            All.Add(new ToggleCheat("Infinite Star Coins", Key.F3,
                () => Cfg()?.infiniteCoins == true, v => { TrainerConfig c = Cfg(); if (c != null) c.infiniteCoins = v; }));

            All.Add(new ToggleCheat("No Hull Loss Outside Battle", Key.F4,
                () => Cfg()?.noHullLossOutsideBattle == true, v => { TrainerConfig c = Cfg(); if (c != null) c.noHullLossOutsideBattle = v; }));

            All.Add(new ActionCheat("Repair Hull To Full", Key.F5, GameActions.RepairToFull));
            All.Add(new ActionCheat("Win Current Battle", Key.F6, GameActions.WinBattle));
            All.Add(new ActionCheat("+1 Cargo Slot", Key.F7, GameActions.AddCargoSlot));
            All.Add(new ActionCheat("+250 Star Coins", Key.F8, GameActions.AddCoins));
            All.Add(new ActionCheat("+1 Move", Key.F9, GameActions.AddMove));
        }

        private static TrainerConfig Cfg() => TrainerMod.Config;
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using FallenAces;
using FallenAces.HUD;
using FallenAces.NewWorldGen;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

namespace FallenAcesTrainer
{
    // Safe accessors for the pieces of the game a cheat needs. Every cheat runs from
    // Update or OnGUI, where any of these can legitimately be absent (main menu, level
    // transition, player death), so each one is a Try* rather than a plain property.
    internal static class Game
    {
        public static bool TryPlayer(out Player player)
        {
            return Player.TryGetPlayer(out player) && player != null;
        }

        public static bool TryMovement(out PlayerMovementHandler movement)
        {
            movement = TryPlayer(out var player) ? player.MovementHandler : null;
            return movement != null;
        }

        public static bool TryHealth(out Health health)
        {
            health = null;
            return TryPlayer(out var player) && player.TryGetHealth(out health) && health != null;
        }

        // The game's own notification banner, so trainer feedback reads in the same
        // voice as "Picked up a key" instead of as a bolted-on mod overlay.
        public static void Notify(string message)
        {
            if (!GlobalEventManager.TryGet(out var events, createIfNoneExist: true)) return;
            var arguments = events.ReusableArguments;
            arguments.AddArgumentAsObject(message);
            arguments.AddArgumentAsInt((int)NotificationHandler.MessageType.Standard);
            events.TriggerEvent((int)GlobalEventManager.Event.ShowMessageOnHud, arguments);
        }

        // True while the pause/main menu or the game's developer console owns the screen.
        // Value request ids 3 and 4 are MainMenuManager.OnRequestAnyMenuIsOpen and
        // DeveloperConsole.OnRequestDeveloperConsoleIsOpen.
        public static bool MenuIsOpen()
        {
            return GlobalEventManager.TryGet(out var events)
                && (events.RequestValue<bool>(3) || events.RequestValue<bool>(4));
        }

        public static bool InGameplay()
        {
            return !WorldLoader.AnyWorldIsLoading && TryPlayer(out _);
        }
    }

    // Keys that already do something in this game folder, and who owns them. Checked
    // rather than remembered, because a default that lands on one of these fires in two
    // places at once and neither side reports it.
    //
    // The game's own list comes from its input asset. The four arrow keys are bound
    // there to a "Test Move" action that no game code ever reads, so the overlay uses
    // them deliberately and they are absent here.
    internal static class ReservedKeys
    {
        private static readonly Dictionary<Key, string> Owners = Build();

        private static Dictionary<Key, string> Build()
        {
            var owners = new Dictionary<Key, string>();
            Claim("Fallen Aces",
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.A, Key.D, Key.S, Key.W, Key.E, Key.F, Key.G, Key.H, Key.Q, Key.R, Key.X, Key.Z,
                Key.Backquote, Key.CapsLock, Key.LeftCtrl, Key.RightCtrl, Key.Enter, Key.Equals,
                Key.Escape, Key.Minus, Key.LeftShift, Key.RightShift, Key.Space, Key.Tab,
                Key.F1, Key.F2, Key.F3, Key.F4, Key.F6, Key.F9);
            Claim("the SuperHot mod", Key.RightBracket);
            Claim("the Kill Tracker mod", Key.F8);
            Claim("the Expanded Inventory mod",
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0);
            return owners;

            void Claim(string owner, params Key[] keys)
            {
                foreach (var key in keys) owners[key] = owner;
            }
        }

        // Returns null when the key is free.
        public static string OwnerOf(Key key)
        {
            return Owners.TryGetValue(key, out var owner) ? owner : null;
        }
    }

    // A single hotkey. Features own their bindings, so adding a cheat never edits a
    // central key table.
    internal sealed class Binding
    {
        public Binding(string name, Key defaultKey, string description, Action press)
        {
            Name = name;
            DefaultKey = defaultKey;
            Description = description;
            Press = press;
        }

        public string Name { get; }
        public Key DefaultKey { get; }
        public string Description { get; }
        public Action Press { get; }
        public ConfigEntry<Key> Hotkey { get; set; }

        public Key Current => Hotkey != null ? Hotkey.Value : DefaultKey;
    }

    // One trainer entry. The concrete kinds below are deliberately few and fixed; the
    // open set is the cheat registry in TrainerCheats, where a new cheat is one more
    // element and nothing else changes.
    internal abstract class Feature
    {
        protected Feature(string section, string id, string label, string description)
        {
            Section = section;
            Id = id;
            Label = label;
            Description = description;
        }

        public string Section { get; }
        public string Id { get; }
        public string Label { get; }
        public string Description { get; }

        public abstract IEnumerable<Binding> Bindings { get; }

        // Right arrow in the menu, or the feature's own hotkey.
        public abstract void Increase();

        // Left arrow in the menu. Actions have nothing to decrease.
        public virtual void Decrease() { }

        // Shown in the menu's right-hand column.
        public abstract string Value { get; }

        // Shown in the ambient strip while the menu is closed. Null keeps it out, so the
        // always-on layer only ever lists what is actually switched on.
        public virtual string Ambient => null;

        // Re-assert onto a freshly created Player. Flags that live on Player, Stamina or
        // Awareness do not survive a level load, so the plugin calls this whenever the
        // Player instance changes.
        public virtual void Reapply() { }

        // Per-frame upkeep for cheats that are not a single flag.
        public virtual void Tick() { }

        // Put the game back the way it was when the plugin unloads.
        public virtual void Restore() { }
    }

    // On/off cheat. Apply runs only when the state actually changes and on reapply:
    // several of the game's setters raise events, so writing them every frame would
    // spam listeners. A cheat that is upkeep rather than a flag passes whileOn instead.
    internal sealed class ToggleFeature : Feature
    {
        private readonly Action<bool> apply;
        private readonly Action whileOn;
        private readonly Binding[] bindings;
        private bool on;

        public ToggleFeature(string section, string id, string label, string description,
            Key hotkey, Action<bool> apply, Action whileOn = null)
            : base(section, id, label, description)
        {
            this.apply = apply;
            this.whileOn = whileOn;
            bindings = hotkey == Key.None
                ? new Binding[0]
                : new[] { new Binding(id, hotkey, description, Flip) };
        }

        public override IEnumerable<Binding> Bindings => bindings;
        public bool On => on;
        public override string Value => on ? "ON" : "off";
        public override string Ambient => on ? Label : null;

        public override void Increase() { Set(true); }
        public override void Decrease() { Set(false); }
        private void Flip() { Set(!on); }

        private void Set(bool value)
        {
            if (on == value) return;
            on = value;
            if (apply != null) apply(on);
            Game.Notify(Label + ": " + (on ? "ON" : "OFF"));
        }

        public override void Tick() { if (on && whileOn != null) whileOn(); }
        public override void Reapply() { if (on && apply != null) apply(true); }
        public override void Restore() { if (on && apply != null) apply(false); }
    }

    // One-shot cheat. Nothing to switch off, so it never appears in the ambient strip.
    internal sealed class ActionFeature : Feature
    {
        private readonly Action run;
        private readonly Binding[] bindings;

        public ActionFeature(string section, string id, string label, string description,
            Key hotkey, Action run)
            : base(section, id, label, description)
        {
            this.run = run;
            bindings = hotkey == Key.None
                ? new Binding[0]
                : new[] { new Binding(id, hotkey, description, Increase) };
        }

        public override IEnumerable<Binding> Bindings => bindings;
        public override string Value => "run >";
        public override void Increase() { run(); }
    }

    // Numeric cheat adjusted in fixed steps. Read is the authority: the value is read
    // back from the game rather than mirrored, so an outside change (the game's own
    // "timescale" console command) is picked up instead of being fought over.
    internal sealed class ScaleFeature : Feature
    {
        private readonly Func<float> read;
        private readonly Action<float> write;
        private readonly float step, min, max, neutral;
        private readonly string format;
        private readonly Binding[] bindings;

        public ScaleFeature(string section, string id, string label, string description,
            Func<float> read, Action<float> write,
            float step, float min, float max, float neutral, string format,
            Key decrease, Key increase, Key reset)
            : base(section, id, label, description)
        {
            this.read = read;
            this.write = write;
            this.step = step;
            this.min = min;
            this.max = max;
            this.neutral = neutral;
            this.format = format;
            var keys = new List<Binding>();
            if (decrease != Key.None) keys.Add(new Binding(id + "Slower", decrease, description + " (slower)", Decrease));
            if (increase != Key.None) keys.Add(new Binding(id + "Faster", increase, description + " (faster)", Increase));
            if (reset != Key.None) keys.Add(new Binding(id + "Reset", reset, description + " (reset)", Reset));
            bindings = keys.ToArray();
        }

        public override IEnumerable<Binding> Bindings => bindings;
        public override string Value => Current.ToString(format) + "x";
        public override string Ambient => Mathf.Approximately(Current, neutral) ? null : Label + " " + Value;

        private float Current => read();

        public override void Increase() { Move(step); }
        public override void Decrease() { Move(-step); }

        private void Move(float delta)
        {
            float next = Mathf.Clamp(TrainerMath.Snap(Current + delta, step), min, max);
            if (Mathf.Approximately(next, Current)) return;
            write(next);
            Game.Notify(Label + ": " + next.ToString(format) + "x");
        }

        private void Reset()
        {
            if (Mathf.Approximately(Current, neutral)) return;
            write(neutral);
            Game.Notify(Label + ": " + neutral.ToString(format) + "x");
        }

        public override void Restore() { write(neutral); }
    }

    internal static class TrainerMath
    {
        // Keeps repeated steps landing on clean multiples instead of drifting through
        // float error, so the readout says 1.50x and not 1.4999999x.
        public static float Snap(float value, float step)
        {
            if (step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }

        // Wraps a menu cursor across a list of the given length.
        public static int WrapIndex(int index, int count)
        {
            if (count <= 0) return 0;
            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}

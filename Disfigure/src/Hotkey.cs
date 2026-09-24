using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Configuration;

namespace DisfigureTrainer;

internal sealed class Hotkey
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly int _key;
    private readonly bool _ctrl, _alt, _shift;
    private bool _wasDown;
    public string Label { get; }

    private Hotkey(string label, int key, bool ctrl, bool alt, bool shift)
    {
        Label = label;
        _key = key;
        _ctrl = ctrl;
        _alt = alt;
        _shift = shift;
    }

    public bool Pressed(bool focused) => Poll(focused, key => (GetAsyncKeyState(key) & 0x8000) != 0);

    internal bool Poll(bool focused, Func<int, bool> down)
    {
        bool primary = _key != 0 && down(_key);
        bool pressed = focused && primary && !_wasDown
            && down(0x11) == _ctrl && down(0x12) == _alt && down(0x10) == _shift;
        // Consume held keys while unfocused, too, so alt-tabbing back cannot
        // trigger actions. Holding one binding never repeats destructive cheats.
        _wasDown = primary;
        return pressed;
    }

    public static Hotkey Parse(string value)
    {
        string label = (value ?? "").Trim();
        if (label.Equals("None", StringComparison.OrdinalIgnoreCase))
            return new Hotkey("None", 0, false, false, false);
        string[] parts = label.Split('+', StringSplitOptions.TrimEntries);
        bool ctrl = false, alt = false, shift = false;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": if (ctrl) throw new FormatException("Repeated Ctrl."); ctrl = true; break;
                case "ALT": if (alt) throw new FormatException("Repeated Alt."); alt = true; break;
                case "SHIFT": if (shift) throw new FormatException("Repeated Shift."); shift = true; break;
                default: throw new FormatException("Modifiers must be Ctrl, Alt or Shift.");
            }
        }
        string name = parts[^1].ToUpperInvariant();
        int key;
        if (name.Length == 1 && (name[0] >= 'A' && name[0] <= 'Z' || name[0] >= '0' && name[0] <= '9'))
            key = name[0];
        else if (name.StartsWith("F") && int.TryParse(name[1..], out int f) && f >= 1 && f <= 24)
            key = 0x70 + f - 1;
        else if ((name.StartsWith("NUMPAD") || name.StartsWith("KEYPAD")) && name.Length == 7 && name[6] >= '0' && name[6] <= '9')
            key = 0x60 + name[6] - '0';
        else if (!NamedKeys.TryGetValue(name, out key))
            throw new FormatException($"Unknown key '{parts[^1]}'.");
        return new Hotkey(label, key, ctrl, alt, shift);
    }

    private static readonly Dictionary<string, int> NamedKeys = new()
    {
        ["SPACE"] = 0x20, ["TAB"] = 9, ["ENTER"] = 13, ["RETURN"] = 13,
        ["ESCAPE"] = 27, ["ESC"] = 27, ["BACKSPACE"] = 8,
        ["INSERT"] = 0x2D, ["DELETE"] = 0x2E, ["HOME"] = 0x24, ["END"] = 0x23,
        ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22,
        ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28,
        ["MINUS"] = 0xBD, ["EQUALS"] = 0xBB, ["COMMA"] = 0xBC, ["PERIOD"] = 0xBE,
        ["NUMPADADD"] = 0x6B, ["NUMPADSUBTRACT"] = 0x6D, ["NUMPADMULTIPLY"] = 0x6A,
        ["NUMPADDIVIDE"] = 0x6F, ["NUMPADDECIMAL"] = 0x6E
    };
}

internal static class TrainerBindings
{
    private const string Help = "Key or combination, e.g. F8, G, NumPad1, Ctrl+F1, Alt+Shift+G. None disables the action. Supports A-Z, 0-9, F1-F24, NumPad0-9, Space, Tab, Enter, Escape, Backspace, Insert, Delete, Home, End, PageUp, PageDown, Left, Right, Up, Down, Minus, Equals, Comma, Period and NumPadAdd/Subtract/Multiply/Divide/Decimal. Restart the game after editing. Choose keys that do not conflict with game controls.";

    public static Hotkey[] Load(ConfigFile config)
    {
        string[] names = { "GodMode", "OneShot", "AddCredits", "LevelUp", "KillAllEnemies", "XpBoost", "XpMagnet", "IncreaseSpeed", "DecreaseSpeed", "ResetSpeed" };
        string[] defaults = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "1", "2", "0" };
        var result = new Hotkey[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var entry = config.Bind(i < 7 ? "Trainer.Keybindings" : "GameSpeed.Keybindings", names[i], defaults[i], Help);
            try { result[i] = Hotkey.Parse(entry.Value); }
            catch (FormatException ex)
            {
                TrainerPlugin.TrainerLog.LogWarning($"Invalid binding {names[i]} = '{entry.Value}': {ex.Message} Action disabled until config is corrected and game restarted.");
                result[i] = Hotkey.Parse("None");
            }
        }
        return result;
    }
}

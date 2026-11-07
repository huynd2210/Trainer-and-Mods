using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExtraCustomer
{
    // The game's mod settings window (Mods > Extra Customer) only knows toggles,
    // sliders, dropdowns and text fields. This draws the hotkey entry as the same
    // "click, then press a key" button the game uses under Options > Keybinds, with the
    // game's texts and warning style. Like every other entry in that window, the new
    // key takes effect on Apply, and Reset puts F8 back.
    //
    // The capture is our own, not KeyBindHelper.StartCapture: the game's Options manager
    // cancels any KeyBindHelper capture every frame its own window is closed, which
    // made keys pressed here register nowhere. Game hotkeys are still silenced while we
    // listen, through a patch on KeyBindHelper.SuppressHotkeys.
    internal static class HotkeyConfigRow
    {
        // The same hotkey as listed under Options > Keybinds; not a conflict with itself.
        private const string OwnGameOptionId = ExtraCustomerMod.Id + ":add_customer";
        private const float ButtonWidth = 224f; // the window's slider width, so the column lines up

        internal static ConfigEntry Entry;

        private static KeyCode pending;
        private static CustomUIWindow window;
        private static CustomUIElement button;
        private static CustomUIElement status;
        private static bool closeOnEscapeBefore;
        private static int restoreCloseOnEscapeFrame = -1;
        private static int escapeConsumedFrame = -1;
        private static int captureEndFrame = -1;
        private static bool capturing;
        private static KeyCode[] keyboardKeys;

        private static bool Capturing => capturing;

        // While listening, and on the frame the key was taken, no hotkey should fire.
        internal static bool SuppressHotkeys => capturing || captureEndFrame == Time.frameCount;

        internal static void Build(CustomUIBuilder b, ModConfig config)
        {
            string text = Safe(Entry.Label, Entry.Key);
            if (Entry.Tooltip != null)
            {
                string link = "modcfg:" + config.ModId + ":" + Entry.Key;
                TMPIconHover.CustomLinks[link] = Entry.Tooltip;
                text += " <link=\"" + link + "\"><sprite name=\"info\" tint=1></link>";
            }

            capturing = false;
            pending = ExtraCustomerMod.AddCustomerKey;
            window = b.GetWindow();

            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddLabel(text, "label_" + Entry.Key);
            b.AddFlexibleSpace();
            b.AddButton(KeyBindHelper.KeyName(pending), OnClick, Entry.Key);
            b.Last.SetFixedWidth(ButtonWidth);
            button = b.Last;
            b.End();

            b.AddWarning("", Entry.Key + "_status");
            status = b.Last;
            status.SetVisible(false);
        }

        internal static string Read() => pending.ToString();

        internal static void Write(object value)
        {
            if (Capturing)
                EndCapture();
            pending = ExtraCustomerMod.ParseKey(ConfigEntry.Format(value));
            SetStatus(null);
            RefreshButton();
        }

        private static void OnClick()
        {
            if (!Alive())
                return;
            // A focused button is also "clicked" by Space/Enter. That key is the one being
            // bound (or was, this frame), not a request to start or cancel listening.
            if (captureEndFrame == Time.frameCount || (Capturing && PressedKey() != KeyCode.None))
                return;
            if (Capturing)
            {
                SetStatus(null);
                EndCapture();
                return;
            }

            capturing = true;
            // Escape cancels the capture; it must not also close this window.
            if (restoreCloseOnEscapeFrame < 0)
                closeOnEscapeBefore = window.closeOnEscape;
            restoreCloseOnEscapeFrame = -1;
            window.closeOnEscape = false;
            SetStatus(LocHelper.GetLocalizedMainMenu("option_keybind_status_waiting"));
            RefreshButton();
        }

        private static void EndCapture()
        {
            if (capturing)
            {
                capturing = false;
                captureEndFrame = Time.frameCount;
            }
            // Restored a frame later, so the Escape that ended the capture closes nothing.
            restoreCloseOnEscapeFrame = Time.frameCount + 1;
            RefreshButton();
        }

        // Called every frame by the hotkey listener.
        internal static void Poll()
        {
            if (restoreCloseOnEscapeFrame >= 0 && Time.frameCount >= restoreCloseOnEscapeFrame)
            {
                restoreCloseOnEscapeFrame = -1;
                if (Alive() && !Capturing)
                    window.closeOnEscape = closeOnEscapeBefore;
            }

            if (!Capturing)
                return;
            if (!Alive())
            {
                capturing = false;
                return;
            }
            if (!Input.anyKeyDown)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escapeConsumedFrame = Time.frameCount;
                SetStatus(null);
                EndCapture();
                return;
            }

            KeyCode key = PressedKey();
            if (key == KeyCode.None)
                return; // a mouse click, e.g. on another control
            if (KeyBindHelper.IsReserved(key))
            {
                SetStatus(LocHelper.GetLocalizedMainMenu("option_keybind_status_reserved", KeyBindHelper.KeyName(key)));
                return;
            }
            string owner = FindOwner(key);
            if (owner != null)
            {
                SetStatus(LocHelper.GetLocalizedMainMenu("option_keybind_status_conflict", KeyBindHelper.KeyName(key), owner));
                return;
            }
            pending = key;
            SetStatus(null);
            EndCapture();
        }

        // Keyboard keys only, the same set the game's Options capture accepts.
        private static KeyCode PressedKey()
        {
            if (keyboardKeys == null)
                keyboardKeys = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
                    .Where(k => k != KeyCode.None && k < KeyCode.Mouse0).Distinct().ToArray();
            foreach (KeyCode k in keyboardKeys)
                if (Input.GetKeyDown(k))
                    return k;
            return KeyCode.None;
        }

        // True on the frame our capture used Escape, so the shop's pause menu ignores it.
        internal static bool ConsumedEscape()
        {
            if (escapeConsumedFrame == Time.frameCount)
                return true;
            return Capturing && Input.GetKeyDown(KeyCode.Escape);
        }

        // Label of the game or mod keybind already on this key, as the Options window
        // refuses duplicates too.
        private static string FindOwner(KeyCode key)
        {
            GameOptionRegistry.EnsureVanilla();
            foreach (GameOptionRegistry.Section section in GameOptionRegistry.Sections)
            {
                foreach (GameOptionRegistry.Entry option in section.entries)
                {
                    if (option.kind != GameOptionRegistry.Kind.KeyBind || option.id == OwnGameOptionId || option.getKey == null)
                        continue;
                    if (Safe(option.getKey, KeyCode.None) == key)
                        return Safe(option.label, option.id);
                }
            }
            return null;
        }

        private static bool Alive() => window != null && window.IsAlive && button != null && button.IsAlive;

        private static void RefreshButton()
        {
            if (Alive())
                button.SetText(Capturing ? LocHelper.GetLocalizedMainMenu("option_keybind_press_key") : KeyBindHelper.KeyName(pending));
        }

        private static void SetStatus(string text)
        {
            if (status == null || !status.IsAlive)
                return;
            status.SetText(text ?? "");
            status.SetVisible(!string.IsNullOrEmpty(text));
        }

        private static T Safe<T>(Func<T> getter, T fallback)
        {
            if (getter == null)
                return fallback;
            try { return getter(); }
            catch (Exception e)
            {
                Debug.LogError("[ExtraCustomer] Settings callback threw: " + e);
                return fallback;
            }
        }
    }

    [HarmonyPatch(typeof(ModConfigWindow), "BuildEntry")]
    internal static class BuildEntryPatch
    {
        private static bool Prefix(CustomUIBuilder b, ModConfig config, ConfigEntry entry)
        {
            if (entry == null || entry != HotkeyConfigRow.Entry)
                return true;
            HotkeyConfigRow.Build(b, config);
            return false;
        }
    }

    [HarmonyPatch(typeof(ModConfigWindow), "ReadWidget")]
    internal static class ReadWidgetPatch
    {
        private static bool Prefix(ConfigEntry entry, ref object __result)
        {
            if (entry == null || entry != HotkeyConfigRow.Entry)
                return true;
            __result = HotkeyConfigRow.Read();
            return false;
        }
    }

    [HarmonyPatch(typeof(ModConfigWindow), "WriteWidget")]
    internal static class WriteWidgetPatch
    {
        private static bool Prefix(ConfigEntry entry, object value)
        {
            if (entry == null || entry != HotkeyConfigRow.Entry)
                return true;
            HotkeyConfigRow.Write(value);
            return false;
        }
    }

    // Game hotkeys (and our own F8 listener) stay quiet while the settings row listens.
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.SuppressHotkeys), MethodType.Getter)]
    internal static class SuppressHotkeysPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && HotkeyConfigRow.SuppressHotkeys)
                __result = true;
        }
    }

    // The shop's Escape handling (pause menu) asks this before acting on Escape.
    [HarmonyPatch(typeof(GameOptionUIManager), nameof(GameOptionUIManager.ConsumedEscapeThisFrame))]
    internal static class ConsumedEscapePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && HotkeyConfigRow.ConsumedEscape())
                __result = true;
        }
    }
}

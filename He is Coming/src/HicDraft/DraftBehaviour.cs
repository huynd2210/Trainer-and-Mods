using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HicDraft
{
    /// <summary>
    /// Per-frame driver. Reads the hotkey straight off the keyboard device rather than through an
    /// InputAction, so it works in every control state the game switches between (overworld, menus,
    /// battle) without joining one of the game's own action maps.
    /// </summary>
    public sealed class DraftBehaviour : MonoBehaviour
    {
        public DraftBehaviour(IntPtr pointer) : base(pointer) { }

        private Key _key = Key.F7;
        private string _parsedFrom;

        private void Update()
        {
            RefreshHotkey();

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_key].wasPressedThisFrame)
                DraftController.Toggle();

            DraftController.Tick();
            DraftSelfTest.Tick(Time.unscaledDeltaTime);
        }

        private void RefreshHotkey()
        {
            string configured = Plugin.Hotkey.Value;
            if (configured == _parsedFrom) return;
            _parsedFrom = configured;

            if (Enum.TryParse(configured, true, out Key parsed) && parsed != Key.None)
            {
                _key = parsed;
                Plugin.Log.LogInfo($"[Draft] Hotkey bound to {_key}.");
            }
            else
            {
                _key = Key.F7;
                Plugin.Log.LogWarning($"[Draft] '{configured}' is not an Input System key name; falling back to F7.");
            }
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LonestarTrainer
{
    /// <summary>
    /// Drives the trainer: reads hotkeys, keeps the always-on cheats applied, and
    /// draws the corner status strip. Created once and kept across scene loads.
    /// </summary>
    internal sealed class TrainerRunner : MonoBehaviour
    {
        // The game's own palette (RareColor): orange is the highest-rarity accent,
        // blue the mid tier, grey the neutral readout colour.
        private static readonly Color Accent = new Color(1f, 0.376f, 0f);        // FF6000
        private static readonly Color Header = new Color(0.153f, 0.573f, 1f);    // 2792FF
        private static readonly Color Muted = new Color(0.788f, 0.788f, 0.788f); // C9C9C9

        private GUIStyle _text;
        private Texture2D _panel;
        private bool _overlayVisible = true;
        private bool _drewOnce;

        public static TrainerRunner Spawn()
        {
            GameObject host = new GameObject("LonestarTrainer");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            return host.AddComponent<TrainerRunner>();
        }

        private void Awake()
        {
            _overlayVisible = TrainerMod.Config == null || TrainerMod.Config.showOverlay;
        }

        private void Update()
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null) return;

            if (cfg.infiniteCoins) GameActions.MaintainCoins();

            if (cfg.hotkeysEnabled) ReadHotkeys(cfg);
        }

        private void ReadHotkeys(TrainerConfig cfg)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Don't steal keys while the dev console has the keyboard.
            if (GameMasterManager.GUIOpen) return;

            if (kb[Key.F11].wasPressedThisFrame)
            {
                _overlayVisible = !_overlayVisible;
                cfg.showOverlay = _overlayVisible;
                TrainerMod.Instance?.SaveModSettings();
            }

            List<ICheat> cheats = CheatRegistry.All;
            for (int i = 0; i < cheats.Count; i++)
            {
                ICheat cheat = cheats[i];
                if (cheat.Hotkey == Key.None) continue;

                KeyControl control = kb[cheat.Hotkey];
                if (control != null && control.wasPressedThisFrame)
                    cheat.Activate();
            }
        }

        private void OnGUI()
        {
            if (!_overlayVisible) return;
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null) return;

            EnsureStyles();

            if (!_drewOnce)
            {
                _drewOnce = true;
                Debug.Log("[LonestarTrainer] overlay drawing at " + Screen.width + "x" + Screen.height);
            }

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            _text.fontSize = Mathf.RoundToInt(13f * scale);

            const string Title = "LONESTAR TRAINER";
            string body = BuildStatus(cfg);
            string flash = Overlay.Current;

            float pad = 10f * scale;
            float line = _text.lineHeight;

            // Width has to fit the widest of the three blocks, not just the body.
            float innerWidth = Mathf.Max(
                _text.CalcSize(new GUIContent(Title)).x,
                _text.CalcSize(new GUIContent(body)).x);
            if (flash != null)
                innerWidth = Mathf.Max(innerWidth, _text.CalcSize(new GUIContent(flash)).x);

            float headerH = line * 1.3f;
            float bodyH = _text.CalcHeight(new GUIContent(body), innerWidth);
            float panelW = innerWidth + pad * 2f;
            float panelH = headerH + bodyH + pad * 1.6f;

            float x = pad;
            float y = Screen.height - panelH - pad;

            GUI.DrawTexture(new Rect(x, y, panelW, panelH), _panel);

            GUI.color = Header;
            GUI.Label(new Rect(x + pad, y + pad * 0.5f, innerWidth, headerH), Title, _text);

            GUI.color = Muted;
            GUI.Label(new Rect(x + pad, y + pad * 0.5f + headerH, innerWidth, bodyH), body, _text);

            if (flash != null)
            {
                GUI.color = Accent;
                GUI.Label(new Rect(x + pad, y - line - pad * 0.5f, innerWidth, line * 1.3f), flash, _text);
            }

            GUI.color = Color.white;
        }

        private string BuildStatus(TrainerConfig cfg)
        {
            StringBuilder active = new StringBuilder();
            List<ICheat> cheats = CheatRegistry.All;
            for (int i = 0; i < cheats.Count; i++)
            {
                if (!cheats[i].IsActive) continue;
                string label = cheats[i].StatusText;
                if (label == null) continue;
                if (active.Length > 0) active.Append(", ");
                active.Append(label);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(active.Length > 0 ? active.ToString() : "no cheats active");
            sb.Append('\n');

            if (cfg.hotkeysEnabled)
                sb.Append("F1 god  F2 kill  F3 coins  F4 hull\nF5 repair  F6 win  F7 cargo  F8 +250  F9 move\nF11 hide");
            else
                sb.Append("hotkeys off - use the mod's settings menu");

            return sb.ToString();
        }

        private void EnsureStyles()
        {
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = false,
                    richText = false,
                    alignment = TextAnchor.UpperLeft
                };
                _text.normal.textColor = Color.white;
            }

            if (_panel == null)
            {
                _panel = new Texture2D(1, 1);
                _panel.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.08f, 0.78f));
                _panel.Apply();
                _panel.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
}

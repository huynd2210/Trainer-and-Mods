using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LonestarTracker
{
    /// <summary>
    /// Draws the voyage readout and the tracker panel, and reads the keys that drive
    /// them. Input comes from the Input System rather than from IMGUI events, and the
    /// panel deliberately has nothing to click: a click the panel does not consume would
    /// fall through to the ship underneath it. Everything is a key or the wheel.
    /// </summary>
    internal sealed class TrackerUI : MonoBehaviour
    {
        private const float RepeatDelay = 0.38f;
        private const float RepeatRate = 0.05f;

        private readonly UiSkin _skin = new UiSkin();
        private readonly List<ITrackerTab> _tabs = new List<ITrackerTab>();
        private readonly Dictionary<ITrackerTab, int> _scroll = new Dictionary<ITrackerTab, int>();

        /// <summary>How often the voyage in progress is written out while nothing happens.</summary>
        private const float CheckpointInterval = 20f;

        private bool _open;
        private int _tab;
        private Rect _contentArea;
        private Key _heldKey = Key.None;
        private float _heldSince;
        private float _lastRepeat;
        private float _lastCheckpoint;
        private BattlesTab _battles;

        public static TrackerUI Spawn()
        {
            GameObject host = new GameObject("LonestarTracker");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            return host.AddComponent<TrackerUI>();
        }

        private void Awake()
        {
            _battles = new BattlesTab();
            _tabs.Add(new OverviewTab());
            _tabs.Add(new CurrentRunTab());
            _tabs.Add(_battles);
            _tabs.Add(new RunsTab());
            foreach (ITrackerTab tab in _tabs) _scroll[tab] = 0;

            RestoreView();
            Tracker.Changed += OnDataChanged;
        }

        private void OnDestroy()
        {
            Tracker.Changed -= OnDataChanged;
        }

        /// <summary>Leaving the panel on Bosses / longest fight should mean finding it there.</summary>
        private void RestoreView()
        {
            TrackerConfig config = TrackerMod.Config;
            if (config == null) return;

            if (!string.IsNullOrEmpty(config.lastFilter)) _battles.FilterName = config.lastFilter;
            if (!string.IsNullOrEmpty(config.lastOrder)) _battles.OrderName = config.lastOrder;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Title != config.lastView) continue;
                _tab = i;
                break;
            }
            if (!Current.Available) StepTab(1);
        }

        private void RememberView()
        {
            TrackerConfig config = TrackerMod.Config;
            if (config == null) return;

            string view = Current.Title;
            string filter = _battles.FilterName;
            string order = _battles.OrderName;
            if (config.lastView == view && config.lastFilter == filter && config.lastOrder == order) return;

            config.lastView = view;
            config.lastFilter = filter;
            config.lastOrder = order;
            if (TrackerMod.Instance != null) TrackerMod.Instance.SaveModSettings();
        }

        private void OnDataChanged()
        {
            foreach (ITrackerTab tab in _tabs) tab.Invalidate();
        }

        // ----------------------------------------------------------------- input

        private void Update()
        {
            TrackerConfig config = TrackerMod.Config;
            if (config == null) return;

            // A voyage that is under way is written out on a timer, so however the game
            // stops - quit, crash, power cut - at most the last few seconds are lost.
            if (Time.unscaledTime - _lastCheckpoint >= CheckpointInterval)
            {
                _lastCheckpoint = Time.unscaledTime;
                Tracker.Save();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // The game's own console owns the keyboard while it is up.
            if (GameMasterManager.GUIOpen) { if (_open) ClosePanel(); return; }

            Key toggle = config.PanelKey;
            if (toggle != Key.None && keyboard[toggle] != null && keyboard[toggle].wasPressedThisFrame)
            {
                if (_open) ClosePanel();
                else { _open = true; if (!Current.Available) StepTab(1); }
            }

            if (!_open) { _heldKey = Key.None; return; }

            if (Pressed(keyboard, Key.Escape)) { ClosePanel(); return; }

            // If the voyage ended while its view was open, move somewhere that still exists.
            if (!Current.Available) StepTab(1);

            if (Pressed(keyboard, Key.RightArrow) || Pressed(keyboard, Key.Tab)) StepTab(1);
            if (Pressed(keyboard, Key.LeftArrow)) StepTab(-1);

            ITrackerTab current = Current;
            int page = Mathf.Max(1, current.RowsPerPage(_contentArea, _skin));

            if (Repeating(keyboard, Key.DownArrow)) Scroll(current, 1, page);
            if (Repeating(keyboard, Key.UpArrow)) Scroll(current, -1, page);
            if (Repeating(keyboard, Key.PageDown)) Scroll(current, page, page);
            if (Repeating(keyboard, Key.PageUp)) Scroll(current, -page, page);
            if (keyboard[Key.Home].wasPressedThisFrame) SetScroll(current, 0, page);
            if (keyboard[Key.End].wasPressedThisFrame) SetScroll(current, current.RowCount, page);

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) Scroll(current, wheel > 0f ? -3 : 3, page);
            }

            if (Pressed(keyboard, Key.F)) { current.HandleKey(Key.F); SetScroll(current, 0, page); }
            if (Pressed(keyboard, Key.S)) { current.HandleKey(Key.S); SetScroll(current, 0, page); }
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        /// <summary>First press, then a repeat once the key has been held a moment.</summary>
        private bool Repeating(Keyboard keyboard, Key key)
        {
            var control = keyboard[key];
            if (control == null) return false;

            if (control.wasPressedThisFrame)
            {
                _heldKey = key;
                _heldSince = Time.unscaledTime;
                _lastRepeat = _heldSince;
                return true;
            }

            if (!control.isPressed || _heldKey != key) return false;

            float now = Time.unscaledTime;
            if (now - _heldSince < RepeatDelay || now - _lastRepeat < RepeatRate) return false;

            _lastRepeat = now;
            return true;
        }

        private void ClosePanel()
        {
            _open = false;
            _heldKey = Key.None;
            RememberView();
        }

        private void OnApplicationQuit() { Tracker.Save(); }

        private void OnApplicationPause(bool paused) { if (paused) Tracker.Save(); }

        private ITrackerTab Current { get { return _tabs[Mathf.Clamp(_tab, 0, _tabs.Count - 1)]; } }

        private void StepTab(int direction)
        {
            for (int i = 1; i <= _tabs.Count; i++)
            {
                int index = ((_tab + direction * i) % _tabs.Count + _tabs.Count) % _tabs.Count;
                if (_tabs[index].Available) { _tab = index; return; }
            }
        }

        private void SelectAvailableTab(int preferred)
        {
            _tab = preferred;
            if (!Current.Available) StepTab(1);
        }

        private void Scroll(ITrackerTab tab, int delta, int page)
        {
            SetScroll(tab, _scroll[tab] + delta, page);
        }

        private void SetScroll(ITrackerTab tab, int value, int page)
        {
            int max = Mathf.Max(0, tab.RowCount - page);
            _scroll[tab] = Mathf.Clamp(value, 0, max);
        }

        // ---------------------------------------------------------------- drawing

        private void OnGUI()
        {
            TrackerConfig config = TrackerMod.Config;
            if (config == null || Tracker.Store == null) return;

            GUI.depth = -100;
            _skin.Refresh(config.textScalePercent);

            if (_open) DrawPanel(config);
            else if (config.showHud) DrawHud(config);

            GUI.color = Color.white;
        }

        private void DrawHud(TrackerConfig config)
        {
            RunRecord run = Tracker.CurrentRun;
            if (run == null) return;

            List<string> parts = new List<string>();
            List<Color> colors = new List<Color>();

            // Spelled out rather than "8 / 3 / 1": a bare number triplet is only readable
            // if you already know the order, and this is meant to be glanced at.
            parts.Add(run.Kills + (run.Kills == 1 ? " KILL" : " KILLS")); colors.Add(Palette.Text);
            parts.Add(run.KillsMinion + " minion"); colors.Add(Palette.Minion);
            parts.Add(run.KillsElite + " elite"); colors.Add(Palette.Elite);
            parts.Add(run.KillsBoss + " boss"); colors.Add(Palette.Boss);
            parts.Add("day " + run.Days); colors.Add(Palette.Muted);

            float gap = Mathf.Round(9f * _skin.Scale);
            float width = 0f;
            for (int i = 0; i < parts.Count; i++)
                width += _skin.Text.CalcSize(new GUIContent(parts[i])).x + (i > 0 ? gap : 0f);

            float pad = Mathf.Round(9f * _skin.Scale);
            float height = _skin.Line + pad;
            float margin = Mathf.Round(14f * _skin.Scale);

            bool right = config.hudCorner == null || config.hudCorner.EndsWith("right");
            bool top = config.hudCorner != null && config.hudCorner.StartsWith("Top");

            float x = right ? Screen.width - width - pad * 2f - margin : margin;
            float y = top ? margin : Screen.height - height - margin;

            Ui.Fill(new Rect(x, y, width + pad * 2f, height), Palette.Hud);
            Ui.Run(new Rect(x + pad, y + pad * 0.5f, width, _skin.Line), _skin, parts, colors, gap);
        }

        private void DrawPanel(TrackerConfig config)
        {
            float pad = _skin.Pad;
            float width = Mathf.Min(Screen.width - pad * 6f, 1240f * _skin.Scale);
            float height = Mathf.Min(Screen.height - pad * 6f, 780f * _skin.Scale);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            Ui.Fill(panel, Palette.Panel);

            float headerHeight = _skin.RowHeight * 2.4f;
            Ui.Fill(new Rect(panel.x, panel.y, panel.width, headerHeight), Palette.PanelHeader);

            float x = panel.x + pad;
            float y = panel.y + pad * 0.4f;
            float titleHeight = _skin.Title.lineHeight * 1.2f;

            const string PanelTitle = "LONESTAR TRACKER";
            float titleWidth = _skin.Title.CalcSize(new GUIContent(PanelTitle)).x;
            Ui.Label(new Rect(x, y, titleWidth + 4f, titleHeight), PanelTitle, Palette.Accent, _skin.Title);

            Stats stats = Tracker.Snapshot;
            string summary = stats.Kills + " kills · " + stats.RunsWon + " voyages won · " +
                             stats.WinRatePercent + "% of battles";
            float summaryLeft = x + titleWidth + pad * 2f;
            float summaryWidth = panel.x + panel.width - pad - summaryLeft;
            if (summaryWidth > 40f * _skin.Scale)
                Ui.Label(new Rect(summaryLeft, y, summaryWidth, titleHeight),
                    Ui.Clip(summary, summaryWidth, _skin), Palette.Muted, _skin.TextRight);

            // Tab strip
            y += titleHeight + pad * 0.2f;
            float tabX = x;
            for (int i = 0; i < _tabs.Count; i++)
            {
                ITrackerTab tab = _tabs[i];
                string title = tab.Title;
                float w = _skin.Text.CalcSize(new GUIContent(title)).x;
                bool active = i == _tab;
                Color color = !tab.Available ? new Color(1f, 1f, 1f, 0.22f)
                            : active ? Palette.Highlight : Palette.Muted;

                Ui.Label(new Rect(tabX, y, w + 4f, _skin.RowHeight), title, color, _skin.Text);
                if (active)
                    Ui.Fill(new Rect(tabX, y + _skin.RowHeight - 2f, w, 2f), Palette.Highlight);

                tabX += w + pad * 1.6f;
            }

            Ui.Fill(new Rect(panel.x, panel.y + headerHeight, panel.width, 1f), Palette.Rule);

            // Content
            float hintHeight = _skin.RowHeight + pad * 0.6f;
            _contentArea = new Rect(panel.x + pad, panel.y + headerHeight + pad * 0.8f,
                panel.width - pad * 2f, panel.height - headerHeight - hintHeight - pad * 1.6f);

            ITrackerTab current = Current;
            int page = Mathf.Max(1, current.RowsPerPage(_contentArea, _skin));
            SetScroll(current, _scroll[current], page);
            current.Draw(_contentArea, _skin, _scroll[current]);

            // Hint strip - the price of having no buttons is saying what the keys do.
            float hintY = panel.y + panel.height - hintHeight;
            Ui.Fill(new Rect(panel.x, hintY, panel.width, 1f), Palette.Rule);

            string position = current.RowCount > 0
                ? (Mathf.Min(_scroll[current] + page, current.RowCount) + " of " + current.RowCount)
                : "";
            float positionWidth = position.Length > 0
                ? _skin.Text.CalcSize(new GUIContent(position)).x + pad
                : 0f;

            float hintWidth = panel.width - pad * 2f - positionWidth;
            string hint = "← → view   ↑ ↓ wheel scroll   " + current.Hint + "   Esc close";
            Ui.Label(new Rect(x, hintY + pad * 0.3f, hintWidth, _skin.RowHeight),
                Ui.Clip(hint, hintWidth, _skin), Palette.Muted, _skin.Text);

            if (position.Length > 0)
                Ui.Label(new Rect(panel.x, hintY + pad * 0.3f, panel.width - pad, _skin.RowHeight),
                    position, Palette.Muted, _skin.TextRight);

            if (Tracker.Store.LastError != null)
                Ui.Label(new Rect(x, hintY - _skin.RowHeight, panel.width - pad * 2f, _skin.RowHeight),
                    "Could not write the log: " + Tracker.Store.LastError, Palette.Lose, _skin.Text);
        }
    }
}

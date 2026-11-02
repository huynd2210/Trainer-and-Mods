using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Game;
using DD2.KillTracker.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DD2.KillTracker
{
	/// <summary>
	/// Lives on its own DontDestroyOnLoad object (the plugin component may be destroyed by the game). Polls the toggle
	/// key, switches the ledger when the DD2 profile changes, flushes it on a timer / after combat / on quit, and draws
	/// the window.
	/// </summary>
	internal sealed class KillTrackerHost : MonoBehaviour
	{
		private const int WindowId = 0x44443254; // "DD2T"
		private const int MaxRenderedRows = 300;
		private const float PollIntervalSeconds = 1f;
		private const float MinSaveIntervalSeconds = 1f;

		private sealed class Row
		{
			public string Id;
			public string Name;
			public string SourceLabel;
			public bool IsMod;
			public bool IsOverride;
			public long Lifetime;
			public long Run;
			public string SearchBlob;
		}

		/// <summary>One table column. A new column is one more entry in <see cref="Columns"/>.</summary>
		private sealed class Column
		{
			public string Header;
			public float Width; // 0 = takes the remaining width
			public Comparison<Row> Ascending;
			public Func<Row, string> Text;
		}

		private static readonly Column[] Columns =
		{
			new Column { Header = "Monster", Width = 0f, Ascending = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase), Text = r => r.Name },
			new Column { Header = "Source", Width = 190f, Ascending = (a, b) => string.Compare(a.SourceLabel, b.SourceLabel, StringComparison.OrdinalIgnoreCase), Text = r => r.SourceLabel },
			new Column { Header = "Lifetime", Width = 72f, Ascending = (a, b) => a.Lifetime.CompareTo(b.Lifetime), Text = r => r.Lifetime.ToString() },
			new Column { Header = "Run", Width = 56f, Ascending = (a, b) => a.Run.CompareTo(b.Run), Text = r => r.Run.ToString() },
		};

		private const int LifetimeColumn = 2;
		private const int RunColumn = 3;

		private readonly List<Row> _rows = new List<Row>();
		private bool _open;
		private bool _guiBroken;
		private Rect _window = new Rect(80f, 80f, 660f, 520f);
		private Vector2 _scroll;
		private string _filter = string.Empty;
		private bool _lifetimeScope; // false = Run, true = Lifetime
		private int _sortColumn = -1;  // -1 = by the active scope's count
		private bool _sortDescending = true;

		private int _builtRevision = -1;
		private string _builtFilter;
		private bool _builtScope;
		private int _builtSort = -2;
		private bool _builtDescending;
		private KillLedger _builtLedger;

		private float _pollTimer;
		private float _flushTimer;
		private GameModeType _lastMode;

		// toggle key cache
		private string _chordText;
		private KeyChord _chord;
		private Key _chordKey;
		private bool _chordKeyValid;
		private bool _legacyWarned;

		public static void Spawn()
		{
			var go = new GameObject("DD2.KillTracker.Host");
			DontDestroyOnLoad(go);
			go.hideFlags = HideFlags.HideAndDontSave;
			go.AddComponent<KillTrackerHost>();
		}

		// ------------------------------------------------------------------ update

		private void Update()
		{
			if (!Cfg.Ready) return;

			// Each step is guarded on its own so one failing step cannot stop the others (the toggle key must keep working).
			Guard.Run("toggle key", () =>
			{
				if (!TogglePressed()) return;
				_open = !_open;
				if (_open) _guiBroken = false;
			});

			_pollTimer += Time.unscaledDeltaTime;
			if (_pollTimer >= PollIntervalSeconds)
			{
				_pollTimer = 0f;
				Guard.Run("profile poll", () => Cfg.Ledger.EnsureProfile(GameBridge.ProfileKey()));
			}

			Guard.Run("combat end watch", WatchCombatEnd);

			_flushTimer += Time.unscaledDeltaTime;
			if (_flushTimer >= Mathf.Max(MinSaveIntervalSeconds, Cfg.SaveIntervalSeconds.Value))
			{
				_flushTimer = 0f;
				Guard.Run("periodic flush", () => Cfg.Ledger.Flush());
			}
		}

		/// <summary>Flushes when the game leaves combat, so a kill is on disk by the time the fight is over.</summary>
		private void WatchCombatEnd()
		{
			GameModeType mode = GameModeMgr.CurrentMode;
			if (_lastMode == GameModeType.COMBAT && mode != GameModeType.COMBAT)
			{
				Cfg.Ledger.Flush();
			}
			_lastMode = mode;
		}

		private bool TogglePressed()
		{
			string text = Cfg.ToggleKey.Value;
			if (text != _chordText)
			{
				_chordText = text;
				_chord = Cfg.ParsedToggleKey();
				_chordKeyValid = Enum.TryParse(_chord.Key, true, out _chordKey);
				if (!_chordKeyValid)
				{
					Cfg.Warn("ToggleKey '" + text + "' is not a key name Unity's Input System knows (e.g. F11, Backquote, K). The window cannot be opened.");
				}
			}
			if (!_chordKeyValid) return false;

			Keyboard kb = Keyboard.current;
			if (kb != null)
			{
				if (!kb[_chordKey].wasPressedThisFrame) return false;
				return (!_chord.Ctrl || kb.ctrlKey.isPressed) && (!_chord.Shift || kb.shiftKey.isPressed) && (!_chord.Alt || kb.altKey.isPressed);
			}

			// No keyboard device from the Input System (should not happen on PC): try the old Input class once, quietly.
			try
			{
				return Enum.TryParse(_chord.Key, true, out KeyCode legacy) && Input.GetKeyDown(legacy);
			}
			catch (Exception e)
			{
				if (!_legacyWarned)
				{
					_legacyWarned = true;
					Cfg.Warn("Keyboard unavailable for the toggle key: " + e.Message);
				}
				return false;
			}
		}

		private void OnApplicationQuit()
		{
			Guard.Run("final flush", () => Cfg.Ledger?.Flush());
		}

		// ------------------------------------------------------------------ rows

		private bool ActiveScopeIsLifetime => _lifetimeScope;

		private void RebuildRowsIfNeeded(KillLedger ledger)
		{
			bool stale = ledger != _builtLedger || ledger.Revision != _builtRevision || _filter != _builtFilter
				|| _lifetimeScope != _builtScope || _sortColumn != _builtSort || _sortDescending != _builtDescending;
			if (!stale) return;

			_builtLedger = ledger;
			_builtRevision = ledger.Revision;
			_builtFilter = _filter;
			_builtScope = _lifetimeScope;
			_builtSort = _sortColumn;
			_builtDescending = _sortDescending;

			_rows.Clear();
			string needle = (_filter ?? string.Empty).Trim().ToLowerInvariant();
			foreach (MonsterStats m in ledger.Monsters)
			{
				if (m.CountFor(_lifetimeScope) <= 0) continue;

				string name = !string.IsNullOrEmpty(m.Name) ? m.Name : m.Id;
				var row = new Row
				{
					Id = m.Id,
					Name = name,
					SourceLabel = m.Source.Kind == SourceKind.Mod ? "[" + m.Source.Name + "]" : m.Source.Label,
					IsMod = m.Source.IsMod,
					IsOverride = m.Source.Kind == SourceKind.Override,
					Lifetime = m.Lifetime,
					Run = m.Run,
				};
				if (row.IsOverride) row.SourceLabel = "[" + m.Source.Name + "] overrides";
				row.SearchBlob = (row.Id + " " + row.Name + " " + row.SourceLabel + " " + (m.Source.Folder ?? "") + " " + (m.Source.ModId ?? "")).ToLowerInvariant();
				if (needle.Length == 0 || row.SearchBlob.Contains(needle)) _rows.Add(row);
			}

			Comparison<Row> byCount = _lifetimeScope ? Columns[LifetimeColumn].Ascending : Columns[RunColumn].Ascending;
			Comparison<Row> primary = _sortColumn < 0 ? byCount : Columns[_sortColumn].Ascending;
			int dir = _sortDescending ? -1 : 1;
			_rows.Sort((a, b) =>
			{
				int c = dir * primary(a, b);
				if (c != 0) return c;
				c = -byCount(a, b); // ties: bigger counts first, then name
				return c != 0 ? c : Columns[0].Ascending(a, b);
			});
		}

		// ------------------------------------------------------------------ drawing

		private void OnGUI()
		{
			if (!Cfg.Ready || !_open || _guiBroken) return;

			try
			{
				GUI.depth = -1000;
				_window = ClampToScreen(GUILayout.Window(WindowId, _window, DrawWindow, "Kill Tracker", Theme.Window, GUILayout.MinWidth(520f), GUILayout.MinHeight(300f)));
			}
			catch (Exception e)
			{
				// OnGUI exceptions repeat every frame: close the window instead of flooding the log.
				_guiBroken = true;
				_open = false;
				Guard.Report("window", e);
			}
		}

		private static Rect ClampToScreen(Rect r)
		{
			r.x = Mathf.Clamp(r.x, -r.width + 80f, Screen.width - 80f);
			r.y = Mathf.Clamp(r.y, 0f, Screen.height - 40f);
			return r;
		}

		private void DrawWindow(int id)
		{
			KillLedger ledger = Cfg.Ledger?.Current;
			if (ledger == null)
			{
				GUILayout.Label("No profile loaded yet.", Theme.Dim);
				GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
				return;
			}

			RebuildRowsIfNeeded(ledger);

			DrawToolbar();
			GUILayout.Space(4f);
			DrawHeader();
			DrawRows();
			GUILayout.Space(4f);
			DrawFooter(ledger);

			GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
		}

		private void DrawToolbar()
		{
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(!_lifetimeScope, "Run", _lifetimeScope ? Theme.TabOff : Theme.TabOn, GUILayout.Width(80f))) _lifetimeScope = false;
			if (GUILayout.Toggle(_lifetimeScope, "Lifetime", _lifetimeScope ? Theme.TabOn : Theme.TabOff, GUILayout.Width(80f))) _lifetimeScope = true;
			GUILayout.Space(10f);
			GUILayout.Label("Filter", Theme.Dim, GUILayout.Width(40f));
			_filter = GUILayout.TextField(_filter ?? string.Empty, Theme.Field);
			if (GUILayout.Button("x", Theme.ClearButton, GUILayout.Width(24f))) _filter = string.Empty;
			GUILayout.EndHorizontal();
		}

		private void DrawHeader()
		{
			GUILayout.BeginHorizontal();
			int activeCountColumn = _lifetimeScope ? LifetimeColumn : RunColumn;
			int effective = _sortColumn < 0 ? activeCountColumn : _sortColumn;
			for (int i = 0; i < Columns.Length; i++)
			{
				Column c = Columns[i];
				string label = c.Header + (i == effective ? (_sortDescending ? "  v" : "  ^") : string.Empty);
				GUIStyle style = i == effective ? Theme.HeaderActive : Theme.HeaderButton;
				bool clicked = c.Width > 0f ? GUILayout.Button(label, style, GUILayout.Width(c.Width)) : GUILayout.Button(label, style, GUILayout.ExpandWidth(true));
				if (clicked) SortBy(i);
			}
			GUILayout.EndHorizontal();
		}

		private void SortBy(int column)
		{
			int activeCountColumn = _lifetimeScope ? LifetimeColumn : RunColumn;
			int effective = _sortColumn < 0 ? activeCountColumn : _sortColumn;
			if (effective == column)
			{
				_sortDescending = !_sortDescending;
			}
			else
			{
				_sortColumn = column;
				// numbers read best biggest-first, text A-Z
				_sortDescending = column == LifetimeColumn || column == RunColumn;
			}
		}

		private void DrawRows()
		{
			_scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.ExpandHeight(true));
			if (_rows.Count == 0)
			{
				GUILayout.Label(string.IsNullOrWhiteSpace(_filter)
					? "No kills recorded in this view yet."
					: "Nothing matches the filter.", Theme.Dim);
			}

			int shown = Math.Min(_rows.Count, MaxRenderedRows);
			int activeCountColumn = _lifetimeScope ? LifetimeColumn : RunColumn;
			for (int i = 0; i < shown; i++)
			{
				Row r = _rows[i];
				GUILayout.BeginHorizontal(i % 2 == 0 ? Theme.Row : Theme.RowAlt);
				GUILayout.Label(r.Name, Theme.Label, GUILayout.ExpandWidth(true));
				GUILayout.Label(r.SourceLabel, r.IsOverride ? Theme.OverrideLabel : r.IsMod ? Theme.ModLabel : Theme.Dim, GUILayout.Width(Columns[1].Width));
				GUILayout.Label(Columns[LifetimeColumn].Text(r), activeCountColumn == LifetimeColumn ? Theme.NumberActive : Theme.Number, GUILayout.Width(Columns[LifetimeColumn].Width));
				GUILayout.Label(Columns[RunColumn].Text(r), activeCountColumn == RunColumn ? Theme.NumberActive : Theme.Number, GUILayout.Width(Columns[RunColumn].Width));
				GUILayout.EndHorizontal();
			}

			if (_rows.Count > shown)
			{
				GUILayout.Label((_rows.Count - shown) + " more rows not shown - use the filter.", Theme.Dim);
			}
			GUILayout.EndScrollView();
		}

		private void DrawFooter(KillLedger ledger)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Run " + ledger.RunTotal + "   Lifetime " + ledger.LifetimeTotal + "   Profile " + ledger.ProfileId, Theme.Dim);
			GUILayout.FlexibleSpace();
			GUILayout.Label(Cfg.ParsedToggleKey() + " closes  -  click a header to sort  -  drag the title to move", Theme.Hint);
			GUILayout.EndHorizontal();
		}
	}
}

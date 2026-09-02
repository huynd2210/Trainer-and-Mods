using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QM.KillTracker;

/// <summary>
/// Hosts the kill tracker window on our own DontDestroyOnLoad object rather than on the plugin
/// component, which the game destroys shortly after startup. Also polls the game state once a second
/// to spot new raids starting and campaign slot switches.
/// </summary>
internal sealed class KillTrackerHost : MonoBehaviour
{
	private const int WindowId = 0x51544B54; // arbitrary, just needs to be unique per IMGUI window
	private const int MaxRenderedRows = 200;
	private const float PollIntervalSeconds = 1f;
	private const float MinSaveIntervalSeconds = 0.5f;

	// The scope and axis selectors are small lookup tables, not if/else chains: adding a scope or
	// an axis later is one row here plus one rendering pass, nothing else to touch.
	private readonly ScopeOption[] _scopes =
	{
		new ScopeOption("Raid", () => KillLedger.Instance.Raid, comparableToVanilla: false),
		new ScopeOption("Campaign", () => KillLedger.Instance.Campaign, comparableToVanilla: true),
		new ScopeOption("Lifetime", () => KillLedger.Instance.Lifetime, comparableToVanilla: false),
	};

	private readonly AxisOption[] _axes =
	{
		new AxisOption("Unit type", scope => scope.ByMobClass, id => KillLedger.Instance.GetMobDisplayName(id)),
		new AxisOption("Faction", scope => scope.ByFaction, ResolveFactionName),
		new AxisOption("Creature class", scope => scope.ByCreatureClass, id => id),
	};

	private readonly List<Row> _rows = new List<Row>();
	private readonly List<Row> _filtered = new List<Row>();

	private bool _windowOpen;
	private Rect _windowRect = new Rect(60f, 60f, 560f, 520f);
	private Vector2 _scroll;
	private string _query = string.Empty;
	private bool _guiBroken;

	private int _scopeIndex;
	private int _axisIndex;
	private int _lastScope = -1;
	private int _lastAxis = -1;
	private string _lastQuery;
	private bool _rowsDirty = true;

	private float _pollTimer;
	private float _flushTimer;
	private bool _raidActive;
	private string _raidLocationId;
	private int _slot = -1;

	public static void Spawn()
	{
		var go = new GameObject("QM.KillTracker.Host");
		DontDestroyOnLoad(go);
		go.hideFlags = HideFlags.HideAndDontSave;
		go.AddComponent<KillTrackerHost>();
		Cfg.Log.LogInfo("[diag] host object spawned.");
	}

	private void Update()
	{
		try
		{
			if (!Cfg.Ready)
			{
				return;
			}

			HandleToggleKey();

			_pollTimer += Time.unscaledDeltaTime;
			if (_pollTimer >= PollIntervalSeconds)
			{
				_pollTimer = 0f;
				PollGameState();
			}

			float interval = Mathf.Max(MinSaveIntervalSeconds, Cfg.SaveIntervalSeconds.Value);
			_flushTimer += Time.unscaledDeltaTime;
			if (_flushTimer >= interval)
			{
				_flushTimer = 0f;
				KillLedger.Instance?.Flush();
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Host update failed: " + e);
		}
	}

	private void HandleToggleKey()
	{
		KeyCode mainKey = Cfg.ToggleKey.Value.MainKey;
		bool pressed = Cfg.ToggleKey.Value.IsDown()
			|| (mainKey != KeyCode.None && Input.GetKeyDown(mainKey));

		if (pressed)
		{
			_windowOpen = !_windowOpen;
			if (_windowOpen)
			{
				_guiBroken = false;
				_rowsDirty = true;
			}
			Cfg.Log.LogInfo("Kill tracker window " + (_windowOpen ? "opened." : "closed."));
		}
	}

	/// <summary>
	/// Watches for a new raid starting and for the campaign slot changing. State objects are
	/// resolved fresh on every call and never cached — the game replaces them wholesale on save load.
	/// </summary>
	private void PollGameState()
	{
		RaidMetadata raid = GameState.Get<RaidMetadata>();
		LocationMetadata location = GameState.Get<LocationMetadata>();
		bool raidActive = raid != null;
		string locationId = location != null ? location.LocationId : null;

		if (raidActive && !_raidActive)
		{
			// A raid just started: start counting from zero.
			KillLedger.Instance?.ResetRaid();
			_raidActive = true;
			_raidLocationId = locationId;
			Cfg.Log.LogInfo("New raid started; raid kill scope cleared.");
		}
		else if (raidActive && locationId != null && locationId != _raidLocationId)
		{
			// Moved to a different location without going back to the ship in between.
			KillLedger.Instance?.ResetRaid();
			_raidLocationId = locationId;
			Cfg.Log.LogInfo("Raid location changed; raid kill scope cleared.");
		}

		if (!raidActive && _raidActive)
		{
			_raidActive = false;
			_raidLocationId = null;
		}

		SavedGameMetadata saved = GameState.Get<SavedGameMetadata>();
		int slot = saved != null ? saved.Slot : -1;
		if (slot != _slot)
		{
			KillLedger.Instance?.SwitchCampaign(slot);
			_slot = slot;
			Cfg.Log.LogInfo("Campaign slot changed to " + slot + ".");
		}

		if (_windowOpen)
		{
			// Counts change during a raid, so refresh the table roughly once a second while open.
			_rowsDirty = true;
		}
	}

	private void OnGUI()
	{
		if (!Cfg.Ready || !_windowOpen || _guiBroken)
		{
			return;
		}

		try
		{
			_windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Kill Tracker");
		}
		catch (Exception e)
		{
			// An exception thrown from OnGUI recurs every frame, so shut the window rather than
			// filling the log with thousands of identical stack traces.
			_guiBroken = true;
			_windowOpen = false;
			Cfg.Log.LogError("Kill tracker window closed after an error: " + e);
		}
	}

	private void DrawWindow(int id)
	{
		RefreshRows();

		GUILayout.BeginHorizontal();
		for (int i = 0; i < _scopes.Length; i++)
		{
			if (GUILayout.Toggle(_scopeIndex == i, _scopes[i].Label))
			{
				_scopeIndex = i;
			}
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		for (int i = 0; i < _axes.Length; i++)
		{
			if (GUILayout.Toggle(_axisIndex == i, _axes[i].Label))
			{
				_axisIndex = i;
			}
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("Search", GUILayout.Width(50f));
		_query = GUILayout.TextField(_query ?? string.Empty);
		if (GUILayout.Button("Clear", GUILayout.Width(55f)))
		{
			_query = string.Empty;
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(4f);

		_scroll = GUILayout.BeginScrollView(_scroll);
		DrawRows();
		GUILayout.EndScrollView();

		GUILayout.Space(4f);
		DrawFooter();
	}

	/// <summary>
	/// Rebuilds the rows only when the scope, the axis, or the query actually changed, and refilters
	/// the cached rows only when the query did. OnGUI runs several times per frame, so neither step
	/// may run on every call.
	/// </summary>
	private void RefreshRows()
	{
		if (KillLedger.Instance == null)
		{
			return;
		}

		if (_rowsDirty || _scopeIndex != _lastScope || _axisIndex != _lastAxis)
		{
			_lastScope = _scopeIndex;
			_lastAxis = _axisIndex;
			_rowsDirty = false;
			RebuildRows();
			_lastQuery = null; // force the filter to re-run on the fresh rows
		}

		if (_query != _lastQuery)
		{
			_lastQuery = _query;
			ApplyQuery();
		}
	}

	private void RebuildRows()
	{
		_rows.Clear();

		KillScope scope = _scopes[_scopeIndex].Get();
		if (scope == null)
		{
			return;
		}

		AxisOption axis = _axes[_axisIndex];
		foreach (KeyValuePair<string, int> kv in axis.Map(scope))
		{
			_rows.Add(new Row(axis.NameFor(kv.Key), kv.Key, kv.Value));
		}

		_rows.Sort((a, b) =>
		{
			int byCount = b.Count.CompareTo(a.Count);
			return byCount != 0 ? byCount : string.CompareOrdinal(a.DisplayName, b.DisplayName);
		});
	}

	private void ApplyQuery()
	{
		_filtered.Clear();
		string needle = (_query ?? string.Empty).Trim().ToLowerInvariant();
		bool matchAll = needle.Length == 0;
		foreach (Row row in _rows)
		{
			if (matchAll || row.Matches(needle))
			{
				_filtered.Add(row);
			}
		}
	}

	private void DrawRows()
	{
		int shown = Mathf.Min(_filtered.Count, MaxRenderedRows);
		for (int i = 0; i < shown; i++)
		{
			Row row = _filtered[i];

			GUILayout.BeginHorizontal();
			GUILayout.Label(row.DisplayName);
			GUILayout.Label(row.Id, GUILayout.Width(180f));
			GUILayout.Label(row.Count.ToString(), GUILayout.Width(60f));
			GUILayout.EndHorizontal();
		}

		if (_filtered.Count > shown)
		{
			GUILayout.Space(4f);
			GUILayout.Label($"{_filtered.Count - shown} more matches not shown — refine your search.");
		}
		else if (_filtered.Count == 0)
		{
			GUILayout.Label("No kills recorded in this view yet.");
		}
	}

	private void DrawFooter()
	{
		ScopeOption selected = _scopes[_scopeIndex];
		KillScope scope = selected.Get();
		if (scope == null)
		{
			return;
		}

		GUILayout.BeginHorizontal();
		GUILayout.Label("Total: " + scope.Total);
		if (selected.ComparableToVanilla)
		{
			Statistics statistics = GameState.Get<Statistics>();
			if (statistics != null)
			{
				GUILayout.Label("Vanilla counter: " + statistics.GetTotalKills());
			}
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.Space(4f);
		if (GUILayout.Button("Close"))
		{
			_windowOpen = false;
		}

		GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
	}

	private void OnApplicationQuit()
	{
		try
		{
			KillLedger.Instance?.Flush();
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Final kill stats flush failed: " + e);
		}
	}

	private static string ResolveFactionName(string id)
	{
		if (id == "none")
		{
			return "(no faction)";
		}
		return KillLedger.ResolveLocalizedName("faction." + id + ".name", id);
	}

	private sealed class ScopeOption
	{
		public readonly string Label;
		public readonly Func<KillScope> Get;

		/// <summary>
		/// Whether the vanilla kill counter is a like-for-like comparison for this scope. It is kept
		/// per campaign save, so it only lines up with the campaign scope — showing it beside the raid
		/// or lifetime totals would read as a mismatch rather than as a cross-check.
		/// </summary>
		public readonly bool ComparableToVanilla;

		public ScopeOption(string label, Func<KillScope> get, bool comparableToVanilla)
		{
			Label = label;
			Get = get;
			ComparableToVanilla = comparableToVanilla;
		}
	}

	private sealed class AxisOption
	{
		public readonly string Label;
		public readonly Func<KillScope, Dictionary<string, int>> Map;
		public readonly Func<string, string> NameFor;

		public AxisOption(string label, Func<KillScope, Dictionary<string, int>> map, Func<string, string> nameFor)
		{
			Label = label;
			Map = map;
			NameFor = nameFor;
		}
	}

	private sealed class Row
	{
		public readonly string DisplayName;
		public readonly string Id;
		public readonly int Count;
		public readonly string SearchBlob;

		public Row(string displayName, string id, int count)
		{
			DisplayName = displayName;
			Id = id;
			Count = count;
			SearchBlob = (id + " " + displayName).ToLowerInvariant();
		}

		public bool Matches(string needle) => SearchBlob.Contains(needle);
	}
}

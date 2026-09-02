using System;
using MGSC;
using UnityEngine;

namespace QM.Spawner;

/// <summary>
/// Hosts the spawner window on our own DontDestroyOnLoad object. The plugin component itself is
/// destroyed by the game shortly after startup, so its Update/OnGUI cannot be relied on.
/// </summary>
internal sealed class SpawnerHost : MonoBehaviour
{
	private const int WindowId = 0x51535057; // arbitrary, just needs to be unique per IMGUI window
	private const int MaxRenderedRows = 200;

	private readonly ItemCatalog _catalog = new ItemCatalog();

	private bool _windowOpen;
	private Rect _windowRect = new Rect(60f, 60f, 560f, 520f);
	private Vector2 _scroll;
	private string _query = string.Empty;
	private string _quantityText = "1";
	private string _status = "Ready.";
	private bool _guiBroken;

	public static void Spawn()
	{
		var go = new GameObject("QM.Spawner.Host");
		DontDestroyOnLoad(go);
		go.hideFlags = HideFlags.HideAndDontSave;
		go.AddComponent<SpawnerHost>();
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

			KeyCode mainKey = Cfg.ToggleKey.Value.MainKey;
			bool pressed = Cfg.ToggleKey.Value.IsDown()
				|| (mainKey != KeyCode.None && Input.GetKeyDown(mainKey));

			if (pressed)
			{
				_windowOpen = !_windowOpen;
				if (_windowOpen)
				{
					// Item ids can change when mods load, so rescan each time the window opens.
					_catalog.Invalidate();
					_guiBroken = false;
				}
				Cfg.Log.LogInfo("Spawner window " + (_windowOpen ? "opened." : "closed."));
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Toggle key handling failed: " + e);
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
			_windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Item Spawner");
		}
		catch (Exception e)
		{
			// An exception thrown from OnGUI recurs every frame, so shut the window rather than
			// filling the log with thousands of identical stack traces.
			_guiBroken = true;
			_windowOpen = false;
			Cfg.Log.LogError("Spawner window closed after an error: " + e);
		}
	}

	private void DrawWindow(int id)
	{
		_catalog.Refresh(_query, Cfg.HideInternalItems.Value);

		GUILayout.BeginHorizontal();
		GUILayout.Label("Search", GUILayout.Width(50f));
		_query = GUILayout.TextField(_query ?? string.Empty);
		if (GUILayout.Button("Clear", GUILayout.Width(55f)))
		{
			_query = string.Empty;
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("Quantity", GUILayout.Width(60f));
		_quantityText = GUILayout.TextField(_quantityText ?? "1", GUILayout.Width(60f));
		foreach (int preset in new[] { 1, 5, 10, 50 })
		{
			if (GUILayout.Button(preset.ToString(), GUILayout.Width(38f)))
			{
				_quantityText = preset.ToString();
			}
		}
		GUILayout.FlexibleSpace();
		GUILayout.Label($"{_catalog.Filtered.Count} / {_catalog.TotalCount}");
		GUILayout.EndHorizontal();

		GUILayout.Space(4f);

		int quantity = ParseQuantity();
		int shown = Mathf.Min(_catalog.Filtered.Count, MaxRenderedRows);

		_scroll = GUILayout.BeginScrollView(_scroll);
		for (int i = 0; i < shown; i++)
		{
			CatalogEntry entry = _catalog.Filtered[i];

			GUILayout.BeginHorizontal();
			GUILayout.Label(entry.Id, GUILayout.Width(230f));
			GUILayout.Label(entry.DisplayName);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Spawn", GUILayout.Width(60f)))
			{
				_status = SpawnItem(entry.Id, quantity);
			}
			GUILayout.EndHorizontal();
		}

		if (_catalog.Filtered.Count > shown)
		{
			GUILayout.Space(4f);
			GUILayout.Label($"{_catalog.Filtered.Count - shown} more matches not shown — refine your search.");
		}
		else if (_catalog.Filtered.Count == 0)
		{
			GUILayout.Label(_catalog.IsBuilt
				? "No items match that search."
				: "Item data is not loaded yet.");
		}
		GUILayout.EndScrollView();

		GUILayout.Space(4f);
		GUILayout.Label(_status);
		if (GUILayout.Button("Close"))
		{
			_windowOpen = false;
		}

		GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
	}

	private int ParseQuantity()
	{
		return int.TryParse(_quantityText, out int parsed) ? Mathf.Clamp(parsed, 1, 999) : 1;
	}

	/// <summary>
	/// Creates <paramref name="count"/> copies of an item and drops them in the ship's cargo, the
	/// same way the game's own <c>allitems</c> command does. State is resolved per call because it
	/// is replaced whenever a save is loaded.
	/// </summary>
	private static string SpawnItem(string itemId, int count)
	{
		MagnumCargo cargo = GameState.Get<MagnumCargo>();
		SpaceTime spaceTime = GameState.Get<SpaceTime>();

		if (cargo == null || spaceTime == null)
		{
			return "No campaign loaded — start or load a save first.";
		}

		ItemFactory factory = SingletonMonoBehaviour<ItemFactory>.Instance;
		if (factory == null)
		{
			return "Item factory unavailable.";
		}

		int spawned = 0;
		try
		{
			for (int i = 0; i < count; i++)
			{
				BasePickupItem item = factory.CreateForInventory(itemId);
				if (item == null)
				{
					return $"Could not create '{itemId}' (created {spawned} before failing).";
				}

				MagnumCargoSystem.AddCargo(cargo, spaceTime, item);
				spawned++;
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError($"Spawning '{itemId}' failed after {spawned}: {e}");
			return $"Error spawning '{itemId}' after {spawned} — see the BepInEx log.";
		}

		RefreshArsenalScreen();
		Cfg.Log.LogInfo($"Spawned {spawned} x {itemId} into ship cargo.");
		return $"Spawned {spawned} x {itemId} into ship cargo.";
	}

	/// <summary>Without this, items only appear after the cargo screen is closed and reopened.</summary>
	private static void RefreshArsenalScreen()
	{
		try
		{
			if (UI.IsShowing<ArsenalScreen>())
			{
				UI.Get<ArsenalScreen>().RefreshView();
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("Could not refresh the arsenal screen: " + e);
		}
	}
}

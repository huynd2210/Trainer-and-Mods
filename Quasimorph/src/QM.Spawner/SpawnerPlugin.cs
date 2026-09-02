using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace QM.Spawner;

[BepInPlugin(Guid, "Quasimorph Item Spawner", "1.0.1")]
public class SpawnerPlugin : BaseUnityPlugin
{
	public const string Guid = "com.claude.quasimorph.spawner";

	private void Awake()
	{
		Cfg.Log = Logger;

		Cfg.ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F10),
			"Opens and closes the spawner window.");

		Cfg.HideInternalItems = Config.Bind("General", "HideInternalItems", true,
			"Hide placeholder records and the implicit weapon/ammo parts that are not real items. "
			+ "Turn off to see the entire raw catalogue.");

		Cfg.Ready = true;

		// The window lives on its own DontDestroyOnLoad object: this plugin component gets
		// destroyed by the game shortly after startup, taking its Update/OnGUI with it.
		SpawnerHost.Spawn();

		Cfg.Log.LogInfo($"Item Spawner 1.0.1 loaded. Press {Cfg.ToggleKey.Value} to open.");
	}

	private void OnDestroy()
	{
		Cfg.Log?.LogWarning("[diag] plugin component was DESTROYED (expected; the window host survives).");
	}
}

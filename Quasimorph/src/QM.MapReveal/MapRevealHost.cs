using System;
using UnityEngine;

namespace QM.MapReveal;

/// <summary>
/// Hosts the toggle hotkey on our own DontDestroyOnLoad object rather than on the plugin
/// component, which the game destroys shortly after startup — taking any Update on it with it.
///
/// There is no permanent status overlay by design: this mod's whole output is the map screen, and
/// a readout parked over the HUD for the entire raid would cost more attention than it returns.
/// The toggle confirms itself with a short-lived message instead.
/// </summary>
internal sealed class MapRevealHost : MonoBehaviour
{
	private GUIStyle _feedbackStyle;
	private string _feedback = string.Empty;
	private float _feedbackUntil;

	public static void Spawn()
	{
		var go = new GameObject("QM.MapReveal.Host");
		DontDestroyOnLoad(go);
		go.hideFlags = HideFlags.HideAndDontSave;
		go.AddComponent<MapRevealHost>();
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

			if (!pressed)
			{
				return;
			}

			Cfg.Enabled.Value = !Cfg.Enabled.Value;
			_feedback = Cfg.Enabled.Value ? "Map Reveal ENABLED" : "Map Reveal DISABLED";
			_feedbackUntil = Time.realtimeSinceStartup + 3f;
			Cfg.Log.LogInfo(_feedback + " (reopen the map to see the change)");
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Host update failed: " + e);
		}
	}

	private void OnGUI()
	{
		try
		{
			if (!Cfg.Ready || Time.realtimeSinceStartup >= _feedbackUntil)
			{
				return;
			}

			_feedbackStyle ??= new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				alignment = TextAnchor.LowerLeft
			};

			GUI.Label(new Rect(10f, Screen.height - 26f, 640f, 20f), _feedback, _feedbackStyle);
		}
		catch (Exception e)
		{
			_feedbackUntil = 0f;
			Cfg.Log.LogError("Toggle feedback suppressed after an error: " + e);
		}
	}
}

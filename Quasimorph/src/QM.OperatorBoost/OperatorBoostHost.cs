using System;
using System.Collections.Generic;
using UnityEngine;

namespace QM.OperatorBoost;

/// <summary>
/// Hosts the hotkey, the status overlay and the max-health refresh on our own DontDestroyOnLoad
/// object rather than on the plugin component, which Quasimorph destroys shortly after startup —
/// Update and OnGUI on the plugin itself stop running for the rest of the session.
/// </summary>
internal sealed class OperatorBoostHost : MonoBehaviour
{
	private GUIStyle _overlayStyle;
	private string _toggleFeedback = string.Empty;
	private float _toggleFeedbackUntil;
	private float _nextHeartbeat;

	public static void Spawn()
	{
		var go = new GameObject("QM.OperatorBoost.Host");
		DontDestroyOnLoad(go);
		go.hideFlags = HideFlags.HideAndDontSave;
		go.AddComponent<OperatorBoostHost>();
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
				Cfg.Enabled.Value = !Cfg.Enabled.Value;
				_toggleFeedback = Cfg.Enabled.Value ? "Operator Boost ENABLED" : "Operator Boost DISABLED";
				_toggleFeedbackUntil = Time.realtimeSinceStartup + 3f;
				OperatorRoster.Invalidate();
				Cfg.Log.LogInfo(_toggleFeedback);
			}

			HealthRefresher.Tick();
			InventoryBoost.Tick();

			if (Cfg.Verbose && Time.realtimeSinceStartup >= _nextHeartbeat)
			{
				_nextHeartbeat = Time.realtimeSinceStartup + 30f;
				Cfg.Log.LogInfo($"[diag] host alive; Enabled={Cfg.Enabled.Value}, "
					+ $"operators={OperatorRoster.Current.Count}");
			}
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
			if (!Cfg.Ready || !Cfg.ShowStatusOverlay.Value)
			{
				return;
			}

			_overlayStyle ??= new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				alignment = TextAnchor.LowerLeft
			};

			string text;
			if (!Cfg.Enabled.Value)
			{
				text = "Operator Boost: OFF";
			}
			else
			{
				List<string> active = Boosts.ActiveSummary();
				text = active.Count == 0
					? "Operator Boost: ON (nothing set)"
					: "Operator Boost: " + string.Join(", ", active.ToArray());
			}

			if (Time.realtimeSinceStartup < _toggleFeedbackUntil)
			{
				text += "  <- " + _toggleFeedback;
			}

			// A grid the mod is refusing to shrink is invisible otherwise: the player lowers a
			// setting, nothing happens, and there is nothing on screen saying why.
			var waiting = new List<string>(InventoryBoost.DeferredShrinks);
			if (waiting.Count > 0)
			{
				text += "\n" + string.Join("; ", waiting.ToArray());
			}

			GUI.Label(new Rect(10f, Screen.height - 42f, Screen.width - 20f, 36f), text, _overlayStyle);
		}
		catch (Exception e)
		{
			Cfg.ShowStatusOverlay.Value = false;
			Cfg.Log.LogError("Status overlay disabled after an error: " + e);
		}
	}
}

using System;
using UnityEngine;
using MGSC;

namespace QM.WarpDrive;

/// <summary>
/// Hosts the hotkey and the status overlay on our own DontDestroyOnLoad object rather than on the
/// plugin component, so scene loads cannot take the UI down with them.
/// </summary>
internal sealed class WarpDriveHost : MonoBehaviour
{
	private GUIStyle _overlayStyle;
	private float _nextHeartbeat;
	private string _toggleFeedback = string.Empty;
	private float _toggleFeedbackUntil;

	public static void Spawn()
	{
		var go = new GameObject("QM.WarpDrive.Host");
		DontDestroyOnLoad(go);
		go.hideFlags = HideFlags.HideAndDontSave;
		go.AddComponent<WarpDriveHost>();
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
				_toggleFeedback = Cfg.Enabled.Value ? "Warp Drive ENABLED" : "Warp Drive DISABLED";
				_toggleFeedbackUntil = Time.realtimeSinceStartup + 3f;
				Cfg.Log.LogInfo(_toggleFeedback);
			}

			if (Cfg.Verbose && Time.realtimeSinceStartup >= _nextHeartbeat)
			{
				_nextHeartbeat = Time.realtimeSinceStartup + 30f;
				Cfg.Log.LogInfo($"[diag] host alive; Enabled={Cfg.Enabled.Value}, "
					+ $"campaign={(GameState.Get<TravelMetadata>() != null)}");
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

			string speed = Cfg.InstantTravel.Value
				? "INSTANT"
				: $"real x{Cfg.RealTimeSpeedMultiplier.Value:0.##}";

			string text = Cfg.Enabled.Value
				? $"Warp Drive: ON ({speed} / time x{Cfg.InGameTimeMultiplier.Value:0.##})"
				: "Warp Drive: OFF";

			if (Time.realtimeSinceStartup < _toggleFeedbackUntil)
			{
				text += "  <- " + _toggleFeedback;
			}

			GUI.Label(new Rect(10f, Screen.height - 26f, 640f, 20f), text, _overlayStyle);
		}
		catch (Exception e)
		{
			Cfg.ShowStatusOverlay.Value = false;
			Cfg.Log.LogError("Status overlay disabled after an error: " + e);
		}
	}
}

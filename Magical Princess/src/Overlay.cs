using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace MagicalPrincessTrainer
{
	/// <summary>
	/// The on-screen panel: the handful of numbers you actually play against, plus the key list.
	/// Everything is driven by the command registry, so it never needs editing when a cheat is added.
	/// </summary>
	internal sealed class Overlay
	{
		private const float Width = 288f;
		private const float Pad = 10f;
		private const float Row = 17f;
		private const float KeyColumn = 62f;

		private static readonly Color Background = new Color(0.055f, 0.04f, 0.07f, 0.88f);
		private static readonly Color Border = new Color(1f, 0.78f, 0.9f, 0.35f);
		private static readonly Color Accent = new Color(1f, 0.78f, 0.9f);
		private static readonly Color KeyColor = new Color(0.79f, 0.66f, 1f);
		private static readonly Color Muted = new Color(0.72f, 0.69f, 0.75f);
		private static readonly Color Value = new Color(1f, 1f, 1f);
		private static readonly Color On = new Color(0.55f, 0.88f, 0.63f);
		private static readonly Color Off = new Color(0.48f, 0.46f, 0.5f);

		private readonly List<ITrainerCommand> _commands;
		private readonly ConfigEntry<bool> _visible;
		private readonly ConfigEntry<float> _scale;

		private Texture2D _pixel;
		private GUIStyle _title;
		private GUIStyle _text;
		private GUIStyle _right;

		internal Overlay(List<ITrainerCommand> commands, ConfigEntry<bool> visible, ConfigEntry<float> scale)
		{
			_commands = commands;
			_visible = visible;
			_scale = scale;
		}

		internal void Draw(string toast)
		{
			if (!_visible.Value && toast == null)
			{
				return;
			}

			EnsureStyles();

			float scale = Mathf.Max(1f, Screen.height / 1080f) * Mathf.Clamp(_scale.Value, 0.5f, 3f);
			Matrix4x4 saved = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(new Vector3(14f, 14f, 0f), Quaternion.identity,
				new Vector3(scale, scale, 1f));

			float bottom = 0f;
			if (_visible.Value)
			{
				bottom = DrawPanel();
			}
			if (toast != null)
			{
				DrawToast(toast, bottom + 6f);
			}

			GUI.matrix = saved;
		}

		private float DrawPanel()
		{
			List<string[]> status = ReadStatus();
			float height = Pad + Row + 6f + status.Count * Row + 8f + _commands.Count * Row + Pad;

			Box(new Rect(0f, 0f, Width, height));

			float y = Pad;
			Label(new Rect(Pad, y, Width - Pad * 2f, Row), "Magical Princess Trainer", Accent, _title);
			y += Row + 6f;

			foreach (string[] line in status)
			{
				Label(new Rect(Pad, y, KeyColumn + 60f, Row), line[0], Muted, _text);
				Label(new Rect(Pad + KeyColumn + 60f, y, Width - Pad * 2f - KeyColumn - 60f, Row), line[1],
					Value, _right);
				y += Row;
			}

			y += 8f;
			foreach (ITrainerCommand command in _commands)
			{
				Label(new Rect(Pad, y, KeyColumn, Row), command.Key.Value.ToString(), KeyColor, _text);
				Label(new Rect(Pad + KeyColumn, y, Width - Pad * 2f - KeyColumn - 34f, Row), command.Label,
					Muted, _text);
				if (command.State != null)
				{
					Label(new Rect(Width - Pad - 34f, y, 34f, Row), command.State,
						command.State == "ON" ? On : Off, _right);
				}
				y += Row;
			}

			return height;
		}

		private void DrawToast(string message, float y)
		{
			float width = Mathf.Min(Width, _text.CalcSize(new GUIContent(message)).x + Pad * 2f);
			Box(new Rect(0f, y, width, Row + 8f));
			Label(new Rect(Pad, y + 4f, width - Pad * 2f, Row), message, Accent, _text);
		}

		/// <summary>The values worth having on screen without opening a menu.</summary>
		private static List<string[]> ReadStatus()
		{
			List<string[]> lines = new List<string[]>();
			if (Game.Ready)
			{
				BasicStatusData s = Game.Status;
				lines.Add(new[] { "Money", s.money.ToString("N0") });
				lines.Add(new[] { "Black coin", s.blackCoin.ToString("N0") });
				lines.Add(new[] { "Stress", s.stress + " / 100" });
				lines.Add(new[] { "Action points", s.activePower + " / " + Game.Data.GetActivePowerMax() });
				lines.Add(new[] { "Skill points", s.skillPoint.ToString() });
				lines.Add(new[] { "Battle level", s.levelBattle + "  (exp " + s.btlExp.ToString("N0") + ")" });
			}
			else
			{
				lines.Add(new[] { "No save loaded", "-" });
			}

			// Cross-run currency: present on the title screen as well, so it sits outside the gate.
			if (Game.MetaReady)
			{
				lines.Add(new[] { "Achievement pts", Game.Global.acvPoint.ToString("N0") });
			}
			return lines;
		}

		private void Box(Rect rect)
		{
			Color saved = GUI.color;
			GUI.color = Background;
			GUI.DrawTexture(rect, _pixel);
			GUI.color = Border;
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _pixel);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), _pixel);
			GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), _pixel);
			GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), _pixel);
			GUI.color = saved;
		}

		private static void Label(Rect rect, string text, Color color, GUIStyle style)
		{
			Color saved = GUI.color;
			GUI.color = color;
			GUI.Label(rect, text, style);
			GUI.color = saved;
		}

		private void EnsureStyles()
		{
			if (_pixel != null)
			{
				return;
			}

			_pixel = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			_pixel.SetPixel(0, 0, Color.white);
			_pixel.Apply();
			_pixel.hideFlags = HideFlags.HideAndDontSave;

			_text = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				alignment = TextAnchor.MiddleLeft,
				wordWrap = false,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0)
			};
			_text.normal.textColor = Color.white;

			_title = new GUIStyle(_text) { fontSize = 14, fontStyle = FontStyle.Bold };
			_title.normal.textColor = Color.white;

			_right = new GUIStyle(_text) { alignment = TextAnchor.MiddleRight };
			_right.normal.textColor = Color.white;
		}
	}
}

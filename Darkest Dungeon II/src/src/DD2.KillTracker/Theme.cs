using UnityEngine;

namespace DD2.KillTracker
{
	/// <summary>
	/// Darkest Dungeon II's look in IMGUI terms: near-black brown panels, aged-parchment text, blood-red rules and
	/// headers, tarnished-gold titles. Colours are named once here; nothing else in the UI picks a colour.
	/// Text/background pairs were checked for WCAG contrast: body text 10:1, dim text 5.2:1, gold 7:1 on rows,
	/// ember (override) 4.9:1, bright gold on blood-red 5.1:1 or better. Hover states keep text above 5:1 too.
	/// Styles are built lazily because GUIStyle can only be created inside OnGUI.
	/// </summary>
	internal static class Theme
	{
		public static readonly Color Panel = Hex(0x17110E);
		public static readonly Color PanelRow = Hex(0x1F1713);
		public static readonly Color PanelRowAlt = Hex(0x251B16);
		public static readonly Color Blood = Hex(0x7A1616);
		public static readonly Color BloodHover = Hex(0x8A1C1C);
		public static readonly Color Ember = Hex(0xE4604F);
		public static readonly Color BloodDeep = Hex(0x3A0D0D);
		public static readonly Color Parchment = Hex(0xD9CAA8);
		public static readonly Color ParchmentDim = Hex(0x9A8E73);
		public static readonly Color Gold = Hex(0xC8A257);
		public static readonly Color GoldBright = Hex(0xE0BC6A);
		public static readonly Color InputBack = Hex(0x0F0B09);

		private static GUIStyle _window, _title, _label, _dim, _number, _numberActive, _headerButton, _headerActive,
			_tabOff, _tabOn, _field, _row, _rowAlt, _mod, _override, _hint, _scroll, _clear;
		private static bool _built;

		public static GUIStyle Window => Build(ref _window);
		public static GUIStyle Title => Build(ref _title);
		public static GUIStyle Label => Build(ref _label);
		public static GUIStyle Dim => Build(ref _dim);
		public static GUIStyle Number => Build(ref _number);
		public static GUIStyle NumberActive => Build(ref _numberActive);
		public static GUIStyle HeaderButton => Build(ref _headerButton);
		public static GUIStyle HeaderActive => Build(ref _headerActive);
		public static GUIStyle TabOff => Build(ref _tabOff);
		public static GUIStyle TabOn => Build(ref _tabOn);
		public static GUIStyle Field => Build(ref _field);
		public static GUIStyle Row => Build(ref _row);
		public static GUIStyle RowAlt => Build(ref _rowAlt);
		public static GUIStyle ModLabel => Build(ref _mod);
		public static GUIStyle OverrideLabel => Build(ref _override);
		public static GUIStyle Hint => Build(ref _hint);
		public static GUIStyle ClearButton => Build(ref _clear);

		private static GUIStyle Build(ref GUIStyle slot)
		{
			// A texture Unity destroyed (scene unload) compares equal to null: rebuild instead of drawing unstyled.
			if (!_built || _window == null || _window.normal.background == null) BuildAll();
			return slot;
		}

		public static void Invalidate() => _built = false;

		private static void BuildAll()
		{
			_built = true;

			Texture2D panel = Bordered(Panel, Blood, 2);
			_window = new GUIStyle(GUI.skin.window)
			{
				border = new RectOffset(3, 3, 3, 3),
				padding = new RectOffset(12, 12, 30, 10),
				alignment = TextAnchor.UpperCenter,
				fontStyle = FontStyle.Bold,
				fontSize = 15,
				contentOffset = new Vector2(0f, -22f),
			};
			SetAll(_window, panel, Gold);

			_title = Text(Gold, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
			_label = Text(Parchment, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
			_dim = Text(ParchmentDim, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
			_number = Text(Parchment, 13, FontStyle.Normal, TextAnchor.MiddleRight);
			_numberActive = Text(Gold, 13, FontStyle.Bold, TextAnchor.MiddleRight);
			_mod = Text(Gold, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
			_override = Text(Ember, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
			_hint = Text(ParchmentDim, 11, FontStyle.Italic, TextAnchor.MiddleLeft);

			_row = Flat(PanelRow);
			_rowAlt = Flat(PanelRowAlt);

			_headerButton = ButtonStyle(BloodDeep, Blood, Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
			_headerActive = ButtonStyle(Blood, BloodHover, GoldBright, TextAnchor.MiddleLeft, FontStyle.Bold);
			_tabOff = ButtonStyle(PanelRowAlt, Blood, ParchmentDim, TextAnchor.MiddleCenter, FontStyle.Normal);
			_tabOn = ButtonStyle(Blood, BloodHover, GoldBright, TextAnchor.MiddleCenter, FontStyle.Bold);
			_clear = ButtonStyle(BloodDeep, Blood, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);

			_field = new GUIStyle(GUI.skin.textField)
			{
				fontSize = 13,
				alignment = TextAnchor.MiddleLeft,
				padding = new RectOffset(6, 6, 3, 3),
				border = new RectOffset(2, 2, 2, 2),
			};
			Texture2D fieldBack = Bordered(InputBack, BloodDeep, 1);
			Texture2D fieldFocus = Bordered(InputBack, Blood, 1);
			_field.normal.background = fieldBack;
			_field.hover.background = fieldBack;
			_field.focused.background = fieldFocus;
			_field.active.background = fieldFocus;
			_field.normal.textColor = Parchment;
			_field.hover.textColor = Parchment;
			_field.focused.textColor = Parchment;
			_field.active.textColor = Parchment;
		}

		private static GUIStyle Text(Color color, int size, FontStyle fontStyle, TextAnchor anchor)
		{
			var s = new GUIStyle(GUI.skin.label)
			{
				fontSize = size,
				fontStyle = fontStyle,
				alignment = anchor,
				wordWrap = false,
				clipping = TextClipping.Clip,
				padding = new RectOffset(4, 4, 1, 1),
			};
			s.normal.textColor = color;
			s.hover.textColor = color;
			return s;
		}

		private static GUIStyle Flat(Color back)
		{
			var s = new GUIStyle(GUI.skin.box) { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0), border = new RectOffset(0, 0, 0, 0) };
			SetAll(s, Solid(back), Parchment);
			return s;
		}

		private static GUIStyle ButtonStyle(Color back, Color hoverBack, Color text, TextAnchor anchor, FontStyle fontStyle)
		{
			var s = new GUIStyle(GUI.skin.button)
			{
				fontSize = 12,
				fontStyle = fontStyle,
				alignment = anchor,
				padding = new RectOffset(6, 6, 3, 3),
				margin = new RectOffset(1, 1, 1, 1),
				border = new RectOffset(0, 0, 0, 0),
			};
			Texture2D normal = Solid(back);
			Texture2D hover = Solid(hoverBack);
			s.normal.background = normal;
			s.hover.background = hover;
			s.active.background = hover;
			s.focused.background = normal;
			s.normal.textColor = text;
			s.hover.textColor = GoldBright;
			s.active.textColor = GoldBright;
			s.focused.textColor = text;
			return s;
		}

		private static void SetAll(GUIStyle s, Texture2D bg, Color text)
		{
			s.normal.background = bg; s.hover.background = bg; s.active.background = bg; s.focused.background = bg;
			s.onNormal.background = bg; s.onHover.background = bg; s.onActive.background = bg; s.onFocused.background = bg;
			s.normal.textColor = text; s.hover.textColor = text; s.active.textColor = text; s.focused.textColor = text;
			s.onNormal.textColor = text; s.onHover.textColor = text; s.onActive.textColor = text; s.onFocused.textColor = text;
		}

		private static Texture2D Solid(Color c)
		{
			var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			t.SetPixel(0, 0, c);
			t.Apply();
			return t;
		}

		/// <summary>A small texture with a coloured rim, used as a 9-slice (the style's border must equal <paramref name="rim"/> or more).</summary>
		private static Texture2D Bordered(Color fill, Color rimColor, int rim)
		{
			const int size = 8;
			var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					bool edge = x < rim || y < rim || x >= size - rim || y >= size - rim;
					t.SetPixel(x, y, edge ? rimColor : fill);
				}
			}
			t.Apply();
			return t;
		}

		private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
	}
}

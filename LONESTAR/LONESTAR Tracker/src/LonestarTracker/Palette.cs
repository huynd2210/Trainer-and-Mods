using UnityEngine;

namespace LonestarTracker
{
    /// <summary>
    /// The game's own colours (RareColor), reused verbatim so the panel reads as part of
    /// LONESTAR rather than as a mod overlay. The rarity ramp - grey, blue, orange -
    /// already means "ordinary, notable, rare" in this game, which is exactly the
    /// minion / elite / boss ladder, so the tiers borrow it.
    /// </summary>
    internal static class Palette
    {
        private static Color Rgb(float r, float g, float b, float a = 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        public static readonly Color Minion = Rgb(201f, 201f, 201f);   // rareColor_0
        public static readonly Color Elite = Rgb(39f, 146f, 255f);     // rareColor_1
        public static readonly Color Boss = Rgb(255f, 96f, 0f);        // rareColor_2

        public static readonly Color Win = Rgb(92f, 230f, 92f);        // historyWin
        public static readonly Color Lose = Rgb(230f, 92f, 92f);       // historyLose

        public static readonly Color Text = Rgb(229f, 244f, 255f);     // DaysColor
        public static readonly Color Muted = Rgb(122f, 140f, 153f);    // eventGray
        public static readonly Color Heading = Rgb(204f, 234f, 255f);  // eventButtonTitle
        public static readonly Color Accent = Rgb(255f, 96f, 0f);      // rareColor_2
        public static readonly Color Highlight = Rgb(255f, 254f, 81f); // importentYellow

        public static readonly Color Panel = new Color(0.04f, 0.05f, 0.08f, 0.94f);
        public static readonly Color PanelHeader = new Color(0.07f, 0.09f, 0.13f, 0.98f);
        public static readonly Color RowOdd = new Color(1f, 1f, 1f, 0.03f);
        public static readonly Color Rule = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Hud = new Color(0.04f, 0.05f, 0.08f, 0.72f);

        public static Color ForTier(Tier tier)
        {
            if (tier == Tier.Boss) return Boss;
            if (tier == Tier.Elite) return Elite;
            return Minion;
        }

        public static Color ForResult(bool won) { return won ? Win : Lose; }

        public static Color ForOutcome(string outcome)
        {
            if (outcome == "Win") return Win;
            if (outcome == "Loss") return Lose;
            return Muted;
        }
    }
}

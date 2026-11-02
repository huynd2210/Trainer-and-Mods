using System.Collections.Generic;
using System.Linq;

namespace LotteryForesight
{
    // Adds the outcome to lottery and scratch ticket tooltips, wherever the ticket is:
    // on a seller's table, in your stock, or in your hands.
    internal static class ForesightTooltip
    {
        private const RenderHandler.ColorPalette Win = RenderHandler.ColorPalette.LightGreen;
        private const RenderHandler.ColorPalette Lose = RenderHandler.ColorPalette.LightRed;
        private const RenderHandler.ColorPalette Info = RenderHandler.ColorPalette.LightYellow;

        internal static void Add(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null)
                return;
            if (item.IsTag("LOTTERY_TICKET_TAG"))
                AddLottery(builder, item);
            else if (item.IsTag("SCRATCHER_TAG"))
                AddScratcher(builder, item);
        }

        // A ticket for the coming draw: the numbers that will come up, and what it wins.
        // Tickets already drawn show the result through the game's own tooltip.
        private static void AddLottery(RichTextBuilder builder, GameItem item)
        {
            if (!string.IsNullOrEmpty(item.GetTagReadonly("LOTTERY_WINNING_NUMBER_TAG").GetString()))
                return;
            if (item.GetTagReadonly("LOTTERY_EDITION_TAG").GetInt() != LotteryHelper.GetCurrentEdition())
                return;

            string draw = Forecast.For(LotteryHelper.GetCurrentEdition());
            HashSet<string> mine = new HashSet<string>(item.GetTagReadonly("LOTTERY_NUMBER_TAG").GetString().Split(' '));
            builder.Add("Next draw: ", Info);
            string[] drawn = draw.Split(' ');
            for (int i = 0; i < drawn.Length; i++)
            {
                builder.Add(drawn[i], mine.Contains(drawn[i]) ? Win : Info);
                if (i < drawn.Length - 1)
                    builder.Add(" ");
            }
            builder.AddLine();

            int matches = LotteryHelper.CountMatches(item, draw);
            int prize = LotteryHelper.GetPrizeForMatches(matches);
            if (prize > 0)
                builder.AddLine("Wins " + prize + " credits (" + matches + " of 6)", ignoreEmpty: true, Win, bold: true);
            else
                builder.AddLine("No prize (" + matches + " of 6)", ignoreEmpty: true, Lose, bold: true);
        }

        // The three symbols are set when the ticket is printed, so the prize is known
        // before scratching. Same rules as ScratcherHelper.UpdateValue.
        private static void AddScratcher(RichTextBuilder builder, GameItem item)
        {
            if (ScratcherHelper.IsFullyScratched(item))
                return;

            List<int> symbols = Enumerable.Range(1, 3)
                .Select(i => item.GetTagReadonly("SCRATCHER_SYMBOL" + i).GetInt())
                .ToList();
            IGrouping<int, int> best = symbols.GroupBy(s => s).OrderByDescending(g => g.Count()).First();
            bool special = best.Key == ScratcherHelper.NUTRIFRUIT_SYMBOL;
            int prize = 0;
            if (best.Count() >= 3)
                prize = special ? ScratcherHelper.MATCH_3_SPECIAL_REWARD : ScratcherHelper.MATCH_3_REWARD;
            else if (best.Count() == 2)
                prize = special ? ScratcherHelper.MATCH_2_SPECIAL_REWARD : ScratcherHelper.MATCH_2_REWARD;

            if (prize > 0)
                builder.AddLine("Scratch prize: " + prize + " credits", ignoreEmpty: true, Win, bold: true);
            else
                builder.AddLine("Scratch prize: nothing", ignoreEmpty: true, Lose, bold: true);
        }
    }
}

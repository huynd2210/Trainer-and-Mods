namespace PawnCounter
{
    // The deal, on the item itself: what the pawner asks while it sits on the counter, and
    // once you hold it, who comes back when, and what selling it would cost you.
    internal static class PawnTooltip
    {
        private const RenderHandler.ColorPalette Info = RenderHandler.ColorPalette.LightYellow;
        private const RenderHandler.ColorPalette Plain = RenderHandler.ColorPalette.LightGray;
        private const RenderHandler.ColorPalette Bad = RenderHandler.ColorPalette.LightRed;

        internal static void Add(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null || !State.Ready)
                return;

            if (item.IsTag(Tags.OfferLoan) && !GeneralHelper.IsItemOwned(item))
            {
                int loan = item.GetTagReadonly(Tags.OfferLoan).GetInt();
                int redeem = item.GetTagReadonly(Tags.OfferRedeem).GetInt();
                builder.AddLine("Pawn: lend " + Pawners.Credits(loan) + ", buyback " + Pawners.Credits(redeem)
                    + " on day " + item.GetTagReadonly(Tags.OfferDue).GetInt(), ignoreEmpty: true, Info);
                builder.AddLine("Your fee " + Pawners.Credits(redeem - loan) + " (" + Pawners.FeeBasis(item) + ")", ignoreEmpty: true, Plain);
                return;
            }

            if (!item.IsTag(Tags.PawnId) || !GeneralHelper.IsItemOwned(item))
                return;
            Pawn pawn = State.Find(item.GetTagReadonly(Tags.PawnId).GetInt());
            if (pawn == null)
                return;
            int today = State.Today;
            string when = pawn.DueDay > today ? "on day " + pawn.DueDay
                : pawn.DueDay == today ? "today" : "late (was due day " + pawn.DueDay + ")";
            builder.AddLine("Pawned by " + pawn.Owner + ": back " + when + " to pay " + Pawners.Credits(pawn.Redeem), ignoreEmpty: true, Info);
            if (item.IsTag(Pool.LockedContainerTag))
                builder.AddLine("Locked: open it and it counts as sold", ignoreEmpty: true, Bad);
            builder.AddLine("Sell it and they'll want its worth (" + Pawners.Credits(pawn.Worth) + ") plus what you got for it, +"
                + PawnCounterMod.LossPremiumPercent + "%", ignoreEmpty: true, Bad);
        }
    }
}

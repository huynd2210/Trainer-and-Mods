using HarmonyLib;
using UnityEngine;

namespace TicketBooth
{
    // The game's mod loader applies every [HarmonyPatch] class in the assembly.

    // A licensed retailer has the lottery, even before day 15: the weekly draw runs.
    [HarmonyPatch(typeof(LotteryHelper), nameof(LotteryHelper.IsLotteryUnlocked))]
    internal static class LotteryUnlockPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && State.Licensed)
                __result = true;
        }
    }

    // Runs after the draw has coloured your own tickets and moved to the next edition.
    [HarmonyPatch(typeof(LotteryHelper), nameof(LotteryHelper.OnDrawWiningNumber))]
    internal static class DrawPatch
    {
        private static void Postfix()
        {
            Claims.OnDraw();
        }
    }

    // A ticket a customer cashed in with you is theirs to win, never yours to redeem,
    // real or fake. Paying one out spends it anyway; this is the guard. Covers
    // scratchers too, which redeem through here.
    [HarmonyPatch(typeof(LotteryHelper), nameof(LotteryHelper.CanRedeemPrize))]
    internal static class ClaimRedeemPatch
    {
        private static void Postfix(GameItem gameItem, ref bool __result)
        {
            if (__result && gameItem != null && gameItem.IsTag(Tags.Claim))
                __result = false;
        }
    }

    // A re-glued symbol sits crooked in the scratch view. Every slot is straightened
    // first, so ordinary tickets always look as before.
    [HarmonyPatch(typeof(ScratcherUIManager), nameof(ScratcherUIManager.SetPrizeSprite))]
    internal static class GlueLookPatch
    {
        private const float Tilt = 7f;

        private static void Postfix(ScratcherUIManager __instance)
        {
            GameItem item = __instance.currentActiveScratcherItem;
            int glued = item != null && item.IsTag(Tags.Glue) ? item.GetTagReadonly(Tags.Glue).GetInt() : 0;
            for (int i = 0; i < __instance.scratchInputs.Count; i++)
                __instance.scratchInputs[i].transform.localRotation = Quaternion.Euler(0f, 0f, i + 1 == glued ? Tilt : 0f);
        }
    }

    // Extra lines on a claim ticket's tooltip: what is being asked, the retailer stamp,
    // and the latest draw's edition, next to the game's own edition and number lines.
    internal static class TicketTooltip
    {
        private const RenderHandler.ColorPalette Info = RenderHandler.ColorPalette.LightYellow;
        private const RenderHandler.ColorPalette Plain = RenderHandler.ColorPalette.LightGray;
        private const RenderHandler.ColorPalette Bad = RenderHandler.ColorPalette.LightRed;
        private const RenderHandler.ColorPalette Hint = RenderHandler.ColorPalette.LightBlue;

        internal static void Add(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null || !item.IsTag(Tags.Claim))
                return;

            bool owned = GeneralHelper.IsItemOwned(item);
            if (owned && item.IsTag(Tags.Spent))
            {
                builder.AddLine("Paid out. Double-click to throw away", ignoreEmpty: true, Plain);
                if (item.IsTag(Tags.Forged))
                    builder.AddLine("It was a fake", ignoreEmpty: true, Bad);
            }

            if (!owned)
                builder.AddLine("Asking " + item.GetTagReadonly(Tags.Asked).GetInt().ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " credits", ignoreEmpty: true, Info);

            if (item.IsTag(Tags.StampDay))
                builder.AddLine("Retailer stamp: day " + item.GetTagReadonly(Tags.StampDay).GetInt(), ignoreEmpty: true, Plain);
            else
                builder.AddLine("No retailer stamp", ignoreEmpty: true, Plain);

            if (!owned && item.IsTag(LotteryHelper.LOTTERY_TICKET_TAG) && State.LastDrawEdition > 0)
                builder.AddLine("Latest draw: edition " + State.LastDrawEdition, ignoreEmpty: true, Plain);

            if (!owned && item.IsTag(ScratcherHelper.SCRATCHER_TAG))
                builder.AddLine("Activate to look at the symbols", ignoreEmpty: true, Hint);
        }
    }
}

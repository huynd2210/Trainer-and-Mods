namespace TicketBooth
{
    // A winner you paid out: their ticket is spent. It is trash worth 1 credit, like a
    // losing lottery ticket, and double-clicking it throws it away. You never get the
    // payout back; your margin on ticket sales is what pays the winners.
    internal static class Spent
    {
        private const int TrashValue = 1;

        // The part that decides what the ticket is worth. Pure tag work, so the offline
        // harness can check it.
        internal static void Mark(GameItem item)
        {
            item.EnableTag(Tags.Spent);
            item.DisableTag(LotteryHelper.LOTTERY_IS_WINNING_TAG);
            item.RemoveGameItemType("LOTTERY");
            item.SetValue(TrashValue);
        }

        // Mark, plus the look of a used lottery ticket and the throw-away action.
        internal static void Apply(GameItem item)
        {
            Mark(item);
            if (item.IsTag(LotteryHelper.LOTTERY_TICKET_TAG))
            {
                // Same as LotteryHelper.CheckTicket does for a losing ticket.
                item.SetSpriteAndShape("Items/items_amenities", "lottery_old");
                item.name = LocHelper.GetLocalizedItem("item_lottery_ticket_old_name");
            }
            AddDiscard(item);
        }

        // Item actions are not saved, so they are put back on every load.
        internal static void RestoreAfterLoad()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null)
                return;
            foreach (GameItem item in entry.GetAllOwnedItemsByTag(Tags.Spent))
                AddDiscard(item);
        }

        private static void AddDiscard(GameItem item)
        {
            item.canActivateSlotItemFunc = (GameItem it, GameInventory inv, SlotMarker marker) => GeneralHelper.IsItemOwned(it);
            item.mayActivateSlotItemFunc = (GameItem it, GameInventory inv, SlotMarker marker) => GeneralHelper.IsItemOwned(it);
            item.onActivateSlotItemFunc = delegate(GameItem it, GameInventory inv, SlotMarker marker)
            {
                PaperUIManager.Instance?.audioSourceDrop?.Play();
                it.Destroy();
            };
        }
    }
}

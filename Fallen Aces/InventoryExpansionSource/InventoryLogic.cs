namespace FallenAcesInventoryExpansion
{
    internal static class InventoryLogic
    {
        internal static int NextSlot(int current, int count, int direction)
        {
            if (count <= 0 || direction == 0) return current;
            if (current < 0 || current >= count) return direction < 0 ? 0 : count - 1;
            return (current + (direction < 0 ? 1 : count - 1)) % count;
        }
    }
}

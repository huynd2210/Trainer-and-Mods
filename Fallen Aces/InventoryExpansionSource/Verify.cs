using System;
using FallenAcesInventoryExpansion;
class Verify
{
    static void Main()
    {
        for (int count = 3; count <= 22; count++)
        {
            int current = 0;
            for (int i = 1; i <= count; i++)
            {
                current = InventoryLogic.NextSlot(current, count, -1);
                if (current != i % count) throw new Exception("Forward traversal failed");
            }
            for (int i = 1; i <= count; i++)
            {
                current = InventoryLogic.NextSlot(current, count, 1);
                if (current != (count - i) % count) throw new Exception("Backward traversal failed");
            }
            if (InventoryLogic.NextSlot(-1, count, -1) != 0 || InventoryLogic.NextSlot(-1, count, 1) != count - 1)
                throw new Exception("Holster exit failed");
            if (InventoryLogic.NextSlot(2, count, 0) != 2) throw new Exception("Zero direction changed selection");
        }
        if (InventoryLogic.NextSlot(-1, 0, 1) != -1) throw new Exception("Empty inventory failed");
        Console.WriteLine("PASS: both scroll directions visit every slot and wrap, capacities 3-22; holster, zero input and empty inventory.");
    }
}


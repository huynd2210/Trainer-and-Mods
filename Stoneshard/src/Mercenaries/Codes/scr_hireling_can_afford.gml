function scr_hireling_can_afford()
{
    // Coins, pouches and (in Brynn) bank exchange bills.
    return scr_gold_count() >= scr_hireling_fee();
}

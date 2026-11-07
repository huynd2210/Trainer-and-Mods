function scr_hireling_payday()
{
    // Midnight: one more day's wage is owed. Paid on the next player turn, when the purse can be checked.
    if (!scr_hireling_hired())
        return;
    var _owed = ds_map_find_value(global.characterDataMap, "mslHirelingDaysOwed")
    if is_undefined(_owed)
        _owed = 0
    ds_map_set(global.characterDataMap, "mslHirelingDaysOwed", _owed + 1)
}

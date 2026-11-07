function scr_hireling_is_away(argument0)
{
    // argument0: a tavern merc's object name. True while that merc serves the player, and forever once they died doing it.
    var _cdm = global.characterDataMap
    var _from = ds_map_find_value(_cdm, "mslHirelingFrom")
    if ((!is_undefined(_from)) && _from == argument0 && scr_hireling_hired())
        return true;
    var _gone = ds_map_find_value(_cdm, "mslHirelingGone")
    return (!is_undefined(_gone)) && string_pos(";" + argument0 + ";", _gone) > 0;
}

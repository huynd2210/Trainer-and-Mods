function scr_hireling_release(argument0)
{
    // Ends the service. argument0 true: the mercenary died, so they never return to their tavern.
    var _cdm = global.characterDataMap
    var _from = ds_map_find_value(_cdm, "mslHirelingFrom")
    if (argument0 && (!is_undefined(_from)))
    {
        var _gone = ds_map_find_value(_cdm, "mslHirelingGone")
        if is_undefined(_gone)
            _gone = ";"
        ds_map_set(_cdm, "mslHirelingGone", _gone + _from + ";")
    }
    scr_hireling_forget()
    ds_map_delete(_cdm, "mslHirelingFrom")
    ds_map_delete(_cdm, "mslHirelingName")
    ds_map_delete(_cdm, "mslHirelingDaysOwed")
    ds_map_delete(_cdm, "mslHirelingFollow")
    ds_map_delete(_cdm, "mslHirelingSprite")
    ds_map_delete(_cdm, "mslHirelingCorpse")
}

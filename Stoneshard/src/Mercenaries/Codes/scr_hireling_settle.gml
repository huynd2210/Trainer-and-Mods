function scr_hireling_settle()
{
    // Pay what is owed, or the mercenary walks.
    var _cdm = global.characterDataMap
    var _owed = ds_map_find_value(_cdm, "mslHirelingDaysOwed")
    if (is_undefined(_owed) || _owed < 1 || (!scr_hireling_hired()))
        return;
    var _sum = _owed * scr_hireling_wage()
    if (scr_gold_count() >= _sum)
    {
        scr_gold_write_off(_sum, noone, true)
        scr_characterGoldSpend("spentDialogues", _sum)
        ds_map_set(_cdm, "mslHirelingDaysOwed", 0)
        scr_hireling_say("~w~" + scr_hireling_name() + "~/~ takes their wage: ~y~" + string(_sum) + "~/~ crowns.")
        return;
    }
    scr_hireling_say("~w~" + scr_hireling_name() + "~/~ goes unpaid and leaves your service.")
    instance_activate_object(o_hireling)
    with (o_hireling)
        instance_destroy_enemy_not_execute() // a departure, not a death: no death log, no corpse
    scr_hireling_release(false)
}

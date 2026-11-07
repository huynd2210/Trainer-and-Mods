function scr_hireling_turn()
{
    // Runs at the end of every player turn. In a room the hireling has not reached yet, bring it in next to the
    // player; otherwise refresh its snapshot. Off-screen culling deactivates instances, so "not in this room yet"
    // is tracked by room, not by instance_exists.
    if (!variable_global_exists("msl_hireling_room"))
        global.msl_hireling_room = -1
    scr_hireling_settle()
    if (global.msl_hireling_room != room)
    {
        if instance_exists(o_hireling)
        {
            global.msl_hireling_room = room
            scr_hireling_snapshot()
            return;
        }
        var _m = ds_map_find_value(global.characterDataMap, "mslHireling")
        if ((!is_undefined(_m)) && ds_exists(_m, ds_type_map))
            scr_hireling_spawn(_m)
        return;
    }
    scr_hireling_snapshot()
}

function scr_hireling_snapshot()
{
    // The hireling's state lives in the character data, which the game saves with the character
    // (the same way it carries enemies chasing the player through doors).
    var _cdm = global.characterDataMap
    with (o_hireling)
    {
        if (HP < 1)
            continue
        if ds_map_exists(_cdm, "mslHireling")
        {
            var _old = ds_map_find_value(_cdm, "mslHireling")
            if ds_exists(_old, ds_type_map)
                ds_map_destroy(_old)
            ds_map_delete(_cdm, "mslHireling")
        }
        ds_map_add_map(_cdm, "mslHireling", scr_locationRoomEntityMobsSaveDataGet(id))
        ds_map_set(_cdm, "mslHirelingFollow", hireling_follow)
        global.msl_hireling_room = room
    }
}

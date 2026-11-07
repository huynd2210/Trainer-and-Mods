function scr_hireling_forget()
{
    if ds_map_exists(global.characterDataMap, "mslHireling")
    {
        var _old = ds_map_find_value(global.characterDataMap, "mslHireling")
        if ds_exists(_old, ds_type_map)
            ds_map_destroy(_old)
        ds_map_delete(global.characterDataMap, "mslHireling")
    }
    global.msl_hireling_room = room
}

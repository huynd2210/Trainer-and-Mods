function scr_hireling_hired()
{
    // A mercenary is in the player's service (its snapshot outlives the instance across rooms and saves).
    return ds_map_exists(global.characterDataMap, "mslHireling") || instance_exists(o_hireling);
}

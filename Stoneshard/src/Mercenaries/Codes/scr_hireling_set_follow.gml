function scr_hireling_set_follow(argument0)
{
    // argument0 true: follow the player; false: hold the current spot.
    with (o_hireling)
    {
        hireling_follow = argument0
        hireling_wait_x = x
        hireling_wait_y = y
        ds_map_set(global.characterDataMap, "mslHirelingFollow", hireling_follow)
        scr_hireling_say("~w~" + scr_hireling_name() + "~/~ " + (hireling_follow ? "follows you." : "holds this spot."))
    }
}

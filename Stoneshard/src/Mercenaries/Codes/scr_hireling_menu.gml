function scr_hireling_menu()
{
    // Right-click on the hireling: the game's own unit menu, with the order that applies now on top
    // ("Wait here" while following, "Follow me" while waiting) and Explore. No Attack.
    if (global.skill_activate || (!is_visible()) || (!visible))
        return;
    if (collision_point(global.guiMouseX, global.guiMouseY, c_GUI, 0, 0) || (!is_allow_actions()))
        return;
    scr_create_context_menu("Explore")
    var _me = id
    with (o_gui_context)
    {
        if (interact_id == _me)
        {
            ds_list_insert(context_desc, 0, 0)
            ds_list_insert(context_desc, 0, 1)
            ds_list_insert(context_name, 0, (_me.hireling_follow ? "Wait here" : "Follow me"))
            ds_list_insert(context_name, 0, (_me.hireling_follow ? "MslHirelingWait" : "MslHirelingFollow"))
        }
    }
}

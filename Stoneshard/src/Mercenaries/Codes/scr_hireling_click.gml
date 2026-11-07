function scr_hireling_click()
{
    // Left-click on the hireling never attacks it. Next to the player it swaps places (to squeeze past in
    // corridors, like the NPC "Swap"); further away it walks the player over.
    if (global.unit_inspect_id == id)
    {
        scr_exploreMenuOpen(id)
        return;
    }
    if (keyboard_check(vk_shift) || scr_mouse_on_unit() != id)
        return;
    var _is_gui = instance_position(global.guiMouseX, global.guiMouseY, c_GUI)
    if (_is_gui && _is_gui.object_index != o_context_button)
        return;
    if (global.inv_select || global.skill_activate || scr_is_cutscene() || (!is_allow_actions()))
        return;
    if ((!instance_exists(o_player)) || o_player.is_moving)
        return;
    scr_hireling_approach()
}

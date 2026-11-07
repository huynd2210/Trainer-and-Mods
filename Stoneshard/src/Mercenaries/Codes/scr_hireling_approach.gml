function scr_hireling_approach()
{
    // What a left-click on the hireling does once the input checks pass: swap when adjacent, else walk over.
    if ((!instance_exists(o_player)) || o_player.is_moving)
        return;
    if (scr_tile_distance(id, o_player) <= 1)
    {
        var _x = o_player.x
        var _y = o_player.y
        var _me = id
        with (o_player)
            scr_change_coordinat(_me.x, _me.y)
        scr_change_coordinat(_x, _y)
        ds_grid_set(o_controller.posgrid, grid_x, grid_y, id)
        scr_collision_add_enemy(o_controller.newgrid, grid_x, grid_y)
        hireling_wait_x = x
        hireling_wait_y = y
        if (global.floor_counter > 0)
            scr_allturn()
        return;
    }
    var _cell = scr_mpgridFindNearestFreeCell(o_controller.newgrid, (x div 26), (y div 26))
    if (_cell[0] != -4 && _cell[1] != -4)
    {
        with (o_player)
            scr_player_move((_cell[0] * 26), (_cell[1] * 26))
    }
}

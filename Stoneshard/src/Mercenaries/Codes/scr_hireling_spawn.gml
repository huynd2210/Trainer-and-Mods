function scr_hireling_spawn(argument0)
{
    // argument0: a snapshot map to restore, or -1 for a fresh hireling. Appears on a free cell next to the player.
    if ((!instance_exists(o_player)) || (!instance_exists(o_controller)))
        return -4;
    var _px = o_player.x
    var _py = o_player.y
    for (var _r = 1; _r <= 3; _r++)
    {
        for (var _dx = -_r; _dx <= _r; _dx++)
        {
            for (var _dy = -_r; _dy <= _r; _dy++)
            {
                if (max(abs(_dx), abs(_dy)) != _r)
                    continue
                var _x = _px + _dx * 26
                var _y = _py + _dy * 26
                if (_x < 26 || _y < 26 || _x > (room_width - 52) || _y > (room_height - 52))
                    continue
                var _gx = _x div 26
                var _gy = _y div 26
                if (scr_check_collision_not_enemy_around(_x, _y) || scr_tile_mark_get_instance(_gx, _gy) || astar_get_cell(o_controller.newgrid, _gx, _gy) == -1 || position_meeting(_x, _y, o_unit))
                    continue
                var _h = scr_enemy_create(_x, _y, o_hireling, false, false)
                if (_h == -4)
                    continue
                if (argument0 != -1 && ds_exists(argument0, ds_type_map))
                {
                    ds_map_set(argument0, "x", _x)
                    ds_map_set(argument0, "y", _y)
                    var _self = ds_map_find_value(argument0, "dataMap")
                    if ((!is_undefined(_self)) && ds_exists(_self, ds_type_map))
                    {
                        ds_map_set(_self, "draw_x", _x)
                        ds_map_set(_self, "draw_y", _y)
                        ds_map_set(_self, "start_x", _x)
                        ds_map_set(_self, "start_y", _y)
                        ds_map_set(_self, "grid_x", _gx)
                        ds_map_set(_self, "grid_y", _gy)
                    }
                    with (_h)
                    {
                        roomEntityIsLoaded = true
                        scr_locationRoomEntityMobsSaveDataSet(id, argument0)
                        state = "idle"
                        target = -4
                    }
                }
                with (_h)
                {
                    var _follow = ds_map_find_value(global.characterDataMap, "mslHirelingFollow")
                    hireling_follow = (is_undefined(_follow) ? true : _follow)
                    hireling_wait_x = x
                    hireling_wait_y = y
                    if ds_map_exists(global.characterDataMap, "mslHirelingName")
                        name = scr_hireling_name()
                    scr_hireling_look()
                    roomEntityIsSavable = false
                    skip_turn = false
                    scr_enemy_registration()
                    scr_unit_light_update()
                }
                global.msl_hireling_room = room
                with (_h)
                    scr_hireling_snapshot()
                return _h;
            }
        }
    }
    return -4;
}

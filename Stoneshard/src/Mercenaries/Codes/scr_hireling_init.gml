function scr_hireling_init()
{
    // A hireling fights for the Mercenary faction: monsters, brigands and the undead are its enemies, and it is theirs.
    scr_faction_map_remove(id)
    faction_key = "Mercenary"
    subfaction_key = "Mercenary"
    combat_array = []
    threat_array = []
    flee_array = []
    var _row = -1
    var _rows = array_length(global.animals_ai)
    for (var _i = 0; _i < _rows; _i++)
    {
        if (global.animals_ai[_i][0] == "Mercenary")
        {
            _row = _i
            break
        }
    }
    if (_row >= 0)
    {
        var _cols = array_length(global.animals_ai[0])
        for (var _j = 0; _j < _cols; _j++)
        {
            var _n = string_to_real(global.animals_ai[_row][_j])
            if (_n == 1 || _n == 2)
                array_push(combat_array, global.animals_ai[0][_j])
        }
    }
    scr_faction_map_add(id)
    hireling_follow = true
    hireling_wait_x = x
    hireling_wait_y = y
    loot_script = gml_Script_scr_loot_blank
    roomEntityIsSavable = false
}

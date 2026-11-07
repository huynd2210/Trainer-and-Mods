function scr_hireling_look()
{
    // Wear the hired merc's own combat sprite (the one the NPC draws when a fight breaks out) instead of the
    // deserter's, and leave their knocked-out pose as the body. Runs at spawn and is re-checked every turn,
    // since torches and stance changes can put the default sprite back.
    var _name = ds_map_find_value(global.characterDataMap, "mslHirelingSprite")
    if is_undefined(_name)
        return;
    var _spr = __asset_get_index(_name)
    if (!sprite_exists(_spr))
        return;
    if (sprite_index != _spr || default_sprite != _spr)
    {
        sprite_index = _spr
        image_index = 0
        scr_units_sprites_init()
        scr_set_hl()
    }
    var _corpse = __asset_get_index(string(ds_map_find_value(global.characterDataMap, "mslHirelingCorpse")))
    if sprite_exists(_corpse)
        corpse_sprite = _corpse
    // Bandits pick up "<sprite>_rest" as an idle animation; the deserter's would show the wrong person.
    if (variable_instance_exists(id, "idle_animation") && ds_exists(idle_animation, ds_type_list))
    {
        for (var _i = ds_list_size(idle_animation) - 1; _i >= 0; _i--)
        {
            if (string_pos("s_bandit_", sprite_get_name(ds_list_find_value(idle_animation, _i))) == 1)
                ds_list_delete(idle_animation, _i)
        }
    }
}

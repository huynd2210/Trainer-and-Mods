function scr_hireling_hire()
{
    // The merc leaves the tavern (hidden the way the game hides NPCs who are elsewhere) and joins the player.
    var _npc = scr_hireling_dialog_npc()
    if (!instance_exists(_npc))
        return;
    scr_gold_write_off(scr_hireling_fee(), noone, true)
    scr_characterGoldSpend("spentDialogues", scr_hireling_fee())
    var _cdm = global.characterDataMap
    ds_map_set(_cdm, "mslHirelingFrom", object_get_name(_npc.object_index))
    ds_map_set(_cdm, "mslHirelingName", string(_npc.name))
    ds_map_set(_cdm, "mslHirelingDaysOwed", 0)
    ds_map_set(_cdm, "mslHirelingFollow", true)
    // Their look: the NPC's fight sprite (NPCs derive it as "<base>_fight"), and "<base>_ko" as the body.
    var _base = (variable_instance_exists(_npc, "default_sprite") && sprite_exists(_npc.default_sprite)) ? _npc.default_sprite : _npc.sprite_index
    var _look = (variable_instance_exists(_npc, "fight_sprite") && sprite_exists(_npc.fight_sprite)) ? _npc.fight_sprite : __asset_get_index(sprite_get_name(_base) + "_fight")
    if (!sprite_exists(_look))
        _look = _base
    ds_map_set(_cdm, "mslHirelingSprite", sprite_get_name(_look))
    ds_map_set(_cdm, "mslHirelingCorpse", sprite_get_name(_base) + "_ko")
    with (_npc)
        scr_invis_enable_npc(false)
    instance_activate_object(o_hireling)
    with (o_hireling)
        instance_destroy_enemy_not_execute() // a departure, not a death: no death log, no corpse
    scr_hireling_forget()
    global.msl_hireling_room = -1
    scr_hireling_spawn(-1)
}

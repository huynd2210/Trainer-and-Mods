function scr_hireling_dialog_npc()
{
    // The tavern mercenary the player is talking to, or noone.
    if (!instance_exists(o_dialogue))
        return noone;
    var _npc = o_dialogue.owner
    if (instance_exists(_npc) && object_is_ancestor(_npc.object_index, o_npc_merc_rival_parent))
        return _npc;
    return noone;
}

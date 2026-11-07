function scr_hireling_dialogue_init()
{
    // Adds the hiring branch to the generic NPC dialogue (tavern mercs use it); scr_hireling_can_offer shows it to
    // mercs only. Runs once, after the game loads its dialogue flows.
    var _flow = variable_struct_get(global.__dialogue_flow_data, "npc_generic")
    if (!is_struct(_flow))
        return;
    var _frag = _flow.Fragments
    if variable_struct_exists(_frag, "condition_CND_MSLHIRE01")
        return;
    var _hub = variable_struct_get(_frag, "actions")
    if (!is_array(_hub))
        return;
    var _newHub = []
    var _added = false
    for (var i = 0; i < array_length(_hub); i++)
    {
        if ((!_added) && _hub[i] == "leave")
        {
            _newHub[array_length(_newHub)] = "condition_CND_MSLHIRE01"
            _added = true
        }
        _newHub[array_length(_newHub)] = _hub[i]
    }
    if (!_added)
        _newHub[array_length(_newHub)] = "condition_CND_MSLHIRE01"
    variable_struct_set(_frag, "actions", _newHub)
    variable_struct_set(_frag, "condition_CND_MSLHIRE01", "mercHire00_pc")
    variable_struct_set(_frag, "mercHire00_pc", "mercHire00")
    variable_struct_set(_frag, "mercHire00", ["mercHire00a_pc", "mercHire00b_pc"])
    variable_struct_set(_frag, "mercHire00a_pc", "mercHire00a")
    variable_struct_set(_frag, "mercHire00a", "instruction_INS_MSLHIRE01")
    variable_struct_set(_frag, "instruction_INS_MSLHIRE01", "@dialogue_end")
    variable_struct_set(_frag, "mercHire00b_pc", "jump_JMP_1A29B368")

    // Player lines are technical choices like "learn"; the hiring itself is an action like the training one.
    var _specs = _flow.Specs
    var _pcSpec = variable_struct_get(_specs, "learn")
    variable_struct_set(_specs, "mercHire00_pc", _pcSpec)
    variable_struct_set(_specs, "mercHire00a_pc", _pcSpec)
    variable_struct_set(_specs, "mercHire00b_pc", _pcSpec)
    variable_struct_set(_specs, "instruction_INS_MSLHIRE01", variable_struct_get(_specs, "instruction_INS_693E181F"))

    var _speakers = _flow.Speakers
    var _npc = variable_struct_get(_speakers, "Generic_NPC")
    var _newNpc = []
    for (i = 0; i < array_length(_npc); i++)
        _newNpc[i] = _npc[i]
    _newNpc[array_length(_newNpc)] = "mercHire00"
    _newNpc[array_length(_newNpc)] = "mercHire00a"
    variable_struct_set(_speakers, "Generic_NPC", _newNpc)
    var _player = variable_struct_get(_speakers, "Player")
    var _newPlayer = []
    for (i = 0; i < array_length(_player); i++)
        _newPlayer[i] = _player[i]
    _newPlayer[array_length(_newPlayer)] = "mercHire00_pc"
    _newPlayer[array_length(_newPlayer)] = "mercHire00a_pc"
    _newPlayer[array_length(_newPlayer)] = "mercHire00b_pc"
    variable_struct_set(_speakers, "Player", _newPlayer)

    var _scripts = _flow.Scripts
    variable_struct_set(_scripts, "condition_CND_MSLHIRE01", method(undefined, gml_Script_scr_hireling_can_offer))
    variable_struct_set(_scripts, "embedded_mercHire00a_pc", method(undefined, gml_Script_scr_hireling_can_afford))
    variable_struct_set(_scripts, "instruction_INS_MSLHIRE01", method(undefined, gml_Script_scr_hireling_hire))
}

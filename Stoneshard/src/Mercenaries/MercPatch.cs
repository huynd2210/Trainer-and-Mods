using ModShardLauncher;
using ModShardLauncher.Mods;
using UndertaleModLib.Models;

namespace Mercenaries;

// Everything the hireling needs, kept apart from the Mod class so a test harness can apply it without an .sml.
// A hireling is a combat mob (child of a brigand deserter, re-sworn to the Mercenary faction) that follows the player.
// It is kept out of room saves and carried in the character data instead, so it follows through doors, area edges
// and save/load alike: after every player turn it is either snapshotted, or, in a room it has not reached yet,
// rebuilt next to the player from the last snapshot.
public static class MercPatch
{
    public const string HirelingObject = "o_hireling";
    const string BaseObject = "o_bandit_deserter_sword";

    // Callees before callers: Msl.AddFunction fails on a call to a function registered later.
    static readonly string[] Functions =
    {
        "scr_hireling_fee", "scr_hireling_wage", "scr_hireling_say", "scr_hireling_name", "scr_hireling_look",
        "scr_hireling_init", "scr_hireling_forget", "scr_hireling_snapshot", "scr_hireling_hired", "scr_hireling_is_away",
        "scr_hireling_release", "scr_hireling_payday", "scr_hireling_settle", "scr_hireling_spawn",
        "scr_hireling_turn", "scr_hireling_set_follow", "scr_hireling_toggle", "scr_hireling_menu", "scr_hireling_approach", "scr_hireling_click",
        "scr_hireling_dialog_npc", "scr_hireling_can_offer", "scr_hireling_can_afford", "scr_hireling_hire",
        "scr_hireling_dialogue_init",
    };

    public static void Apply(Func<string, string> code)
    {
        AddHirelingObject();
        foreach (var f in Functions)
            Msl.AddFunction(code(f + ".gml"), f);

        var o = Msl.GetObject(HirelingObject);
        Msl.AddNewEvent(o, "event_inherited()\nscr_hireling_init()\n", EventType.Create, 0);
        Msl.AddNewEvent(o, TurnEvent, EventType.Other, 10);
        // Permanent death: the snapshot goes, and the merc never returns to their tavern.
        // (The game itself logs "<name> dies.")
        Msl.AddNewEvent(o, "event_inherited()\nif (HP < 1)\n    scr_hireling_release(true)\n", EventType.Destroy, 0);

        // Orders. Right-click: the unit menu with Wait here / Follow me (scr_hireling_menu). Left-click: swap or walk
        // over, never attack (scr_hireling_click). Both replace the enemy's own mouse events, not extend them.
        Msl.AddNewEvent(o, "scr_hireling_click()\n", EventType.Mouse, 4);
        Msl.AddNewEvent(o, "scr_hireling_menu()\n", EventType.Mouse, 5);
        Msl.LoadGML("gml_Object_o_context_button_Mouse_4")
            .MatchFrom("    switch func")
            .InsertAbove("    if (func == \"MslHirelingFollow\" || func == \"MslHirelingWait\")\n    {\n        var _mslFollow = (func == \"MslHirelingFollow\")\n        with (interact_id)\n            scr_hireling_set_follow(_mslFollow)\n    }")
            .Save();

        // Hiring. Tavern mercs talk through the generic NPC dialogue; the branch is added once the flows are loaded.
        Msl.LoadGML("gml_Object_o_dataLoader_Other_10")
            .MatchFrom("scr_dialogue_loader_init(")
            .InsertBelow("scr_hireling_dialogue_init()")
            .Save();
        // A merc in the player's service (or dead in it) is not in their tavern. Alarm 5 is where an NPC
        // decides whether it is present; the game's own silent removal (no death, no loot) takes them out.
        Msl.AddNewEvent(Msl.GetObject("o_npc_merc_rival_parent"),
            "event_inherited()\nif scr_hireling_is_away(object_get_name(object_index))\n    scr_invis_enable_npc(false, true)\n",
            EventType.Alarm, 5);
        // Wages: one day owed at every midnight.
        Msl.LoadGML("gml_GlobalScript_scr_timeEveryDayTrigger")
            .MatchFrom("scr_psy_set(\"ChatTime\", 0)")
            .InsertAbove("        scr_hireling_payday()")
            .Save();
        Msl.InjectTableDialogLocalization(
            Line("mercHire00_pc", "Looking for work? I could use a sword at my side."),
            Line("mercHire00", "Always. Five hundred crowns to sign on, then fifty a day, settled every midnight.#The day you can't pay me, I walk. And if I fall in your service, that's the end of it."),
            Line("mercHire00a_pc", "Deal. ~y~[500 crowns, then 50 a day]~/~"),
            Line("mercHire00a", "Then I'm yours. Lead the way. ~lg~[Right-click your mercenary to have them wait or follow]~/~"),
            Line("mercHire00b_pc", "Not now."));

        // Never treat the player as a target: no "sees the player -> attack" paths, no joining allies that attack them.
        // (The state chooser itself, scr_enemy_choose_state, can't be recompiled, so the two helpers it asks are patched.)
        Msl.LoadGML("gml_GlobalScript_scr_enemy_vision_check")
            .MatchFrom("if (!instance_exists(argument0))")
            .InsertAbove("    if (instance_exists(argument0) && instance_exists(argument1) && argument1.object_index == o_hireling && argument0.object_index == o_player)\n        return false;")
            .Save();
        Msl.LoadGML("gml_GlobalScript_scr_enemy_find_ally_attack_state")
            .MatchFrom("var _target = -4") // first one is in scr_enemy_find_ally_state, the first function of the script
            .InsertAbove("    if (argument0 == \"attack\" && object_index == o_hireling)\n        return -4;")
            .Save();
        // "attack" is the attack-the-player state: entered without a target, it targets the player. A hireling fights
        // other units in "npc combat" instead, or stands down. It also ignores noises (mostly the player's own steps)
        // and never enters the stealth states, which would show "becomes alarmed" and lead back to "attack".
        Msl.LoadGML("gml_GlobalScript_scr_stateAttack")
            .MatchFrom("var _is_visual_need = scr_isAttackVisualTransitionNeed(id)")
            .InsertAbove("    if (object_index == o_hireling)\n    {\n        if (instance_exists(target) && target.object_index != o_player && target.object_index != o_path_target && instance_is(target, o_unit))\n            scr_stateNPCCombat()\n        else\n        {\n            target = -4\n            state = \"idle\"\n        }\n        return;\n    }")
            .Save();
        foreach (var s in new[] { "gml_GlobalScript_scr_stateSearch", "gml_GlobalScript_scr_stateAlarm" })
            Msl.LoadGML(s)
                .MatchFrom("scr_suspicious()")
                .InsertAbove("    if (object_index == o_hireling)\n        return;")
                .Save();
        Msl.LoadGML("gml_GlobalScript_scr_enemy_noise_hearing_check")
            .MatchFrom("var _hearing = is_noise_hearing")
            .InsertAbove("    if (object_index == o_hireling)\n    {\n        is_noise_hearing = false\n        return false;\n    }")
            .Save();

        // Rooms never save the hireling (the character data carries it), and enemies chasing through a door don't take it.
        Msl.LoadGML("gml_GlobalScript_scr_locationRoomEntityMobsInstanceGet")
            .MatchFrom("if ((!persistent) && (!(instance_is(id, o_NPC))))")
            .ReplaceBy("        if ((!persistent) && (!(instance_is(id, o_NPC))) && object_index != o_hireling)")
            .Save();
        Msl.LoadGML("gml_Object_o_enemyTransitionsController_Other_10")
            .MatchFrom("if (ds_list_empty(lock_turn) && HP >= 1 && (!isFakeMob))")
            .ReplaceBy("            if (ds_list_empty(lock_turn) && HP >= 1 && (!isFakeMob) && object_index != o_hireling)")
            .Save();

        // A new player instance means a new room or a loaded game: the hireling has to be brought in again.
        Msl.LoadGML("gml_Object_o_player_Create_0")
            .MatchFrom("blink_has = true")
            .InsertBelow("global.msl_hireling_room = -1")
            .Save();
        // End of every player turn (also while waiting/resting, unlike the enemy transit update above it).
        Msl.LoadGML("gml_Object_o_player_Other_14")
            .MatchFrom("scr_enemyTransitTriggersUpdate()")
            .InsertBelow("scr_hireling_turn()")
            .Save();
    }

    // The hireling's turn. Following: suppress the idle patrol and step toward the player instead.
    // Waiting: patrol around the spot where it was told to wait.
    // "search"/"alarm" are the stealth states that end in attacking the player; a hireling never enters them.
    // A won fight leaves "npc combat" with no target, where the unit stands until a long timeout; end it at once.
    const string TurnEvent = @"scr_hireling_look()
if (state == ""search"" || state == ""alarm"")
    state = ""idle""
if (state == ""npc combat"" && (!instance_exists(target)))
{
    state = ""idle""
    target = -4
}
if hireling_follow
    fsm_idle_from_flee_time = 2
else
{
    x_start = hireling_wait_x
    y_start = hireling_wait_y
}
event_inherited()
if (hireling_follow && HP > 0 && state == ""idle"" && instance_exists(o_player))
{
    if (scr_tile_distance(id, o_player) > 2)
        scr_enemy_priority_path_target(o_player.x, o_player.y)
}
";

    static LocalizationSentence Line(string id, string en) =>
        new(id, new Dictionary<ModLanguage, string> { [ModLanguage.English] = en });

    static void AddHirelingObject()
    {
        var parent = Msl.GetObject(BaseObject);
        var o = Msl.AddObject(HirelingObject);
        o.ParentId = parent;
        o.Sprite = parent.Sprite;
        o.TextureMaskId = parent.TextureMaskId;
        o.Visible = parent.Visible;
        o.Solid = parent.Solid;
        o.Depth = parent.Depth;
        o.Persistent = false;
        o.Awake = parent.Awake;
        o.CollisionShape = parent.CollisionShape;
        o.UsesPhysics = parent.UsesPhysics;
    }
}

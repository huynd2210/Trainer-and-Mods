// Deadzoned Morgue -- a trad-roguelike character dump at the end of every run.
//
// When a run ends (death, seppuku, win, or "start new run" from the escape
// menu) this writes morgue/<date>_<class>_<result>.txt in the game's save
// folder and appends one line to morgue/runs.txt.
//
// The game keeps no history of a run, so the mod tracks one: kills by enemy,
// notable events, the last console messages, turns, play time and what hurt
// you last. The tracker is saved alongside the game's own run save so a
// resumed run keeps it.

function mrg_start()
{
    global.MRG_active = true;
    global.MRG_turns = 0;
    global.MRG_time = 0;
    global.MRG_taken = 0;
    global.MRG_src = "";
    global.MRG_srcDmg = 0;
    global.MRG_force = "";
    global.MRG_notes = [];
    global.MRG_msgs = [];
    global.MRG_killName = [];
    global.MRG_killNum = [];
    if (variable_global_exists("MRG_seen") && ds_exists(global.MRG_seen, ds_type_map))
        ds_map_clear(global.MRG_seen);
    else
        global.MRG_seen = ds_map_create();
    mrg_snapshot();
    mrg_note("Started a " + global.vClass + " run of " + mrg_mode_name());
}

function mrg_ready()
{
    return variable_global_exists("MRG_active");
}

// Remember what the notes poll compares against, so starting or loading a
// run does not log every existing level and talent as new.
function mrg_snapshot()
{
    global.MRG_lastLevel = global.vLevel;
    global.MRG_lastXPLevel = global.vXP_Level;
    global.MRG_talents = mrg_talent_list();
}

function mrg_talent_list()
{
    var t = [];
    for (var i = 1; i <= global.vTalentsMax; i++)
    {
        if (global.vTalent[i] != "")
            array_push(t, global.vTalent[i] + (global.vTalent_Boost[i] ? " II" : ""));
    }
    return t;
}

function mrg_clock()
{
    var s = floor(global.MRG_time);
    var h = s div 3600;
    var m = (s div 60) mod 60;
    s = s mod 60;
    return string(h) + ":" + mrg_pad2(m) + ":" + mrg_pad2(s);
}

function mrg_pad2(arg0)
{
    return (arg0 < 10 ? "0" : "") + string(arg0);
}

function mrg_note(arg0)
{
    if (array_length(global.MRG_notes) >= 300)
        array_delete(global.MRG_notes, 0, 1);
    array_push(global.MRG_notes, mrg_clean(mrg_clock() + "  floor " + string(global.vLevel) + "  " + arg0));
}

// Values go through the game's ini files and our '|' separated lists.
function mrg_clean(arg0)
{
    return string_replace_all(string_replace_all(string(arg0), "|", "/"), "\"", "'");
}

function mrg_mode_name()
{
    var n = "game mode " + string(global.vGameMode);
    if (global.vGameMode == 1)
        n = "The Outzone Bust Up";
    else if (global.vGameMode == 2)
        n = "Lair of the Beast";
    else if (global.vGameMode == 3)
        n = "True Horror Tales";
    if (global.vPlayingSeeded)
        n += " (seeded challenge)";
    return n;
}

function mrg_a(arg0)
{
    var c = string_lower(string_char_at(arg0, 1));
    if (c == "a" || c == "e" || c == "i" || c == "o" || c == "u")
        return "an " + arg0;
    return "a " + arg0;
}

function mrg_enemy_name(arg0)
{
    var n = "";
    if (variable_instance_exists(arg0, "vName"))
        n = arg0.vName;
    if (n == "")
        n = object_get_name(arg0.object_index);
    return n;
}

// ---- hooks ------------------------------------------------------------------

// Con_Global step: play time and the notes poll.
function mrg_tick()
{
    if (!mrg_ready() || !global.MRG_active)
        exit;
    if (room == rLevel && !instance_exists(EscapeMenu))
        global.MRG_time += delta_time / 1000000;
    if (global.vLevel != global.MRG_lastLevel)
    {
        global.MRG_lastLevel = global.vLevel;
        mrg_note("Reached floor " + string(global.vLevel));
    }
    if (global.vXP_Level != global.MRG_lastXPLevel)
    {
        global.MRG_lastXPLevel = global.vXP_Level;
        mrg_note("Reached character level " + string(global.vXP_Level));
    }
    var t = mrg_talent_list();
    if (array_length(t) != array_length(global.MRG_talents) || string(t) != string(global.MRG_talents))
    {
        for (var i = 0; i < array_length(t); i++)
        {
            if (!array_contains(global.MRG_talents, t[i]))
                mrg_note("Learned talent " + t[i]);
        }
        global.MRG_talents = t;
    }
}

// sTurnEnd, when the player's turn passes to the enemies.
function mrg_turn()
{
    if (mrg_ready())
        global.MRG_turns += 1;
}

// sConsole: keep the last 20 messages.
function mrg_msg(arg0)
{
    if (!mrg_ready())
        exit;
    if (array_length(global.MRG_msgs) >= 20)
        array_delete(global.MRG_msgs, 0, 1);
    array_push(global.MRG_msgs, mrg_clean(arg0));
}

// Name the next damage source when the caller's scope can't (poison ticks).
function mrg_cause(arg0)
{
    if (mrg_ready())
        global.MRG_force = arg0;
}

// sHurtHero, after the damage is applied. Runs in the attacker's scope for
// enemy attacks, and in the hero's (with the Explosion as other) for blasts.
function mrg_hurt(arg0, arg1)
{
    if (!mrg_ready() || arg1)
        exit;
    var src = "";
    if (global.MRG_force != "")
        src = global.MRG_force;
    else if (object_is_ancestor(object_index, Par_Enemy))
        src = mrg_a(mrg_enemy_name(id));
    else if (instance_exists(other) && other.object_index == Explosion)
        src = "an explosion";
    else
        src = "something (" + object_get_name(object_index) + ")";
    global.MRG_force = "";
    global.MRG_src = src;
    global.MRG_srcDmg = arg0;
    global.MRG_taken += arg0;
}

// sGiveHeroXPKill: every enemy the hero is credited with.
function mrg_kill(arg0)
{
    if (!mrg_ready() || !instance_exists(arg0) || ds_map_exists(global.MRG_seen, arg0))
        exit;
    ds_map_add(global.MRG_seen, arg0, 1);
    var n = mrg_enemy_name(arg0);
    var i = array_get_index(global.MRG_killName, n);
    if (i < 0)
    {
        array_push(global.MRG_killName, n);
        array_push(global.MRG_killNum, 1);
    }
    else
        global.MRG_killNum[i] += 1;
    if (string_pos("Boss", object_get_name(arg0.object_index)) > 0 || arg0.object_index == StarEye)
        mrg_note("Slew the " + n);
}

function mrg_kill_total()
{
    var t = 0;
    for (var i = 0; i < array_length(global.MRG_killNum); i++)
        t += global.MRG_killNum[i];
    return t;
}

// sSaveRun / sLoadRun: carry the tracker in the game's own run save.
function mrg_save(arg0)
{
    if (!mrg_ready())
        exit;
    var kills = "";
    for (var i = 0; i < array_length(global.MRG_killName); i++)
        kills += mrg_clean(global.MRG_killName[i]) + "=" + string(global.MRG_killNum[i]) + "|";
    ini_open(arg0);
    ini_write_real("Morgue", "Turns", global.MRG_turns);
    ini_write_real("Morgue", "Time", global.MRG_time);
    ini_write_real("Morgue", "Taken", global.MRG_taken);
    ini_write_string("Morgue", "Kills", kills);
    ini_write_string("Morgue", "Notes", mrg_join(global.MRG_notes));
    ini_write_string("Morgue", "Messages", mrg_join(global.MRG_msgs));
    ini_close();
}

function mrg_load(arg0)
{
    mrg_start();
    global.MRG_notes = [];
    ini_open(arg0);
    var tracked = ini_section_exists("Morgue");
    global.MRG_turns = ini_read_real("Morgue", "Turns", 0);
    global.MRG_time = ini_read_real("Morgue", "Time", 0);
    global.MRG_taken = ini_read_real("Morgue", "Taken", 0);
    var kills = mrg_split(ini_read_string("Morgue", "Kills", ""));
    global.MRG_notes = mrg_split(ini_read_string("Morgue", "Notes", ""));
    global.MRG_msgs = mrg_split(ini_read_string("Morgue", "Messages", ""));
    ini_close();
    for (var i = 0; i < array_length(kills); i++)
    {
        var eq = string_last_pos("=", kills[i]);
        if (eq > 0)
        {
            array_push(global.MRG_killName, string_copy(kills[i], 1, eq - 1));
            array_push(global.MRG_killNum, real(string_delete(kills[i], 1, eq)));
        }
    }
    mrg_snapshot();
    if (!tracked)
        mrg_note("Run resumed; the morgue mod was not tracking it before this point");
    else
        mrg_note("Run resumed");
}

function mrg_join(arg0)
{
    var s = "";
    for (var i = 0; i < array_length(arg0); i++)
        s += (i > 0 ? "|" : "") + arg0[i];
    return s;
}

function mrg_split(arg0)
{
    if (arg0 == "")
        return [];
    return string_split(arg0, "|", true);
}

// ---- the dump ---------------------------------------------------------------

function mrg_col(arg0, arg1)
{
    var s = string(arg0);
    if (string_length(s) < arg1)
        s += string_repeat(" ", arg1 - string_length(s));
    return s;
}

function mrg_rcol(arg0, arg1)
{
    var s = string(arg0);
    if (string_length(s) < arg1)
        s = string_repeat(" ", arg1 - string_length(s)) + s;
    return s;
}

function mrg_stat(arg0, arg1, arg2)
{
    var s = mrg_col(arg0, 8) + mrg_rcol(arg1, 3);
    if (arg1 != arg2)
        s += "  (base " + string(arg2) + ")";
    return s;
}

function mrg_weapon_lines(arg0)
{
    var id_ = global.vLdWep[arg0];
    var lines = [];
    if (!id_ || global.vItems[id_] == "")
    {
        array_push(lines, "  " + string(arg0) + "  (empty)");
        return lines;
    }
    var wt = global.vItems_WeaponType[id_];
    var s = (arg0 == global.vLdWep_Equip ? "* " : "  ") + string(arg0) + "  " + mrg_col(global.vItems[id_], 22) + mrg_col(string_lower(wt), 7);
    s += "dmg " + mrg_col(sGetHeroWeaponStat("Hero", "Damage", id_), 4);
    if (wt != "Melee")
    {
        s += "range " + mrg_col(sGetHeroWeaponStat("Hero", "Range", id_), 4);
        s += "ammo " + mrg_col(string(global.vLdWep_Ammo[arg0]) + "/" + string(sGetHeroWeaponStat("Hero", "Ammo", id_)), 7);
    }
    s += "durability " + string(global.vItems_Durability[id_]);
    array_push(lines, s);
    var traits = "";
    for (var t = 1; t <= 4; t++)
    {
        var tr = global.vItems_Trait[id_][t];
        if (tr != "")
            traits += (traits == "" ? "" : ", ") + tr + (global.vItems_Boost_Trait[id_][t] ? " +" : "");
    }
    if (traits != "")
        array_push(lines, "        traits: " + traits);
    return lines;
}

function mrg_is_loadout(arg0)
{
    for (var i = 1; i <= 3; i++)
    {
        if (global.vLdWep[i] == arg0)
            return true;
    }
    return false;
}

// Build and write the dump. arg0: "died", "won" or "abandoned".
function mrg_end(arg0)
{
    if (!mrg_ready() || !global.MRG_active || global.vClass == "Tutorial")
        exit;
    var result = arg0;
    var what = "";
    var cls = global.vClass;
    if (result == "died")
    {
        if (global.vHealth > 0)
        {
            result = "seppuku";
            what = "committed seppuku";
        }
        else
            what = "killed by " + (global.MRG_src == "" ? "something" : global.MRG_src);
    }
    else if (result == "won")
        what = "won " + mrg_mode_name();
    else
        what = "abandoned the run";
    if (global.vPlayingEndless && result != "won")
        what += " (endless)";
    var headline = what + " on floor " + string(global.vLevel);
    mrg_note(string_upper(string_char_at(what, 1)) + string_delete(what, 1, 1) + (result == "died" ? " (" + string(global.MRG_srcDmg) + " damage)" : ""));

    var dt = date_current_datetime();
    var stamp = string(date_get_year(dt)) + "-" + mrg_pad2(date_get_month(dt)) + "-" + mrg_pad2(date_get_day(dt));
    var clock = mrg_pad2(date_get_hour(dt)) + ":" + mrg_pad2(date_get_minute(dt));
    var kills = mrg_kill_total();

    var L = [];
    array_push(L, "  [Deadzoned morgue file]" + string_repeat(" ", 30) + stamp + " " + clock);
    array_push(L, "");
    array_push(L, "  " + cls + ", character level " + string(global.vXP_Level));
    array_push(L, "  " + headline);
    array_push(L, "");
    array_push(L, "  " + mrg_col("Mode", 11) + mrg_col(mrg_mode_name(), 34) + mrg_col("Score", 11) + string(global.vScore));
    array_push(L, "  " + mrg_col("Target", 11) + mrg_col(string_replace(global.vGameBoss, "Boss ", ""), 34) + mrg_col("Floor", 11) + string(global.vLevel));
    array_push(L, "  " + mrg_col("Seed", 11) + mrg_col(global.vSeed, 34) + mrg_col("Turns", 11) + string(global.MRG_turns));
    array_push(L, "  " + mrg_col("Play time", 11) + mrg_col(mrg_clock(), 34) + mrg_col("Kills", 11) + string(kills));
    array_push(L, "");
    array_push(L, "  " + mrg_col("Health", 11) + mrg_col(string(max(global.vHealth, 0)) + " / " + string(global.vHeroHealthMax), 21) + mrg_stat("Skill", global.vHeroSkill, global.vSkill));
    array_push(L, "  " + mrg_col("XP level", 11) + mrg_col(global.vXP_Level, 21) + mrg_stat("Power", global.vHeroPower, global.vPower));
    array_push(L, "  " + mrg_col("XP", 11) + mrg_col(string(global.vXP) + " / " + string(global.vXP_NextLevel), 21) + mrg_stat("Tech", global.vHeroTech, global.vTech));
    array_push(L, "  " + mrg_col("Dmg taken", 11) + mrg_col(global.MRG_taken, 21) + mrg_stat("Luck", global.vHeroLuck, global.vLuck));
    var status = "";
    if (global.vPoison)
        status += "poisoned (" + string(global.vPoison) + ")";
    if (global.vPlague)
        status += (status == "" ? "" : ", ") + "plague";
    if (status != "")
        array_push(L, "  " + mrg_col("Status", 11) + status);

    array_push(L, "");
    array_push(L, "  [Loadout]");
    for (var w = 1; w <= 3; w++)
    {
        var wl = mrg_weapon_lines(w);
        for (var k = 0; k < array_length(wl); k++)
            array_push(L, "  " + wl[k]);
    }

    // everything else carried, grouped by the game's item types
    var types = [];
    for (var i = 1; i <= global.vItemsMax; i++)
    {
        if (global.vItems[i] != "" && !mrg_is_loadout(i) && !array_contains(types, global.vItems_Type[i]))
            array_push(types, global.vItems_Type[i]);
    }
    for (var ti = 0; ti < array_length(types); ti++)
    {
        array_push(L, "");
        array_push(L, "  [" + types[ti] + "]");
        for (var i = 1; i <= global.vItemsMax; i++)
        {
            if (global.vItems[i] != "" && !mrg_is_loadout(i) && global.vItems_Type[i] == types[ti])
            {
                var line = "    " + global.vItems[i];
                if (global.vItems_Amount[i] > 1)
                    line += "  x" + string(global.vItems_Amount[i]);
                array_push(L, line);
            }
        }
    }

    array_push(L, "");
    array_push(L, "  [Talents]");
    var tl = mrg_talent_list();
    if (array_length(tl) == 0)
        array_push(L, "    (none)");
    for (var i = 0; i < array_length(tl); i++)
        array_push(L, "    " + tl[i]);

    array_push(L, "");
    array_push(L, "  [Kills]  " + string(kills) + " total");
    // most-killed first; ties keep first-killed order
    var order = [];
    for (var i = 0; i < array_length(global.MRG_killName); i++)
        array_push(order, i);
    for (var i = 1; i < array_length(order); i++)
    {
        var cur = order[i];
        var j = i - 1;
        while (j >= 0 && global.MRG_killNum[order[j]] < global.MRG_killNum[cur])
        {
            order[j + 1] = order[j];
            j--;
        }
        order[j + 1] = cur;
    }
    for (var i = 0; i < array_length(order); i++)
        array_push(L, "  " + mrg_rcol(global.MRG_killNum[order[i]], 5) + "  " + global.MRG_killName[order[i]]);

    array_push(L, "");
    array_push(L, "  [Notes]");
    for (var i = 0; i < array_length(global.MRG_notes); i++)
        array_push(L, "  " + global.MRG_notes[i]);

    array_push(L, "");
    array_push(L, "  [Last messages]");
    for (var i = 0; i < array_length(global.MRG_msgs); i++)
        array_push(L, "    " + global.MRG_msgs[i]);
    array_push(L, "");

    if (!directory_exists("morgue"))
        directory_create("morgue");
    var name = "morgue/" + stamp + "_" + mrg_pad2(date_get_hour(dt)) + "-" + mrg_pad2(date_get_minute(dt)) + "-" + mrg_pad2(date_get_second(dt)) + "_" + string_replace_all(cls, " ", "-") + "_" + result + ".txt";
    var f = file_text_open_write(name);
    for (var i = 0; i < array_length(L); i++)
    {
        file_text_write_string(f, L[i]);
        file_text_writeln(f);
    }
    file_text_close(f);

    f = file_text_open_append("morgue/runs.txt");
    file_text_write_string(f, stamp + " " + clock + "  " + mrg_col(cls, 16) + mrg_col(result, 10) + "floor " + mrg_col(global.vLevel, 4) + "score " + mrg_col(global.vScore, 8) + mrg_col(mrg_mode_name(), 22) + "  " + headline);
    file_text_writeln(f);
    file_text_close(f);

    global.MRG_lastFile = name;
    // A win can go on as an endless run, which keeps tracking; anything else
    // is over until the next sNewGame.
    global.MRG_active = result == "won";
}

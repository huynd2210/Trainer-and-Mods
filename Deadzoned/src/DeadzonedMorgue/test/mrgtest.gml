// Deadzoned Morgue -- in-game test driver. TEST BUILDS ONLY (built on top of
// the dice mod, so both mods run together).
//
// Plays real turns, scores kills, round-trips the run through the game's own
// save/load, then ends runs every way the mod handles: "start new run" and
// seppuku through the real escape-menu keys, death by a real enemy attack,
// and a win. Logs to mrg_test.log and quits itself.

function mrgt_log(arg0)
{
    var f = file_text_open_append("mrg_test.log");
    file_text_write_string(f, "[" + string(global.T_f) + "] " + arg0);
    file_text_writeln(f);
    file_text_close(f);
}

function mrgt_expect(arg0, arg1)
{
    if (arg1)
        mrgt_log("PASS " + arg0);
    else
    {
        mrgt_log("FAIL " + arg0);
        global.T_fail++;
    }
}

function mrgt_key(arg0)
{
    if (arg0 == "up") return global.vKey_Up;
    if (arg0 == "down") return global.vKey_Down;
    if (arg0 == "confirm") return global.vKey_MenuConfirm;
    if (arg0 == "back") return global.vKey_MenuBack;
    if (arg0 == "skip") return global.vKey_Skip;
    return -1;
}

function mrgt_enemy()
{
    var best = noone;
    var bd = 999999;
    with (Par_Enemy)
    {
        var d = point_distance(x, y, Par_Hero.x, Par_Hero.y);
        if (vHealth > 0 && object_index != Gnoblin && object_index != StarEye && d < bd)
        {
            best = id;
            bd = d;
        }
    }
    return best;
}

// Kill arg0 enemies through the real attack code (dice mod forces the hit).
function mrgt_kills(arg0)
{
    global.DZM_mode[0] = 1;
    global.DZM_mode[6] = 2;
    var done = 0;
    repeat (arg0)
    {
        var e = mrgt_enemy();
        if (e == noone)
            break;
        e.vHealth = 1;
        var o = instance_create(0, 0, HeroAction);
        o.vAction = "Attack";
        o.vTouchObj = e;
        with (o)
            event_perform(ev_alarm, 1);
        with (HeroAction)
            instance_destroy();
        if (e.vHealth <= 0)
        {
            done++;
            with (e)
                instance_destroy();
        }
    }
    global.DZM_mode[0] = 0;
    global.DZM_mode[6] = 0;
    return done;
}

function mrgt_file_has(arg0)
{
    if (!variable_global_exists("MRG_lastFile") || !file_exists(global.MRG_lastFile))
        return false;
    var f = file_text_open_read(global.MRG_lastFile);
    var all_ = "";
    while (!file_text_eof(f))
    {
        all_ += file_text_read_string(f) + "\n";
        file_text_readln(f);
    }
    file_text_close(f);
    return string_pos(arg0, all_) > 0;
}

function mrgt_step()
{
    if (!variable_global_exists("T_f"))
    {
        global.T_f = 0;
        global.T_i = 0;
        global.T_wait = 0;
        global.T_fail = 0;
        global.T_release = -1;
        global.T_file = "";
        global.T_lastLogged = -1;
        global.T_cmds = [
            "waitroom rTitle", "wait 30", "mouseoff",
            "newrun", "waitlevel", "wait 60",
            "key skip", "wait 40", "key skip", "wait 40", "key skip", "wait 40", "turns 3", "kills 3",
            "saveload",
            "menu", "wait 15", "key down", "key down", "key down", "key down", "key confirm", "state quit",
            "key confirm", "wait 30", "result abandoned",
            "waitlevel", "wait 60",
            "kills 2", "die", "waitroom rGameOver", "wait 10", "result died", "has killed by ",
            "newrun", "waitlevel", "wait 60", "win", "waitroom rWon", "wait 10", "result won",
            "newrun", "waitlevel", "wait 60",
            "menu", "wait 15", "key down", "key down", "key down", "key down", "key confirm", "state quit",
            "key down", "key confirm", "waitroom rGameOver", "wait 10", "result seppuku",
            "end"];
        mrgt_log("test start");
    }
    global.T_f++;
    if (global.T_release >= 0)
    {
        keyboard_key_release(global.T_release);
        global.T_release = -1;
    }
    if (global.T_f > 9000)
    {
        mrgt_expect("finished within time limit (stuck at: " + global.T_cmds[global.T_i] + ", room " + room_get_name(room) + ")", false);
        game_end();
        exit;
    }
    if (global.T_wait > 0)
    {
        global.T_wait--;
        exit;
    }
    if (global.T_i >= array_length(global.T_cmds))
        exit;
    var cmd = global.T_cmds[global.T_i];
    if (global.T_i != global.T_lastLogged)
    {
        global.T_lastLogged = global.T_i;
        mrgt_log("step " + cmd + "  (room " + room_get_name(room) + ", hp " + (variable_global_exists("vHealth") ? string(global.vHealth) : "-") + ")");
    }
    var sp = string_pos(" ", cmd);
    var verb = cmd;
    var rest = "";
    if (sp > 0)
    {
        verb = string_copy(cmd, 1, sp - 1);
        rest = string_delete(cmd, 1, sp);
    }
    var done = true;
    if (verb == "wait")
        global.T_wait = real(rest);
    else if (verb == "waitroom")
    {
        done = room_get_name(room) == rest;
        if (!done && room_get_name(room) == "rIntro" && global.T_f > 60)
            room_goto(asset_get_index(rest));
    }
    else if (verb == "mouseoff")
    {
        global.vStt_MouseSupport = false;
        global.vUsingPad = false;
    }
    else if (verb == "menu")
        instance_create(0, 0, EscapeMenu);
    else if (verb == "key")
    {
        keyboard_key_press(mrgt_key(rest));
        global.T_release = mrgt_key(rest);
        global.T_wait = 4;
    }
    else if (verb == "state")
    {
        var got = instance_exists(EscapeMenu) ? EscapeMenu.vState : "no menu";
        mrgt_expect("menu is '" + rest + "' (got '" + got + "')", got == rest);
    }
    else if (verb == "newrun")
    {
        global.vPlayingEndless = false;
        sRmTran(rLevel);
        sNewGame();
    }
    else if (verb == "waitlevel")
        done = room_get_name(room) == "rLevel" && instance_exists(Hero) && instance_exists(Par_Enemy) && !instance_exists(GenerateLevel) && global.vTurn == "Player";
    else if (verb == "kills")
    {
        var before = mrg_kill_total();
        var n = mrgt_kills(real(rest));
        mrgt_expect("kills tracked: " + string(n) + " killed, tracker went " + string(before) + " -> " + string(mrg_kill_total()), n > 0 && mrg_kill_total() == before + n);
    }
    else if (verb == "turns")
        mrgt_expect("turns counted (" + string(global.MRG_turns) + " >= " + rest + ")", global.MRG_turns >= real(rest));
    else if (verb == "saveload")
    {
        var k = mrg_kill_total();
        var t = global.MRG_turns;
        var nn = array_length(global.MRG_notes);
        var mm = array_length(global.MRG_msgs);
        sSaveRun();
        global.MRG_killName = [];
        global.MRG_killNum = [];
        global.MRG_turns = 0;
        sLoadRun();
        mrgt_expect("save/load keeps kills " + string(k) + "=" + string(mrg_kill_total()) + ", turns " + string(t) + "=" + string(global.MRG_turns) + ", notes " + string(nn) + "+1=" + string(array_length(global.MRG_notes)) + ", msgs " + string(mm) + "=" + string(array_length(global.MRG_msgs)),
            k == mrg_kill_total() && t == global.MRG_turns && array_length(global.MRG_notes) == nn + 1 && array_length(global.MRG_msgs) == mm);
    }
    else if (verb == "die")
    {
        global.DZM_mode[4] = 1;
        global.DZM_mode[2] = 2;
        global.vHealth = 1;
        var e = mrgt_enemy();
        global.T_killer = mrg_enemy_name(e);
        var o = instance_create(0, 0, EnemyAction);
        o.vAction = "Attack";
        o.vEnemyActor = e;
        with (o)
            event_perform(ev_alarm, 1);
        with (EnemyAction)
            instance_destroy();
        global.DZM_mode[4] = 0;
        global.DZM_mode[2] = 0;
        mrgt_log("attacked by " + global.T_killer + ", health now " + string(global.vHealth) + ", cause recorded: " + global.MRG_src);
    }
    else if (verb == "win")
        sRmTran(rWon);
    else if (verb == "result")
    {
        var f = variable_global_exists("MRG_lastFile") ? global.MRG_lastFile : "";
        mrgt_expect("new morgue for '" + rest + "': " + f, f != global.T_file && string_pos("_" + rest + ".txt", f) > 0 && file_exists(f));
        global.T_file = f;
    }
    else if (verb == "has")
        mrgt_expect("morgue says '" + rest + mrg_a(global.T_killer) + "'", mrgt_file_has(rest + mrg_a(global.T_killer)));
    else if (verb == "end")
    {
        mrgt_log("test end, failures: " + string(global.T_fail));
        game_end();
    }
    if (done)
    {
        global.T_i++;
        if (verb != "key" && verb != "wait")
            global.T_wait = 2;
    }
}

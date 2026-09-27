// Deadzoned Dice Mod -- in-game test driver. TEST BUILDS ONLY.
//
// Runs a fixed command list from Con_Global's Step: drives the escape menu
// through its real key handlers (keyboard_key_press), screenshots the pages,
// starts a run, then fires scripted attacks both ways under each override and
// tallies the text risers the game spawns ("miss", "dodge", "Damage", "Crit").
// Writes dzm_test.log in the save sandbox and quits itself.

function dzmt_log(arg0)
{
    var f = file_text_open_append("dzm_test.log");
    file_text_write_string(f, "[" + string(global.T_f) + "] " + arg0);
    file_text_writeln(f);
    file_text_close(f);
}

function dzmt_expect(arg0, arg1)
{
    if (arg1)
        dzmt_log("PASS " + arg0);
    else
    {
        dzmt_log("FAIL " + arg0);
        global.T_fail++;
    }
}

function dzmt_modes(arg0)
{
    for (var i = 0; i < 8; i++)
        global.DZM_mode[i] = arg0[i];
}

function dzmt_menu()
{
    if (!instance_exists(EscapeMenu))
        return "no menu";
    return EscapeMenu.vState + " " + string(EscapeMenu.vSel);
}

function dzmt_key(arg0)
{
    if (arg0 == "up") return global.vKey_Up;
    if (arg0 == "down") return global.vKey_Down;
    if (arg0 == "left") return global.vKey_Left;
    if (arg0 == "right") return global.vKey_Right;
    if (arg0 == "confirm") return global.vKey_MenuConfirm;
    if (arg0 == "back") return global.vKey_MenuBack;
    return -1;
}

// Count and clear the text risers: [miss, dodge, Damage, Crit, Death, other]
function dzmt_tally(arg0)
{
    global.T_c = arg0;
    with (TextRiser)
    {
        var k = 5;
        if (vType == "miss" || vType == "marked") k = 0;
        else if (vType == "dodge") k = 1;
        else if (vType == "Damage") k = 2;
        else if (vType == "Crit") k = 3;
        else if (vType == "Death") k = 4;
        global.T_c[@ k] += 1;
        instance_destroy();
    }
    return global.T_c;
}

function dzmt_fmt(arg0)
{
    return "miss " + string(arg0[0]) + " dodge " + string(arg0[1]) + " dmg " + string(arg0[2]) + " crit " + string(arg0[3]) + " death " + string(arg0[4]) + " other " + string(arg0[5]);
}

function dzmt_target()
{
    var best = noone;
    with (Par_Enemy)
    {
        if (best == noone && object_index != Gnoblin && object_index != StarEye && vHealth > 0)
            best = id;
    }
    return best;
}

function dzmt_hero(arg0, arg1)
{
    var c = [0, 0, 0, 0, 0, 0];
    dzmt_tally([0, 0, 0, 0, 0, 0]);
    repeat (arg1)
    {
        arg0.vHealth = 9999;
        arg0.vBusyState = "";
        arg0.vAutoCritHit = false;
        if (global.vLdWep[global.vLdWep_Equip])
            global.vItems_Durability[global.vLdWep[global.vLdWep_Equip]] = 99;
        global.vLdWep_Ammo[global.vLdWep_Equip] = 9;
        var o = instance_create(0, 0, HeroAction);
        o.vAction = "Attack";
        o.vTouchObj = arg0;
        with (o)
            event_perform(ev_alarm, 1);
        with (HeroAction)
            instance_destroy();
        c = dzmt_tally(c);
    }
    return c;
}

function dzmt_enemy(arg0, arg1)
{
    var c = [0, 0, 0, 0, 0, 0];
    dzmt_tally([0, 0, 0, 0, 0, 0]);
    repeat (arg1)
    {
        global.vHealth = 9999;
        arg0.vTurnAttacks = 0;
        var o = instance_create(0, 0, EnemyAction);
        o.vAction = "Attack";
        o.vEnemyActor = arg0;
        with (o)
            event_perform(ev_alarm, 1);
        with (EnemyAction)
            instance_destroy();
        c = dzmt_tally(c);
    }
    global.vHealth = 50;
    return c;
}

function dzmt_combat()
{
    var n = 200;
    var e = dzmt_target();
    if (e == noone)
    {
        dzmt_expect("found an enemy to test against", false);
        return;
    }
    dzmt_log("target " + object_get_name(e.object_index) + " weapon " + global.vLdWep_Name[global.vLdWep_Equip] + " class " + global.vClass);
    // one bare attack, to see what it leaves behind
    e.vHealth = 9999;
    var o = instance_create(0, 0, HeroAction);
    o.vAction = "Attack";
    o.vTouchObj = e;
    with (o)
        event_perform(ev_alarm, 1);
    var names = "";
    with (TextRiser)
        names += vType + ",";
    dzmt_log("diag: risers " + string(instance_number(TextRiser)) + " [" + names + "] target hp " + string(e.vHealth) + " HeroAction left " + string(instance_number(HeroAction)) + " o.target exists " + string(instance_exists(o) ? o.target : -1));
    with (HeroAction)
        instance_destroy();
    var d = [0, 0, 0, 0, 0, 0];
    d = dzmt_tally(d);
    dzmt_log("diag: tally after = " + dzmt_fmt(d));
    // modes: [hit, crit, dodge, procs, en hit, en crit, en dodge, hack]
    dzmt_modes([0, 0, 0, 0, 0, 0, 0, 0]);
    dzmt_log("hit chance shown, normal: " + string(sHero_HitChance(e)) + "  crit shown: " + string(sHeroAction_GetCritChance()) + "  hack shown: " + string(sHackTerminal_Chance()));
    var c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, all normal: " + dzmt_fmt(c));

    dzmt_modes([1, 0, 0, 0, 0, 0, 2, 0]);
    c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, hit always + enemy dodge never: " + dzmt_fmt(c));
    dzmt_expect("always hit: no misses or dodges", c[0] == 0 && c[1] == 0 && (c[2] + c[4]) == n);

    dzmt_modes([1, 1, 0, 0, 0, 0, 2, 0]);
    dzmt_log("hit chance shown, always: " + string(sHero_HitChance(e)) + "  crit shown, always: " + string(sHeroAction_GetCritChance()));
    c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, + crit always: " + dzmt_fmt(c));
    dzmt_expect("always crit: every hit crits", (c[3] + c[4]) == n && c[0] == 0);

    dzmt_modes([1, 2, 0, 0, 0, 0, 2, 0]);
    c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, + crit never: " + dzmt_fmt(c));
    dzmt_expect("never crit: no crits", c[3] == 0 && c[0] == 0);

    dzmt_modes([2, 0, 0, 0, 0, 0, 0, 0]);
    dzmt_log("hit chance shown, never: " + string(sHero_HitChance(e)));
    c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, hit never: " + dzmt_fmt(c));
    dzmt_expect("never hit: all misses", c[0] == n && c[2] == 0);

    dzmt_modes([1, 0, 0, 0, 0, 0, 1, 0]);
    c = dzmt_hero(e, n);
    dzmt_log("YOU ATTACK, hit always + enemy dodge always: " + dzmt_fmt(c));
    dzmt_expect("enemy always dodges", c[1] == n && c[2] == 0);

    dzmt_modes([0, 0, 0, 0, 0, 0, 0, 0]);
    c = dzmt_enemy(e, n);
    dzmt_log("ENEMY ATTACKS, all normal: " + dzmt_fmt(c));

    dzmt_modes([0, 0, 0, 0, 2, 0, 0, 0]);
    c = dzmt_enemy(e, n);
    dzmt_log("ENEMY ATTACKS, enemy hit never: " + dzmt_fmt(c));
    dzmt_expect("enemy never hits: all misses", c[0] == n && c[2] == 0 && c[1] == 0);

    dzmt_modes([0, 0, 2, 0, 1, 1, 0, 0]);
    c = dzmt_enemy(e, n);
    dzmt_log("ENEMY ATTACKS, en hit always + en crit always + your dodge never: " + dzmt_fmt(c));
    dzmt_expect("enemy always hits and crits", c[2] == n && c[3] == n && c[0] == 0 && c[1] == 0);

    dzmt_modes([0, 0, 2, 0, 1, 2, 0, 0]);
    c = dzmt_enemy(e, n);
    dzmt_log("ENEMY ATTACKS, en hit always + en crit never + your dodge never: " + dzmt_fmt(c));
    dzmt_expect("enemy never crits", c[2] == n && c[3] == 0);

    dzmt_modes([0, 0, 1, 0, 1, 0, 0, 0]);
    c = dzmt_enemy(e, n);
    dzmt_log("ENEMY ATTACKS, en hit always + your dodge always: " + dzmt_fmt(c));
    dzmt_expect("you always dodge", c[1] == n && c[2] == 0);

    for (var m = 0; m < 3; m++)
    {
        dzmt_modes([0, 0, 0, m, 0, 0, 0, m]);
        var hacks = 0;
        var procs = 0;
        repeat (n)
        {
            hacks += sHackTerminal();
            procs += dzm_rd(3, 50);
        }
        dzmt_log("mode " + string(m) + ": hacks ok " + string(hacks) + "/" + string(n) + ", 50% procs " + string(procs) + "/" + string(n) + ", hack shown " + string(sHackTerminal_Chance()));
        if (m == 1)
            dzmt_expect("always: every hack and proc succeeds", hacks == n && procs == n);
        if (m == 2)
            dzmt_expect("never: no hack or proc succeeds", hacks == 0 && procs == 0);
    }
    dzmt_modes([0, 0, 0, 0, 0, 0, 0, 0]);
}

function dzmt_step()
{
    if (!variable_global_exists("T_f"))
    {
        global.T_f = 0;
        global.T_i = 0;
        global.T_wait = 0;
        global.T_fail = 0;
        global.T_release = -1;
        global.T_cmds = [
            "waitroom rTitle", "wait 60", "mouseoff", "menu", "wait 15",
            "state settings 1",
            "key down", "key down", "key down", "key down", "state settings 5", "shot t1_title_settings",
            "key confirm", "state dice rolls 1", "shot t2_title_dice",
            "key down", "key down", "key right", "mode 2 1", "key right", "mode 2 2", "key left", "mode 2 1",
            "shot t3_title_dice_changed", "ini 2 1",
            "key back", "state settings",
            "close", "wait 10", "reset",
            "newrun", "waitlevel", "wait 60",
            "menu", "wait 15", "key down", "key down", "state main 3", "key confirm", "state settings 1",
            "shot t4_level_settings", "key down", "key down", "key down", "state settings 4",
            "key confirm", "state dice rolls 1", "key down", "key down", "key down", "key down", "shot t5_level_dice", "mousezones",
            "key back", "key back", "state main", "close", "wait 20",
            "combat", "end"];
        dzmt_log("test start");
    }
    global.T_f++;
    if (global.T_release >= 0)
    {
        keyboard_key_release(global.T_release);
        global.T_release = -1;
    }
    if (global.T_f > 7200)
    {
        dzmt_expect("finished within time limit (stuck at: " + global.T_cmds[global.T_i] + ", room " + room_get_name(room) + ")", false);
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
    else if (verb == "close")
    {
        with (EscapeMenu)
            instance_destroy();
    }
    else if (verb == "key")
    {
        keyboard_key_press(dzmt_key(rest));
        global.T_release = dzmt_key(rest);
        global.T_wait = 4;
    }
    else if (verb == "state")
    {
        var got = dzmt_menu();
        dzmt_expect("menu is '" + rest + "' (got '" + got + "')", string_pos(rest, got) == 1);
    }
    else if (verb == "mode")
    {
        var a = real(string_copy(rest, 1, 1));
        var b = real(string_copy(rest, 3, 1));
        dzmt_expect("category " + string(a) + " is mode " + string(b) + " (got " + string(global.DZM_mode[a]) + ")", global.DZM_mode[a] == b);
    }
    else if (verb == "ini")
    {
        var a = real(string_copy(rest, 1, 1));
        var b = real(string_copy(rest, 3, 1));
        ini_open("dicemod.ini");
        var v = ini_read_real("dice", string(a), -1);
        ini_close();
        dzmt_expect("dicemod.ini saved category " + string(a) + " = " + string(b) + " (got " + string(v) + ")", v == b);
    }
    else if (verb == "reset")
    {
        for (var i = 0; i < 8; i++)
        {
            if (global.DZM_mode[i] != 0)
                dzm_change(i, 3 - global.DZM_mode[i]);
        }
        dzmt_expect("all categories back to normal", global.DZM_mode[2] == 0);
    }
    else if (verb == "shot")
    {
        if (surface_exists(application_surface))
            surface_save(application_surface, rest + ".png");
        else
            screen_save(rest + ".png");
        dzmt_log("screenshot " + rest);
    }
    else if (verb == "newrun")
    {
        global.vPlayingEndless = false;
        sRmTran(rLevel);
        sNewGame();
    }
    else if (verb == "waitlevel")
        done = room_get_name(room) == "rLevel" && instance_exists(Hero) && instance_exists(Par_Enemy) && !instance_exists(GenerateLevel);
    else if (verb == "mousezones")
    {
        // hover the middle of each row's text, left then right of the value
        global.vStt_MouseSupport = true;
        var ok = true;
        var detail = "";
        with (EscapeMenu)
        {
            for (var r = 1; r <= 8; r++)
            {
                var ty = dzm_menu_top() + ((r - 1) * dzm_menu_sep()) + 4;
                vSel = -1;
                dzm_menu_select(150, ty);
                var l = vSel == r && vSelDir == "left";
                vSel = -1;
                dzm_menu_select(220, ty);
                var rr = vSel == r && vSelDir == "right";
                if (!(l && rr))
                {
                    ok = false;
                    detail += " row" + string(r);
                }
            }
            vSel = -1;
            dzm_menu_select(150, dzm_menu_top() - 5);
            if (vSel != -1)
            {
                ok = false;
                detail += " above-list";
            }
        }
        global.vStt_MouseSupport = false;
        dzmt_expect("mouse hover picks the row under the cursor, both halves" + detail, ok);
    }
    else if (verb == "combat")
        dzmt_combat();
    else if (verb == "end")
    {
        dzmt_log("test end, failures: " + string(global.T_fail));
        game_end();
    }
    if (done)
    {
        global.T_i++;
        if (verb != "key" && verb != "wait")
            global.T_wait = 2;
    }
}

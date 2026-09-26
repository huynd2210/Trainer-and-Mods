
// ==========================================================================
//  CSD2 AUTOPLAY  --  injected into O_foodbrain : End Step
//  Toggle: F8
//
//  The bot never simulates game logic. It reads the state the game reads,
//  decides which key a player would press, and presses it through
//  keyboard_key_press / keyboard_key_release.
//
//   * WHAT to place  -> RCP_ar[slot][2] from SCRIPT_recipepopulate, the same
//     table the grader compares against. Stage-correct via LVT_whichstage, and
//     variation-correct via the LVG_randnumb* args (many recipes are
//     RCP_ar[arg3], i.e. the required slot is chosen at random per order).
//   * HOW MANY placed -> LVT_<slot>button minus LVT_STORE<slot>button.
//   * ORDER -> layered dishes grade order; stackCKarray[LV_stackstat][0].
//   * PAGE -> y < 1080. Pages are not visibility.
//   * LEGAL YET -> LV_lockunlockcode, a press ALLOWANCE (not a requirement).
//   * COOKING -> LV_status 1 cooking, 2 ready, 3 burnt. Ready dishes burn, so
//     they outrank everything; cooking dishes are never opened.
//   * MASH -> LV_ingredientminimum counts TOTAL presses and can exceed the
//     recipe (trash 416/417, the fire chore). Recipe satisfied but Enter still
//     locked means keep pressing.
//
//  SCRIPT_recipepopulate runs in O_foodbrain scope, never on the pane: it sets
//  LSRCHK_cookrawtilt, which the grader reads BEFORE recomputing it.
// ==========================================================================

if (!variable_instance_exists(id, "AP_init"))
{
    AP_init     = 1;
    AP_on       = 0;
    AP_key      = -1;
    AP_key2     = -1;   // modifier held alongside (holding-station shift)
    AP_phase    = 0;    // 0 decide, 1 tap, 2 hold, 3 cooldown
    AP_wait     = 0;
    AP_holdref  = noone;
    AP_holdstop = 10000;
    AP_idle     = 0;
}

if (keyboard_check_pressed(vk_f8))
{
    AP_on = 1 - AP_on;
    if (AP_key >= 0)
        keyboard_key_release(AP_key);
    if (AP_key2 >= 0)
        keyboard_key_release(AP_key2);
    AP_key     = -1;
    AP_key2    = -1;
    AP_phase   = 0;
    AP_wait    = 0;
    AP_idle    = 0;
    AP_holdref = noone;
    audio_play_sound(SND_tinyselect, 0, 0);
    // Flush on toggle so the tail of a shift is never lost.
    if (variable_instance_exists(id, "AP_logbuf"))
    {
        AP_logbuf += "=== autoplay toggled to " + string(AP_on) + " ===" + chr(10);
        var lf0 = file_text_open_append("autoplay.log");
        if (lf0 >= 0)
        {
            file_text_write_string(lf0, AP_logbuf);
            file_text_close(lf0);
            AP_logbuf = "";
        }
    }
}

// ==========================================================================
//  CAMPAIGN MODE (F9) -- play shifts back to back for medals.
//
//  Gold comes from playing a shift cleanly, and the bot already does that, so
//  this adds no scoring logic. It only removes the clicking between shifts:
//  confirm the end-of-day screens, then start the next shift.
//
//  This side only owns the in-shift half: the F9 toggle while playing, and
//  confirming the end-of-day screens. Choosing and launching the next shift
//  lives in campaign.gml on O_mainmenu, because O_foodbrain is created by the
//  loading screen and does not exist on the menu.
// ==========================================================================

if (!variable_global_exists("APC_on"))
{
    global.APC_on = 0;
    global.APC_cool = 0;
}

if (keyboard_check_pressed(vk_f9))
{
    global.APC_on = 1 - global.APC_on;
    global.APC_cool = 60;
    audio_play_sound(SND_tinyselect, 0, 0);
    if (global.APC_on == 1)
        AP_on = 1;
    if (variable_instance_exists(id, "AP_logbuf"))
        AP_logbuf += "=== campaign mode " + string(global.APC_on) + " ===" + chr(10);
}

if (global.APC_on == 1)
{
    AP_on = 1;   // the shift bot is always on while grinding
    if (global.APC_cool > 0)
        global.APC_cool -= 1;

    // The menu half of campaign mode lives in campaign.gml on O_mainmenu:
    // O_foodbrain does not exist outside a shift, so it cannot run here.
}

// No instance_exists(O_foodbar) gate here. CSD2 opens a shift with a PREP
// PHASE (LV_prepornot == 0, "Prep any food to start day") in which holding
// stations are stocked and no order tickets exist yet. Requiring a foodbar
// switched the bot off during precisely the phase holding-station work belongs
// to. Every branch below no-ops when its objects are absent.
if (AP_on == 1 && room == ROOM_mainroom)
{
    // ---------------- diagnostic log ----------------
    // Appends to autoplay.log in the game's save folder
    // (%LOCALAPPDATA%\NEW_CSD2_PS4Steam). Records every ticket state change and
    // every key the bot presses, so a bad order can be traced after the fact
    // instead of guessed at. chr(10) rather than a "\n" escape: this bytecode
    // version does not process escape sequences in strings.
    if (!variable_instance_exists(id, "AP_logbuf"))
    {
        AP_logbuf = "";
        AP_frame  = 0;
        AP_flush  = 0;
        AP_why    = "";
        AP_i = 0;
        repeat (121)
        {
            AP_laststat[AP_i] = -999;
            AP_i += 1;
        }
        AP_logbuf += "=== autoplay session start ===" + chr(10);
    }
    AP_frame += 1;

    with (O_foodbar)
    {
        var lst = LV_whichprepst;
        if (lst > 0 && lst < 15)
        {
            if (other.AP_laststat[lst] != LV_status)
            {
                other.AP_logbuf += string(other.AP_frame) + " BAR st=" + string(lst) + " fc=" + string(LV_foodcode) + " status " + string(other.AP_laststat[lst]) + "->" + string(LV_status) + " phase=" + string(LV_stagephase) + "/" + string(LV_endingstagephase) + " wstage=" + string(LVT_whichstage) + " err=" + string(LRCHK_errors) + " HS=" + string(LS_HSstatus) + " carry=" + string(LRCHK_prepcarry) + chr(10);
                other.AP_laststat[lst] = LV_status;
            }
        }
    }
    with (O_holdingstationbar)
    {
        var lst2 = LV_whichprepst;
        if (lst2 > 100 && lst2 < 110)
        {
            if (other.AP_laststat[lst2] != LV_status)
            {
                other.AP_logbuf += string(other.AP_frame) + " HSBAR st=" + string(lst2) + " fc=" + string(LV_foodcode) + " status " + string(other.AP_laststat[lst2]) + "->" + string(LV_status) + " serv=" + string(LV_HSservings) + chr(10);
                other.AP_laststat[lst2] = LV_status;
            }
        }
    }

    AP_flush += 1;
    if (AP_flush > 30)
    {
        AP_flush = 0;
        if (string_length(AP_logbuf) > 0)
        {
            var lf = file_text_open_append("autoplay.log");
            if (lf >= 0)
            {
                file_text_write_string(lf, AP_logbuf);
                file_text_close(lf);
                AP_logbuf = "";
            }
        }
    }

    if (AP_phase == 1)
    {
        // A tap: one frame down, then release. Ingredient buttons read
        // keyboard_check (held) and only re-arm on RELEASE.
        if (AP_key >= 0)
            keyboard_key_release(AP_key);
        if (AP_key2 >= 0)
            keyboard_key_release(AP_key2);
        AP_key   = -1;
        AP_key2  = -1;
        AP_phase = 3;
        AP_wait  = 1;
    }
    else if (AP_phase == 2)
    {
        // Hold-to-fill pour: the grader reads LV_A7buttonreturn, which is just
        // LV_lockunlockcode at the instant of release.
        var stillhold = 0;
        if (instance_exists(AP_holdref))
        {
            if (AP_holdref.LV_lockunlockcode > AP_holdstop)
                stillhold = 1;
        }
        if (stillhold == 0)
        {
            if (AP_key >= 0)
                keyboard_key_release(AP_key);
            AP_key     = -1;
            AP_holdref = noone;
            AP_phase   = 3;
            AP_wait    = 1;
        }
    }
    else if (AP_phase == 3)
    {
        if (AP_wait > 0)
            AP_wait -= 1;
        else
            AP_phase = 0;
    }
    else
    {
        // ------------------------- decide -------------------------
        // A ticket that finishes WITHOUT an ingredient pane never runs the
        // grader, and it is the grader that clears LV_selected. Instant-pickup
        // orders (LV_endingstagephase == 2, or a cooked dish reopened when
        // LV_endingstagephase == 6, or LS_HSstatus == 10) therefore sit at
        // status 100 with LV_selected still 1. Treating that as "a station is
        // open" parked the bot for seconds at a time -- 263s of a single shift.
        // A finished bar is not an open station.
        AP_sel     = -1;
        AP_haspane = 0;
        with (O_foodbar)
        {
            if (LV_selected == 1 && LV_status != 100 && LV_status != 3)
                other.AP_sel = LV_whichprepst;
        }
        with (O_holdingstationbar)
        {
            if (LV_selected == 1 && LV_status != 100 && LV_status != 3)
                other.AP_sel = LV_whichprepst;
        }

        AP_k       = -1;
        AP_k2      = -1;
        AP_kfin    = -1;   // finishing ingredient, deliberately pressed last
        AP_finslot = -1;
        AP_dohold  = 0;
        AP_holdref = noone;
        AP_why     = "idle";

        // ---------------- holding-station survey ----------------
        // Hstations_array[100 + n] is O_foodbrain's own registry: [1] is the
        // held HS foodcode, [2] the servings left. A dish is satisfied by
        // its own foodcode + 1000 (the recipepopulate LVT_addnumbcode split).
        AP_freehs = 0;
        with (O_holdingstationreserve)
        {
            if (LV_deactivate == 0 && LV_destroyself != 1 && y > -3)
                other.AP_freehs += 1;
        }
        AP_hsbusy = 0;
        with (O_holdingstationbar)
        {
            if (LV_foodcode >= 500 && LV_foodcode <= 550)
                other.AP_hsbusy = 1;   // a batch is still on the menu screen
        }

        // Match all three pages of the game's HS menu: required entrees,
        // optional entrees, then sides. Optional-only and side-only menus also
        // wait for a batch before the shift starts (foodbrain Alarm 11).
        AP_ncand = 0;
        AP_i = 0;
        repeat (24)
        {
            var menurow = 120 + AP_i;
            if (AP_i >= 8 && AP_i < 16)
                menurow = 100 + AP_i - 8;
            if (AP_i >= 16)
                menurow = 60 + AP_i - 16;
            var fcv = LVA_fullmenuarray[menurow][1];
            if (fcv > 1000)
            {
                AP_cand[AP_ncand] = fcv;
                AP_ncand += 1;
            }
            AP_i += 1;
        }
        // Covered = a stocked station with servings left, or a batch already
        // being prepped for it.
        // How MANY stations hold each candidate -- a count, not a flag. With more
        // stations than dish types, one batch each leaves the rest idle;
        // counting lets the spares fill evenly (2 of A, 2 of B across five
        // stations) instead of stopping once every dish is represented once.
        AP_i = 0;
        repeat (AP_ncand)
        {
            AP_chk    = AP_cand[AP_i];
            AP_chkres = 0;
            var j = 101;
            repeat (9)
            {
                if (Hstations_array[j][1] == AP_chk && Hstations_array[j][2] > 0)
                    AP_chkres += 1;
                j += 1;
            }
            with (O_holdingstationbar)
            {
                if (LV_foodcode == other.AP_chk)
                    other.AP_chkres += 1;
            }
            AP_cnt[AP_i] = AP_chkres;
            AP_i += 1;
        }
        AP_ncov = 0;
        AP_i = 0;
        repeat (AP_ncand)
        {
            if (AP_cnt[AP_i] > 0)
                AP_ncov += 1;
            AP_i += 1;
        }

        // Emergency: a customer is already blocked waiting on an HS item.
        AP_hsneed  = -1;
        AP_hsurg   = 999999;
        AP_hsemerg = 0;
        with (O_foodbar)
        {
            if (LV_letsgo == 1 && LV_deactivate == 0 && LS_HSstatus == 8 && LV_status != 100)
            {
                var t = alarm[9];
                if (t < 0)
                    t = 999998;
                if (t < other.AP_hsurg)
                {
                    other.AP_hsurg  = t;
                    other.AP_hsneed = LV_foodcode;
                }
            }
        }
        if (AP_hsneed > 0)
        {
            AP_chk    = AP_hsneed + 1000;
            AP_chkres = 0;
            var j2 = 101;
            repeat (9)
            {
                if (Hstations_array[j2][1] == AP_chk && Hstations_array[j2][2] > 0)
                    AP_chkres = 1;
                j2 += 1;
            }
            with (O_holdingstationbar)
            {
                if (LV_foodcode == other.AP_chk)
                    other.AP_chkres = 1;
            }
            if (AP_chkres == 1)
                AP_hsneed = -1;      // already covered, nothing to start
            else
                AP_hsemerg = 1;      // may take the last free slot
        }

        // Otherwise top up for variety: the first menu dish nothing holds yet.
        // Keep a spare during the shift, but let a one-station kitchen start.
        // An open HS menu has ALREADY consumed its reserve. Finish choosing
        // its food even when opening it reduced free reserves from 2 to 1
        // (or from 1 to 0 in prep); otherwise the menu stalls indefinitely.
        if (AP_hsneed < 0 && (AP_freehs >= 2 || AP_hsbusy == 1 || (LV_prepornot == 0 && AP_freehs >= 1)))
        {
            // Variety first: anything nothing is holding yet.
            AP_i = 0;
            repeat (AP_ncand)
            {
                if (AP_cnt[AP_i] == 0 && AP_hsneed < 0)
                    AP_hsneed = AP_cand[AP_i] - 1000;
                AP_i += 1;
            }
            // Every dish already represented but stations still spare: stock
            // another batch of whichever is held least, so five stations and two
            // dishes become 2+2 rather than 1+1 with three sitting empty. Ties
            // take the first, which round-robins naturally as counts even out.
            // The outer condition still reserves one free station.
            if (AP_hsneed < 0)
            {
                AP_least = 999;
                AP_i = 0;
                repeat (AP_ncand)
                {
                    if (AP_cnt[AP_i] < AP_least)
                    {
                        AP_least  = AP_cnt[AP_i];
                        AP_hsneed = AP_cand[AP_i] - 1000;
                    }
                    AP_i += 1;
                }
            }
        }

        if (AP_sel == -1)
        {
            AP_bestv    = 999999;
            AP_bestrank = 9;
            AP_pick     = -1;
            AP_pickhs   = -1;

            // 0 cooked and burning | 1 HS batch mid-prep
            // 2 ordinary ticket or restock, by patience
            with (O_foodbar)
            {
                if (LV_letsgo == 1 && LV_deactivate == 0 && LV_selected == 0 && LV_status != 3 && LV_status != 100 && LS_HSstatus != 8)
                {
                    var rank = 9;
                    var urg  = 999998;
                    if (LV_status == 2)
                    {
                        rank = 0;
                        urg  = LV_cooktimeangle;   // 0 -> -360, lower = nearer burning
                    }
                    else if (LV_status != 1)
                    {
                        rank = 2;
                        urg  = alarm[9];
                        if (urg < 0)
                            urg = 999998;
                    }
                    if (rank < 9)
                    {
                        if (rank < other.AP_bestrank || (rank == other.AP_bestrank && urg < other.AP_bestv))
                        {
                            other.AP_bestrank = rank;
                            other.AP_bestv    = urg;
                            other.AP_pick     = LV_whichprepst;
                            other.AP_pickhs   = -1;
                        }
                    }
                }
            }
            with (O_holdingstationbar)
            {
                // No LV_deactivate here: unlike O_foodbar and the reserve slots,
                // O_holdingstationbar never defines it, and reading it throws.
                // status 10 is "stocked and holding" -- the finished state. It
                // must NOT count as needing work or the bot reopens it forever.
                if (LV_selected == 0 && LV_status != 3 && LV_status != 100 && LV_status != 1 && LV_status != 10)
                {
                    if (other.AP_bestrank > 1)
                    {
                        other.AP_bestrank = 1;
                        other.AP_bestv    = 0;
                        other.AP_pick     = -1;
                        other.AP_pickhs   = LV_whichprepst - 100;
                    }
                }
            }
            if (AP_hsneed > 0 && AP_hsbusy == 0)
            {
                // Stocking competes with ordinary tickets on the SAME scale.
                // Ranking it below them meant it never ran: a shift almost
                // always has some ticket waiting, so a "only when idle" rank
                // never won. An emergency inherits the blocked customer's
                // patience; a speculative top-up behaves like a ticket sitting
                // at half a patience bar, so it goes ahead while everything
                // else is comfortable and yields as soon as anything gets close
                // to timing out.
                var srank = 2;
                var surg  = AP_hsurg;
                if (AP_hsemerg == 0)
                {
                    var pfps = 30;
                    if (global.OPfpscap == 1)
                        pfps = 60;
                    surg = global.patience * pfps * 0.5;
                }
                with (O_holdingstationreserve)
                {
                    if (LV_deactivate == 0 && LV_destroyself != 1 && y > -3)
                    {
                        if (srank < other.AP_bestrank || (srank == other.AP_bestrank && surg < other.AP_bestv))
                        {
                            other.AP_bestrank = srank;
                            other.AP_bestv    = surg;
                            other.AP_pick     = -1;
                            other.AP_pickhs   = LV_whichprepst - 100;
                        }
                    }
                }
            }

            if (AP_pick > 0 && AP_pick < 15)
            {
                // rebuilt every time so custom key rebinds are always honoured
                AP_PSK[1]  = global.KEY_PS1;
                AP_PSK[2]  = global.KEY_PS2;
                AP_PSK[3]  = global.KEY_PS3;
                AP_PSK[4]  = global.KEY_PS4;
                AP_PSK[5]  = global.KEY_PS5;
                AP_PSK[6]  = global.KEY_PS6;
                AP_PSK[7]  = global.KEY_PS7;
                AP_PSK[8]  = global.KEY_PS8;
                AP_PSK[9]  = global.KEY_PS9;
                AP_PSK[10] = global.KEY_PS10;
                AP_PSK[11] = global.KEY_PS11;
                AP_PSK[12] = global.KEY_PS12;
                AP_PSK[13] = global.KEY_PS13;
                AP_PSK[14] = global.KEY_PS14;
                AP_k = AP_PSK[AP_pick];
                AP_why = "open st=" + string(AP_pick);
            }
            else if (AP_pickhs > 0 && AP_pickhs < 9)
            {
                AP_HSK[1] = global.KEY_HS1;
                AP_HSK[2] = global.KEY_HS2;
                AP_HSK[3] = global.KEY_HS3;
                AP_HSK[4] = global.KEY_HS4;
                AP_HSK[5] = global.KEY_HS5;
                AP_HSK[6] = global.KEY_HS6;
                AP_HSK[7] = global.KEY_HS7;
                AP_HSK[8] = global.KEY_HS8;
                AP_k = AP_HSK[AP_pickhs];
                AP_why = "hsclaim slot=" + string(AP_pickhs) + " want=" + string(AP_hsneed) + " emerg=" + string(AP_hsemerg);
                if (global.KEYSHIFT_HS_on == 1)
                    AP_k2 = global.KEYSHIFT_HS;
            }
        }
        else
        {
            AP_pane = noone;
            with (O_NEWingredientpane)
            {
                if (LV_whichprepst == other.AP_sel)
                    other.AP_pane = id;
            }

            if (instance_exists(AP_pane))
            {
                AP_haspane = 1;
                AP_fc = AP_pane.LV_foodcode;

                if (AP_fc >= 500 && AP_fc <= 550)
                {
                    // Holding-station MENU, not a recipe. Buttons are drawn from
                    // LVA_fullmenuarray, whose [3] becomes the button's
                    // LV_ingredientkeycode, so match on FOODGLOSS[dish][15].
                    if (AP_hsneed > 0)
                    {
                        with (O_NEWingredientbuttonshelper)
                        {
                            if (LV_whichprepst == other.AP_sel && LV_controlkeycode < 2000 && LV_keybinding == 0 && y < 1080)
                            {
                                if (LV_ingredientkeycode == global.FOODGLOSS[other.AP_hsneed][15] && other.AP_k < 0 && LV_firstlockout == 0)
                                {
                                    if (global.KEY_universalfoods == 0)
                                        other.AP_k = ord(LS_keyboardinput);
                                    else
                                        other.AP_k = LS_keyboardinput;
                                    other.AP_why = "hsmenupick fc=" + string(other.AP_hsneed);
                                }
                            }
                        }
                        if (AP_k < 0)
                        {
                            AP_k = global.KEY_pageturn[0];   // dish is on another menu page
                            AP_why = "hsmenupage";
                        }
                    }
                }
                else
                {
                    AP_i = 0;
                    repeat (25)
                    {
                        RCP_ar[AP_i][2] = 0;
                        AP_i += 1;
                    }
                    AP_i = 0;
                    repeat (48)
                    {
                        stackCKarray[AP_i][0] = 0;
                        AP_i += 1;
                    }
                    LV_recipestack = 0;

                    script_execute(SCRIPT_recipepopulate, AP_fc, AP_pane.LS_recipecarryover, AP_pane.LVT_whichstage, AP_pane.LVG_randnumbfromfoodbar, AP_pane.LVG_randnumbfromfoodbarB, AP_pane.LVG_randnumbfromfoodbarC, AP_pane.LVG_randnumbfromfoodbarD);

                    AP_pl[1]  = AP_pane.LVT_A1button - AP_pane.LVT_STOREA1button;
                    AP_pl[2]  = AP_pane.LVT_A2button - AP_pane.LVT_STOREA2button;
                    AP_pl[3]  = AP_pane.LVT_A3button - AP_pane.LVT_STOREA3button;
                    AP_pl[4]  = AP_pane.LVT_A4button - AP_pane.LVT_STOREA4button;
                    AP_pl[5]  = AP_pane.LVT_A5button - AP_pane.LVT_STOREA5button;
                    AP_pl[6]  = AP_pane.LVT_A6button - AP_pane.LVT_STOREA6button;
                    AP_pl[7]  = AP_pane.LVT_A7button - AP_pane.LVT_STOREA7button;
                    AP_pl[8]  = AP_pane.LVT_A8button - AP_pane.LVT_STOREA8button;
                    AP_pl[9]  = AP_pane.LVT_B1button - AP_pane.LVT_STOREB1button;
                    AP_pl[10] = AP_pane.LVT_B2button - AP_pane.LVT_STOREB2button;
                    AP_pl[11] = AP_pane.LVT_B3button - AP_pane.LVT_STOREB3button;
                    AP_pl[12] = AP_pane.LVT_B4button - AP_pane.LVT_STOREB4button;
                    AP_pl[13] = AP_pane.LVT_B5button - AP_pane.LVT_STOREB5button;
                    AP_pl[14] = AP_pane.LVT_B6button - AP_pane.LVT_STOREB6button;
                    AP_pl[15] = AP_pane.LVT_B7button - AP_pane.LVT_STOREB7button;
                    AP_pl[16] = AP_pane.LVT_B8button - AP_pane.LVT_STOREB8button;
                    AP_pl[17] = AP_pane.LVT_C1button - AP_pane.LVT_STOREC1button;
                    AP_pl[18] = AP_pane.LVT_C2button - AP_pane.LVT_STOREC2button;
                    AP_pl[19] = AP_pane.LVT_C3button - AP_pane.LVT_STOREC3button;
                    AP_pl[20] = AP_pane.LVT_C4button - AP_pane.LVT_STOREC4button;
                    AP_pl[21] = AP_pane.LVT_C5button - AP_pane.LVT_STOREC5button;
                    AP_pl[22] = AP_pane.LVT_C6button - AP_pane.LVT_STOREC6button;
                    AP_pl[23] = AP_pane.LVT_C7button - AP_pane.LVT_STOREC7button;
                    AP_pl[24] = AP_pane.LVT_C8button - AP_pane.LVT_STOREC8button;

                    AP_needcount = 0;
                    AP_i = 1;
                    repeat (24)
                    {
                        AP_want[AP_i] = 0;
                        AP_rcp[AP_i]  = RCP_ar[AP_i][2];
                        if (AP_pl[AP_i] < AP_rcp[AP_i])
                        {
                            AP_want[AP_i] = 1;
                            AP_needcount += 1;
                        }
                        AP_i += 1;
                    }

                    if (LV_recipestack == 1)
                    {
                        AP_i = 1;
                        repeat (24)
                        {
                            AP_want[AP_i] = 0;
                            AP_i += 1;
                        }
                        AP_needcount = 0;
                        var sp = AP_pane.LV_stackstat;
                        var nx = 0;
                        if (sp >= 0 && sp < 48)
                            nx = stackCKarray[sp][0];
                        if (nx > 0 && nx < 25)
                        {
                            AP_want[nx]  = 1;
                            AP_needcount = 1;
                        }
                    }

                    AP_enterok = 0;
                    AP_short   = 0;
                    with (O_NEWingredientbuttonshelper)
                    {
                        if (LV_whichprepst == other.AP_sel && LV_controlkeycode == 2001)
                        {
                            if (LV_enterlocked == 0)
                                other.AP_enterok = 1;
                            // The REAL gate. LV_enterlocked lags it: it is only
                            // cleared by alarm[5], which Alarm_0 arms 5 frames
                            // out and which re-arms itself once per frame. So
                            // right after a pane opens there is a window where
                            // the recipe is already satisfied and Enter is still
                            // locked. Reading LV_enterlocked alone made the mash
                            // fallback fire on perfectly good dishes and add one
                            // stray ingredient -- exactly one graded error, i.e.
                            // an "average" order. Compare the counts instead.
                            if (LVNEW_ingredientcount < LV_ingredientminimum)
                                other.AP_short = 1;
                        }
                    }

                    if (AP_needcount == 0 && AP_enterok == 0 && AP_short == 1)
                    {
                        AP_logbuf += string(AP_frame) + " MASH st=" + string(AP_sel) + " fc=" + string(AP_fc) + " (recipe done, count short of ingredientminimum)" + chr(10);
                        // Recipe satisfied but the game still will not take it:
                        // LV_ingredientminimum counts TOTAL presses and can
                        // exceed the recipe. That is the mash (trash 416/417
                        // want 6 and 11 presses for a 2-item recipe) and the
                        // fire chore, where foodGFXcontroller clears the lock
                        // once the flames are out. Self-selecting: on an
                        // ordinary dish Enter is already unlocked here.
                        // EVERY slot is fair game here, not just recipe slots.
                        // Trash 416/417 need Sanitize to finish, and Sanitize is
                        // slot 4 with RCP_ar[4] == 0 (it is in RCP_arDoNotList,
                        // so it is deliberately not part of the graded recipe).
                        // Restricting this to recipe slots meant the bot mashed
                        // Trash and Mash until their allowances ran out, never
                        // pressed S, and stranded on the ticket.
                        // LV_lockunlockcode below still decides what is legal to
                        // press right now, so widening cannot press anything the
                        // game would refuse.
                        AP_i = 1;
                        repeat (24)
                        {
                            AP_want[AP_i] = 1;
                            AP_i += 1;
                        }
                        AP_needcount = 24;
                    }

                    // Beer pitcher (300/301). LVT_A7button starts at 12000 and
                    // the grader wants LV_A7buttonreturn -- the code at the
                    // instant of release -- inside (11925, 11935]. Holding the
                    // button to land in that window is unreliable, so tap it
                    // instead and keep tapping while the code is still above the
                    // window. Stopping on the code rather than a fixed tap count
                    // stays correct whatever each tap is worth.
                    if (AP_fc == 300 || AP_fc == 301)
                    {
                        AP_i = 1;
                        repeat (24)
                        {
                            AP_want[AP_i] = 0;
                            AP_i += 1;
                        }
                        AP_needcount = 0;
                        with (O_NEWingredientbuttonshelper)
                        {
                            if (LV_whichprepst == other.AP_sel && LV_controlkeycode < 2000 && LV_attachedto > 0 && LV_attachedto < 25)
                            {
                                if (LV_lockunlockcode > 11935 && LV_lockunlockcode < 20001)
                                {
                                    other.AP_want[LV_attachedto] = 1;
                                    other.AP_needcount = 1;
                                }
                            }
                        }
                    }

                    AP_holdstop = 10000;
                    if (AP_fc == 208)
                        AP_holdstop = 10920;

                    if (AP_needcount > 0)
                    {
                        AP_offpage = 0;
                        AP_i = 1;
                        repeat (24)
                        {
                            AP_is451[AP_i] = 0;
                            AP_i += 1;
                        }
                        with (O_NEWingredientbuttonshelper)
                        {
                            if (LV_whichprepst == other.AP_sel && LV_controlkeycode < 2000 && LV_keybinding == 0 && LV_attachedto > 0 && LV_attachedto < 25)
                            {
                                if (other.AP_want[LV_attachedto] == 1)
                                {
                                    var c = LV_lockunlockcode;
                                    // 451-460 is the one class the terminal
                                    // cascade does NOT kill: on code == 0 it gets
                                    // -50 into the pressable 401-411 band while
                                    // everything else goes to -100. So these are
                                    // unlocked BY the finisher, and waiting for
                                    // them before pressing it deadlocks.
                                    // Flag per SLOT, never count instances:
                                    // the pane creates TWO helpers per slot, so
                                    // incrementing a counter here double-counts
                                    // and the finisher gate below never matches.
                                    if (c > 450 && c < 461)
                                        other.AP_is451[LV_attachedto] = 1;
                                    if ((c > 0 && c < 11) || (c > 100 && c < 111) || (c > 200 && c < 211) || (c > 300 && c < 311) || (c > 400 && c < 411) || (c > 500 && c < 511) || (c > 600 && c < 610) || (c > 700 && c < 711) || (c > 900 && c < 911) || (c > 1000 && c < 1011) || (c > 10000 && c < 20001))
                                    {
                                        if (y < 1080)
                                        {
                                            if (LV_firstlockout == 0)
                                            {
                                                if (c == 1)
                                                {
                                                    // FINISHING INGREDIENT. A press
                                                    // takes this to 0, and the
                                                    // code == 0 cascade sets EVERY
                                                    // other button on the station to
                                                    // -100. Pressing it early is
                                                    // unrecoverable: okonomiyaki's
                                                    // finish stage was ruined that
                                                    // way, losing 4 ingredients that
                                                    // were still to be placed. Hold
                                                    // it back until it is the only
                                                    // thing left.
                                                    if (other.AP_kfin < 0)
                                                    {
                                                        if (global.KEY_universalfoods == 0)
                                                            other.AP_kfin = ord(LS_keyboardinput);
                                                        else
                                                            other.AP_kfin = LS_keyboardinput;
                                                        other.AP_finslot = LV_attachedto;
                                                    }
                                                }
                                                else if (other.AP_k < 0)
                                                {
                                                    if (global.KEY_universalfoods == 0)
                                                        other.AP_k = ord(LS_keyboardinput);
                                                    else
                                                        other.AP_k = LS_keyboardinput;
                                                    other.AP_why = "ing slot=" + string(LV_attachedto) + " code=" + string(c);
                                                    if (c > 10000 && other.AP_fc != 300 && other.AP_fc != 301)
                                                    {
                                                        other.AP_dohold  = 1;
                                                        other.AP_holdref = id;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                            other.AP_offpage = 1;
                                    }
                                }
                            }
                        }
                        AP_want451 = 0;
                        AP_i = 1;
                        repeat (24)
                        {
                            AP_want451 += AP_is451[AP_i];
                            AP_i += 1;
                        }
                        if (AP_k < 0 && AP_kfin >= 0 && AP_needcount == (1 + AP_want451))
                        {
                            // Safe to press the finisher: everything else this
                            // stage wants is either already placed, or sits in
                            // the 451-460 band that this very press unlocks.
                            AP_k   = AP_kfin;
                            AP_why = "finisher slot=" + string(AP_finslot) + " unlocks451=" + string(AP_want451);
                        }
                        if (AP_k < 0 && AP_offpage == 1)
                        {
                            AP_k = global.KEY_pageturn[0];
                            AP_why = "page";
                        }
                    }
                    else if (AP_enterok == 1)
                    {
                        AP_k = global.KEY_serve[0];
                        AP_why = "serve";
                        AP_dbg = "";
                        AP_i = 1;
                        repeat (24)
                        {
                            if (AP_rcp[AP_i] > 0 || AP_pl[AP_i] != 0)
                                AP_dbg += " " + string(AP_i) + ":" + string(AP_pl[AP_i]) + "/" + string(AP_rcp[AP_i]);
                            AP_i += 1;
                        }
                        AP_logbuf += string(AP_frame) + " SERVE st=" + string(AP_sel) + " fc=" + string(AP_fc) + " wstage=" + string(AP_pane.LVT_whichstage) + " carry=" + string(AP_pane.LRCHK_prepcarry) + " placed/req:" + AP_dbg + chr(10);
                    }
                }
            }
        }

        // End of day. Confirm the results screens so the campaign rolls on.
        // Overrides whatever else was chosen: there is nothing else useful to
        // do on a finished day. Paced, rather than pressed every 3 frames, so
        // it cannot skip through a screen the player has not seen.
        // Only the day-complete screen here. The medal / results splash is
        // confirmed by splash.gml on O_splashstartend itself -- doing it from
        // two places at once would advance two screens per press.
        if (global.APC_on == 1 && LV_daycomplete == 1)
        {
            AP_k       = -1;
            AP_k2      = -1;
            AP_dohold  = 0;
            AP_holdref = noone;
            if (global.APC_cool <= 0)
            {
                AP_k   = vk_enter;
                AP_why = "confirm-screen";
                global.APC_cool = 20;
            }
        }

        if (AP_k < 0)
        {
            // Nothing to do. If that persists with a station open, nudge the
            // page over: harmless on a single-page dish, and it recovers a
            // wanted button parked off-screen.
            AP_idle += 1;
            // Stall snapshot: dump exactly what the bot can see, so a "stuck
            // cycling pages" report names its own cause instead of needing
            // another round of inference.
            if (AP_sel != -1 && (AP_idle mod 120) == 20)
            {
                AP_dbg = "";
                with (O_NEWingredientbuttonshelper)
                {
                    if (LV_whichprepst == other.AP_sel && LV_controlkeycode < 2000 && LV_attachedto > 0 && LV_attachedto < 25)
                    {
                        if (other.AP_want[LV_attachedto] == 1)
                        {
                            other.AP_dbg += " s" + string(LV_attachedto) + "=c" + string(LV_lockunlockcode);
                            if (y < 1080)
                                other.AP_dbg += "/on";
                            else
                                other.AP_dbg += "/off";
                            other.AP_dbg += "/fl" + string(LV_firstlockout);
                        }
                    }
                }
                AP_logbuf += string(AP_frame) + " IDLE st=" + string(AP_sel) + " fc=" + string(AP_fc) + " need=" + string(AP_needcount) + " w451=" + string(AP_want451) + " kfin=" + string(AP_kfin) + " enter=" + string(AP_enterok) + " offpage=" + string(AP_offpage) + " pane=" + string(AP_haspane) + " wanted:" + AP_dbg + chr(10);
            }
            if (AP_idle > 90 && AP_sel != -1)
            {
                AP_k = global.KEY_pageturn[0];
                AP_why = "nudge";
                AP_idle = 0;
            }
        }

        if (AP_k >= 0)
        {
            AP_logbuf += string(AP_frame) + " PRESS " + AP_why + " key=" + string(AP_k) + " sel=" + string(AP_sel) + chr(10);
            if (AP_k2 >= 0)
            {
                keyboard_key_press(AP_k2);
                AP_key2 = AP_k2;
            }
            keyboard_key_press(AP_k);
            AP_key  = AP_k;
            AP_idle = 0;
            if (AP_dohold == 1)
                AP_phase = 2;
            else
                AP_phase = 1;
        }
    }
}

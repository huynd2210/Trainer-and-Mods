
// ==========================================================================
//  CSD2 AUTOPLAY -- campaign mode, MENU side.
//  Appended to O_mainmenu : Begin Step.
//
//  This cannot live in the bot's own event: O_foodbrain is not placed in any
//  room, it is created by O_loadingscreen / O_splashstartend when a shift
//  starts, so it does not exist on the main menu at all. F9 pressed in the menu
//  reached nothing until this existed.
//
//  Starting a shift is exactly what the menu's own confirm handler does
//  (O_itemcatalogdisplay user event 0): set global.LVLwhichrest /
//  global.LVLwhichstage, then spawn O_loadingscreen with LVAN_progress = 1.
//
//  global.MODEchefforhire[rest][shift] is the unlock table: >= 0 available,
//  -1000 not a shift, [67] the restaurant's shift count. O_initvars fills
//  [0..59][0..69], so staying inside those bounds is required -- an
//  out-of-range array read is a hard crash, not a silent zero.
// ==========================================================================

if (!variable_global_exists("APC_on"))
{
    global.APC_on    = 0;
    global.APC_cool  = 0;
    global.APC_rest  = 0;   // our OWN cursor -- see below
    global.APC_shift = 0;
    global.APC_held  = 0;
}

// Release anything we pressed last frame (splash screens read
// keyboard_check_pressed, so the key has to actually go up again).
if (global.APC_held == 1)
{
    keyboard_key_release(vk_enter);
    global.APC_held = 0;
}

if (keyboard_check_pressed(vk_f9))
{
    global.APC_on   = 1 - global.APC_on;
    global.APC_cool = 30;
    audio_play_sound(SND_tinyselect, 0, 0);
    var lf0 = file_text_open_append("autoplay.log");
    if (lf0 >= 0)
    {
        file_text_write_string(lf0, "=== campaign mode " + string(global.APC_on) + " (menu) ===" + chr(10));
        // Silver and gold both require UAsaturn[0] == 0; with it set, a perfect
        // shift still caps at bronze, so the grind would never finish.
        if (global.UAsaturn[0] != 0)
            file_text_write_string(lf0, "CAMPAIGN WARNING UAsaturn is on - gold is unobtainable until it is off" + chr(10));
        file_text_close(lf0);
    }
}

if (global.APC_on == 1)
{
    if (global.APC_cool > 0)
        global.APC_cool -= 1;

    // Splash screens are confirmed by splash.gml on O_splashstartend itself.
}

if (global.APC_on == 1 && room == ROOM_mainmenu && global.APC_held == 0)
{
    if (global.APC_cool > 0)
    {
        // waiting
    }
    else if (!instance_exists(O_loadingscreen) && !instance_exists(O_splashstartend))
    {
        // Continue from OUR OWN cursor. global.LVLwhichstage cannot be used:
        // O_mainmenu user event 0 resets it (and LVLwhichrest) to 0, so reading
        // it here restarts the scan at shift 1 -- the shift just played.
        APC_r = global.APC_rest;
        APC_s = global.APC_shift;
        if (APC_r < 1 || APC_r > 59)
            APC_r = 1;
        if (APC_s < 0 || APC_s > 59)
            APC_s = 0;

        APC_found = 0;
        repeat (400)
        {
            if (APC_found == 0)
            {
                APC_s += 1;
                APC_lim = global.MODEchefforhire[APC_r][67];
                if (APC_lim > 59)
                    APC_lim = 59;
                if (APC_s > APC_lim)
                {
                    APC_s = 1;
                    APC_r += 1;
                    if (APC_r > 59)
                        APC_r = 1;   // wrap, so it cycles rather than sticking
                }
                // MODEchefforhire[rest][shift] IS the medal, round-tripped
                // straight to the save's [C4H] REST<n> keys: -1000 not a shift,
                // -1 locked, 0 unplayed, 1 bronze, 2 silver, 3 gold. Skip
                // anything already gold; there is nothing left to win there.
                APC_m = global.MODEchefforhire[APC_r][APC_s];
                if (APC_m >= 0 && APC_m < 3)
                    APC_found = 1;
            }
        }

        var lf1 = file_text_open_append("autoplay.log");
        if (APC_found == 1)
        {
            global.APC_cool = 300;   // let the room change settle before looking again
            if (lf1 >= 0)
                file_text_write_string(lf1, "CAMPAIGN start rest=" + string(APC_r) + " shift=" + string(APC_s) + " medal=" + string(APC_m) + chr(10));
            with (instance_create(0, 0, O_loadingscreen))
            {
                LVAN_progress = 1;
            }
            global.LVLwhichrest  = APC_r;
            global.LVLwhichstage = APC_s;
            global.APC_rest      = APC_r;   // advance our cursor
            global.APC_shift     = APC_s;
        }
        else
        {
            global.APC_on = 0;
            if (lf1 >= 0)
                file_text_write_string(lf1, "CAMPAIGN every unlocked shift is gold - stopping" + chr(10));
        }
        if (lf1 >= 0)
            file_text_close(lf1);
    }
}

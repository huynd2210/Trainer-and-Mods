// Deadzoned Dice Mod -- roll overrides and their settings page.
//
// Every roll the mod touches belongs to one category. A category's mode is
// 0 = normal (the game's own roll), 1 = always succeeds, 2 = never succeeds.
// Categories are indices into the tables below; the patched game code refers
// to them by number:
//   0 your hits     1 your crits     2 your dodges    3 your procs
//   4 enemy hits    5 enemy crits    6 enemy dodges   7 hacking

function dzm_init()
{
    if (variable_global_exists("DZM_mode"))
        return;
    global.DZM_label = ["your hits", "your crits", "your dodges", "your procs", "enemy hits", "enemy crits", "enemy dodges", "hacking"];
    global.DZM_tip = ["[ your attacks landing ]", "[ your hits being critical ]", "[ you evading enemy attacks ]", "[ talent and item chances in combat ]", "[ enemy attacks landing ]", "[ enemy hits being critical ]", "[ enemies evading your attacks ]", "[ terminal hacks succeeding ]"];
    global.DZM_value = ["normal", "always", "never"];
    global.DZM_count = array_length(global.DZM_label);
    global.DZM_mode = array_create(global.DZM_count, 0);
    ini_open("dicemod.ini");
    for (var i = 0; i < global.DZM_count; i++)
        global.DZM_mode[i] = clamp(floor(ini_read_real("dice", string(i), 0)), 0, 2);
    ini_close();
}

function dzm_mode(arg0)
{
    dzm_init();
    return global.DZM_mode[arg0];
}

// Cycle a category's mode by +1/-1 and save it.
function dzm_change(arg0, arg1)
{
    dzm_init();
    global.DZM_mode[arg0] = (global.DZM_mode[arg0] + arg1 + 3) mod 3;
    ini_open("dicemod.ini");
    ini_write_real("dice", string(arg0), global.DZM_mode[arg0]);
    ini_close();
}

// A finished yes/no roll, after the category's override.
function dzm_roll(arg0, arg1)
{
    var m = dzm_mode(arg0);
    if (m == 1)
        return true;
    if (m == 2)
        return false;
    return arg1;
}

// Drop-in for rd(). The vanilla roll is always made, so the random stream
// advances exactly as it would unmodded.
function dzm_rd(arg0, arg1)
{
    return dzm_roll(arg0, rd(arg1));
}

// A chance the game shows or gates on. 0 still means "not possible" (melee
// out of reach, no line of fire), so an impossible action stays impossible.
// arg2 is what "never" shows while the action stays possible.
function dzm_pct(arg0, arg1, arg2)
{
    if (arg1 <= 0)
        return arg1;
    var m = dzm_mode(arg0);
    if (m == 1)
        return 100;
    if (m == 2)
        return arg2;
    return arg1;
}

// --- Settings page: the escape menu's "dice rolls" state -------------------
// Geometry follows the menu's own value pages (video, audio): label centred
// at x+115, value centred at x+185 between < >, left/right halves of the
// value column step the value down/up with the mouse.

// "dice rolls" is the last entry of the settings list, which has one more
// entry (credits) on the title screen.
function dzm_settings_slot()
{
    if (room == rTitle)
        return 5;
    return 4;
}

function dzm_menu_top()
{
    return 52;
}

function dzm_menu_sep()
{
    return 12;
}

// Escape menu Begin Step, while vState == "dice rolls". cx/cy are the cursor
// relative to the menu.
function dzm_menu_select(arg0, arg1)
{
    dzm_init();
    var n = global.DZM_count;
    if (sUsingMouse())
    {
        // rows are 12 apart and the text is ~7 tall, so start each row's
        // hover band 2 above its text to centre the band on the text
        var brd = dzm_menu_sep();
        var base_y = dzm_menu_top() - 2;
        var m = 185;
        if (arg1 > base_y && arg1 < (base_y + (brd * n)) && arg0 > (m - 75) && arg0 < (m + 75))
        {
            vSel = 1 + floor((arg1 - base_y) / brd);
            if (arg0 < m)
                vSelDir = "left";
            else
                vSelDir = "right";
        }
    }
    else
    {
        vConfirmType = "Arrow";
        if (vSel < 1)
            vSel = 1;
        if (sInput_Press("Up"))
        {
            vSel--;
            if (vSel < 1)
                vSel = n;
        }
        if (sInput_Press("Down"))
        {
            vSel++;
            if (vSel > n)
                vSel = 1;
        }
    }
}

// Escape menu Draw, while vState == "dice rolls".
function dzm_menu_draw()
{
    dzm_init();
    var using_mouse = sUsingMouse();
    var vxl = x + 115;
    var vxr = x + 185;
    vy = y + dzm_menu_top();
    for (var sel = 1; sel <= global.DZM_count; sel++)
    {
        var text = global.DZM_value[global.DZM_mode[sel - 1]];
        var col = "Pink";
        if (vSel == sel)
            col = "White";
        draw_set_halign(fa_center);
        sDrawText(vxl, vy, global.DZM_label[sel - 1], col);
        col = "Purple Dark";
        if (vSel == sel)
            col = "Pink";
        sDrawText(vxr, vy, text, col);
        var xm = round(text_width(text) / 2);
        col = "Purple Dark";
        if (vSel == sel && (vSelDir == "left" || !using_mouse))
            col = "White";
        draw_set_halign(fa_right);
        sDrawText(vxr - xm, vy, "< ", col);
        col = "Purple Dark";
        if (vSel == sel && (vSelDir == "right" || !using_mouse))
            col = "White";
        draw_set_halign(fa_left);
        sDrawText(vxr + xm, vy, " >", col);
        vy += dzm_menu_sep();
    }
    if (vSel >= 1 && vSel <= global.DZM_count)
    {
        draw_set_halign(fa_center);
        sDrawText(Camera.vCenter_X, vy + 3, global.DZM_tip[vSel - 1], "Green");
    }
}

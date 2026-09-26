
// --- CSD2 AUTOPLAY: campaign status on the menu, drawn in GUI space ---
// O_foodbrain (which draws the in-shift indicator) does not exist here, so
// without this there is no way to tell whether F9 registered.
if (variable_global_exists("APC_on"))
{
    if (global.APC_on == 1)
    {
        var _f = draw_get_font();
        var _c = draw_get_colour();
        var _a = draw_get_alpha();
        var _h = draw_get_halign();
        var _v = draw_get_valign();

        draw_set_font(FONT_18CabinBold);
        draw_set_halign(fa_left);
        draw_set_valign(fa_top);
        draw_set_alpha(1);

        var _s = "CAMPAIGN  -  F9 to stop";
        draw_set_colour(c_black);
        draw_text(25, 25, _s);
        draw_text(23, 25, _s);
        draw_text(25, 23, _s);
        draw_set_colour(c_white);
        draw_text(24, 24, _s);

        draw_set_font(_f);
        draw_set_colour(_c);
        draw_set_alpha(_a);
        draw_set_halign(_h);
        draw_set_valign(_v);
    }
}

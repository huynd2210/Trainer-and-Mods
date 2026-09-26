
// --- CSD2 AUTOPLAY: on-screen status, drawn in GUI space ---
if (variable_instance_exists(id, "AP_on"))
{
    if (AP_on == 1)
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

        var _s = "AUTOPLAY  -  F8 to stop";
        if (variable_global_exists("APC_on"))
        {
            if (global.APC_on == 1)
                _s = "AUTOPLAY + CAMPAIGN  -  F9 to stop";
        }

        // Second line reports the holding-station survey, so a "why didn't it
        // stock anything" can be answered by looking at the screen:
        //   menu  = HS dishes on today's menu (0 means nothing to stock)
        //   held  = how many of those are already covered
        //   free  = empty reserve slots (shift top-ups need 2; prep needs 1)
        //   want  = the dish it is currently trying to stock, or "-"
        var _d = "";
        if (variable_instance_exists(id, "AP_ncand"))
        {
            var _w = "-";
            if (AP_hsneed > 0)
            {
                _w = string(AP_hsneed);
                if (AP_hsemerg == 1)
                    _w += "!";
            }
            var _ph = "shift";
            if (LV_prepornot == 0)
                _ph = "PREP";
            _d = _ph + "  HS  menu:" + string(AP_ncand) + "  held:" + string(AP_ncov) + "  free:" + string(AP_freehs) + "  want:" + _w;
        }

        draw_set_colour(c_black);
        draw_text(25, 25, _s);
        draw_text(23, 25, _s);
        draw_text(25, 23, _s);
        draw_set_colour(c_white);
        draw_text(24, 24, _s);

        if (_d != "")
        {
            draw_set_colour(c_black);
            draw_text(25, 49, _d);
            draw_text(23, 49, _d);
            draw_text(25, 47, _d);
            draw_set_colour(c_white);
            draw_text(24, 48, _d);
        }

        draw_set_font(_f);
        draw_set_colour(_c);
        draw_set_alpha(_a);
        draw_set_halign(_h);
        draw_set_valign(_v);
    }
}

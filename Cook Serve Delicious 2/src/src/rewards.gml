
// ==========================================================================
//  CSD2 AUTOPLAY -- campaign mode: clear the post-shift REWARDS screens.
//  Appended to O_unlockingmenu : Begin Step (event created by the patcher).
//
//  Fourth host object, for the same reason as the other three: the screen that
//  needs confirming owns the only instance alive while it is up. The end of a
//  shift walks through THREE different objects --
//
//      O_foodbrain      "day complete" bar        (autoplay.gml presses enter,
//                                                  then deactivates itself)
//      O_splashstartend medal / results           (splash.gml)
//      O_unlockingmenu  the rewards, this file
//
//  -- and O_unlockingmenu is created by O_splashstartend's user event 1, which
//  destroys the splash in the same step. So splash.gml is already gone by the
//  time these screens appear, and the campaign sat here forever.
//
//  HOW MANY PRESSES: not fixed. O_unlockingmenu alarm[0] tallies the shift and
//  fills LV_gift (+4 per level gained) and LV_foodgift; each accepted press
//  spends exactly one of them (user event 0 / user event 2 decrement at the top
//  before anything else, so this always terminates). After the gifts run out
//  there may be one more screen for a restaurant upgrade (stage 4) and one for
//  a level-up (stage 5), and the last press spawns O_loadingscreen and destroys
//  the menu. Counting presses up front is therefore wrong -- press until the
//  object is gone.
//
//  THE READY GATE: user event 15 ignores input entirely unless
//  LVUNLa_stage >= 3. Each reward restarts the animation at stage 0 and
//  alarm[1] walks it 0 -> 1 -> 2 -> 3 over about 24 frames. Pressing early is
//  silently dropped, so the press has to be gated on the stage rather than on
//  a timer, or most of the rewards would be skipped and the menu would stall
//  holding the ones it never saw.
//
//  RELEASING MATTERS: the screens read the key as PRESSED. Release is done
//  FIRST and unconditionally, never inside the ready gate -- a press that lands
//  resets LVUNLa_stage to 0, which closes the gate, so a release placed behind
//  the gate would never run and the key would stay down.
// ==========================================================================

if (variable_global_exists("APC_on"))
{
    if (global.APC_on == 1)
    {
        if (!variable_instance_exists(id, "APU_st"))
        {
            // O_foodbrain deactivates itself on the day-complete press and
            // O_splashstartend is destroyed on its own, so both leave enter
            // down with their release pending. Start from a known-up key.
            keyboard_key_release(vk_enter);
            keyboard_key_release(vk_space);
            APU_st = 0;   // 0 = waiting, 1 = key is down, release next frame
            APU_t  = 15;
            APU_n  = 0;   // presses spent, for the log
        }

        if (APU_st == 1)
        {
            keyboard_key_release(vk_enter);
            APU_st = 0;
            APU_t  = 15;   // pause before the next one, so rewards are visible
        }
        else if (LVUM_visible == 1 && LVUNLa_stage >= 3)
        {
            APU_t -= 1;
            if (APU_t <= 0)
            {
                keyboard_key_press(vk_enter);
                APU_st = 1;
                APU_n += 1;

                // Cheap -- a handful of lines per shift -- and it is what turns
                // "it hung on the rewards" into a report that names the screen.
                // LVFB_leveledup is set by the creator through a with-block,
                // not by Create, so read it defensively -- an unset instance
                // variable is a hard crash, and this mod has already shipped
                // that bug once (O_holdingstationbar.LV_deactivate).
                APU_lvl = -2;
                if (variable_instance_exists(id, "LVFB_leveledup"))
                    APU_lvl = LVFB_leveledup;

                var lfu = file_text_open_append("autoplay.log");
                if (lfu >= 0)
                {
                    file_text_write_string(lfu, "REWARD press " + string(APU_n) + " stage=" + string(LVUNLa_stage) + " gift=" + string(LV_gift) + " food=" + string(LV_foodgift) + " lvlup=" + string(APU_lvl) + chr(10));
                    file_text_close(lfu);
                }
            }
        }
    }
}

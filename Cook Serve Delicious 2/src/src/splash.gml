
// ==========================================================================
//  CSD2 AUTOPLAY -- campaign mode: confirm the medal / results screen.
//  Appended to O_splashstartend : Begin Step (event created by the patcher).
//
//  This has to live on O_splashstartend itself. The screen appears in
//  ROOM_mainroom, where O_mainmenu does not exist, and O_foodbrain is not
//  created until the day actually starts (O_splashstartend user event 0 makes
//  it) -- so neither of the other two hosts is alive while this screen is up,
//  and a confirm placed in them silently never runs.
//
//  O_splashstartend's own Step reads keyboard_check_pressed(vk_enter) and turns
//  it into LVSS_input = 1 -> user event 1. Pressing the key rather than calling
//  that event directly keeps the screen's own bookkeeping intact.
//
//  Begin Step runs before Step, so the key is down by the time the screen looks
//  for it. It is held two frames and released, because these screens read the
//  key as PRESSED, not held, and a key that never goes up can never press again.
// ==========================================================================

if (variable_global_exists("APC_on"))
{
    if (global.APC_on == 1)
    {
        if (!variable_instance_exists(id, "APS_t"))
            APS_t = 40;          // let the screen finish animating in first

        APS_t -= 1;

        if (APS_t == 0)
            keyboard_key_press(vk_enter);

        if (APS_t <= -2)
        {
            keyboard_key_release(vk_enter);
            APS_t = 40;          // if this screen is still up, try again
        }
    }
}

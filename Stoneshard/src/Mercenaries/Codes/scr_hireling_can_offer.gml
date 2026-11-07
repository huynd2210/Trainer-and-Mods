function scr_hireling_can_offer()
{
    // One mercenary at a time.
    return instance_exists(scr_hireling_dialog_npc()) && (!scr_hireling_hired());
}

function scr_hireling_name()
{
    var _n = ds_map_find_value(global.characterDataMap, "mslHirelingName")
    if (is_undefined(_n) || _n == "" || _n == "N/A")
        return "Your mercenary";
    return string(_n);
}

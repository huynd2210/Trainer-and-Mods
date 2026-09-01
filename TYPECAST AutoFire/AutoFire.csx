using System;
using UndertaleModLib.Compiler;
using UndertaleModLib.Util;

EnsureDataLoaded();

const string playerCreateName = "gml_Object_Player_Create_0";
const string playerStepName = "gml_Object_Player_Step_0";

UndertaleCode playerCreate = Data.Code.ByName(playerCreateName)
    ?? throw new Exception($"TYPECAST code entry not found: {playerCreateName}");
UndertaleCode playerStep = Data.Code.ByName(playerStepName)
    ?? throw new Exception($"TYPECAST code entry not found: {playerStepName}");

GlobalDecompileContext globalContext = new(Data);
Underanalyzer.Decompiler.IDecompileSettings settings = Data.ToolInfo.DecompilerSettings;

string Decompile(UndertaleCode code) =>
    new Underanalyzer.Decompiler.DecompileContext(globalContext, code, settings).DecompileToString();

string createSource = Decompile(playerCreate);
string stepSource = Decompile(playerStep);

if (createSource.Contains("autofire_enabled") || stepSource.Contains("autofire_enabled"))
{
    throw new Exception("TYPECAST AutoFire is already installed in this data file.");
}

const string createMarker = "show_debug_message(\"NEW PLAYER CREATED\");";
if (!createSource.Contains(createMarker))
{
    throw new Exception("Unsupported TYPECAST build: Player Create marker was not found.");
}

createSource = createSource.Replace(
    createMarker,
    "autofire_enabled = true;\n" +
    "autofire_timer = 0;\n" +
    "autofire_interval = 1;\n" +
    createMarker);

const string inputStartMarker =
    "if (God.input_method == 0)\n" +
    "{\n" +
    "    if (keyboard_check_pressed(vk_anykey) && (keyboard_key >= ord(\"A\") && keyboard_key <= ord(\"Z\")))";
const string inputEndMarker = "if (projectile_hit)";

int inputStart = stepSource.IndexOf(inputStartMarker, StringComparison.Ordinal);
int inputEnd = inputStart < 0
    ? -1
    : stepSource.IndexOf(inputEndMarker, inputStart, StringComparison.Ordinal);

if (inputStart < 0 || inputEnd < 0 || inputEnd <= inputStart)
{
    throw new Exception("Unsupported TYPECAST build: Player input block was not found.");
}

string originalInput = stepSource.Substring(inputStart, inputEnd - inputStart).TrimEnd();
string indentedOriginalInput = "    " + originalInput.Replace("\n", "\n    ");

string autoFireInput = @"
if (keyboard_check_pressed(vk_f6))
{
    autofire_enabled = !autofire_enabled;
    show_debug_message(autofire_enabled ? ""TYPECAST AutoFire: ON"" : ""TYPECAST AutoFire: OFF"");
}

if (autofire_enabled)
{
    if (autofire_timer > 0)
    {
        autofire_timer -= 1;
    }

    if (autofire_timer <= 0)
    {
        if (jammed)
        {
            jammed_timer -= 40;
            play_sound(sfx_jammed_reduce, x, y, 0);
            autofire_timer = autofire_interval;
        }
        else
        {
            var _auto_key = -1;
            var _auto_best_distance = 1000000;

            for (var _auto_i = 0; _auto_i < ds_list_size(Enemy_Handler.enemy_list); _auto_i++)
            {
                var _auto_enemy = ds_list_find_value(Enemy_Handler.enemy_list, _auto_i);
                if (instance_exists(_auto_enemy))
                {
                    var _auto_range_player = true;
                    if (God.multiplayer)
                    {
                        _auto_range_player = _auto_enemy.range_players_both ? true : (_auto_enemy.range_players_id == id);
                    }

                    if (_auto_enemy.in_range && _auto_range_player && _auto_enemy.targetable && _auto_enemy.damage_button >= 0)
                    {
                        var _auto_has_shot = false;
                        for (var _auto_p = 0; _auto_p < instance_number(Projectile); _auto_p++)
                        {
                            var _auto_projectile = instance_find(Projectile, _auto_p);
                            if (instance_exists(_auto_projectile) && _auto_projectile.team == 0 && _auto_projectile.owner == id && _auto_projectile.target == _auto_enemy)
                            {
                                _auto_has_shot = true;
                                break;
                            }
                        }

                        if (!_auto_has_shot)
                        {
                            var _auto_distance = point_distance(x, y, _auto_enemy.x_center, _auto_enemy.y_center);
                            if (_auto_distance < _auto_best_distance)
                            {
                                _auto_best_distance = _auto_distance;
                                _auto_key = _auto_enemy.damage_button;
                            }
                        }
                    }
                }
            }

            if (_auto_key >= 0)
            {
                var _auto_hit = false;
                var _auto_in_range_count = 0;
                var _auto_projectile_count = 0;

                for (var _auto_i = 0; _auto_i < ds_list_size(Enemy_Handler.enemy_list); _auto_i++)
                {
                    var _auto_enemy = ds_list_find_value(Enemy_Handler.enemy_list, _auto_i);
                    if (instance_exists(_auto_enemy))
                    {
                        var _auto_range_player = true;
                        if (God.multiplayer)
                        {
                            _auto_range_player = _auto_enemy.range_players_both ? true : (_auto_enemy.range_players_id == id);
                        }

                        if (_auto_enemy.damage_button == _auto_key && _auto_enemy.in_range && _auto_range_player && _auto_enemy.targetable)
                        {
                            var _auto_has_shot = false;
                            for (var _auto_p = 0; _auto_p < instance_number(Projectile); _auto_p++)
                            {
                                var _auto_projectile = instance_find(Projectile, _auto_p);
                                if (instance_exists(_auto_projectile) && _auto_projectile.team == 0 && _auto_projectile.owner == id && _auto_projectile.target == _auto_enemy)
                                {
                                    _auto_has_shot = true;
                                    break;
                                }
                            }

                            if (!_auto_has_shot)
                            {
                                var _auto_dir = point_direction(x, y, _auto_enemy.x_center, _auto_enemy.y_center);
                                var _auto_x = x + lengthdir_x(16, _auto_dir);
                                var _auto_y = y + lengthdir_y(16, _auto_dir);
                                var _auto_new_projectile = instance_create_depth(_auto_x, _auto_y, depth + 1, Projectile);
                                _auto_new_projectile.move_dir = _auto_dir;
                                _auto_new_projectile.target = _auto_enemy;
                                _auto_new_projectile.current_speed = projectile_speed;
                                _auto_new_projectile.owner = id;
                                _auto_projectile_count += 1;
                                _auto_in_range_count += 1;
                                _auto_hit = true;
                                God.raging_bull = false;
                                squash_stretch(1.25, 1.25);

                                if (God.time_scale == 0.25)
                                {
                                    God.slowmo_projectiles_shot += 1;
                                }
                            }
                        }
                    }
                }

                if (_auto_projectile_count >= 29)
                {
                    God.eye_storm = true;
                }

                if (_auto_hit)
                {
                    if (_auto_in_range_count < 3)
                    {
                        play_sound(sfx_player_shoot, x, y, 1);
                        play_bark(2.5, Audio_Handler.kill_sounds);
                    }
                    else
                    {
                        play_sound(sfx_player_shoot_burst, x, y, 1);
                        play_bark(2.5, Audio_Handler.big_kill_sounds);
                    }
                    autofire_timer = autofire_interval;
                }
            }
        }
    }
}
else
{
" + indentedOriginalInput + @"
}
";

stepSource = stepSource.Substring(0, inputStart) + autoFireInput + stepSource.Substring(inputEnd);

CodeImportGroup importGroup = new(Data, globalContext, settings)
{
    MainThreadAction = MainThreadAction
};
importGroup.QueueReplace(playerCreate, createSource);
importGroup.QueueReplace(playerStep, stepSource);
importGroup.Import();

ScriptMessage("TYPECAST AutoFire installed. AutoFire starts ON; press F6 in-game to toggle it.");

// Deadzoned Dice Mod -- UndertaleModTool patch script.
//
// Adds src\dzm.gml as a new global script, then edits seven existing code
// entries by exact-text replacement on their decompiled source:
//
//   sHero_HitChance, sHeroAction_GetCritChance,
//   sHackTerminal, sHackTerminal_Chance      shown/gating chances + hack result
//   HeroAction alarm 1                       your attack: hit, crit, enemy dodge, procs
//   EnemyAction alarm 1                      enemy attack: hit, crit, your dodge, procs
//   EscapeMenu step, begin step, draw        the "dice rolls" settings page
//
// Every edit states how many times its anchor must occur; any mismatch aborts
// the build, so a game update that moves the code fails loudly instead of
// half-patching. Runs on vanilla or on a data.win with other mods (the
// morgue mod edits none of these anchors), never on one that has this mod.
//
// The source folder comes from DZM_SRC.

using System; using System.IO; using System.Linq; using System.Collections.Generic;
using UndertaleModLib.Models;

EnsureDataLoaded();

string src = Environment.GetEnvironmentVariable("DZM_SRC");
if (string.IsNullOrEmpty(src)) throw new Exception("DZM_SRC is not set - point it at the folder holding dzm.gml");
string dzmPath = Path.Combine(src, "dzm.gml");
if (!File.Exists(dzmPath)) throw new Exception("missing " + dzmPath);

if (Data.Code.ByName("gml_GlobalScript_dzm") is not null)
    throw new Exception("this data.win is ALREADY PATCHED - patch a vanilla file instead");
if (Data.Code.ByName("gml_Object_HeroAction_Alarm_1") is null)
    throw new Exception("HeroAction alarm 1 not found - is this really Deadzoned?");

GlobalDecompileContext gdc = new(Data);
var settings = Data.ToolInfo.DecompilerSettings;
UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data, gdc, settings);

string N(string s) => s.Replace("\r\n", "\n");

// (code entry, anchor, replacement, required occurrences)
var edits = new List<(string code, string find, string repl, int count)>();
void E(string code, string find, string repl, int count = 1) =>
    edits.Add(("gml_" + code, N(find), N(repl), count));

// ---- shown and gating chances ---------------------------------------------
E("GlobalScript_sHero_HitChance",
  "return round(chance);",
  "return dzm_pct(0, round(chance), 1);");
E("GlobalScript_sHeroAction_GetCritChance",
  "return ceil(chance);",
  "return dzm_pct(1, ceil(chance), 0);");
E("GlobalScript_sHackTerminal_Chance",
  "return clamp((40 + global.vTech) - global.vLevel, 10, 90);",
  "return dzm_pct(7, clamp((40 + global.vTech) - global.vLevel, 10, 90), 0);");
E("GlobalScript_sHackTerminal",
  "return success;",
  "return dzm_roll(7, success);");

// ---- your attack ------------------------------------------------------------
const string HA = "Object_HeroAction_Alarm_1";
E(HA,
@"    if (normal_hit || skill_trick)
    {
        was_hit = true;",
@"    var dzm_h = dzm_mode(0);
    if (dzm_h == 1)
    {
        normal_hit = true;
    }
    else if (dzm_h == 2)
    {
        normal_hit = false;
        skill_trick = false;
    }
    if (normal_hit || skill_trick)
    {
        was_hit = true;");
E(HA,
@"        if (dodged)
        {
            sTextRiser(target.x, target.y, ""dodge"");",
@"        dodged = dzm_roll(6, dodged);
        if (dodged)
        {
            sTextRiser(target.x, target.y, ""dodge"");");
E(HA,
  "if (rd(critical_chance) || skill_trick)",
  "if (dzm_roll(1, rd(critical_chance) || skill_trick))");
// your talent / item / weapon-trait procs
E(HA, "(has == 1 && rd(22)) || (has == 2 && rd(33))",           // trick shot, trick feint, swift strike, swift trigger
      "(has == 1 && dzm_rd(3, 22)) || (has == 2 && dzm_rd(3, 33))", 4);
E(HA, @"weapon_type == ""Pistol"" && rd(33))",                   // trickslinger
      @"weapon_type == ""Pistol"" && dzm_rd(3, 33))");
E(HA, "(has == 1 && rd(44)) || (has == 1 && rd(66))",           // body slam
      "(has == 1 && dzm_rd(3, 44)) || (has == 1 && dzm_rd(3, 66))");
E(HA, "(has == 1 && rd(33)) || (has == 2 && rd(50))",           // decapitation
      "(has == 1 && dzm_rd(3, 33)) || (has == 2 && dzm_rd(3, 50))");
E(HA, @"if (rd(22))
            {
                if ((weapon_type == ""Melee"" && sHeroHasTalent(""Lucky Smack""))",
      @"if (dzm_rd(3, 22))
            {
                if ((weapon_type == ""Melee"" && sHeroHasTalent(""Lucky Smack""))");
E(HA, "global.vHeroHas_MortalArcana && rd(15)", "global.vHeroHas_MortalArcana && dzm_rd(3, 15)");
E(HA, @"sHeroWeaponHasTrait(""Stun"", weapon_id) && rd(33)", @"sHeroWeaponHasTrait(""Stun"", weapon_id) && dzm_rd(3, 33)");
E(HA, @"sHeroWeaponHasTrait(""Knockback"", weapon_id) && rd(33)", @"sHeroWeaponHasTrait(""Knockback"", weapon_id) && dzm_rd(3, 33)");
E(HA, "(has == 1 && rd(66)) || has == 2", "(has == 1 && dzm_rd(3, 66)) || has == 2");   // hurl opponent
E(HA, @"rd(66) && sHeroHasTalent(""Blood Rejuvenation"")", @"dzm_rd(3, 66) && sHeroHasTalent(""Blood Rejuvenation"")");
E(HA, @"rd(33) && sHeroHasTalent(""Blood Invigoration"")", @"dzm_rd(3, 33) && sHeroHasTalent(""Blood Invigoration"")");
E(HA, "if (rd(chn))", "if (dzm_rd(3, chn))");                 // rapid / storm fire
E(HA, @"rd(33) && sHeroHasTalent(""Chrome Jaw"")", @"dzm_rd(3, 33) && sHeroHasTalent(""Chrome Jaw"")");
E(HA, @"rd(33) && sHeroHasTalent(""Fanged Bite"")", @"dzm_rd(3, 33) && sHeroHasTalent(""Fanged Bite"")");
E(HA, @"weapon_type == ""Pistol"" && rd(50))",                  // hipslinger, offslinger
      @"weapon_type == ""Pistol"" && dzm_rd(3, 50))", 2);
E(HA, "(has == 1 && rd(10)) || (has == 2 && rd(15))",           // load lover
      "(has == 1 && dzm_rd(3, 10)) || (has == 2 && dzm_rd(3, 15))");
E(HA, "was_hit && rd(33) && target.vHealth", "was_hit && dzm_rd(3, 33) && target.vHealth");   // shred

// ---- enemy attack -----------------------------------------------------------
const string EA = "Object_EnemyAction_Alarm_1";
E(EA,
@"    if (object_index == StarEye)
    {
        if (normal_hit)",
@"    var dzm_h = dzm_mode(4);
    if (dzm_h == 1)
    {
        normal_hit = true;
    }
    else if (dzm_h == 2)
    {
        normal_hit = false;
        chip_hit = false;
    }
    if (object_index == StarEye)
    {
        if (normal_hit)");
E(EA,
@"            var dodge = false;
            if (rd(sHero_DodgeChance()))",
@"            critical_hit = dzm_roll(5, critical_hit);
            var dodge = false;
            if (rd(sHero_DodgeChance()))");
E(EA,
@"            var displace = false;",
@"            dodge = dzm_roll(2, dodge);
            var displace = false;");
E(EA,
@"                    if (dodge)
                    {",
@"                    dodge = dzm_roll(2, dodge);
                    if (dodge)
                    {");
// your defensive procs
E(EA, "(has == 1 && rd(15)) || (has == 2 && rd(22))",           // sixth sense
      "(has == 1 && dzm_rd(3, 15)) || (has == 2 && dzm_rd(3, 22))");
E(EA, "if (rd(5))", "if (dzm_rd(3, 5))");                       // blindworm sac
E(EA, @"sHeroHasTalent(""Beast Instinct"") && rd(25)", @"sHeroHasTalent(""Beast Instinct"") && dzm_rd(3, 25)");
E(EA, @"sHeroHasTalent(""Sense Thoughts"") && rd(33)", @"sHeroHasTalent(""Sense Thoughts"") && dzm_rd(3, 33)");
E(EA, "(has == 1 && rd(22)) || (has == 2 && rd(33))",           // hand parry
      "(has == 1 && dzm_rd(3, 22)) || (has == 2 && dzm_rd(3, 33))");
E(EA, @"rd(33) && sHeroHasTalent(""Hypnotise"")", @"dzm_rd(3, 33) && sHeroHasTalent(""Hypnotise"")");
E(EA, "if (rd(11))", "if (dzm_rd(3, 11))");                     // displacement field
E(EA, @"rd(33) && sHeroHasTalent(""Instil Despair"")", @"dzm_rd(3, 33) && sHeroHasTalent(""Instil Despair"")");
E(EA, "(has == 1 && rd(44)) || (has == 2 && rd(66))",           // death wish
      "(has == 1 && dzm_rd(3, 44)) || (has == 2 && dzm_rd(3, 66))");
E(EA, @"rd(50) && sHeroHasTalent(""Lucky Miss"")", @"dzm_rd(3, 50) && sHeroHasTalent(""Lucky Miss"")");
E(EA, "(has == 1 && rd(33)) || (has == 2 && rd(50))",           // sucker punch
      "(has == 1 && dzm_rd(3, 33)) || (has == 2 && dzm_rd(3, 50))");
E(EA, @"sHeroHasTalent(""Herbalist"") && rd(88)", @"sHeroHasTalent(""Herbalist"") && dzm_rd(3, 88)");
E(EA, "if (rd(15))", "if (dzm_rd(3, 15))");                     // forearm guard

// ---- settings page ----------------------------------------------------------
E("Object_EscapeMenu_Step_1",                                   // one more settings entry
@"            if (room == rTitle)
            {
                amt = 4;
            }
            else
            {
                amt = 3;
            }",
@"            if (room == rTitle)
            {
                amt = 5;
            }
            else
            {
                amt = 4;
            }");
E("Object_EscapeMenu_Step_1",
@"    if ((prev_Sel != vSel && vSel) || (prev_SelDir != vSelDir && vSelDir != """"))",
@"    if (vState == ""dice rolls"")
    {
        dzm_menu_select(cx, cy);
    }
    if ((prev_Sel != vSel && vSel) || (prev_SelDir != vSelDir && vSelDir != """"))");
E("Object_EscapeMenu_Step_0",
@"    if (pressed != 0 && vSel)
    {
        if (vState == ""main"")",
@"    if (pressed != 0 && vSel)
    {
        if (vState == ""settings"" && vSel == dzm_settings_slot())
        {
            vState = ""dice rolls"";
            vSel = 1;
            if (sUsingMouse())
            {
                vSel = -1;
            }
        }
        else if (vState == ""dice rolls"")
        {
            dzm_change(vSel - 1, pressed);
        }
        else if (vState == ""main"")");
E("Object_EscapeMenu_Step_0",
  @"else if (vState == ""video"" || vState == ""audio"" || vState == ""controls"")",
  @"else if (vState == ""video"" || vState == ""audio"" || vState == ""controls"" || vState == ""dice rolls"")");
E("Object_EscapeMenu_Draw_0",
@"        amt = 3;
        if (room == rTitle)
        {
            amt += 1;
        }",
@"        amt = 3;
        if (room == rTitle)
        {
            amt += 1;
        }
        amt += 1;");
E("Object_EscapeMenu_Draw_0",
@"                t = ""credits"";
            }
            col = ""Pink"";",
@"                t = ""credits"";
            }
            if (i == dzm_settings_slot())
            {
                t = ""dice rolls"";
            }
            col = ""Pink"";");
E("Object_EscapeMenu_Draw_0",
@"    else
    {
        vxl = x + 115;",
@"    else if (vState == ""dice rolls"")
    {
        dzm_menu_draw();
    }
    else
    {
        vxl = x + 115;");

// ---- apply ------------------------------------------------------------------
var sources = new Dictionary<string, string>();
int bad = 0;
foreach (var (code, find, repl, count) in edits)
{
    if (!sources.ContainsKey(code))
    {
        var entry = Data.Code.ByName(code);
        if (entry is null) { Console.WriteLine("MISSING CODE " + code); bad++; sources[code] = ""; continue; }
        sources[code] = N(new Underanalyzer.Decompiler.DecompileContext(gdc, entry, settings).DecompileToString());
    }
    string s = sources[code];
    int n = 0;
    for (int at = s.IndexOf(find, StringComparison.Ordinal); at >= 0; at = s.IndexOf(find, at + find.Length, StringComparison.Ordinal)) n++;
    if (n != count)
    {
        Console.WriteLine($"ANCHOR {code}: expected {count}, found {n}: {find.Split('\n')[0].Trim()}");
        bad++;
        continue;
    }
    sources[code] = s.Replace(find, repl);
}
if (bad > 0) throw new Exception($"{bad} anchor problem(s) - nothing was written");

importGroup.QueueReplace("gml_GlobalScript_dzm", N(File.ReadAllText(dzmPath)));
foreach (var kv in sources)
    importGroup.QueueReplace(kv.Key, kv.Value);

try { importGroup.Import(); }
catch (Exception ex) { Console.WriteLine("=== COMPILE ERRORS ==="); Console.WriteLine(ex.Message); throw; }

// The importer registers each function (gml_Script_dzm_*) but not the file's
// own script asset, which every vanilla global script has (e.g. 'sCRT').
Data.Scripts.Add(new UndertaleScript { Name = Data.Strings.MakeString("dzm"), Code = Data.Code.ByName("gml_GlobalScript_dzm") });

Console.WriteLine($"PATCH OK: {edits.Count} edits across {sources.Count} code entries, + gml_GlobalScript_dzm");

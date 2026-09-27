using System; using System.IO; using System.Linq;
EnsureDataLoaded();
GlobalDecompileContext ctx = new(Data);
var st = Data.ToolInfo.DecompilerSettings;
string outDir = Environment.GetEnvironmentVariable("MRG_OUT");
Directory.CreateDirectory(outDir);
string[] names = { "gml_GlobalScript_mrg", "gml_GlobalScript_sNewGame", "gml_GlobalScript_sSaveRun", "gml_GlobalScript_sLoadRun",
  "gml_GlobalScript_sHurtHero", "gml_GlobalScript_sGiveHeroXPKill", "gml_GlobalScript_sConsole", "gml_GlobalScript_sTurnEnd",
  "gml_Object_EscapeMenu_Step_0", "gml_Object_Con_GameOver_Create_0", "gml_Object_Con_Won_Create_0", "gml_Object_Con_Global_Step_0",
  "gml_Object_HeroAction_Alarm_1", "gml_Object_EnemyAction_Alarm_1", "gml_Object_EscapeMenu_Step_1", "gml_Object_EscapeMenu_Draw_0" };
int bad = 0;
foreach (var n in names) {
  var c = Data.Code.ByName(n);
  if (c is null) { Console.WriteLine("MISSING " + n); bad++; continue; }
  File.WriteAllText(Path.Combine(outDir, n + ".gml"), new Underanalyzer.Decompiler.DecompileContext(ctx, c, st).DecompileToString());
}
foreach (var s in new[]{"mrg","dzm"}) Console.WriteLine($"script {s}: {Data.Scripts.ByName(s)?.Code?.Name?.Content ?? "absent"}, in global init: {Data.GlobalInitScripts.Any(g => g.Code?.Name?.Content == "gml_GlobalScript_" + s)}");
foreach (var f in new[]{"mrg_start","mrg_end","mrg_tick","mrg_hurt","mrg_kill","mrg_msg","mrg_turn","mrg_cause","mrg_save","mrg_load"})
  if (Data.Scripts.ByName("gml_Script_" + f) is null || Data.Functions.ByName("gml_Script_" + f) is null) { Console.WriteLine("UNREGISTERED " + f); bad++; }
Console.WriteLine(bad == 0 ? "VERIFY OK" : $"VERIFY FAILED: {bad}");

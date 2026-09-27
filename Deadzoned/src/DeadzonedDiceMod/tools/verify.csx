// Decompile the patched data.win back out: every touched entry, plus a check
// that the new global script is registered to run at game start.
using System; using System.IO; using System.Linq;
EnsureDataLoaded();
GlobalDecompileContext ctx = new(Data);
var st = Data.ToolInfo.DecompilerSettings;
string outDir = Environment.GetEnvironmentVariable("DZM_OUT");
Directory.CreateDirectory(outDir);
string[] names = {
  "gml_GlobalScript_dzm", "gml_GlobalScript_sHero_HitChance", "gml_GlobalScript_sHeroAction_GetCritChance",
  "gml_GlobalScript_sHackTerminal", "gml_GlobalScript_sHackTerminal_Chance",
  "gml_Object_HeroAction_Alarm_1", "gml_Object_EnemyAction_Alarm_1",
  "gml_Object_EscapeMenu_Step_0", "gml_Object_EscapeMenu_Step_1", "gml_Object_EscapeMenu_Draw_0" };
int bad = 0;
foreach (var n in names) {
  var c = Data.Code.ByName(n);
  if (c is null) { Console.WriteLine("MISSING " + n); bad++; continue; }
  string s = new Underanalyzer.Decompiler.DecompileContext(ctx, c, st).DecompileToString();
  File.WriteAllText(Path.Combine(outDir, n + ".gml"), s);
  Console.WriteLine($"OK {n} {s.Length} chars, dzm refs={s.Split("dzm_").Length - 1}");
}
bool inInit = Data.GlobalInitScripts.Any(g => g.Code?.Name?.Content == "gml_GlobalScript_dzm");
Console.WriteLine("dzm in GlobalInitScripts: " + inInit);
foreach (var f in new[]{"dzm_init","dzm_mode","dzm_change","dzm_roll","dzm_rd","dzm_pct","dzm_settings_slot","dzm_menu_select","dzm_menu_draw"}) {
  bool fn = Data.Functions.ByName("gml_Script_" + f) is not null; bool sc = Data.Scripts.ByName("gml_Script_" + f) is not null;
  Console.WriteLine($"  {f}: function={fn} script={sc}");
  if (!sc || !fn) bad++;
}
if (!inInit) bad++;
if (Data.Scripts.ByName("dzm")?.Code?.Name?.Content != "gml_GlobalScript_dzm") { Console.WriteLine("file script 'dzm' missing"); bad++; }
Console.WriteLine(bad == 0 ? "VERIFY OK" : $"VERIFY FAILED: {bad}");

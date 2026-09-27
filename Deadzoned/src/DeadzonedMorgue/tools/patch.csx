// Deadzoned Morgue -- UndertaleModTool patch script.
//
// Adds src\mrg.gml as a new global script and hooks it in:
//
//   sNewGame            start tracking a run            (end of function)
//   sLoadRun / sSaveRun carry the tracker in the run save
//   sHurtHero           what hurt you, how much         (after the damage)
//   sGiveHeroXPKill     kills by enemy
//   sConsole            last messages
//   sTurnEnd            turn count; names poison ticks
//   EscapeMenu step     "start new run" writes an "abandoned" morgue
//   Con_GameOver create death / seppuku morgue          (appended)
//   Con_Won create      win morgue                      (appended)
//   Con_Global step     play time and notes             (appended)
//
// Works on a vanilla data.win or one that already has other mods (the dice
// mod edits none of these anchors). Every edit states how many times its
// anchor must occur; any mismatch aborts before anything is written.
//
// The source folder comes from MRG_SRC.

using System; using System.IO; using System.Linq; using System.Collections.Generic;
using UndertaleModLib.Models;

EnsureDataLoaded();

string src = Environment.GetEnvironmentVariable("MRG_SRC");
if (string.IsNullOrEmpty(src)) throw new Exception("MRG_SRC is not set - point it at the folder holding mrg.gml");
string mrgPath = Path.Combine(src, "mrg.gml");
if (!File.Exists(mrgPath)) throw new Exception("missing " + mrgPath);

if (Data.Code.ByName("gml_GlobalScript_mrg") is not null)
    throw new Exception("the morgue mod is ALREADY in this data.win");
if (Data.Code.ByName("gml_GlobalScript_sHurtHero") is null)
    throw new Exception("sHurtHero not found - is this really Deadzoned?");

GlobalDecompileContext gdc = new(Data);
var settings = Data.ToolInfo.DecompilerSettings;
UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data, gdc, settings);

string N(string s) => s.Replace("\r\n", "\n");

var edits = new List<(string code, string find, string repl, int count)>();
void E(string code, string find, string repl, int count = 1) =>
    edits.Add(("gml_" + code, N(find), N(repl), count));

E("GlobalScript_sNewGame",
@"    global.vStm_Hth = (global.vHealth + global.vStm_Offset) * -1;
}",
@"    global.vStm_Hth = (global.vHealth + global.vStm_Offset) * -1;
    mrg_start();
}");
E("GlobalScript_sSaveRun",
@"    ini_write_real(""Level"", ""Device"", global.vLvl_Device);
    ini_close();",
@"    ini_write_real(""Level"", ""Device"", global.vLvl_Device);
    ini_close();
    mrg_save(save_dir(file));");
E("GlobalScript_sLoadRun",
@"        global.vLvl_Device = ini_read_real(""Level"", ""Device"", global.vLevel + 3);
        ini_close();",
@"        global.vLvl_Device = ini_read_real(""Level"", ""Device"", global.vLevel + 3);
        ini_close();
        mrg_load(save_dir(file));");
E("GlobalScript_sHurtHero",
@"    global.vHealth -= arg0;",
@"    global.vHealth -= arg0;
    mrg_hurt(arg0, arg1);");
E("GlobalScript_sGiveHeroXPKill",
@"function sGiveHeroXPKill(arg0, arg1)
{",
@"function sGiveHeroXPKill(arg0, arg1)
{
    mrg_kill(arg1);");
E("GlobalScript_sConsole",
@"function sConsole(arg0, arg1 = """")
{",
@"function sConsole(arg0, arg1 = """")
{
    mrg_msg(arg0);");
E("GlobalScript_sTurnEnd",
@"global.vTurn = ""Enemy"";",
@"global.vTurn = ""Enemy"";
            mrg_turn();");
E("GlobalScript_sTurnEnd",
@"                sHurtHero(1);",
@"                mrg_cause(""poison"");
                sHurtHero(1);");
E("Object_EscapeMenu_Step_0",
@"            if (vSel == 1)
            {
                sRmTran(rLevel);",
@"            if (vSel == 1)
            {
                mrg_end(""abandoned"");
                sRmTran(rLevel);");

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

var appends = new (string obj, EventType ev, uint sub, string code)[] {
    ("Con_GameOver", EventType.Create, 0, "mrg_end(\"died\");"),
    ("Con_Won",      EventType.Create, 0, "mrg_end(\"won\");"),
    ("Con_Global",   EventType.Step,   (uint)EventSubtypeStep.Step, "mrg_tick();"),
};
foreach (var a in appends)
    if (Data.GameObjects.ByName(a.obj) is null) { Console.WriteLine("MISSING OBJECT " + a.obj); bad++; }
if (bad > 0) throw new Exception($"{bad} anchor problem(s) - nothing was written");

importGroup.QueueReplace("gml_GlobalScript_mrg", N(File.ReadAllText(mrgPath)));
foreach (var kv in sources)
    importGroup.QueueReplace(kv.Key, kv.Value);
foreach (var a in appends)
{
    var obj = Data.GameObjects.ByName(a.obj);
    var handler = a.ev == EventType.Create ? obj.EventHandlerFor(EventType.Create, Data)
                                           : obj.EventHandlerFor(EventType.Step, (EventSubtypeStep)a.sub, Data);
    importGroup.QueueAppend(handler, "\n" + a.code + "\n");
}

try { importGroup.Import(); }
catch (Exception ex) { Console.WriteLine("=== COMPILE ERRORS ==="); Console.WriteLine(ex.Message); throw; }

// Mirror vanilla global scripts, which each have a file-level script asset.
Data.Scripts.Add(new UndertaleScript { Name = Data.Strings.MakeString("mrg"), Code = Data.Code.ByName("gml_GlobalScript_mrg") });

Console.WriteLine($"PATCH OK: {edits.Count} edits across {sources.Count} code entries, {appends.Length} appends, + gml_GlobalScript_mrg");

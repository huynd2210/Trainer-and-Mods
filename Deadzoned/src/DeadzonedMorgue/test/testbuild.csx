// Morgue TEST data.win: dice + morgue build, plus the mrgtest driver, renamed
// so its save sandbox is separate, with Steam achievements/stats stubbed.
using System; using System.IO; using System.Linq;
using UndertaleModLib.Models;
EnsureDataLoaded();
string src = Environment.GetEnvironmentVariable("MRG_TEST");
Data.GeneralInfo.Name = Data.Strings.MakeString("DeadzonedMorgueTest");
Data.GeneralInfo.DisplayName = Data.Strings.MakeString("Deadzoned MORGUE TEST");
GlobalDecompileContext gdc = new(Data);
UndertaleModLib.Compiler.CodeImportGroup g = new(Data, gdc, Data.ToolInfo.DecompilerSettings);
g.QueueReplace("gml_GlobalScript_mrgtest", File.ReadAllText(Path.Combine(src, "mrgtest.gml")).Replace("\r\n", "\n"));
g.QueueAppend(Data.GameObjects.ByName("Con_Global").EventHandlerFor(EventType.Step, EventSubtypeStep.Step, Data), "\nmrgt_step();\n");
g.QueueReplace("gml_GlobalScript_sStm_GiveAchievement", "function sStm_GiveAchievement(arg0) { }");
// Keep the game's own setup (the results screens read vOnlineRecord); drop only the upload.
string up = new Underanalyzer.Decompiler.DecompileContext(gdc, Data.Code.ByName("gml_GlobalScript_sStm_UploadLeaderboardStuff"), Data.ToolInfo.DecompilerSettings).DecompileToString();
if (!up.Contains("steam_upload_score(vStm_Leaderboard, scr)")) throw new Exception("upload anchor missing");
g.QueueReplace("gml_GlobalScript_sStm_UploadLeaderboardStuff", up.Replace("steam_upload_score(vStm_Leaderboard, scr)", "-1"));
g.QueueReplace("gml_Object_Stm_AchievementManager_Alarm_1", "");
// Other_69 only acts on upload results, and nothing is uploaded now.
try { g.Import(); } catch (Exception ex) { Console.WriteLine("=== COMPILE ERRORS ==="); Console.WriteLine(ex.Message); throw; }
Console.WriteLine("TEST BUILD OK");

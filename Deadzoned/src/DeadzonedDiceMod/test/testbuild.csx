// Build a TEST data.win: the patched build plus the dzmtest driver, renamed
// so its save sandbox is separate from the real game's.
using System; using System.IO; using System.Linq;
using UndertaleModLib.Models;
EnsureDataLoaded();
string src = Environment.GetEnvironmentVariable("DZM_TEST");
Console.WriteLine($"GEN8 Name='{Data.GeneralInfo.Name.Content}' FileName='{Data.GeneralInfo.FileName.Content}' DisplayName='{Data.GeneralInfo.DisplayName.Content}'");
Data.GeneralInfo.Name = Data.Strings.MakeString("DeadzonedDiceTest");
Data.GeneralInfo.DisplayName = Data.Strings.MakeString("Deadzoned DICE TEST");
GlobalDecompileContext gdc = new(Data);
UndertaleModLib.Compiler.CodeImportGroup g = new(Data, gdc, Data.ToolInfo.DecompilerSettings);
g.QueueReplace("gml_GlobalScript_dzmtest", File.ReadAllText(Path.Combine(src, "dzmtest.gml")).Replace("\r\n", "\n"));
var con = Data.GameObjects.ByName("Con_Global");
g.QueueAppend(con.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, Data), "\ndzmt_step();\n");
// No Steam side effects from a test run: achievements, stats, leaderboards.
g.QueueReplace("gml_GlobalScript_sStm_GiveAchievement", "function sStm_GiveAchievement(arg0) { }");
g.QueueReplace("gml_GlobalScript_sStm_UploadLeaderboardStuff", "function sStm_UploadLeaderboardStuff() { }");
g.QueueReplace("gml_Object_Stm_AchievementManager_Alarm_1", "");
g.QueueReplace("gml_Object_UploadHighScore_Other_69", "");
try { g.Import(); } catch (Exception ex) { Console.WriteLine("=== COMPILE ERRORS ==="); Console.WriteLine(ex.Message); throw; }
Console.WriteLine("TEST BUILD OK");

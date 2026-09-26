// CSD2 Autoplay -- UndertaleModTool patch script.
//
// Appends the bot to O_foodbrain's End Step and the status indicator to its
// Draw GUI event (which does not exist in the vanilla game and is created),
// plus campaign mode across the three objects that own the screens between
// shifts. Six events on four objects; nothing else in the data file is touched.
//
// The GML source directory comes from the CSD2AP_SRC environment variable so
// this works from any install location. Always run it against a VANILLA
// data.win: QueueAppend appends, so patching an already-patched file would
// stack a second copy of the bot.

using System; using System.IO; using System.Linq;
using UndertaleModLib.Models;

EnsureDataLoaded();

string src = Environment.GetEnvironmentVariable("CSD2AP_SRC");
if (string.IsNullOrEmpty(src))
    throw new Exception("CSD2AP_SRC is not set - it must point at the folder holding autoplay.gml and indicator.gml");
if (!src.EndsWith("\\") && !src.EndsWith("/"))
    src += "\\";

string autoplayPath  = src + "autoplay.gml";
string indicatorPath = src + "indicator.gml";
if (!File.Exists(autoplayPath))  throw new Exception("missing " + autoplayPath);
if (!File.Exists(indicatorPath)) throw new Exception("missing " + indicatorPath);

string autoplay  = File.ReadAllText(autoplayPath);
string indicator = File.ReadAllText(indicatorPath);

var brain = Data.GameObjects.ByName("O_foodbrain");
if (brain is null)
    throw new Exception("O_foodbrain not found - is this really Cook, Serve, Delicious! 2?");

GlobalDecompileContext gdc = new(Data);
var decompilerSettings = Data.ToolInfo.DecompilerSettings;
UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data, gdc, decompilerSettings);

// Guard against double-patching: if the End Step already mentions the bot,
// this data file is not vanilla.
var endStepExisting = brain.EventHandlerFor(EventType.Step, EventSubtypeStep.EndStep, Data);
string existing = new Underanalyzer.Decompiler.DecompileContext(gdc, endStepExisting, decompilerSettings).DecompileToString();
if (existing.Contains("AP_init"))
    throw new Exception("this data.win is ALREADY PATCHED - patch a vanilla file instead");

var endStep = brain.EventHandlerFor(EventType.Step, EventSubtypeStep.EndStep, Data);
var drawGui = brain.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, Data);

importGroup.QueueAppend(endStep, autoplay);
importGroup.QueueAppend(drawGui, indicator);

// Campaign mode's menu half must live on O_mainmenu: O_foodbrain is created by
// O_loadingscreen when a shift starts and does not exist on the menu at all.
var menu = Data.GameObjects.ByName("O_mainmenu");
if (menu is null) throw new Exception("O_mainmenu not found");
string campaign    = File.ReadAllText(src + "campaign.gml");
string campaignHud = File.ReadAllText(src + "campaign_hud.gml");
importGroup.QueueAppend(menu.EventHandlerFor(EventType.Step, EventSubtypeStep.BeginStep, Data), campaign);
importGroup.QueueAppend(menu.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, Data), campaignHud);

// The medal / results screen appears in ROOM_mainroom, where O_mainmenu does
// not exist and O_foodbrain has not been created yet, so confirming it has to
// be hosted on the screen's own object.
var splash = Data.GameObjects.ByName("O_splashstartend");
if (splash is null) throw new Exception("O_splashstartend not found");
string splashGml = File.ReadAllText(src + "splash.gml");
importGroup.QueueAppend(splash.EventHandlerFor(EventType.Step, EventSubtypeStep.BeginStep, Data), splashGml);

// The rewards screens after that are a THIRD object again: O_splashstartend
// creates O_unlockingmenu and destroys itself in the same step, so the confirm
// on the splash is already gone by the time the rewards are up. The number of
// screens varies with what the shift unlocked.
var unlock = Data.GameObjects.ByName("O_unlockingmenu");
if (unlock is null) throw new Exception("O_unlockingmenu not found");
string rewardsGml = File.ReadAllText(src + "rewards.gml");
importGroup.QueueAppend(unlock.EventHandlerFor(EventType.Step, EventSubtypeStep.BeginStep, Data), rewardsGml);

try { importGroup.Import(); }
catch (Exception ex) { Console.WriteLine("=== COMPILE ERRORS ==="); Console.WriteLine(ex.Message); throw; }

Console.WriteLine("PATCH OK: bot -> O_foodbrain End Step/Draw GUI, campaign -> O_mainmenu Begin Step/Draw GUI, confirm -> O_splashstartend Begin Step, rewards -> O_unlockingmenu Begin Step");

// CSD2 Autoplay -- decompile the patched events back out, so "it compiled" can
// be checked against "it is the code we meant".
//
// Output directory comes from CSD2AP_OUT, defaulting to .\build.
using System; using System.IO; using System.Linq;
EnsureDataLoaded();
GlobalDecompileContext ctx = new(Data);
var st = Data.ToolInfo.DecompilerSettings;

string outDir = Environment.GetEnvironmentVariable("CSD2AP_OUT");
if (string.IsNullOrEmpty(outDir)) outDir = "build";
Directory.CreateDirectory(outDir);

// One entry per QueueAppend in patch.csx, with a marker unique to the appended
// source. A missing marker means the append silently went to the wrong event.
var want = new (string code, string marker)[] {
    ("gml_Object_O_foodbrain_Step_2",       "AP_init"),      // the bot
    ("gml_Object_O_foodbrain_Draw_64",      "AUTOPLAY"),     // in-shift indicator
    ("gml_Object_O_mainmenu_Step_1",        "APC_rest"),     // campaign, menu side
    ("gml_Object_O_mainmenu_Draw_64",       "CAMPAIGN"),     // campaign indicator
    ("gml_Object_O_splashstartend_Step_1",  "APS_t"),        // medal / start-of-day
    ("gml_Object_O_unlockingmenu_Step_1",   "APU_st"),       // post-shift rewards
};

int bad = 0;
foreach (var (n, marker) in want) {
    var c = Data.Code.ByName(n);
    if (c is null) { Console.WriteLine("MISSING " + n); bad++; continue; }
    string s = new Underanalyzer.Decompiler.DecompileContext(ctx, c, st).DecompileToString();
    File.WriteAllText(Path.Combine(outDir, n + ".gml"), s);
    bool ok = s.Contains(marker);
    if (!ok) bad++;
    Console.WriteLine($"{(ok ? "OK  " : "BAD ")} {n}: {s.Length} chars, {marker}={ok}");
}
Console.WriteLine(bad == 0 ? "VERIFY OK: all 6 patched events present" : $"VERIFY FAILED: {bad} problem(s)");

using System; using System.IO; using System.Linq;
EnsureDataLoaded();
string outDir = Environment.GetEnvironmentVariable("DZ_OUT");
Directory.CreateDirectory(outDir);
GlobalDecompileContext ctx = new(Data);
var settings = Data.ToolInfo.DecompilerSettings;
int ok=0, bad=0;
foreach (var code in Data.Code.Where(c => c.ParentEntry is null)) {
    try {
        File.WriteAllText(Path.Join(outDir, code.Name.Content + ".gml"),
            new Underanalyzer.Decompiler.DecompileContext(ctx, code, settings).DecompileToString());
        ok++;
    } catch (Exception e) { File.WriteAllText(Path.Join(outDir, code.Name.Content + ".FAILED.txt"), e.ToString()); bad++; }
}
Console.WriteLine($"ALL decompiled ok={ok} failed={bad}");

using System.Text.Json;

namespace SwampKeeperMods;

public sealed class ModData
{
    public int Version { get; set; } = 1;
    public long LifetimeKills { get; set; }
    public int[] PreparedItems { get; set; } = Array.Empty<int>();
    public Dictionary<string,long> OnlineCredits {get;set;}=new();
}

// A flushed temporary file is atomically promoted; the preceding version remains as .bak.
public sealed class Storage
{
    public ModData Data { get; private set; } = new();
    public string FilePath { get; }
    public string Warning { get; private set; } = "";
    public bool Writable { get; private set; } = true;
    public Storage(string path)
    {
        FilePath = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return;
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            try
            {
                var data = JsonSerializer.Deserialize<ModData>(File.ReadAllText(candidate));
                if (data == null || data.Version != 1 || data.LifetimeKills < 0 || data.PreparedItems == null)
                    throw new InvalidDataException("Invalid mod data");
                data.PreparedItems = data.PreparedItems.Where(i => i >= 0 && i < 40).Distinct().OrderBy(i => i).ToArray();
                if(data.OnlineCredits==null || data.OnlineCredits.Any(p=>p.Key.Length is <1 or >128 || p.Value<0))throw new InvalidDataException("Invalid online kill ledger");
                Data = data;
                if (candidate != path)
                {
                    Warning = "Recovered backup; the last saved kill may be missing.";
                    if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks);
                    // Preserve the valid backup when repairing the primary file.
                    File.Copy(candidate, path, true);
                }
                return;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException) { }
        }
        Writable = false;
        Warning = "Cannot read mod data. Files preserved; saving disabled. See LogOutput.log.";
    }

    // The cumulative count and replay ledger are committed in the same atomic save.
    public long CreditOnlineKills(string runId,long cumulative)
    {
        if(string.IsNullOrEmpty(runId) || runId.Length>128 || cumulative<0)throw new ArgumentException("Invalid online run credit");
        Data.OnlineCredits.TryGetValue(runId,out long before);
        if(cumulative<=before)return 0;
        long added=cumulative-before;
        Data.LifetimeKills=added>long.MaxValue-Data.LifetimeKills?long.MaxValue:Data.LifetimeKills+added;
        Data.OnlineCredits[runId]=cumulative;Save();return added;
    }

    public bool Save()
    {
        if (!Writable) return false;
        try
        {
            using (var stream = new FileStream(FilePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, Data, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(FilePath + ".tmp", FilePath, FilePath + ".bak");
            else File.Move(FilePath + ".tmp", FilePath);
            if (!Warning.StartsWith("Recovered")) Warning = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warning = "Save failed; progress is in memory only: " + ex.Message;
            return false;
        }
    }
}

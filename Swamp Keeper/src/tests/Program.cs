using SwampKeeperMods;
using System.Text.Json;

var folder = Path.Combine(Path.GetTempPath(), "SwampKeeperStorageTests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
var path = Path.Combine(folder, "progress.json");
int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message); passed++;
}
var store = new Storage(path);
Check(store.Data.LifetimeKills == 0 && store.Writable, "First install starts at zero");
for (int i = 1; i <= 200; i++)
{
    store.Data.LifetimeKills++;
    if (!store.Save() || new Storage(path).Data.LifetimeKills != i) throw new Exception("Lost kill at " + i);
}
Check(true, "200 consecutive flushed kill writes survive independent reloads");
store.Data.PreparedItems = new[] { 39, -1, 0, 39, 40, 12 };
store.Save();
Check(new Storage(path).Data.PreparedItems.SequenceEqual(new[] { 0, 12, 39 }), "Loadout sanitizes invalid and duplicate IDs");
File.WriteAllText(path, "{broken");
var recovered = new Storage(path);
Check(recovered.Writable && recovered.Data.LifetimeKills == 200 && recovered.Warning.Contains("Recovered"), "Damaged primary recovers valid previous file");
Check(Directory.GetFiles(folder, "*.corrupt-*").Length == 1, "Recovery preserves damaged original for inspection");
recovered.Data.LifetimeKills++;
Check(recovered.Save() && new Storage(path).Data.LifetimeKills == 201, "Recovered store continues saving");
File.WriteAllText(path, "bad primary"); File.WriteAllText(path + ".bak", "bad backup");
var unreadable = new Storage(path);
Check(!unreadable.Writable && !unreadable.Save() && File.ReadAllText(path) == "bad primary", "Unrecoverable data is preserved without silently resetting history");
File.Delete(path);
File.WriteAllText(path + ".bak", JsonSerializer.Serialize(new ModData { LifetimeKills = 9000000000 }));
var backupOnly = new Storage(path);
Check(backupOnly.Data.LifetimeKills == 9000000000 && backupOnly.Save(), "Backup-only recovery preserves 64-bit totals");
Check(backupOnly.CreditOnlineKills("session-run-a",5)==5,"First authoritative online kill count credits its delta");
var onlineReload=new Storage(path);
Check(onlineReload.CreditOnlineKills("session-run-a",5)==0 && onlineReload.Data.LifetimeKills==9000000005,"Online snapshot replay after process restart cannot double-credit kills");
Check(onlineReload.CreditOnlineKills("session-run-a",3)==0 && onlineReload.CreditOnlineKills("session-run-a",8)==3,"Stale online snapshots are ignored and subsequent kills credit only the increase");
Check(onlineReload.CreditOnlineKills("session-run-b",2)==2 && new Storage(path).Data.OnlineCredits["session-run-a"]==8,"New run credits separately while the previous replay ledger persists");
File.WriteAllText(path,"corrupt after online commit");
var replayRecovery=new Storage(path);
replayRecovery.CreditOnlineKills("session-run-b",2);
Check(replayRecovery.CreditOnlineKills("session-run-a",8)==0 && replayRecovery.Data.LifetimeKills==9000000010,"Atomic backup recovery replays missing online credit without duplicating retained credit");
Console.WriteLine($"{passed} checks passed. Artifacts: {folder}");

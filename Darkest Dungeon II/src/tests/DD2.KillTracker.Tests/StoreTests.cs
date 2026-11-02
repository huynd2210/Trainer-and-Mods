using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker.Tests
{
	internal static class StoreTests
	{
		private static LedgerStore NewStore(TempDir dir, List<string> warnings = null) =>
			new LedgerStore(dir.File("data"), s => { }, s => warnings?.Add(s));

		private static KillLedger WithKills(string profile, int n)
		{
			var l = new KillLedger { ProfileId = profile };
			for (int i = 0; i < n; i++) l.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);
			return l;
		}

		public static void Register(List<(string, Action)> t)
		{
			t.Add(("store: save then load returns the same data", () =>
			{
				using (var dir = new TempDir())
				{
					LedgerStore store = NewStore(dir);
					KillLedger l = WithKills("profile_1", 3);
					Assert.True(store.Save(l), "save");
					Assert.True(!l.Dirty, "clean after save");
					KillLedger back = store.Load("profile_1");
					Assert.Equal(3L, back.LifetimeTotal);
					Assert.Equal("profile_1", back.ProfileId);
				}
			}));

			t.Add(("store: second save replaces atomically, keeps the previous version as .bak, leaves no .tmp", () =>
			{
				using (var dir = new TempDir())
				{
					LedgerStore store = NewStore(dir);
					KillLedger l = WithKills("profile_1", 1);
					store.Save(l);
					l.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);
					store.Save(l);

					string path = store.PathFor("profile_1");
					Assert.True(File.Exists(path), "main exists");
					Assert.True(File.Exists(path + ".bak"), "bak exists");
					Assert.True(!File.Exists(path + ".tmp"), "tmp gone");
					Assert.Equal(2L, KillLedger.Deserialize(File.ReadAllText(path)).LifetimeTotal, "main has latest");
					Assert.Equal(1L, KillLedger.Deserialize(File.ReadAllText(path + ".bak")).LifetimeTotal, "bak has previous");
				}
			}));

			t.Add(("store: a corrupt file is moved aside untouched, never overwritten, and the backup is used", () =>
			{
				using (var dir = new TempDir())
				{
					var warnings = new List<string>();
					LedgerStore store = NewStore(dir, warnings);
					KillLedger l = WithKills("profile_1", 1);
					store.Save(l);
					l.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);
					store.Save(l); // main = 2 kills, bak = 1 kill

					string path = store.PathFor("profile_1");
					const string garbage = "{\"version\":1,\"monsters\":{\"ghoul\":{\"lifetime\":";
					File.WriteAllText(path, garbage);

					KillLedger back = store.Load("profile_1");
					Assert.Equal(1L, back.LifetimeTotal, "restored from .bak");
					Assert.True(back.Dirty, "restored data is flagged for rewriting");
					string[] aside = Directory.GetFiles(store.Directory, "*.corrupt-*");
					Assert.Equal(1, aside.Length, "corrupt file kept aside");
					Assert.Equal(garbage, File.ReadAllText(aside[0]), "corrupt content preserved byte for byte");
					Assert.True(warnings.Any(w => w.Contains("corrupt")), "a warning was logged");

					store.Save(back);
					Assert.Equal(garbage, File.ReadAllText(aside[0]), "saving never touches the aside copy");
					Assert.Equal(1L, KillLedger.Deserialize(File.ReadAllText(path)).LifetimeTotal, "main rewritten from the restored data");
				}
			}));

			t.Add(("store: corrupt file with no backup starts empty and keeps the corrupt file", () =>
			{
				using (var dir = new TempDir())
				{
					var warnings = new List<string>();
					LedgerStore store = NewStore(dir, warnings);
					Directory.CreateDirectory(store.Directory);
					string path = store.PathFor("profile_9");
					File.WriteAllText(path, "this is not json");

					KillLedger back = store.Load("profile_9");
					Assert.Equal(0L, back.LifetimeTotal);
					Assert.True(!back.PersistBlocked, "an empty fresh ledger may be saved");
					Assert.Equal(1, Directory.GetFiles(store.Directory, "*.corrupt-*").Length, "kept aside");
					Assert.True(!File.Exists(path), "original name is free for the fresh ledger");
					Assert.True(warnings.Count > 0, "warned");
				}
			}));

			t.Add(("store: main and backup both corrupt: both are preserved, ledger starts empty", () =>
			{
				using (var dir = new TempDir())
				{
					LedgerStore store = NewStore(dir);
					Directory.CreateDirectory(store.Directory);
					string path = store.PathFor("p");
					File.WriteAllText(path, "garbage 1");
					File.WriteAllText(path + ".bak", "garbage 2");
					KillLedger back = store.Load("p");
					Assert.Equal(0L, back.LifetimeTotal);
					Assert.Equal("garbage 2", File.ReadAllText(path + ".bak"), "bak untouched");
					Assert.Equal(1, Directory.GetFiles(store.Directory, "*.corrupt-*").Length);
				}
			}));

			t.Add(("store: a file that cannot be read is not treated as corrupt and is never overwritten", () =>
			{
				using (var dir = new TempDir())
				{
					LedgerStore store = NewStore(dir);
					store.Save(WithKills("profile_1", 5));
					string path = store.PathFor("profile_1");
					string before = File.ReadAllText(path);

					using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
					{
						KillLedger locked = store.Load("profile_1");
						Assert.True(locked.PersistBlocked, "blocked");
						locked.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);
						Assert.True(!store.Save(locked), "save refused");
					}
					Assert.Equal(before, File.ReadAllText(path), "file untouched");
					Assert.Equal(0, Directory.GetFiles(store.Directory, "*.corrupt-*").Length, "nothing moved aside");
				}
			}));

			t.Add(("store: profile ids become safe file names", () =>
			{
				Assert.Equal("profile_3", LedgerStore.SafeName("profile_3"));
				Assert.Equal("mod_profile_3", LedgerStore.SafeName("mod_profile_3"));
				Assert.Equal("a_b_c__d", LedgerStore.SafeName("a/b\\c: d"));
				Assert.Equal("default", LedgerStore.SafeName(null));
				Assert.Equal("default", LedgerStore.SafeName(""));
				Assert.True(!LedgerStore.SafeName("..\\..\\evil").Contains(".."), "no path traversal");
			}));

			t.Add(("manager: switching profile flushes the old ledger and keeps profiles separate", () =>
			{
				using (var dir = new TempDir())
				{
					var mgr = new LedgerManager(NewStore(dir), 100);
					KillLedger a = mgr.EnsureProfile("profile_1");
					a.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);
					a.Record(Make.Kill("ghoul"), SourceInfo.BaseGame);

					KillLedger b = mgr.EnsureProfile("mod_profile_1");
					Assert.True(!ReferenceEquals(a, b), "different ledger");
					Assert.Equal(0L, b.LifetimeTotal, "fresh profile starts empty");
					Assert.True(File.Exists(NewStore(dir).PathFor("profile_1")), "old profile was flushed on switch");
					b.Record(Make.Kill("wolf"), SourceInfo.BaseGame);

					KillLedger a2 = mgr.EnsureProfile("profile_1");
					Assert.Equal(2L, a2.LifetimeTotal, "profile 1 reloaded from disk");
					Assert.Null(a2.Find("wolf"), "no bleed between profiles");
					Assert.Equal(1L, mgr.EnsureProfile("mod_profile_1").LifetimeTotal, "mod profile flushed and reloaded");
					Assert.True(ReferenceEquals(mgr.Current, mgr.EnsureProfile("mod_profile_1")), "same key is a no-op");
				}
			}));

			t.Add(("manager: flush writes only when dirty; max records applies to loaded ledgers", () =>
			{
				using (var dir = new TempDir())
				{
					var mgr = new LedgerManager(NewStore(dir), 2);
					KillLedger l = mgr.EnsureProfile("p");
					Assert.True(!mgr.Flush(), "nothing to write");
					for (int i = 0; i < 5; i++) l.Record(Make.Kill("a"), SourceInfo.BaseGame);
					Assert.Equal(2, l.Kills.Count, "cap from config");
					Assert.True(mgr.Flush(), "dirty ledger written");
					Assert.True(!mgr.Flush(), "clean afterwards");
					mgr.SetMaxRecords(1);
					Assert.Equal(1, l.Kills.Count, "cap lowered live");
				}
			}));
		}
	}
}

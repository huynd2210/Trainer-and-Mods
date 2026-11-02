using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker.Tests
{
	internal static class AttributionTests
	{
		private const string Cat = "ActorDataClass";
		private static readonly SourceInfo Base = SourceInfo.BaseGame;
		private static readonly SourceInfo Cata = new SourceInfo(SourceKind.Dlc, "Catacombs", null, "dlc_catacombs");
		private static readonly SourceInfo ModA = new SourceInfo(SourceKind.Mod, "Mod A", "111", "111");
		private static readonly SourceInfo ModB = new SourceInfo(SourceKind.Mod, "Mod B", null, "modb");

		private static OriginClassifier Classifier()
		{
			var c = new OriginClassifier();
			c.SetBaseRoots(new[] { @"C:\Game\Data\StreamingAssets\Excel" });
			c.SetDlcs(new[] { new DlcEntry { FolderName = "dlc_catacombs", Title = "Catacombs" }, new DlcEntry { FolderName = "dlc_dul_cru", Title = null } });
			c.SetMods(new[]
			{
				new ModEntry { Path = @"C:\Steam\workshop\content\1940340\3000111", Title = "Cool Monsters", WorkshopId = "3000111" },
				new ModEntry { Path = @"C:\Game\Data\StreamingAssets\mods\LocalPack", Title = "mods", WorkshopId = null },
				new ModEntry { Path = @"C:\Steam\workshop\content\1940340\30001", Title = "Short Id Mod", WorkshopId = "30001" },
			});
			return c;
		}

		public static void Register(List<(string, Action)> t)
		{
			// ------------------------------------------------------------ folder -> source
			t.Add(("classifier: base folder, game-type folder and base files are Base game", () =>
			{
				OriginClassifier c = Classifier();
				Assert.Equal(SourceKind.Base, c.Classify(@"C:\Game\Data\StreamingAssets\Excel").Kind);
				Assert.Equal(SourceKind.Base, c.Classify(@"C:\Game\Data\StreamingAssets\Excel\kingdom").Kind, "game-type folder");
				Assert.Equal(SourceKind.Base, c.Classify("c:/game/data/streamingassets/excel/").Kind, "case and slashes");
			}));

			t.Add(("classifier: a DLC sub-folder is named after the DLC", () =>
			{
				OriginClassifier c = Classifier();
				SourceInfo s = c.Classify(@"C:\Game\Data\StreamingAssets\Excel\dlc_catacombs");
				Assert.Equal(SourceKind.Dlc, s.Kind);
				Assert.Equal("Catacombs", s.Name);
				Assert.Equal("Catacombs (DLC)", s.Label);
				Assert.Equal("Dul Cru", c.Classify(@"C:\Game\Data\StreamingAssets\Excel\dlc_dul_cru").Name, "pretty name fallback");
			}));

			t.Add(("classifier: mod folders, their Overrides sub-folder and DLC sub-folders map to the mod, with workshop id", () =>
			{
				OriginClassifier c = Classifier();
				SourceInfo s = c.Classify(@"C:\Steam\workshop\content\1940340\3000111");
				Assert.Equal(SourceKind.Mod, s.Kind);
				Assert.Equal("Cool Monsters", s.Name);
				Assert.Equal("3000111", s.ModId);
				Assert.Equal("3000111", s.Folder);
				Assert.True(c.Classify(@"C:\Steam\workshop\content\1940340\3000111\Overrides").SameAs(s), "Overrides folder");
				Assert.True(c.Classify(@"C:\Steam\workshop\content\1940340\3000111\Overrides\expedition").SameAs(s), "game type under Overrides");
				Assert.True(c.Classify(@"C:\Steam\workshop\content\1940340\3000111\dlc_catacombs").SameAs(s), "mod's own DLC folder");
			}));

			t.Add(("classifier: sibling folders with a shared prefix are not confused", () =>
			{
				OriginClassifier c = Classifier();
				Assert.Equal("Short Id Mod", c.Classify(@"C:\Steam\workshop\content\1940340\30001").Name);
				Assert.Equal("Cool Monsters", c.Classify(@"C:\Steam\workshop\content\1940340\3000111").Name);
				Assert.Equal(SourceKind.Unknown, c.Classify(@"C:\Steam\workshop\content\1940340\300011").Kind, "unregistered neighbour");
			}));

			t.Add(("classifier: a local mod without a manifest title is named after its folder; no id", () =>
			{
				OriginClassifier c = Classifier();
				SourceInfo s = c.Classify(@"C:\Game\Data\StreamingAssets\mods\LocalPack");
				Assert.Equal("LocalPack", s.Name, "title 'mods' is the shared folder name, use the leaf");
				Assert.Null(s.ModId, "local mods have no workshop id");
				Assert.Equal(SourceKind.Mod, s.Kind);
			}));

			t.Add(("classifier: unrelated folder is Unknown, empty is Unknown", () =>
			{
				OriginClassifier c = Classifier();
				Assert.Equal(SourceKind.Unknown, c.Classify(@"D:\elsewhere").Kind);
				Assert.Equal(SourceKind.Unknown, c.Classify("").Kind);
				Assert.Equal(SourceKind.Unknown, c.Classify(null).Kind);
			}));

			// ------------------------------------------------------------ merge rules
			t.Add(("tracker: base monster is Base game, DLC monster is the DLC, mod-added monster is a Mod", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "base_ghoul", Base, false);
				tr.Note(Cat, "cata_effigy", Cata, false);
				tr.Note(Cat, "mod_wolf", ModA, false);
				Assert.Equal(SourceKind.Base, tr.Resolve(Cat, "base_ghoul").Kind);
				Assert.Equal(SourceKind.Dlc, tr.Resolve(Cat, "cata_effigy").Kind);
				SourceInfo m = tr.Resolve(Cat, "mod_wolf");
				Assert.Equal(SourceKind.Mod, m.Kind);
				Assert.Equal("Mod A", m.Name);
				Assert.Null(tr.Resolve(Cat, "never_seen"), "unseen id");
				Assert.Null(tr.Resolve("Other", "base_ghoul"), "other category");
			}));

			t.Add(("tracker: a mod REPLACING a vanilla id (override flag) is an Override by that mod, not a new monster", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "beastmen_huntsman", Base, false);
				tr.Note(Cat, "beastmen_huntsman", ModA, true);
				SourceInfo s = tr.Resolve(Cat, "beastmen_huntsman");
				Assert.Equal(SourceKind.Override, s.Kind);
				Assert.Equal("Mod A", s.Name);
				Assert.Equal("111", s.ModId);
				Assert.Equal("Mod A (override)", s.Label);
				Assert.True(s.IsMod);
			}));

			t.Add(("tracker: a mod that only extends a vanilla id (no override flag) leaves it Base game", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "beastmen_huntsman", Base, false);
				tr.Note(Cat, "beastmen_huntsman", ModA, false);
				Assert.Equal(SourceKind.Base, tr.Resolve(Cat, "beastmen_huntsman").Kind);
			}));

			t.Add(("tracker: a DLC folder extending a base id keeps Base game", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "cultist_altar", Base, false);
				tr.Note(Cat, "cultist_altar", Cata, false);
				Assert.Equal(SourceKind.Base, tr.Resolve(Cat, "cultist_altar").Kind);
			}));

			t.Add(("tracker: two mods - the later load order wins when it overrides, loses when it does not", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "x", ModA, false);
				tr.Note(Cat, "x", ModB, true);
				SourceInfo s = tr.Resolve(Cat, "x");
				Assert.Equal(SourceKind.Override, s.Kind);
				Assert.Equal("Mod B", s.Name);

				var tr2 = new OriginTracker();
				tr2.Note(Cat, "y", ModA, false);
				tr2.Note(Cat, "y", ModB, false);
				Assert.Equal(SourceKind.Mod, tr2.Resolve(Cat, "y").Kind);
				Assert.Equal("Mod A", tr2.Resolve(Cat, "y").Name);
			}));

			t.Add(("tracker: base overriding base (game-type folders) stays Base game", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "z", Base, false);
				tr.Note(Cat, "z", Base, true);
				Assert.Equal(SourceKind.Base, tr.Resolve(Cat, "z").Kind);
			}));

			t.Add(("tracker: the game re-reads its folders several times; replaying a pass is idempotent and bounded", () =>
			{
				var tr = new OriginTracker();
				for (int pass = 0; pass < 5; pass++)
				{
					tr.Note(Cat, "a", Base, false);
					tr.Note(Cat, "a", ModA, true);
					tr.Note(Cat, "b", ModB, false);
				}
				Assert.Equal(3, tr.Count, "stored sightings");
				Assert.Equal(SourceKind.Override, tr.Resolve(Cat, "a").Kind);
				Assert.Equal(SourceKind.Mod, tr.Resolve(Cat, "b").Kind);
			}));

			t.Add(("tracker: backfilling the base game after a mod was seen turns a false 'new monster' into an Override", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "beastmen_huntsman", ModA, true); // mod seen, base load was missed
				Assert.Equal(SourceKind.Mod, tr.Resolve(Cat, "beastmen_huntsman").Kind, "before the backfill it cannot know");
				Assert.Equal(0, tr.BaseCount);
				tr.NoteFirst(Cat, "beastmen_huntsman", Base, false);
				Assert.Equal(SourceKind.Override, tr.Resolve(Cat, "beastmen_huntsman").Kind, "after the backfill");
				Assert.Equal(1, tr.BaseCount);
			}));

			t.Add(("tracker: Reset forgets everything", () =>
			{
				var tr = new OriginTracker();
				tr.Note(Cat, "a", Base, false);
				tr.Reset();
				Assert.Null(tr.Resolve(Cat, "a"));
				Assert.Equal(0, tr.Count);
				Assert.Equal(0, tr.BaseCount);
			}));

			// ------------------------------------------------------------ end to end on files
			t.Add(("attribution end to end: base + DLC + a mod with its own monster and an Overrides folder, replayed like the game does", () =>
			{
				using (var dir = new TempDir())
				{
					string excel = Path.Combine(dir.Path, "Excel");
					string dlc = Path.Combine(excel, "dlc_catacombs");
					string mod = Path.Combine(dir.Path, "workshop", "777");
					string overrides = Path.Combine(mod, "Overrides");
					foreach (string d in new[] { excel, dlc, mod, overrides }) Directory.CreateDirectory(d);

					File.WriteAllText(Path.Combine(excel, "ghoul_data_export.Group.csv"),
						"element_start,base_ghoul,ActorDataClass\r\nm_Tags,monster,\r\nelement_end\r\nelement_start,base_ghoul,ActorDataStats\r\nkey_map,health_max,\r\nelement_end\r\n"
						+ "element_start,base_ghoul_corpse,ActorDataClass\r\nm_Tags,corpse,monster,\r\nelement_end\r\n");
					File.WriteAllText(Path.Combine(dlc, "effigy_data_export_DLC2.Group.csv"),
						"element_start,cata_effigy,ActorDataClass\nm_Tags,monster,\nelement_end\nelement_start,base_ghoul,ActorDataClass\nm_Tags,extra,\nelement_end\n");
					File.WriteAllText(Path.Combine(mod, "wolf.Group.csv"),
						"element_start,mod_wolf,ActorDataClass\nm_Tags,monster,\nelement_end\n");
					File.WriteAllText(Path.Combine(overrides, "ghoul_fix.Group.csv"),
						"element_start,base_ghoul,ActorDataClass\nm_Tags,monster,\nelement_end\n");

					var classifier = new OriginClassifier();
					classifier.SetBaseRoots(new[] { excel });
					classifier.SetDlcs(new[] { new DlcEntry { FolderName = "dlc_catacombs", Title = "Catacombs" } });
					classifier.SetMods(new[] { new ModEntry { Path = mod, Title = "Wolf Pack", WorkshopId = "777" } });

					var tracker = new OriginTracker();
					// the game's order: base root, owned DLC folders, then each mod (root, then Overrides with the override flag)
					foreach ((string folder, bool flag) in new[] { (excel, false), (dlc, false), (mod, false), (overrides, true) })
					{
						SourceInfo origin = classifier.Classify(folder);
						GroupCsv.ScanFolder(folder, (type, name) => tracker.Note(type, name, origin, flag));
					}

					Assert.Equal(SourceKind.Override, tracker.Resolve(Cat, "base_ghoul").Kind, "vanilla id replaced by the mod");
					Assert.Equal("Wolf Pack", tracker.Resolve(Cat, "base_ghoul").Name);
					Assert.Equal(SourceKind.Mod, tracker.Resolve(Cat, "mod_wolf").Kind, "mod's own monster");
					Assert.Equal("777", tracker.Resolve(Cat, "mod_wolf").ModId);
					Assert.Equal(SourceKind.Dlc, tracker.Resolve(Cat, "cata_effigy").Kind);
					Assert.Equal(SourceKind.Base, tracker.Resolve(Cat, "base_ghoul_corpse").Kind);
					Assert.Null(tracker.Resolve(Cat, "ActorDataStats"), "non-ActorDataClass rows are not monsters (category filtering is the caller's job; the tracker keys by category)");

					// The same with the base game backfilled afterwards (the plugin started after the base load)
					var late = new OriginTracker();
					foreach ((string folder, bool flag) in new[] { (mod, false), (overrides, true) })
					{
						SourceInfo origin = classifier.Classify(folder);
						GroupCsv.ScanFolder(folder, (type, name) => late.Note(type, name, origin, flag));
					}
					foreach ((string folder, bool flag) in new[] { (dlc, false), (excel, false) }) // reversed: NoteFirst prepends
					{
						SourceInfo origin = classifier.Classify(folder);
						GroupCsv.ScanFolder(folder, (type, name) => late.NoteFirst(type, name, origin, flag));
					}
					Assert.Equal(SourceKind.Override, late.Resolve(Cat, "base_ghoul").Kind, "late backfill gives the same answer");
					Assert.Equal(SourceKind.Base, late.Resolve(Cat, "base_ghoul_corpse").Kind);
					Assert.Equal(SourceKind.Dlc, late.Resolve(Cat, "cata_effigy").Kind);
				}
			}));

			t.Add(("attribution against the real game data (skipped when the game is not installed): base ids and the DLC folders classify as expected", () =>
			{
				string excel = @"C:\Program Files (x86)\Steam\steamapps\common\Darkest Dungeon® II\Darkest Dungeon II_Data\StreamingAssets\Excel";
				if (!Directory.Exists(excel))
				{
					Console.WriteLine("      (game data not found, skipped)");
					return;
				}

				var classifier = new OriginClassifier();
				classifier.SetBaseRoots(new[] { excel });
				var dlcFolders = Directory.GetDirectories(excel, "dlc_*").Select(Path.GetFileName).ToList();
				classifier.SetDlcs(dlcFolders.Select(f => new DlcEntry { FolderName = f }));

				var tracker = new OriginTracker();
				// the game's order: base root, owned DLC folders, then the current game type's folder (override flag set)
				var folders = new List<(string folder, bool flag)> { (excel, false) };
				folders.AddRange(dlcFolders.Select(f => (Path.Combine(excel, f), false)));
				folders.Add((Path.Combine(excel, "expedition"), true));
				foreach ((string folder, bool flag) in folders)
				{
					SourceInfo origin = classifier.Classify(folder);
					GroupCsv.ScanFolder(folder, (type, name) => { if (type == Cat) tracker.Note(type, name, origin, flag); });
				}

				int ids = tracker.Ids(Cat).Count();
				Assert.True(ids > 400, "expected hundreds of ActorDataClass ids, found " + ids);
				Assert.Equal(SourceKind.Base, tracker.Resolve(Cat, "beastmen_huntsman").Kind, "vanilla monster");
				Assert.Equal(SourceKind.Base, tracker.Resolve(Cat, "beastmen_huntsman_corpse").Kind, "vanilla corpse class");
				SourceInfo effigy = tracker.Resolve(Cat, "vermin_ectoplasmic_effigy");
				Assert.Equal(SourceKind.Dlc, effigy.Kind, "defined only in dlc_catacombs");
				Assert.Equal("Catacombs", effigy.Name);
				Assert.Equal(SourceKind.Base, tracker.Resolve(Cat, "cultist_altar").Kind, "defined only in the expedition game-type folder: still the base game");
				Console.WriteLine("      (" + ids + " base/DLC monster ids classified)");
			}));
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker.Tests
{
	internal static class MiscTests
	{
		public static void Register(List<(string, Action)> t)
		{
			t.Add(("group csv: element headers are read like the game does (CRLF, trailing commas, other types, noise)", () =>
			{
				string text = "garbage before\r\nelement_start,a_monster,ActorDataClass\r\nm_Tags,monster,\r\nelement_end\r\n"
					+ "element_start,a_monster,ActorDataStats\r\nadd_stats,5,\r\nelement_end\r\n"
					+ "element_start,,,Broken\r\nelement_end\r\n" // only two non-empty fields: ignored
					+ "element_start, spaced ,ActorDataClass \nm_Tags,x,\nelement_end";
				var seen = new List<string>();
				GroupCsv.ScanText(text, (type, name) => seen.Add(type + ":" + name));
				Assert.Equal("ActorDataClass:a_monster|ActorDataStats:a_monster|ActorDataClass:spaced", string.Join("|", seen));
			}));

			t.Add(("group csv: an element_start with no element_end is ignored, empty text is fine", () =>
			{
				var seen = new List<string>();
				GroupCsv.ScanText("element_start,a,ActorDataClass\nm_Tags,x,", (t2, n) => seen.Add(n));
				GroupCsv.ScanText("", (t2, n) => seen.Add(n));
				GroupCsv.ScanText(null, (t2, n) => seen.Add(n));
				Assert.Equal(0, seen.Count);
			}));

			t.Add(("group csv: single-type files use the file name up to the first dot as the id; folders are not recursed", () =>
			{
				using (var dir = new TempDir())
				{
					File.WriteAllText(dir.File("my_ghost.ActorDataClass.csv"), "x");
					File.WriteAllText(dir.File("other.Buff.csv"), "x");
					Directory.CreateDirectory(dir.File("sub"));
					File.WriteAllText(Path.Combine(dir.File("sub"), "deep.ActorDataClass.csv"), "x");
					File.WriteAllText(dir.File("also.Group.csv"), "element_start,grouped,ActorDataClass\nelement_end");

					var singles = new List<string>();
					GroupCsv.ScanSingleTypeFiles(dir.Path, "ActorDataClass", "csv", (type, name) => singles.Add(name));
					Assert.Equal("my_ghost", string.Join(",", singles));

					var groups = new List<string>();
					GroupCsv.ScanFolder(dir.Path, (type, name) => groups.Add(name));
					Assert.Equal("grouped", string.Join(",", groups));
					GroupCsv.ScanFolder(Path.Combine(dir.Path, "missing"), (type, name) => groups.Add("never"));
					Assert.Equal(1, groups.Count, "a missing folder is not an error");
				}
			}));

			t.Add(("kill policy: enemies count; corpses, props, cover, heroes and allies do not (unless non-enemy counting is on, corpses never)", () =>
			{
				string[] monster = { "monster", "beastmen", "beastmen_grunt" };
				Assert.True(KillPolicy.ShouldCount("skill", monster, 1, false), "plain monster");
				Assert.True(KillPolicy.ShouldCount("effect", monster, 1, false), "dot/effect kill");
				Assert.True(KillPolicy.ShouldCount("chain", monster, 1, false), "chain kill of a real monster");
				Assert.True(KillPolicy.ShouldCount("skill", null, 1, false), "no tags known: the team decides");
				Assert.True(KillPolicy.ShouldCount("skill", new[] { "some_mod_tag" }, 2, false), "a modded monster with arbitrary tags");

				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "corpse", "monster" }, 1, false), "corpse");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "corpse", "monster" }, 1, true), "corpse stays out even with non-enemy counting");
				Assert.True(!KillPolicy.ShouldCount("effect", new[] { "monster", "prop" }, 1, false), "prop");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "monster", "cover" }, 1, false), "barricade");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "monster", "inanimate" }, 1, false), "book stack");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "hero" }, 0, false), "hero");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "monster", "friendly" }, 1, false), "friendly");
				Assert.True(!KillPolicy.ShouldCount("skill", new[] { "monster", "kingdoms_ally" }, 1, false), "kingdoms ally");
				Assert.True(!KillPolicy.ShouldCount("skill", monster, 0, false), "anything on the party's team");

				Assert.True(KillPolicy.ShouldCount("skill", new[] { "hero" }, 0, true), "hero counted when enabled");
				Assert.True(KillPolicy.ShouldCount("skill", new[] { "monster", "prop" }, 1, true), "prop counted when enabled");
			}));

			t.Add(("kill policy: clean-up and cheat death types are never kills", () =>
			{
				string[] monster = { "monster" };
				foreach (string type in new[] { "team", "round", "capture", "debug" })
				{
					Assert.True(!KillPolicy.ShouldCount(type, monster, 1, false), type + " ignored");
					Assert.True(!KillPolicy.ShouldCount(type, monster, 1, true), type + " ignored even with non-enemy counting");
				}
				Assert.True(KillPolicy.ShouldCount("some_mod_death_type", monster, 1, false), "unknown death types count");
				Assert.True(KillPolicy.ShouldCount(null, monster, 1, false), "missing death type counts");
				Assert.True(KillPolicy.IsHero(new[] { "hero_story" }) && KillPolicy.IsHero(new[] { "x", "hero" }) && !KillPolicy.IsHero(new[] { "monster" }) && !KillPolicy.IsHero(null));
			}));

			t.Add(("key chord: plain keys, modifiers, case, fallback", () =>
			{
				KeyChord a = KeyChord.Parse("F11", "F1");
				Assert.Equal("F11", a.Key);
				Assert.True(!a.Ctrl && !a.Shift && !a.Alt);
				KeyChord b = KeyChord.Parse(" ctrl + SHIFT + k ", "F11");
				Assert.Equal("k", b.Key);
				Assert.True(b.Ctrl && b.Shift && !b.Alt, "modifiers");
				Assert.Equal("Ctrl+Shift+k", b.ToString());
				Assert.Equal("F11", KeyChord.Parse("", "F11").Key, "empty falls back");
				Assert.Equal("F11", KeyChord.Parse("Ctrl+", "F11").Key, "modifier only falls back");
				Assert.Equal("Backquote", KeyChord.Parse("Alt+Backquote", "F11").Key);
			}));
		}
	}
}

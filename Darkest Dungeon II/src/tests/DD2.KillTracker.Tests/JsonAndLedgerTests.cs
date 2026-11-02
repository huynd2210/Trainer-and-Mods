using System;
using System.Collections.Generic;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker.Tests
{
	internal static class JsonTests
	{
		public static void Register(List<(string, Action)> t)
		{
			t.Add(("json: round-trip of nested values, escapes and unicode", () =>
			{
				var root = new Dictionary<string, object>
				{
					["s"] = "quote\" slash\\ tab\t nl\n unicode \u00e9\u4e2d ctl\u0001",
					["n"] = 123456789012L,
					["d"] = 1.5,
					["b"] = true,
					["z"] = null,
					["arr"] = new List<object> { 1L, "two", new Dictionary<string, object> { ["k"] = "v" }, new List<object>() },
					["obj"] = new Dictionary<string, object>(),
				};
				string text = Json.Write(root);
				Assert.True(Json.TryParse(text, out object parsed), "parse failed: " + text);
				string again = Json.Write(parsed);
				Assert.Equal(text, again, "second serialization");
				var o = (Dictionary<string, object>)parsed;
				Assert.Equal((string)root["s"], (string)o["s"], "string with escapes");
				Assert.Equal(123456789012L, (long)o["n"], "long");
				Assert.Equal(true, (bool)o["b"], "bool");
				Assert.Null(o["z"], "null");
			}));

			t.Add(("json: malformed documents are rejected, never thrown", () =>
			{
				foreach (string bad in new[] { "", "   ", "{", "{\"a\":", "{\"a\":1,}", "[1,2", "{\"a\" 1}", "{\"a\":1} trailing", "\"unterminated", "{\"a\":\"\\q\"}", "nope" })
				{
					Assert.True(!Json.TryParse(bad, out _), "should reject: " + bad);
				}
			}));

			t.Add(("json: absurd nesting is rejected instead of overflowing the stack", () =>
			{
				string deep = new string('[', 5000) + new string(']', 5000);
				Assert.True(!Json.TryParse(deep, out _));
			}));
		}
	}

	internal static class LedgerTests
	{
		public static void Register(List<(string, Action)> t)
		{
			t.Add(("ledger: aggregation counts per monster, lifetime and run together", () =>
			{
				var l = new KillLedger();
				l.BeginRun("r1");
				for (int i = 0; i < 3; i++) l.Record(Make.Kill("cultist_brawler", "Cultist Brawler", "r1", "2026-10-06T10:00:0" + i + "Z"), SourceInfo.BaseGame);
				l.Record(Make.Kill("beastmen_huntsman", null, "r1"), SourceInfo.BaseGame);

				Assert.Equal(4L, l.LifetimeTotal, "lifetime total");
				Assert.Equal(4L, l.RunTotal, "run total");
				MonsterStats b = l.Find("cultist_brawler");
				Assert.Equal(3L, b.Lifetime, "brawler lifetime");
				Assert.Equal(3L, b.Run, "brawler run");
				Assert.Equal("Cultist Brawler", b.Name, "name");
				Assert.Equal("2026-10-06T10:00:00Z", b.FirstKillUtc, "first");
				Assert.Equal("2026-10-06T10:00:02Z", b.LastKillUtc, "last");
				Assert.Null(l.Find("beastmen_huntsman").Name, "unknown name stays null");
				Assert.True(l.Dirty, "dirty after a kill");
				Assert.Equal("Base game", l.Kills[0].ContentSource, "content source on raw record");
			}));

			t.Add(("ledger: a new run zeroes the run counters, never the lifetime ones", () =>
			{
				var l = new KillLedger();
				l.BeginRun("r1");
				l.Record(Make.Kill("a", run: "r1"), SourceInfo.BaseGame);
				l.Record(Make.Kill("a", run: "r1"), SourceInfo.BaseGame);
				l.BeginRun("r1");
				Assert.Equal(2L, l.RunTotal, "same id is a no-op");
				l.BeginRun("r2");
				Assert.Equal(0L, l.RunTotal, "run total reset");
				Assert.Equal(0L, l.Find("a").Run, "monster run reset");
				Assert.Equal(2L, l.Find("a").Lifetime, "lifetime kept");
				l.Record(Make.Kill("a", run: "r2"), SourceInfo.BaseGame);
				Assert.Equal(1L, l.RunTotal);
				Assert.Equal(3L, l.LifetimeTotal);
			}));

			t.Add(("ledger: a kill carrying a different game run id starts a new run", () =>
			{
				var l = new KillLedger();
				l.Record(Make.Kill("a", run: "261001_120000"), SourceInfo.BaseGame);
				l.Record(Make.Kill("a", run: "261001_120000"), SourceInfo.BaseGame);
				l.Record(Make.Kill("a", run: "261002_090000"), SourceInfo.BaseGame);
				Assert.Equal(1L, l.RunTotal, "run total after id change");
				Assert.Equal(3L, l.LifetimeTotal);
				Assert.Equal("261002_090000", l.RunId);
			}));

			t.Add(("ledger: an invented run id is replaced by the game's real one without clearing counters", () =>
			{
				var l = new KillLedger();
				l.BeginRun("new-20261006T120000Z", synthetic: true);
				l.Record(Make.Kill("a"), SourceInfo.BaseGame);
				l.ObserveRunId("261006_120105");
				Assert.Equal("261006_120105", l.RunId, "adopted");
				Assert.True(!l.RunIdSynthetic, "no longer synthetic");
				Assert.Equal(1L, l.RunTotal, "counters survive the adoption");
				l.ObserveRunId("261006_120105");
				l.ObserveRunId(null);
				l.ObserveRunId("");
				Assert.Equal(1L, l.RunTotal, "repeat / empty ids change nothing");
				l.ObserveRunId("261007_080000");
				Assert.Equal(0L, l.RunTotal, "a different real id is a new run");
			}));

			t.Add(("ledger: raw log is capped, aggregates stay exact", () =>
			{
				var l = new KillLedger { MaxRecords = 3 };
				for (int i = 0; i < 10; i++) l.Record(Make.Kill("m" + (i % 2), t: "2026-10-06T12:00:" + i.ToString("00") + "Z"), SourceInfo.BaseGame);
				Assert.Equal(3, l.Kills.Count, "kept records");
				Assert.Equal("2026-10-06T12:00:09Z", l.Kills[2].TimeUtc, "newest kept");
				Assert.Equal("2026-10-06T12:00:07Z", l.Kills[0].TimeUtc, "oldest kept is the 8th");
				Assert.Equal(7L, l.DroppedRecords, "dropped");
				Assert.Equal(10L, l.LifetimeTotal, "total exact");
				Assert.Equal(5L, l.Find("m0").Lifetime + 0, "m0");
				Assert.Equal(5L, l.Find("m1").Lifetime, "m1");

				l.MaxRecords = 1;
				Assert.Equal(1, l.Kills.Count, "shrinking the cap trims");
				Assert.Equal(9L, l.DroppedRecords);
			}));

			t.Add(("ledger: MaxRecords 0 keeps totals only", () =>
			{
				var l = new KillLedger { MaxRecords = 0 };
				l.Record(Make.Kill("a"), SourceInfo.BaseGame);
				Assert.Equal(0, l.Kills.Count);
				Assert.Equal(1L, l.Find("a").Lifetime);
				Assert.Equal(1L, l.DroppedRecords);
			}));

			t.Add(("ledger: serialize/deserialize round-trip keeps everything, nulls stay null", () =>
			{
				var l = new KillLedger { ProfileId = "profile_2" };
				l.BeginRun("261006_101010");
				var mod = new SourceInfo(SourceKind.Mod, "Cool \"Mod\"", "123456789", "cool_mod");
				var ovr = new SourceInfo(SourceKind.Override, "Other Mod", null, "other");
				l.Record(new KillRecord { MonsterId = "base_ghoul", MonsterName = "Ghoul", TimeUtc = "2026-10-06T10:00:00Z", RunId = "261006_101010",
					KillerClassId = "highwayman", KillerHeroName = "Dismas", SourceType = "dot", SourceId = "bleed", Region = "crossroads" }, SourceInfo.BaseGame);
				l.Record(Make.Kill("mod_wolf", "Wolf \u00e9", "261006_101010"), mod);
				l.Record(Make.Kill("base_cultist", null, "261006_101010"), ovr);
				l.BeginRun("261006_111111");
				l.Record(Make.Kill("mod_wolf", "Wolf \u00e9", "261006_111111"), mod);

				KillLedger back = KillLedger.Deserialize(l.Serialize());
				Assert.NotNull(back, "deserialized");
				Assert.Equal("profile_2", back.ProfileId);
				Assert.Equal(l.RunId, back.RunId, "run id");
				Assert.Equal(l.LifetimeTotal, back.LifetimeTotal, "lifetime");
				Assert.Equal(l.RunTotal, back.RunTotal, "run total");
				Assert.Equal(4L, back.LifetimeTotal);
				Assert.Equal(1L, back.RunTotal);
				MonsterStats wolf = back.Find("mod_wolf");
				Assert.Equal(2L, wolf.Lifetime);
				Assert.Equal(1L, wolf.Run);
				Assert.Equal(SourceKind.Mod, wolf.Source.Kind);
				Assert.Equal("Cool \"Mod\"", wolf.Source.Name);
				Assert.Equal("123456789", wolf.Source.ModId);
				Assert.Equal("cool_mod", wolf.Source.Folder);
				Assert.Equal(SourceKind.Override, back.Find("base_cultist").Source.Kind);
				Assert.Null(back.Find("base_cultist").Name, "unset name stays null");
				Assert.Equal("Wolf \u00e9", wolf.Name);
				Assert.Equal(4, back.Kills.Count);
				KillRecord first = back.Kills[0];
				Assert.Equal("Dismas", first.KillerHeroName);
				Assert.Equal("highwayman", first.KillerClassId);
				Assert.Equal("bleed", first.SourceId);
				Assert.Equal("crossroads", first.Region);
				Assert.Null(back.Kills[1].KillerClassId, "unset killer stays null");
				Assert.Null(back.Kills[1].Region, "unset region stays null");
				Assert.Equal(l.Serialize(), back.Serialize(), "stable serialization");
			}));

			t.Add(("ledger: example document (printed below, pretty-printed for reading)", () =>
			{
				var l = new KillLedger { ProfileId = "profile_3", MaxRecords = 100 };
				l.BeginRun("261006_183000");
				var mod = new SourceInfo(SourceKind.Mod, "Wolf Pack", "3412345678", "3412345678");
				var ovr = new SourceInfo(SourceKind.Override, "Hard Mode Plus", "3500000001", "3500000001");
				l.Record(new KillRecord { MonsterId = "beastmen_huntsman", MonsterName = "Huntsman", TimeUtc = "2026-10-06T18:41:19Z", RunId = "261006_183000",
					KillerClassId = "highwayman", KillerHeroName = "Dismas", SourceType = "dot", SourceId = "bleed", Region = "crossroads" }, SourceInfo.BaseGame);
				l.Record(new KillRecord { MonsterId = "wolfpack_alpha", MonsterName = "Alpha Wolf", TimeUtc = "2026-10-06T18:44:02Z", RunId = "261006_183000",
					KillerClassId = "plague_doctor", KillerHeroName = "Reynauld", SourceType = "skill", SourceId = "pd_noxious_blast", Region = "crossroads" }, mod);
				l.Record(new KillRecord { MonsterId = "cultist_brawler", MonsterName = "Cultist Brawler", TimeUtc = "2026-10-06T18:47:40Z", RunId = "261006_183000",
					SourceType = "skill", SourceId = "hs_riposte" }, ovr);
				Assert.True(Json.TryParse(l.Serialize(), out object parsed));
				Console.WriteLine("      " + Json.Write(parsed, true).Replace("\n", "\n      "));
			}));

			t.Add(("ledger: files that are valid JSON but not a ledger are rejected", () =>
			{
				Assert.Null(KillLedger.Deserialize("{}"));
				Assert.Null(KillLedger.Deserialize("[]"));
				Assert.Null(KillLedger.Deserialize("{\"version\":1}"));
				Assert.Null(KillLedger.Deserialize("{\"monsters\":{}}"));
				Assert.Null(KillLedger.Deserialize("{\"version\":1,\"monsters\":{\"a\":5}}"));
				Assert.Null(KillLedger.Deserialize("not json at all"));
				Assert.NotNull(KillLedger.Deserialize("{\"version\":1,\"monsters\":{}}"));
			}));

			t.Add(("ledger: stored totals are recomputed from the per-monster counts", () =>
			{
				string text = "{\"version\":1,\"totals\":{\"lifetime\":999,\"run\":999},\"monsters\":{\"a\":{\"lifetime\":4,\"run\":1},\"b\":{\"lifetime\":2,\"run\":0}}}";
				KillLedger l = KillLedger.Deserialize(text);
				Assert.Equal(6L, l.LifetimeTotal);
				Assert.Equal(1L, l.RunTotal);
			}));

			t.Add(("death guard: the same death is accepted once, old keys age out", () =>
			{
				var g = new DeathGuard(2);
				Assert.True(g.FirstTime("1:a"));
				Assert.True(!g.FirstTime("1:a"), "repeat");
				Assert.True(g.FirstTime("2:a"));
				Assert.True(g.FirstTime("3:a"));
				Assert.True(g.FirstTime("1:a"), "evicted keys are accepted again");
				g.Clear();
				Assert.True(g.FirstTime("3:a"), "cleared");
				Assert.True(g.FirstTime(null), "null key never blocks");
			}));
		}
	}
}

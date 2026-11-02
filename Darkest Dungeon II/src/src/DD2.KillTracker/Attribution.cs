using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker
{
	/// <summary>
	/// Works out which mod, DLC or the base game each monster definition came from. Fed by patches on the game's data
	/// loading (see <see cref="Patches"/>), read by the kill recorder and the UI.
	///
	/// How DD2 loads monsters (all decompiled from IronCrown.dll): a monster is an <c>ActorDataClass</c> element in a
	/// <c>*.Group.csv</c> file. <c>ResourceGroupCsvDatabase</c> reads the base Excel folder, then each owned DLC's folder,
	/// then - once mods are enabled - every loaded mod folder (<c>GatherModResources</c>), and
	/// <c>Library.AddLibraryElement</c> merges elements with the same id. We watch each folder as it is read and replay
	/// the same merge rules in <see cref="OriginTracker"/>.
	/// </summary>
	internal static class Attribution
	{
		/// <summary>Definition categories we attribute. A new kind of content is one more entry here.</summary>
		private static readonly HashSet<string> TrackedCategories = new HashSet<string>(StringComparer.Ordinal) { "ActorDataClass" };

		public const string MonsterCategory = "ActorDataClass";

		private static readonly OriginTracker Tracker = new OriginTracker();
		private static readonly OriginClassifier Classifier = new OriginClassifier();
		private static bool _baseSeeded;
		private static readonly HashSet<string> LoggedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Number of sightings stored; for the log line printed after loading.</summary>
		public static int SightingCount => Tracker.Count;

		/// <summary>Forget everything: the game is about to reload its data (mods unloaded, data cleared).</summary>
		public static void Reset()
		{
			Tracker.Reset();
			_baseSeeded = false;
			LoggedFolders.Clear();
		}

		private static void RefreshClassifier()
		{
			Classifier.SetMods(GameBridge.Mods());
			Classifier.SetDlcs(GameBridge.Dlcs());
			Classifier.SetBaseRoots(GameBridge.BaseRoots());
		}

		/// <summary>A <c>*.Group.csv</c> folder has just been read by the game.</summary>
		public static void OnGroupFolderRead(string folder, bool overrideFlag)
		{
			RefreshClassifier();
			SourceInfo origin = Classifier.Classify(folder);
			var found = new List<string>();
			GroupCsv.ScanFolder(folder, (type, name) =>
			{
				if (!TrackedCategories.Contains(type)) return;
				Tracker.Note(type, name, origin, overrideFlag);
				found.Add(name);
			});

			if (origin.IsMod)
			{
				LogModFolder(origin, folder, overrideFlag, found);
				// Mods load while the game is on a loading screen: the right moment for the (one-off) base-game backfill,
				// instead of a hitch on the first kill of the next fight.
				EnsureBaseKnown();
			}
		}

		/// <summary>One log line per mod folder, so a tester can see in the BepInEx log what was attributed to whom.</summary>
		private static void LogModFolder(SourceInfo origin, string folder, bool overrideFlag, List<string> ids)
		{
			if (ids.Count == 0 || !LoggedFolders.Add(folder + "|" + overrideFlag + "|" + ids.Count)) return;
			string sample = string.Join(", ", ids.Take(8)) + (ids.Count > 8 ? ", ..." : string.Empty);
			Cfg.Info("[attribution] mod '" + origin.Name + "' (id " + (origin.ModId ?? "local") + ") " + (overrideFlag ? "overrides" : "defines")
				+ " " + ids.Count + " monster definition(s) in " + folder + ": " + sample);
		}

		/// <summary>A folder's single-type files (<c>name.Type.csv</c>) have just been read by a text database.</summary>
		public static void OnSingleTypeFolderRead(string folder, IEnumerable<string> fileTypes, string extension, bool overrideFlag)
		{
			// Called once per text database per folder (hundreds of times per load): bail out cheaply unless this
			// database holds a category we attribute.
			var wanted = new List<string>();
			foreach (string fileType in fileTypes)
			{
				if (TrackedCategories.Contains(fileType)) wanted.Add(fileType);
			}
			if (wanted.Count == 0) return;

			RefreshClassifier();
			SourceInfo origin = Classifier.Classify(folder);
			foreach (string fileType in wanted)
			{
				GroupCsv.ScanSingleTypeFiles(folder, fileType, extension, (type, name) => Tracker.Note(type, name, origin, overrideFlag));
			}
			if (origin.IsMod) EnsureBaseKnown();
		}

		/// <summary>Who owns this monster id? Unknown when the loader never reported it.</summary>
		public static SourceInfo Resolve(string monsterId)
		{
			EnsureBaseKnown();
			return Tracker.Resolve(MonsterCategory, monsterId) ?? SourceInfo.Unknown;
		}

		/// <summary>
		/// If the game loaded its base data before our patches were in place (or the tracker was reset afterwards) we
		/// have no base-game sightings, which would make a mod's override of a vanilla id look like a brand new
		/// monster. Backfill by reading the same base folders ourselves, placed before everything already seen.
		/// </summary>
		private static void EnsureBaseKnown()
		{
			if (_baseSeeded || Tracker.BaseCount > 0) return;
			_baseSeeded = true;

			Guard.Run("base-game backfill", () =>
			{
				RefreshClassifier();
				var seeds = new List<(string folder, bool overrideFlag)>();
				foreach (string root in GameBridge.BaseRoots())
				{
					seeds.Add((root, false));
					foreach (DlcEntry dlc in GameBridge.Dlcs()) seeds.Add((Path.Combine(root, dlc.FolderName), false));
					foreach (string gameType in GameBridge.GameTypeFolderNames()) seeds.Add((Path.Combine(root, gameType), true));
				}

				// NoteFirst puts each sighting in front, so feed them back to front to end up in load order.
				for (int i = seeds.Count - 1; i >= 0; i--)
				{
					(string folder, bool overrideFlag) = seeds[i];
					SourceInfo origin = Classifier.Classify(folder);
					GroupCsv.ScanFolder(folder, (type, name) =>
					{
						if (TrackedCategories.Contains(type)) Tracker.NoteFirst(type, name, origin, overrideFlag);
					});
				}
				Cfg.Info("Backfilled base-game monster definitions (" + Tracker.BaseCount + " sightings).");
			});
		}
	}
}

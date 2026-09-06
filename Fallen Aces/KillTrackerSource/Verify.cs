using System;
using FallenAcesKillTracker;

internal static class Verify
{
    private static int failures;

    private static void Equal(int expected, int actual, string message)
    {
        if (expected != actual)
        {
            Console.Error.WriteLine("FAIL: " + message + " (expected " + expected + ", got " + actual + ")");
            failures++;
        }
    }

    private static void Main()
    {
        var tracker = new KillTrackerState();

        tracker.MarkUnconscious(1, "Pipe Guy");
        tracker.MarkUnconscious(1, "Pipe Guy");
        Equal(0, tracker.Killed, "knockout does not count as a kill");
        Equal(1, tracker.Unconscious, "duplicate knockout is ignored");

        tracker.MarkKilled(1, "Pipe Guy");
        Equal(1, tracker.Killed, "killing an unconscious actor adds one kill");
        Equal(0, tracker.Unconscious, "killing an unconscious actor transfers the count");
        Equal(1, tracker.Categories["Pipe Guy"].Killed, "category kill was transferred");
        Equal(0, tracker.Categories["Pipe Guy"].Unconscious, "category knockout was removed");

        tracker.MarkKilled(1, "Pipe Guy");
        Equal(1, tracker.Killed, "duplicate death is ignored");

        tracker.MarkKilled(2, "Pistol Guy");
        tracker.MarkUnconscious(3, "Pistol Guy");
        Equal(2, tracker.Killed, "direct kill increments total");
        Equal(1, tracker.Unconscious, "second category knockout increments total");
        Equal(1, tracker.Categories["Pistol Guy"].Killed, "direct category kill tracked");
        Equal(1, tracker.Categories["Pistol Guy"].Unconscious, "direct category knockout tracked");

        tracker.MarkAwake(3);
        Equal(0, tracker.Unconscious, "waking removes unconscious count");
        Equal(1, tracker.Categories["Pistol Guy"].Killed, "waking preserves other category counts");

        tracker.MarkAwake(2);
        Equal(1, tracker.Killed, "revival removes a kill when the game reports one");

        tracker.Reset();
        Equal(0, tracker.Killed, "reset clears killed total");
        Equal(0, tracker.Unconscious, "reset clears unconscious total");
        Equal(0, tracker.Categories.Count, "reset clears category rows");

        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KillTrackerVerify-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            string path = System.IO.Path.Combine(directory, "counts.xml");
            var store = new KillTrackerStore(path);
            store.Load(tracker);
            tracker.Changed += delegate { store.Save(tracker); };
            tracker.MarkKilled(10, "A & B <boss> ü");
            tracker.MarkUnconscious(11, "Guard");
            tracker.BeginWorld();
            Equal(1, tracker.Killed, "world change preserves kills");
            Equal(1, tracker.Unconscious, "world change preserves knockouts");
            Equal(0, tracker.ActorCount, "world change discards transient instance IDs");
            tracker.MarkKilled(10, "Guard");
            Equal(2, tracker.Killed, "reused ID in a new world is a new actor");

            var restored = new KillTrackerState();
            var restartedStore = new KillTrackerStore(path);
            restartedStore.Load(restored);
            Equal(2, restored.Killed, "restart restores kills from disk");
            Equal(1, restored.Unconscious, "restart restores knockouts from disk");
            Equal(1, restored.Categories["A & B <boss> ü"].Killed, "category text round trips");
            restored.Changed += delegate { restartedStore.Save(restored); };
            restored.MarkUnconscious(20, "Guard");
            restored.MarkKilled(20, "Guard");
            restored.MarkKilled(20, "Guard");
            var check = new KillTrackerState();
            new KillTrackerStore(path).Load(check);
            Equal(3, check.Killed, "post-restart transfer and duplicate suppression saved");
            Equal(1, check.Unconscious, "post-restart transfer preserves historical totals");

            System.IO.File.WriteAllText(path, "broken");
            var recovery = new KillTrackerStore(path);
            recovery.Load(check);
            Equal(2, check.Killed, "corrupt primary recovers previous complete save");
            recovery.Save(check);
            new KillTrackerStore(path).Load(new KillTrackerState());
            System.IO.File.WriteAllText(path, "broken");
            System.IO.File.WriteAllText(path + ".bak", "broken backup");
            var protectedStore = new KillTrackerStore(path);
            bool loadFailed = false, saveFailed = false;
            try { protectedStore.Load(check); } catch { loadFailed = true; }
            try { protectedStore.Save(check); } catch (System.IO.IOException) { saveFailed = true; }
            Equal(1, loadFailed && saveFailed ? 1 : 0, "unreadable data is protected from overwrite");
            Equal(1, System.IO.File.ReadAllText(path) == "broken" ? 1 : 0, "failed load leaves original intact");
        }
        finally { System.IO.Directory.Delete(directory, true); }

        if (failures != 0)
        {
            Environment.Exit(1);
        }
        Console.WriteLine("PASS: totals, per-type counts, transfer, duplicate suppression, waking, reset, world transitions, disk persistence and corruption recovery.");
    }
}

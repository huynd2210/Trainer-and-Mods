using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KillTracker;
using birdseye;
using birdseye.Regiments;
using birdseye.Regiments.Commanders;
using birdseye.Regiments.Dismounts;
using birdseye.Regiments.Platoons;
using birdseye.Regiments.Units;

/// <summary>
/// Runs the plugin against the real game assembly outside the game:
///  1. Harmony applies every patch to MainAssembly.dll (catches wrong names/overloads, which
///     compile fine but fail at load).
///  2. A scripted battle is pushed through the game's own event dispatch
///     (PlatoonEventsController.OnUnitRemoved / OnBoidDied / OnPlatoonRemoved), using
///     uninitialised game objects, and the .txt the plugin writes is checked line by line.
///
/// Usage: VerifyKillTracker.exe <ManagedDir> <BepInExCoreDir> <OutDir>
/// </summary>
internal static class Program
{
    private static int failures;
    private static string[] probeDirs;

    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: VerifyKillTracker <ManagedDir> <BepInExCoreDir> <OutDir>");
            return 2;
        }
        probeDirs = new[] { Path.GetFullPath(args[0]), Path.GetFullPath(args[1]), AppDomain.CurrentDomain.BaseDirectory, Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "out")) };
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Run(Path.GetFullPath(args[2]));
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name + ".dll";
        foreach (string dir in probeDirs)
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }
        }
        return null;
    }

    // Kept out of Main so no game type is touched before the resolver is installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string outDir)
    {
        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, true);
        }
        Directory.CreateDirectory(outDir);
        var cfg = new ConfigFile(Path.Combine(outDir, "test.cfg"), true);
        KillTrackerPlugin.Enabled = cfg.Bind("General", "Enabled", true, "");
        KillTrackerPlugin.Folder = cfg.Bind("General", "Folder", Path.Combine(outDir, "logs"), "");
        KillTrackerPlugin.Log = new ManualLogSource("KillTracker");
        var errors = new ErrorCounter();
        KillTrackerPlugin.Log.LogEvent += errors.LogEvent;

        Console.WriteLine("== Harmony patches against MainAssembly ==");
        var harmony = new Harmony("verify.killtracker");
        harmony.PatchAll(typeof(KillTrackerPlugin).Assembly);
        var patched = harmony.GetPatchedMethods().ToList();
        foreach (MethodBase m in patched)
        {
            Patches info = Harmony.GetPatchInfo(m);
            Console.WriteLine("  " + m.DeclaringType.Name + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")"
                + "  prefix " + info.Prefixes.Count + " postfix " + info.Postfixes.Count + " finalizer " + info.Finalizers.Count);
        }
        Check(patched.Count == 6, "6 methods patched (got " + patched.Count + ")");
        Check(patched.Count(m => m.Name == "ApplyCasualty") == 2, "both ApplyCasualty overloads patched");
        Check(patched.Where(m => m.Name == "ApplyCasualty" || m.Name == "KillUnit").All(m => Harmony.GetPatchInfo(m).Finalizers.Count == 1), "attribution patches have finalizers");

        // A throwing ApplyCasualty (an uninitialised PlatoonBoidDamage has no squads) must still
        // pop its shooter, or every later soldier death would be credited to it.
        MethodInfo areaCasualty = AccessTools.Method(typeof(PlatoonBoidDamage), "ApplyCasualty", new[] { typeof(Commander), typeof(PlatoonCore) });
        var shooterPlt = Fake<PlatoonCore>();
        bool threw = false;
        try
        {
            areaCasualty.Invoke(Fake<PlatoonBoidDamage>(), new object[] { Fake<Commander>(), shooterPlt });
        }
        catch (TargetInvocationException)
        {
            threw = true;
        }
        Check(threw, "uninitialised ApplyCasualty throws (precondition)");
        Check(ReferenceEquals(Attribution.Current.killer, null), "shooter popped after ApplyCasualty threw");

        Console.WriteLine("\n== Scripted battle ==");
        Battle();
        Check(errors.count == 0, "plugin logged no errors (" + errors.count + ")");

        string[] files = Directory.GetFiles(Path.Combine(outDir, "logs"), "*.txt");
        Check(files.Length == 1, "one battle file written (" + files.Length + ")");
        if (files.Length != 1)
        {
            return;
        }
        string text = File.ReadAllText(files[0]);
        Console.WriteLine("\n----- " + Path.GetFileName(files[0]) + " -----");
        Console.WriteLine(text);
        Console.WriteLine("-----");
        string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Func<string, bool> has = s => lines.Any(l => l.Contains(s));
        Func<string, int> countOf = s => lines.Count(l => l.Contains(s));

        Check(has("KILL unit [A1] destroyed unit [E1]"), "vehicle kill line");
        Check(has("KILL unit [A2] killed 3 soldiers of unit [E2]"), "3 soldier kills in a burst merge into one line");
        Check(has("KILL unit [A2] killed 1 soldier of unit [E2]"), "a kill after the quiet gap starts a new line");
        Check(has("LOSS 2 soldiers of unit [A3] killed by unit [E3]"), "own soldier losses with killer");
        Check(has("LOSS 1 soldier of unit [A3] killed (cause unknown)"), "unattributed own soldier loss");
        Check(has("FF   unit [A1] destroyed allied unit [L1]"), "friendly fire on an ally");
        Check(has("LOSS unit [A4] destroyed by unit [A1] (friendly fire)"), "own unit killed by own fire is a loss");
        Check(has("KILL unit [A1] destroyed unit [H1]"), "helicopter kill");
        Check(has("KILL Tac strike destroyed unit [E4]"), "tac strike credited to you");
        Check(has("KILL unit [A1] WIPED OUT unit [E1]"), "enemy platoon wiped out");
        Check(has("LOSS unit [A4] WIPED OUT by unit [E3]"), "own platoon wiped out");
        Check(!has("[L2]") && !has("[E9]"), "allied losses and allied kills left out");
        Check(!has("[T1]"), "infantry-only squad 'unit' death not double counted");
        Check(!has("[S1]"), "slave platoon wipe not reported");
        Check(countOf("[A5]") == 0, "undeploy and service removals ignored");
        Check(has("Result:  Victory (Decisive)"), "result line");
        Check(has("YOUR KILLS:   2 vehicles, 1 helicopter, 4 soldiers  |  1 platoon wiped out"), "kill totals");
        Check(has("YOUR LOSSES:  1 vehicle, 3 soldiers  |  1 platoon wiped out"), "loss totals");
        Check(has("Friendly fire on allies: 1"), "friendly fire count");
        Check(has("unit [A2]") && lines.Any(l => l.Trim().StartsWith("unit [A2]") && l.Contains("4 soldiers")), "by-platoon kill breakdown");
        Check(!has("after the end"), "events after End are not written");
        int kill = Array.FindIndex(lines, l => l.Contains("destroyed unit [E1]"));
        int burst = Array.FindIndex(lines, l => l.Contains("killed 3 soldiers"));
        Check(kill >= 0 && burst > kill, "lines stay in the order they happened");
    }

    private static void Battle()
    {
        float now = 0f;
        BattleLog.Clock = () => now;
        PlatoonEvents.UnitRemoved = new EventList<Action<UnitCore, UnitDeathEventData>>(8);
        PlatoonEvents.BoidRemoved = new EventList<Action<PlatoonCore, BoidCore, bool>>(8);
        PlatoonEvents.PlatoonRemoved = new EventList<Action<PlatoonCore, PlatoonDeathEventData>>(8);

        var me = Fake<PlayerCommander>();
        me.side = eSides.Blue;
        var ally = Fake<Commander>();
        ally.side = eSides.Blue;
        var enemy = Fake<Commander>();
        enemy.side = eSides.Red;
        GameQueries.activePlayer = me;

        PlatoonCore a1 = Plt("A1", me), a2 = Plt("A2", me), a3 = Plt("A3", me), a4 = Plt("A4", me), a5 = Plt("A5", me);
        PlatoonCore e1 = Plt("E1", enemy), e2 = Plt("E2", enemy), e3 = Plt("E3", enemy), e4 = Plt("E4", enemy), e9 = Plt("E9", enemy);
        PlatoonCore h1 = Plt("H1", enemy, eDisplayType.AtkHelo), t1 = Plt("T1", enemy, eDisplayType.TruckInf);
        PlatoonCore l1 = Plt("L1", ally), l2 = Plt("L2", ally), s1 = Plt("S1", enemy);
        s1.isSlave = true;

        BattleTracker.Begin();
        Check(PlatoonEvents.UnitRemoved.occupied == 1 && PlatoonEvents.BoidRemoved.occupied == 1 && PlatoonEvents.PlatoonRemoved.occupied == 1, "subscribed to the three platoon events");

        UnitDies(e1, a1, me);                       // t=0 my tank kills an enemy tank
        now = 1; SoldierDies(e2, a2, me);           // burst of three
        now = 2; SoldierDies(e2, a2, me);
        now = 3; SoldierDies(e2, a2, me);
        now = 4; SoldierDies(a3, e3, enemy);        // I lose two soldiers
        now = 5; SoldierDies(a3, e3, enemy);
        now = 6; SoldierDies(a3, null, null);       // and one to an unknown cause
        now = 7; UnitDies(l1, a1, me);              // I hit an ally
        UnitDies(a4, a1, me);                       // I hit myself
        UnitDies(h1, a1, me);                       // helicopter
        UnitDies(e4, null, me);                     // off-map tac strike
        UnitDies(l2, e9, enemy);                    // ally's loss: not mine
        UnitDies(e9, l2, ally);                     // ally's kill: not mine
        UnitDies(t1, a1, me);                       // truck infantry squad: soldiers already counted
        Controller(a5).OnUnitRemoved(Unit(a5));     // undeployed: no death data
        Controller(a5).OnBoidDied(Fake<BoidCore>(), true); // mounting up
        Wipe(e1, a1, me, ePltEOLState.Destroyed);
        Wipe(a4, e3, enemy, ePltEOLState.Destroyed);
        Wipe(s1, a1, me, ePltEOLState.Destroyed);   // company member
        Wipe(a5, null, null, ePltEOLState.Retreated);
        BattleTracker.Tick();
        now = 20; BattleTracker.Tick();             // quiet gap: everything so far goes out
        SoldierDies(e2, a2, me);                    // same shooter/target, new line
        now = 40; BattleTracker.Tick();

        BattleTracker.Finish(eSides.Blue, eGameResult.Decisive, eSessionEndType.ProperFinish);
        BattleTracker.End();
        Check(PlatoonEvents.UnitRemoved.occupied == 0 && PlatoonEvents.BoidRemoved.occupied == 0 && PlatoonEvents.PlatoonRemoved.occupied == 0, "unsubscribed after the battle");
        e1.callsign = "after the end";
        UnitDies(e1, a1, me);
    }

    private static PlatoonCore Plt(string callsign, Commander cmdr, eDisplayType type = default(eDisplayType))
    {
        var p = Fake<PlatoonCore>();
        p.callsign = callsign;
        p.commander = cmdr;
        p.parameters = Fake<PlatoonParameters>();
        p.parameters.displayType = type;
        p.events = Controller(p);
        return p;
    }

    private static PlatoonEventsController Controller(PlatoonCore p)
    {
        if (ReferenceEquals(p.events, null))
        {
            p.events = Fake<PlatoonEventsController>();
            p.events.platoon = p;
        }
        return p.events;
    }

    private static UnitCore Unit(PlatoonCore p)
    {
        var u = Fake<UnitCore>();
        u.platoon = p;
        return u;
    }

    private static void UnitDies(PlatoonCore victim, PlatoonCore killer, Commander cmdr)
    {
        UnitCore u = Unit(victim);
        Controller(victim).OnUnitRemoved(u, new UnitDeathEventData(u, killer, cmdr));
    }

    // What ApplyCasualty does around a soldier's death: the patch holds the shooter while the
    // game raises the event.
    private static void SoldierDies(PlatoonCore victim, PlatoonCore killer, Commander cmdr)
    {
        if (!ReferenceEquals(cmdr, null) || !ReferenceEquals(killer, null))
        {
            Attribution.Push(killer, cmdr);
        }
        Controller(victim).OnBoidDied(Fake<BoidCore>(), false);
        if (!ReferenceEquals(cmdr, null) || !ReferenceEquals(killer, null))
        {
            Attribution.Pop();
        }
    }

    private static void Wipe(PlatoonCore victim, PlatoonCore killer, Commander cmdr, ePltEOLState state)
    {
        var pded = state == ePltEOLState.Destroyed ? new PlatoonDeathEventData(victim, killer, cmdr) : new PlatoonDeathEventData(state, victim);
        Controller(victim).OnPlatoonRemoved(pded);
    }

    private static T Fake<T>()
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    private sealed class ErrorCounter : ILogListener
    {
        public int count;

        public void LogEvent(object sender, LogEventArgs e)
        {
            if (e.Level <= LogLevel.Error)
            {
                count++;
                Console.WriteLine("  [plugin " + e.Level + "] " + e.Data);
            }
        }

        public void Dispose()
        {
        }
    }
}

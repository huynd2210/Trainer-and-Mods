using SwampKeeperCoop.Protocol;

namespace SwampKeeperCoop;

// An acceptance gate runs only under -coop-integration-test in the sandbox copies, never in normal play.
// Each gate owns its host checks, its guest checks and the guest's scripted input; the driver only iterates.
internal abstract class Gate
{
    internal bool Done {get;set;}
    // Host walks into stage 0 before cloning; preparation stays in the preparation area.
    internal virtual bool StartsRun=>true;
    // Basic movement/attack/screenshot checks share the first 11 seconds with the gate.
    internal virtual bool RequiresBasics=>true;
    internal virtual bool GuestMoves=>true;
    internal virtual float HostLimit=>100;
    internal virtual float GuestFinish=>45;
    internal virtual float GuestLimit=>95;
    internal virtual void Host(float elapsed){}
    internal virtual void Guest(float sessionSeconds,WorldState? world){}
    // Extra scripted guest keys, keyed by authoritative session time so both peers agree.
    internal virtual uint GuestKeys(float sessionSeconds)=>0;
    internal virtual UnityEngine.Vector2 GuestMove(float sessionSeconds)=>UnityEngine.Vector2.zero;
    internal virtual void GuestFinished(WorldState? last){}
    // Called every frame while the link is down and the driver is recovering it.
    internal virtual void Disconnected(float elapsed){}
}

internal static class Gates
{
    private static readonly Dictionary<string,Func<Gate>> registry=new()
    {
        ["-coop-gameplay-test"]=()=>new GameplayChecks(),
        ["-coop-death-test"]=()=>new DeathChecks(),
        ["-coop-preparation-test"]=()=>new PreparationChecks(),
        ["-coop-map-test"]=()=>new MapChecks(),
        ["-coop-paths-test"]=()=>new PathChecks(),
        ["-coop-well-test"]=()=>new WellChecks(),
        ["-coop-boss-test"]=()=>new BossChecks(),
        ["-coop-effects-test"]=()=>new EffectChecks(),
        ["-coop-network-test"]=()=>new NetworkChecks(),
        ["-coop-menu-test"]=()=>new MenuChecks(),
    };
    internal static readonly List<Gate> Active=new();
    internal static void Load(string[] args){foreach(var (flag,make) in registry)if(args.Contains(flag))Active.Add(make());}
    internal static T? Get<T>() where T:Gate=>Active.OfType<T>().FirstOrDefault();
    internal static bool StartsRun=>Active.All(g=>g.StartsRun);
    internal static bool RequiresBasics=>Active.Count==0 || Active.Any(g=>g.RequiresBasics);
    internal static bool GuestMoves=>Active.All(g=>g.GuestMoves);
    internal static bool AllDone=>Active.All(g=>g.Done);
    internal static float HostLimit=>Active.Count==0?30:Active.Max(g=>g.HostLimit);
    internal static float GuestFinish=>Active.Count==0?14:Active.Max(g=>g.GuestFinish);
    internal static float GuestLimit=>Active.Count==0?25:Active.Max(g=>g.GuestLimit);
    internal static UnityEngine.Vector2 GuestMove(float t){var v=UnityEngine.Vector2.zero;foreach(var g in Active)v+=g.GuestMove(t);return v;}
    internal static uint GuestKeys(float t){uint keys=0;foreach(var g in Active)keys|=g.GuestKeys(t);return keys;}
}

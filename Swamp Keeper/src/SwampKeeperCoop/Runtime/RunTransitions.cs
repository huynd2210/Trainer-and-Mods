using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SwampKeeperCoop;

internal static class RunTransitions
{
    private static GameObject? blocker;
    internal static bool Moving {get;private set;}
    internal static int Generation {get;private set;}=1;
    internal static Player? Leader {get;private set;}
    // Where both actors live between scripted moves. The leader's parent at the end of a transition can be a
    // temporary carrier (the merchant's cart) that is switched off later, taking anything parented to it along.
    internal static Transform? Home;
    // A spot beside the leader that a player could actually stand on. Dropping an actor inside scenery strands it and
    // makes some boss AI spin forever (the Fox's dash-target search never finds a spot around such a target).
    internal static Vector3 Beside(Player leader)
    {
        var at=leader.transform.position;
        foreach(var offset in new[]{new Vector3(1.3f,0,0),new Vector3(-1.3f,0,0),new Vector3(0,-1f,0),new Vector3(0,1f,0)})
            if(UnityEngine.AI.NavMesh.SamplePosition(at+offset,out var hit,.4f,UnityEngine.AI.NavMesh.AllAreas))
                return new Vector3(hit.position.x,hit.position.y,at.z);
        return at;
    }
    private static PlayerState lastHostState=PlayerState.Idle;
    // Native scripted moves (cart ride, ladder climb, landing) can carry the host on after the transition hook ends.
    // When the host regains control far from the guest, bring the guest along, as the transition itself does.
    internal static void Regroup()
    {
        var host=Plugin.Host;var remote=Plugin.Remote;
        if(Plugin.Guest || !host || !remote || Moving)return;
        var state=host!.state;bool scripted=(int)lastHostState<0;lastHostState=state;
        if(!scripted || (int)state<0 || TeamLife.Dead(host) || TeamLife.Dead(remote))return;
        if(!remote!.gameObject.activeInHierarchy || remote.transform.parent!=Home)remote.transform.SetParent(Home,true);
        if(Vector3.Distance(host.transform.position,remote.transform.position)<=4)return;
        remote.transform.position=Beside(host);
        if(remote.po && remote.po.rg){remote.po.rg.position=remote.transform.position;remote.po.rg.velocity=Vector2.zero;}
        Plugin.Record("Guest regrouped with the host after a native scripted move ("+state+")");
    }
    internal static void Begin()
    {
        // A second native Move started mid-transition (e.g. a double-confirmed popup) must not stack another control lock.
        if(Plugin.Guest || !Plugin.Remote || Moving)return;
        if(!GameManager.gm.current)Plugin.Driver?.ApplyPreparedLoadouts();
        if(!blocker){blocker=new GameObject("CoopTransitionLock");UnityEngine.Object.DontDestroyOnLoad(blocker);}
        Leader=!TeamLife.Dead(Plugin.Host)?Plugin.Host:Plugin.Remote;
        Moving=true;Plugin.RemoteInput.Clear();Plugin.Remote!.StopControl(blocker!);Plugin.Remote.StopAiming(blocker!);
        Plugin.Record("Shared transition started; host state="+Plugin.Host!.state);
    }
    internal static void Complete()
    {
        if(!Moving || !Plugin.Remote || !Plugin.Host)return;
        var remote=Plugin.Remote!;var host=Plugin.Host!;
        var leader=Leader?Leader!:host;var companion=leader.Pointer==host.Pointer?remote:host;
        companion.transform.SetParent(Home,true);
        companion.transform.position=Beside(leader);
        TeamLife.Revive(host);TeamLife.Revive(remote);
        remote.ResetArrows();remote.ChangeState(PlayerState.Idle);
        remote.StartControl(blocker!);remote.StartAiming(blocker!);
        Generation++;Moving=false;
        Plugin.Record("Transition parents: leader="+(leader.transform.parent?AssetKey.Path(leader.transform.parent):"<root>")+" leaderState="+leader.state);
        Plugin.Record("Shared stage transition completed: generation="+Generation+" stage="+(GameManager.gm.current?GameManager.gm.current.stageIndex:-1)+" guest inventory="+remote.items.Count);
    }
}

[HarmonyPatch]
internal class TransitionIterator
{
    private static int lastState=-999;
    static IEnumerable<MethodBase> TargetMethods()=>typeof(GameManager).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
        .Where(t=>t.Name.StartsWith("_Move_") || t.Name.StartsWith("_Well_") || t.Name.StartsWith("_Tab_"))
        .Select(t=>t.GetMethod("MoveNext",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)).Where(m=>m!=null).Cast<MethodBase>();
    static bool Prefix(object __instance,out PlayerScope.State? __state,ref bool __result)
    {
        __state=null;
        var state=__instance.GetType().GetProperty("__1__state")?.GetValue(__instance);
        if(state is int restartState && restartState==0 && Plugin.Driver?.Restarting==true){__result=true;return false;}
        if(state is int first && first==0 && !PreparationGate.Allowed()){__result=true;return false;}
        if(state is int n && n==0)RunTransitions.Begin();
        __state=RunTransitions.Moving?Owners.Enter(RunTransitions.Leader):null;
        return true;
    }
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
    static void Postfix(object __instance,bool __result)
    {
        if(Plugin.Test && RunTransitions.Moving)
        {
            var state=__instance.GetType().GetProperty("__1__state")?.GetValue(__instance);
            if(state is int n && n!=lastState){lastState=n;Plugin.Record("Native transition iterator state="+n+" result="+__result);}
            if(state is int waiting && waiting==5)
            {
                // This native pass-area step waits for arrival at its NPC, not a timer.
                var npc=__instance.GetType().GetProperty("_npc_5__4")?.GetValue(__instance) as NPC;
                if(npc)Plugin.Host!.transform.position=npc!.transform.position;
            }
        }
        if(!__result)RunTransitions.Complete();
    }
}

[HarmonyPatch]
internal class PreparationGate
{
    static IEnumerable<MethodBase> TargetMethods()=>new[]{(MethodBase)AccessTools.Method(typeof(GameManager),nameof(GameManager.MoveStage)),AccessTools.Method(typeof(GameManager),nameof(GameManager.MoveWell))};
    internal static bool Allowed()
    {
        if(Plugin.Guest || Plugin.Driver?.Ready!=true || GameManager.gm.current)return true;
        if(Plugin.Driver.HostReady && Plugin.Driver.GuestReady)return true;
        Plugin.Status="Both players must ready up before starting. Open F7 to prepare.";SessionMenu.Instance?.Show();return false;
    }
    static bool Prefix()=>Allowed();
}

[HarmonyPatch(typeof(UI_Title),nameof(UI_Title.StartGame))]
internal class CoordinatedStart
{
    static bool Prefix()=>Plugin.Driver==null || Plugin.Driver.StartingGame;
}

[HarmonyPatch(typeof(UI_End),nameof(UI_End.Restart))]
internal class CoordinatedRestart
{
    static void Prefix(bool quick)=>Plugin.Driver?.BeginRestart(quick);
}
[HarmonyPatch(typeof(UI_Pause.__c__DisplayClass10_0),nameof(UI_Pause.__c__DisplayClass10_0._Restart_b__0))]
internal class CoordinatedPauseRestart
{
    static void Prefix(UI_Pause.__c__DisplayClass10_0 __instance)=>Plugin.Driver?.BeginRestart(__instance.quick);
}

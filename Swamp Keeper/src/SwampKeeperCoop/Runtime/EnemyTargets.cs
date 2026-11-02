using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SwampKeeperCoop;

internal static class EnemyTargets
{
    internal static IEnumerable<Type> Types=>typeof(Enemy).Assembly.GetTypes().Where(t=>typeof(Enemy).IsAssignableFrom(t));
    private static readonly Dictionary<IntPtr,(int Frame,Player? Player)> targets=new();
    internal static Player? For(Enemy enemy)
    {
        if(enemy.state==EnemyState.Die && Plugin.EnemyLastHit.TryGetValue(enemy.Pointer,out var killer) && killer)return killer;
        if(targets.TryGetValue(enemy.Pointer,out var cached) && cached.Frame==Time.frameCount && cached.Player)return cached.Player;
        var pos=enemy.transform.position;Player? target=null;float distance=float.MaxValue;
        foreach(var player in new[]{Plugin.Host,Plugin.Remote})
        {
            if(!player || player!.state==PlayerState.Die)continue;
            float d=(player.transform.position-pos).sqrMagnitude;
            if(d<distance){distance=d;target=player;}
        }
        targets[enemy.Pointer]=(Time.frameCount,target);return target;
    }
}

[HarmonyPatch]
internal class EnemyTargetScope
{
    private static readonly HashSet<string> callbacks=new(){"Start","OnEnable","LateUpdate","Update","Idle","Run","DoAttack","Attack","GetOnAttackRange","GetRunTargetGap","FSMMain","Die"};
    static IEnumerable<MethodBase> TargetMethods()=>EnemyTargets.Types.SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
        .Where(m=>callbacks.Contains(m.Name)&&Owners.Patchable(m)).Cast<MethodBase>();
    static void Prefix(Enemy __instance,out PlayerScope.State? __state)=>__state=Owners.Enter(EnemyTargets.For(__instance));
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}

[HarmonyPatch]
internal class EnemyCoroutineTargetScope
{
    static IEnumerable<MethodBase> TargetMethods()=>EnemyTargets.Types.SelectMany(t=>t.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
        .Where(t=>typeof(Enemy).IsAssignableFrom(t.GetProperty("__4__this")?.PropertyType))
        .Select(t=>t.GetMethod("MoveNext",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)).Where(m=>m!=null).Cast<MethodBase>();
    static void Prefix(object __instance,out PlayerScope.State? __state)
    {
        var enemy=__instance.GetType().GetProperty("__4__this")?.GetValue(__instance) as Enemy;
        __state=enemy?Owners.Enter(EnemyTargets.For(enemy!)):null;
    }
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}

[HarmonyPatch(typeof(Enemy),nameof(Enemy.OnEnable))]
internal class ResetPooledCredit
{
    static void Prefix(Enemy __instance)=>Plugin.EnemyLastHit.Remove(__instance.Pointer);
}

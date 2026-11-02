using HarmonyLib;
using System.Reflection;
using UnityEngine;
namespace SwampKeeperCoop;
[HarmonyPatch]
internal class PlayerScope
{
    internal sealed class State {internal Player? Player;internal bool Remote;internal CustomKeyManager? Input;internal ControllerType Controller;}
    static IEnumerable<MethodBase> TargetMethods()=>typeof(Player).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly)
        .Where(m=>!m.IsSpecialName && m.Name!="Finalize" && Owners.Patchable(m)).Cast<MethodBase>();
    static void Prefix(Player __instance,MethodBase __originalMethod,out State? __state)
    {
        __state=null;
        // The real player's lifecycle owns the global singleton itself.
        if(!Plugin.IsRemote(__instance) && (__originalMethod.Name==nameof(Player.Awake) || __originalMethod.Name==nameof(Player.OnDestroy)))return;
        __state=Owners.Enter(__instance);
        if(Plugin.IsRemote(__instance) && __originalMethod.Name==nameof(Player.Start))
        {
            if(__instance.dodge.dodgeBars==null)__instance.dodge.Awake();
            foreach(var pool in __instance.GetComponentsInChildren<ObjectPool>(true))
                if(pool.activePool==null || pool.deactivePool==null)pool.Awake();
            var dodgePool=__instance.dodge.dodgeBarPool;
            if(dodgePool.activePool==null || dodgePool.deactivePool==null)dodgePool.Awake();
        }
    }
    static void Finalizer(State? __state)
    {
        Owners.Exit(__state);
    }
}

[HarmonyPatch]
internal class CoroutineScope
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(Player).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
        .Where(t=>t.GetProperty("__4__this")?.PropertyType==typeof(Player))
        .Select(t=>(MethodBase?)AccessTools.Method(t,"MoveNext")).Where(m=>m!=null).Cast<MethodBase>();
    static void Prefix(object __instance,out PlayerScope.State? __state)
    {
        __state=null;
        var owner=__instance.GetType().GetProperty("__4__this")?.GetValue(__instance) as Player;
        if(owner==null)return;
        __state=Owners.Enter(owner);
    }
    static void Finalizer(PlayerScope.State? __state)
    {
        Owners.Exit(__state);
    }
}

[HarmonyPatch(typeof(Player),nameof(Player.ShootArrow))]
internal class CaptureProjectileOwner
{
    static void Postfix(Player __instance,Arrow_Player __result)
    {
        if(__result)Plugin.ProjectileOwners[__result.Pointer]=__instance;
    }
}
[HarmonyPatch]
internal class ProjectileScope
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(Arrow_Player).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly)
        .Where(m=>!m.IsSpecialName && m.Name!="Finalize" && !m.ContainsGenericParameters)
        .Cast<MethodBase>().Concat(new[]{(MethodBase)AccessTools.Method(typeof(Projectile),nameof(Projectile.OnTriggerEnter2D)),AccessTools.Method(typeof(Projectile),nameof(Projectile.Update))});
    static void Prefix(Projectile __instance,out PlayerScope.State? __state)
    {
        __state=null;
        // Arrows fired by items/skills straight from a pool never pass Player.ShootArrow; the spawn registry knows their owner.
        if(!Plugin.ProjectileOwners.TryGetValue(__instance.Pointer,out var owner)||!owner)owner=Owners.Find(__instance);
        if(!owner)return;
        __state=Owners.Enter(owner);
    }
    static void Finalizer(PlayerScope.State? __state)
    {
        Owners.Exit(__state);
    }
}
[HarmonyPatch]
internal class ProjectileCoroutineScope
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(Arrow_Player).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
        .Where(t=>t.GetProperty("__4__this")?.PropertyType==typeof(Arrow_Player))
        .Select(t=>(MethodBase?)AccessTools.Method(t,"MoveNext")).Where(m=>m!=null).Cast<MethodBase>();
    static void Prefix(object __instance,out PlayerScope.State? __state)
    {
        __state=null;
        var arrow=__instance.GetType().GetProperty("__4__this")?.GetValue(__instance) as Arrow_Player;
        if(arrow==null)return;
        if(!Plugin.ProjectileOwners.TryGetValue(arrow.Pointer,out var owner)||!owner)owner=Owners.Find(arrow);
        if(!owner)return;
        __state=Owners.Enter(owner);
    }
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}
[HarmonyPatch(typeof(Enemy),nameof(Enemy.ApplyDamage))]
internal class DamageCredit
{
    // Integration tests only: dealt hits per actor.
    internal static readonly Dictionary<IntPtr,int> Hits=new();private static int unscoped;
    internal static int HitsBy(Player? p){return p && Hits.TryGetValue(p!.Pointer,out int n)?n:0;}
    static void Postfix(Enemy __instance,AttackResult __result)
    {
        if(__result!=AttackResult.Dealed || !Player.player)return;
        Plugin.EnemyLastHit[__instance.Pointer]=Player.player;
        if(Plugin.Test)
        {
            Hits.TryGetValue(Player.player.Pointer,out int n);Hits[Player.player.Pointer]=n+1;
            // Diagnostics: damage that no ownership scope claimed falls back to the host singleton.
            if(Owners.Depth==0 && unscoped++<12)Plugin.Record("Unscoped hit credited to "+TestActions.Who(Player.player)+" on "+__instance.GetIl2CppType().Name+" frame "+Time.frameCount);
        }
    }
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetMoveDir))]
internal class MoveInput
{
    static bool Prefix(bool ignoreStop,ref Vector2 __result)
    {
        if(!Plugin.RemoteContext)return true;
        __result=ignoreStop || !CustomKeyManager.cm.stopInput?Plugin.RemoteInput.Move:Vector2.zero;return false;
    }
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetAimingDir))]
internal class AimInput
{
    static bool Prefix(bool ignoreStop,ref Vector2 __result){if(!Plugin.RemoteContext)return true;__result=ignoreStop || !CustomKeyManager.cm.stopInput?Plugin.RemoteInput.Aim:Vector2.zero;return false;}
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetMouseWorldPoint))]
internal class AimPoint
{
    static bool Prefix(ref Vector2 __result){if(!Plugin.RemoteContext || !Plugin.Remote)return true;__result=Plugin.RemoteInput.Mouse;return false;}
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetCustomKey))]
internal class ButtonInput
{
    static bool Prefix(CustomKeyCode key,bool ignoreStop,ref bool __result){if(!Plugin.RemoteContext)return true;__result=(ignoreStop || !CustomKeyManager.cm.stopInput) && Plugin.RemoteInput.Held(key);return false;}
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetCustomKeyDown))]
internal class PressInput
{
    static bool Prefix(CustomKeyCode key,bool ignoreStop,ref bool __result){if(!Plugin.RemoteContext)return true;__result=(ignoreStop || !CustomKeyManager.cm.stopInput) && Plugin.RemoteInput.Down(key);return false;}
}
[HarmonyPatch(typeof(CustomKeyManager),nameof(CustomKeyManager.GetCustomKeyUp))]
internal class ReleaseInput
{
    static bool Prefix(CustomKeyCode key,bool ignoreStop,ref bool __result){if(!Plugin.RemoteContext)return true;__result=(ignoreStop || !CustomKeyManager.cm.stopInput) && Plugin.RemoteInput.Up(key);return false;}
}
[HarmonyPatch]
internal class BlockProbeSaves
{
    static IEnumerable<MethodBase> TargetMethods()=>new[]{(MethodBase)AccessTools.Method(typeof(DataManager),nameof(DataManager.SaveSave)),AccessTools.Method(typeof(DataManager),nameof(DataManager.SaveSettings))};
    static bool Prefix()=>!Plugin.Test && !Plugin.Guest;
}

[HarmonyPatch(typeof(Player),nameof(Player.ApplyDamage))]
internal class IntegrationKeepAlive
{
    // Integration tests: attacks that reached each actor (blocked so long runs cannot end in a wipe).
    internal static readonly Dictionary<IntPtr,int> Taken=new();
    internal static int TakenBy(Player? p){return p && Taken.TryGetValue(p!.Pointer,out int n)?n:0;}
    static bool Prefix(Player __instance,ref AttackResult __result)
    {
        if(!Plugin.Test)return true;
        Taken.TryGetValue(__instance.Pointer,out int n);Taken[__instance.Pointer]=n+1;
        __result=AttackResult.Failed;return false;
    }
}





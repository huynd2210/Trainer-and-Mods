using HarmonyLib;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace SwampKeeperCoop;

internal static class TeamLife
{
    private static GameObject? marker;
    internal static bool Wiping {get;private set;}
    private static GameObject Marker
    {
        get{if(!marker){marker=new GameObject("CoopDownedPlayer");UnityEngine.Object.DontDestroyOnLoad(marker);}return marker!;}
    }
    internal static bool Dead(Player? p)=>p && p!.state==PlayerState.Die;
    internal static void Down(Player player)
    {
        player.StopControl(Marker);player.StopAiming(Marker);player.SetInvincible(Marker);
        player.po.rg.velocity=Vector2.zero;player.po.force=Vector2.zero;
        player.sr.color=new(.45f,.55f,.55f,1);
        Plugin.Record("Co-op actor down: "+(Plugin.IsRemote(player)?"guest":"host")+"; survivor continues");
    }
    internal static System.Collections.IEnumerator WaitForRevival(Player player)
    {
        while(player && player.state==PlayerState.Die)yield return null;
    }
    internal static void BeginWipe(){Wiping=true;Plugin.Record("Both players down; native run-ending sequence started");}
    internal static void Revive(Player player)
    {
        if(!Dead(player))return;
        player.RemoveInvincible(Marker);player.StartControl(Marker);player.StartAiming(Marker);
        player.sr.color=Color.white;player.RegrowTail();player.ChangeState(PlayerState.Idle);
        Plugin.Record("Co-op actor revived at next stage: "+(Plugin.IsRemote(player)?"guest":"host"));
    }
    internal static void Reset()=>Wiping=false;
}

[HarmonyPatch(typeof(Player),nameof(Player.Die))]
internal class CooperativeDeath
{
    static bool Prefix(Player __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if(Plugin.Guest || !Plugin.Remote || !Plugin.Host || TeamLife.Wiping)return true;
        TeamLife.Down(__instance);
        if(TeamLife.Dead(Plugin.Host) && TeamLife.Dead(Plugin.Remote))
        {
            TeamLife.BeginWipe();
            // Exactly one native sequence owns end-of-run records and the restart screen.
            __result=Plugin.Host!.Die();return false;
        }
        __result=TeamLife.WaitForRevival(__instance).WrapToIl2Cpp();return false;
    }
}

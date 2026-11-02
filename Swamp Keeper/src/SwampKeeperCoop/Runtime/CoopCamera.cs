using UnityEngine;

namespace SwampKeeperCoop;

internal static class CoopCamera
{
    internal static void HostFrame()
    {
        if(!CamMove.cm || !Plugin.Host || !Plugin.Remote || RunTransitions.Moving || TeamLife.Wiping)return;
        var player=TeamLife.Dead(Plugin.Host)?Plugin.Remote!:Plugin.Host!;
        foreach(var camera in CamMove.cm.mainCams)
            if(camera && (!camera.Follow || camera.Follow.Pointer!=player.camTarget.Pointer))camera.Follow=player.camTarget;
    }
}

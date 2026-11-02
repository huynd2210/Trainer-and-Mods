using UnityEngine;

namespace SwampKeeperCoop;

internal sealed class DeathChecks:Gate
{
    private int phase;private float next;private string previousRun="";
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var h=Plugin.Host!;var r=Plugin.Remote!;
        if(phase==0)
        {
            if(elapsed<12)return;
            r.ChangeState(PlayerState.Die);phase=1;next=elapsed+2;
        }
        else if(phase==1)
        {
            if(!TeamLife.Dead(r) || TeamLife.Dead(h) || TeamLife.Wiping || UIManager.um.diePopUp.gameObject.activeInHierarchy)throw new InvalidOperationException("One player death incorrectly ended the run");
            Plugin.Record("Single-player death verified: guest down; host survives and native run-ending screen remains hidden");
            GameManager.gm.MoveStage(1,true);phase=2;next=elapsed+6;
        }
        else if(phase==2)
        {
            if(RunTransitions.Moving){next=elapsed+.5f;return;}
            if(TeamLife.Dead(r) || !r.canControl || Vector3.Distance(h.transform.position,r.transform.position)>3)throw new InvalidOperationException("Next stage did not revive and restore guest control");
            Plugin.Record("Stage revival verified: guest alive and independently controllable");
            h.ChangeState(PlayerState.Die);phase=3;next=elapsed+2;
        }
        else if(phase==3)
        {
            if(!TeamLife.Dead(h) || TeamLife.Dead(r) || TeamLife.Wiping)throw new InvalidOperationException("Host death incorrectly killed guest");
            Plugin.Record("Host-only death verified: guest survives");
            r.ChangeState(PlayerState.Die);phase=4;next=elapsed+5;
        }
        else if(phase==4)
        {
            if(!TeamLife.Wiping || !UIManager.um.diePopUp.gameObject.activeInHierarchy)throw new InvalidOperationException("Team wipe did not reach native run-ending screen");
            Plugin.Record("NATIVE DEATH CHECKS PASSED: independent deaths, next-stage revival, one native team-wipe screen");
            previousRun=Plugin.RunId;UIManager.um.diePopUp.Restart(false);phase=5;next=elapsed+8;
        }
        else if(phase==5)
        {
            if(Plugin.Driver!.Restarting){next=elapsed+.5f;return;}
            if(Plugin.RunId==previousRun || TeamLife.Dead(h) || TeamLife.Dead(r) || !r.canControl || GameManager.gm.current)throw new InvalidOperationException("Native restart failed to recreate both actors in preparation");
            Plugin.Driver.SetReady(Array.Empty<int>(),new[]{0,1},true);
            Plugin.Record("NATIVE RESTART CHECK PASSED: new run identity, two living actors, preparation restored");Done=true;
        }
    }
}


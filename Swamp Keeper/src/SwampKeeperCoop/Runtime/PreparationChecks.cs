using UnityEngine;

namespace SwampKeeperCoop;

internal sealed class PreparationChecks:Gate
{
    private int phase;private float next;private bool guestDone;
    internal override bool StartsRun=>false;
    internal override bool RequiresBasics=>false;
    internal override bool GuestMoves=>false;
    internal override float HostLimit=>50;
    internal override float GuestFinish=>30;
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        if(phase==0)
        {
            if(elapsed<1)return;
            GameManager.gm.MoveStage(0,true);
            if(GameManager.gm.current || RunTransitions.Moving)throw new InvalidOperationException("Unready players were allowed to start");
            Plugin.Record("Readiness gate verified: starting is blocked before both players ready");phase=1;next=2;
        }
        else if(phase==1)
        {
            SessionMenu.Instance!.AuditPreparation(false);phase=2;next=6;
        }
        else if(phase==2)
        {
            if(!Plugin.Driver!.HostReady || !Plugin.Driver.GuestReady){next=elapsed+.2f;return;}
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"coop-preparation-host.png"));
            phase=3;next=elapsed+2;
        }
        else if(phase==3)
        {
            SessionMenu.Instance!.Close();GameManager.gm.MoveStage(0,true);phase=4;next=elapsed+6;
        }
        else if(phase==4)
        {
            if(RunTransitions.Moving){next=elapsed+.5f;return;}
            var h=Plugin.Host!;var r=Plugin.Remote!;
            int[] Ids(Player p)=>Enumerable.Range(0,p.items.Count).Select(i=>p.items[i].index).OrderBy(i=>i).ToArray();
            if(!Ids(h).SequenceEqual(new[]{0,39}) || !Ids(r).SequenceEqual(new[]{10,20}) || h.skills[0].index!=12 || h.skills[1].index!=5 || r.skills[0].index!=1 || r.skills[1].index!=17)
                throw new InvalidOperationException("Prepared loadouts/skills did not remain separate");
            Plugin.Record("Prepared control states: host="+h.canControl+" guest="+r.canControl);
            if(!h.canControl || !r.canControl)throw new InvalidOperationException("Prepared actors remained locked after run start");
            Plugin.Record("NATIVE PREPARATION CHECKS PASSED: both ready; separate RPC-selected items and skills equipped; both players controllable");Done=true;
        }
    }
    internal override void Guest(float elapsed,SwampKeeperCoop.Protocol.WorldState? world)
    {
        if(guestDone || elapsed<next)return;
        if(phase==0 && elapsed>2 && Plugin.Driver!.IsPreparation){SessionMenu.Instance!.AuditPreparation(true);phase=1;next=6;}
        else if(phase==1)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"coop-preparation-guest.png"));phase=2;next=8;
        }
        else if(phase==2){SessionMenu.Instance!.Close();guestDone=true;}
    }
}

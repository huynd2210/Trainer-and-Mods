using SwampKeeperCoop.Protocol;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// The Well path: the guest enters the Well from preparation, trials are started from the Well's own start button
// and cleared by alternating actors (boss trials included), and the run leaves the Well through its native exit.
internal sealed class WellChecks:Gate
{
    private enum Step {Ready,Enter,Arrive,Trial,Fight,Cleared,Leave,Left}
    private Step step;private float next,since,logAt;private bool advanced;private int trials,bossTrials,lastTrial=-1;
    private readonly HashSet<int> seenTrials=new();private bool guestSawWell;
    internal override bool StartsRun=>false;
    internal override bool RequiresBasics=>false;
    internal override bool GuestMoves=>false;
    internal override float HostLimit=>880;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>930;
    private int Wanted=>Math.Min(GameManager.gm.well.trials.Length,int.TryParse(Arg("-coop-well-trials"),out int n)?n:3);
    private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0 && i+1<a.Length?a[i+1]:"";}
    private static Player Leader(int n)=>n%2==0?Plugin.Remote!:Plugin.Host!;
    private void Go(Step value,float elapsed,float wait=0){step=value;since=elapsed;next=elapsed+wait;}
    private static void Use(Player actor,Interactables target)
    {
        TestActions.Place(actor,TestActions.Inside(target),true);
        if(Plugin.IsRemote(actor))Plugin.RemoteInput.Inject(CustomKeyCode.Interact);
        else{var s=Owners.Enter(actor);try{target.Interact();}finally{Owners.Exit(s);}}
    }
    private static string State(Stage_Well w)=>"trial="+w.currentTrial+" start="+w.isStart+" end="+w.isEnd+" endless="+w.isEndless+" bossWave="+w.bossWave+" spawnBoss="+w.spawnBoss+" spawned="+w.spawnCount+"/"+w.maxSpawnCount+" left="+w.leftEnemyCount+" alive="+Object.FindObjectsOfType<Enemy>().Count(e=>e.gameObject.activeInHierarchy && e.state!=EnemyState.Die)+" startButton="+(w.startButton && w.startButton.activeInHierarchy);
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var gm=GameManager.gm;var h=Plugin.Host!;var r=Plugin.Remote!;var well=gm.well;
        if(step>=Step.Arrive && step<Step.Left && elapsed>=logAt){Plugin.Record("Well state: "+State(well)+" moving="+RunTransitions.Moving);logAt=elapsed+5;}
        switch(step)
        {
            case Step.Ready:
                if(elapsed<3)return;
                if(!Plugin.Driver!.HostReady)Plugin.Driver.SetReady(Array.Empty<int>(),new[]{0,1},true);
                if(!Plugin.Driver.GuestReady){next=elapsed+.2f;return;}
                Go(Step.Enter,elapsed);break;
            case Step.Enter:
            {
                if(RunTransitions.Moving || gm.current){Plugin.Record("Guest entered the Well from preparation through the native Well interaction");Go(Step.Arrive,elapsed,1);return;}
                var settings=UIManager.um.battleSettings;
                if(settings && settings.gameObject.activeInHierarchy && !advanced){advanced=true;Plugin.Record("Guest's Well interaction opened the host's battle-settings popup; host advances");settings.advanceButton.onClick.Invoke();next=elapsed+1;return;}
                var entrance=Object.FindObjectsOfType<Well>().First();
                if(!advanced){TestActions.Place(r,TestActions.Inside(entrance),true);Plugin.RemoteInput.Inject(CustomKeyCode.Interact);}
                if(elapsed-since>15)throw new InvalidOperationException("Guest could not enter the Well: first="+(r.firstInteractable?r.firstInteractable.name:"none"));
                next=elapsed+1;break;
            }
            case Step.Arrive:
                if(RunTransitions.Moving || gm.current?.Pointer!=well.Pointer || UIManager.um.isEventNow || (int)h.state<0 || !h.canControl){if(elapsed-since>60)throw new TimeoutException("Well never became playable");next=elapsed+.5f;return;}
                if(TeamLife.Dead(h) || TeamLife.Dead(r) || !r.canControl || Vector3.Distance(h.transform.position,r.transform.position)>4)throw new InvalidOperationException("Entering the Well lost an actor");
                Plugin.Record("Well playable with both actors: "+State(well));Go(Step.Trial,elapsed,1);break;
            case Step.Trial:
            {
                if(trials>=Wanted){Go(Step.Leave,elapsed);return;}
                if(well.isStart && !well.isEnd){Plugin.Record("Well trial "+well.currentTrial+" started by "+TestActions.Who(Leader(trials)));Go(Step.Fight,elapsed);return;}
                var button=Object.FindObjectsOfType<WellStart>().FirstOrDefault(b=>b.gameObject.activeInHierarchy);
                if(button)Use(Leader(trials),button!);
                if(elapsed-since>60)throw new TimeoutException("Well trial did not start: "+State(well));
                next=elapsed+1;break;
            }
            case Step.Fight:
            {
                if(well.currentTrial!=lastTrial){lastTrial=well.currentTrial;seenTrials.Add(lastTrial);}
                if(well.bossWave || well.spawnBoss)bossTrials=1;
                var killer=Leader(trials);
                // Well waves are not tracked in the Well's eList; fight whatever is alive.
                foreach(var e in Object.FindObjectsOfType<Enemy>())if(e && e.gameObject.activeInHierarchy && e.state!=EnemyState.Die && e.isStart && e.TryCast<Boss_Fox_Bead>()==null)
                {
                    var boss=e.TryCast<Boss>();
                    if(boss)TestActions.BossStep(killer,boss!);else TestActions.KillStep(killer,e);
                }
                if(well.isEnd || (!well.isStart && trials>0) || (well.startButton && well.startButton.activeInHierarchy && elapsed-since>5))
                {
                    trials++;Plugin.Record("Well trial cleared ("+trials+"/"+Wanted+"): "+State(well)+" host kills="+h.killCount+" guest kills="+r.killCount);Go(Step.Trial,elapsed,2);return;
                }
                if(elapsed-since>240)throw new TimeoutException("Well trial did not clear: "+State(well));
                break;
            }
            case Step.Leave:
                // The Well has no exit trigger: a Well run ends when the team falls, on the native run-end screen.
                if(well.checkLeave)throw new InvalidOperationException("Well unexpectedly has an exit trigger; extend this gate");
                if(!TeamLife.Dead(r)){r.ChangeState(PlayerState.Die);next=elapsed+2;return;}
                if(!TeamLife.Dead(h)){if(TeamLife.Wiping)throw new InvalidOperationException("One death in the Well ended the run");h.ChangeState(PlayerState.Die);Go(Step.Left,elapsed,2);return;}
                break;
            case Step.Left:
                if(!TeamLife.Wiping || !UIManager.um.diePopUp.gameObject.activeInHierarchy){if(elapsed-since>30)throw new TimeoutException("Well wipe did not reach the run-end screen");next=elapsed+1;return;}
                Plugin.Record("WELL CHECKS PASSED: guest entered the Well; "+trials+" trials (boss trial "+(bossTrials>0)+") started from the Well button and cleared by alternating players; one fallen player kept the run going; team wipe reached the Well's run-end screen");Done=true;break;
        }
    }
    internal override void Guest(float t,WorldState? world)
    {
        if(world==null)return;
        if(Plugin.Driver!.IsPreparation && !Plugin.Driver.GuestReady)Plugin.Driver.SetReady(Array.Empty<int>(),new[]{0,1},true);
        if(world.Well)guestSawWell=true;
    }
    internal override void GuestFinished(WorldState? last)
    {
        if(!guestSawWell)throw new InvalidOperationException("Guest never rendered the Well");
        Plugin.Record("Guest rendered the Well");
    }
}

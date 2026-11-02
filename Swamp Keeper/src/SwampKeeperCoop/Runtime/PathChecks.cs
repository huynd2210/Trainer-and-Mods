using SwampKeeperCoop.Protocol;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// Full run: preparation -> every stage through its native exit -> each boss killed in-run -> run clear.
// Leaders alternate, so both the guest and the host must be able to trigger each kind of transition.
internal sealed class PathChecks:Gate
{
    private enum Step {Ready,Start,Await,Clear,Leave,Boss,BossEnd,Ending}
    private float diedAt=-1;private Step step;private bool advanced,debugJump,stageWasBoss;private Player? exitLeader;private float next,since;private int expected,lastStage=-2,hostKills,guestKills,retries;
    private readonly HashSet<int> seen=new();
    internal override bool StartsRun=>false;
    internal override bool RequiresBasics=>false;
    internal override bool GuestMoves=>false;
    internal override float HostLimit=>1400;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>1450;
    private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0 && i+1<a.Length?a[i+1]:"";}
    private int bossExits;
    private static Player Leader(int stage)=>stage%2==0?Plugin.Remote!:Plugin.Host!;
    // Boss stages are left through the merchant; alternate who talks to it so both actors do.
    private Player ExitLeader(Stage stage,int index)=>stage.TryCast<Stage_Boss>()?(bossExits%2==0?Plugin.Remote!:Plugin.Host!):Leader(index);
    private void Go(Step value,float elapsed,float wait=0){step=value;since=elapsed;next=elapsed+wait;}
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var gm=GameManager.gm;var h=Plugin.Host!;var r=Plugin.Remote!;var stage=gm.current;
        switch(step)
        {
            case Step.Ready:
                if(elapsed<3)return;
                if(!Plugin.Driver!.HostReady)Plugin.Driver.SetReady(Array.Empty<int>(),new[]{0,1},true);
                if(!Plugin.Driver.GuestReady){if(elapsed-since>20)throw new TimeoutException("Guest never readied");next=elapsed+.2f;return;}
                if(int.TryParse(Arg("-coop-path-from"),out int from) && from>0)
                {
                    if(h.state!=PlayerState.Idle){next=elapsed+.5f;return;}
                    // Debug only: jump to a later stage; the evidence run always starts from preparation.
                    if(r.state!=PlayerState.Idle)TestActions.Place(r,h.transform.position+new Vector3(1.3f,0,0));
                    Plugin.Record("DEBUG: path starts at stage "+from);debugJump=true;GameManager.gm.MoveStage(from,true);expected=from;Go(Step.Await,elapsed,2);return;
                }
                foreach(var i in Object.FindObjectsOfType<Interactables>())Plugin.Record("Preparation interactable: "+i.name+" "+i.GetIl2CppType().Name+" at "+i.transform.position);
                var ladder=Object.FindObjectsOfType<Ladder>().FirstOrDefault();
                if(!ladder)throw new InvalidOperationException("No native run entrance in preparation");
                TestActions.Place(r,ladder!.transform.position,true);Go(Step.Start,elapsed,.5f);
                break;
            case Step.Start:
                if(RunTransitions.Moving || gm.current){Plugin.Record("Guest started the run through the native ladder interaction");expected=0;Go(Step.Await,elapsed,1);return;}
                var settings=UIManager.um.battleSettings;
                if(settings && settings.gameObject.activeInHierarchy && !advanced)
                {
                    advanced=true;
                    // The native ladder opens the host's difficulty popup; the run starts once the host advances it.
                    Plugin.Record("Guest's ladder interaction opened the host's battle-settings popup; host advances");
                    settings.advanceButton.onClick.Invoke();next=elapsed+1;return;
                }
                if(!advanced)Plugin.RemoteInput.Inject(CustomKeyCode.Interact);
                if(elapsed-since>8)throw new InvalidOperationException("Guest Interact at the ladder did not start the run: interactables="+r.interactables.Count+" first="+(r.firstInteractable?r.firstInteractable.name:"none"));
                next=elapsed+1;break;
            case Step.Await:
                if(RunTransitions.Moving || !stage || stage!.stageIndex!=expected || UIManager.um.isEventNow || (int)h.state<0 || !h.canControl)
                {
                    if(elapsed-since>60)throw new TimeoutException("Stage "+expected+" never became playable: current="+(stage?stage!.stageIndex:-1)+" moving="+RunTransitions.Moving+" event="+UIManager.um.isEventNow+" host="+h.state+"/"+h.canControl);
                    next=elapsed+.5f;return;
                }
                if(debugJump){debugJump=false;TestActions.Place(r,h.transform.position+new Vector3(1.3f,0,0));}
                if(TeamLife.Dead(h) || TeamLife.Dead(r) || !r.canControl || Vector3.Distance(h.transform.position,r.transform.position)>4)
                    throw new InvalidOperationException("Stage "+expected+" lost an actor: guestActive="+r.gameObject.activeInHierarchy+" guestParent="+(r.transform.parent?AssetKey.Path(r.transform.parent):"<root>")+" hostParent="+(h.transform.parent?AssetKey.Path(h.transform.parent):"<root>")+" hostDead="+TeamLife.Dead(h)+" guestDead="+TeamLife.Dead(r)+" guestControl="+r.canControl+" gap="+Vector3.Distance(h.transform.position,r.transform.position));
                if(!r.gameObject.activeInHierarchy)throw new InvalidOperationException("Guest actor inactive at stage "+expected+": parent="+(r.transform.parent?AssetKey.Path(r.transform.parent):"<root>"));
                Plugin.Record("Path stage "+expected+" "+stage.name+" ("+stage.GetIl2CppType().Name+") playable; both actors present; guest inventory="+r.items.Count);
                var boss=stage.TryCast<Stage_Boss>();
                if(boss && boss!.boss){hostKills=h.killCount;guestKills=r.killCount;Go(Step.Boss,elapsed,1);}else Go(Step.Clear,elapsed,1);
                break;
            case Step.Clear:
                // A stage starts when a player crosses its start trigger; only a started stage honours its exit.
                if(!stage!.isStart)
                {
                    if(!stage.checkStart)throw new InvalidOperationException("Stage "+expected+" has neither started nor a start trigger");
                    TestActions.Place(Leader(expected),TestActions.Inside(stage.checkStart),true);
                    if(elapsed-since>20)throw new TimeoutException("Stage "+expected+" start trigger did not start it");
                    next=elapsed+.5f;return;
                }
                // Normal stages end on their wave timer; ending it here leaves the native exit trigger as the only path forward.
                if(!stage.isEnd){stage.isEnd=true;stage.OpenExit();}
                Go(Step.Leave,elapsed,1);retries=0;break;
            case Step.Leave:
                if(RunTransitions.Moving || !stage || stage!.stageIndex!=expected)
                {
                    Plugin.Record("Path exit from stage "+expected+" triggered by "+TestActions.Who(exitLeader));
                    if(exitLeader && stageWasBoss)bossExits++;
                    expected++;Go(Step.Await,elapsed,1);return;
                }
                var leader=ExitLeader(stage,expected);exitLeader=leader;stageWasBoss=stage.TryCast<Stage_Boss>();
                if(stage.checkLeave){TestActions.Place(leader,TestActions.Inside(stage.checkLeave),true);}
                else
                {
                    // Boss stages are left by talking to the merchant who drives in after the boss dies.
                    var merchant=Object.FindObjectsOfType<NPC_Merchant>().FirstOrDefault(m=>m.gameObject.activeInHierarchy && m.spawnEnd);
                    Interactables? exit=merchant?merchant:Object.FindObjectsOfType<Ladder>().FirstOrDefault(l=>l.gameObject.activeInHierarchy && Vector3.Distance(l.transform.position,leader.transform.position)<40);
                    if(!exit){if(elapsed-since>45)throw new TimeoutException("No merchant or ladder appeared to leave stage "+expected);next=elapsed+1;return;}
                    if(exit){TestActions.Place(leader,TestActions.Inside(exit!),true);if(Plugin.IsRemote(leader))Plugin.RemoteInput.Inject(CustomKeyCode.Interact);else{var s=Owners.Enter(leader);try{exit.Interact();}finally{Owners.Exit(s);}}}
                    if(retries%5==4)Plugin.Record("Exit attempt: leader="+TestActions.Who(leader)+" first="+(leader.firstInteractable?leader.firstInteractable.name:"none")+" state="+leader.state+" control="+leader.canControl);
                    if(retries++==0)Plugin.Record("Stage "+expected+" exits through "+(exit?exit!.name+" "+exit.GetIl2CppType().Name:"none"));
                }
                if(stage.checkLeave && elapsed-since>8 && retries++==8)
                {
                    var box=stage.checkLeave;
                    Plugin.Record("Exit diagnostics: timeScale="+Time.timeScale+" paused="+TimeManager.tm.isPause+" boxTag="+box.targetTag+" boxLayer="+LayerMask.LayerToName(box.gameObject.layer)+" boxTrigger="+(box.col && box.col.isTrigger)+" boxRb="+(bool)box.rg+" leader="+TestActions.Who(leader)
                        +" colliders="+string.Join(";",leader.GetComponentsInChildren<Collider2D>(true).Select(c=>c.name+"/"+c.tag+"/"+LayerMask.LayerToName(c.gameObject.layer)+"/en="+c.enabled+"/act="+c.gameObject.activeInHierarchy+"/trig="+c.isTrigger+"/hit="+Physics2D.IsTouching(c,box.col))));
                }
                if(elapsed-since>60)throw new TimeoutException("Exit of stage "+expected+" did not trigger: checkLeave="+(bool)stage.checkLeave+" active="+(stage.checkLeave && stage.checkLeave.gameObject.activeInHierarchy)+" col="+(stage.checkLeave && stage.checkLeave.col && stage.checkLeave.col.enabled)+" leaderAt="+leader.transform.position+" box="+(stage.checkLeave?TestActions.Inside(stage.checkLeave).ToString():"-")+" isEnd="+stage.isEnd+" collision="+(stage.checkLeave && stage.checkLeave.isCollision));
                next=elapsed+1;break;
            case Step.Boss:
            {
                var b=stage!.TryCast<Stage_Boss>()!.boss;var killer=Leader(expected);
                if(b.state==EnemyState.Die)
                {
                    int dh=h.killCount-hostKills,dr=r.killCount-guestKills;
                    // Kill credit lands at the end of the native death sequence.
                    if(dh+dr==0 && diedAt<0)diedAt=elapsed;
                    if(dh+dr==0 && elapsed-diedAt<6)return;
                    diedAt=-1;
                    Plugin.Record("Boss "+b.GetIl2CppType().Name+" killed by "+TestActions.Who(killer)+": host kills +"+dh+" guest kills +"+dr);
                    if(Plugin.IsRemote(killer)?dr<1 || dh!=0:dh<1 || dr!=0)throw new InvalidOperationException("Boss kill credited to the wrong actor");
                    Go(Step.BossEnd,elapsed,.5f);return;
                }
                if(!b.gameObject.activeInHierarchy || !b.isStart){if(elapsed-since>45)throw new TimeoutException("Boss never started: active="+b.gameObject.activeInHierarchy);return;}
                TestActions.BossStep(killer,b);
                if(elapsed-since>120)throw new TimeoutException("Boss did not die: "+TestActions.Describe(b));
                break;
            }
            case Step.BossEnd:
                if(expected==10){Go(Step.Ending,elapsed,1);return;}
                if(!stage!.isEnd){if(elapsed-since>60)throw new TimeoutException("Boss stage did not end natively");next=elapsed+.5f;return;}
                Plugin.Record("Boss stage "+expected+" ended natively");Go(Step.Leave,elapsed,.5f);retries=0;break;
            case Step.Ending:
                if(UIManager.um.clearPopUp && UIManager.um.clearPopUp.gameObject.activeInHierarchy)
                {
                    Plugin.Record("PATH CHECKS PASSED: every stage 0-10 entered and left through native triggers by alternating actors; 3 bosses killed in-run; run-clear screen reached");Done=true;return;
                }
                if(elapsed-since>150)throw new TimeoutException("Final boss did not reach the run-clear screen: event="+UIManager.um.isEventNow+" stage="+(stage?stage!.stageIndex:-1));
                next=elapsed+1;break;
        }
    }
    internal override void Guest(float t,WorldState? world)
    {
        if(world==null)return;
        if(Plugin.Driver!.IsPreparation && !Plugin.Driver.GuestReady)Plugin.Driver.SetReady(Array.Empty<int>(),new[]{0,1},true);
        if(world.Stage!=lastStage){lastStage=world.Stage;if(world.Stage>=0)seen.Add(world.Stage);}
    }
    internal override void GuestFinished(WorldState? last)
    {
        var missing=Enumerable.Range(0,11).Where(i=>!seen.Contains(i)).ToArray();
        if(missing.Length>0)throw new InvalidOperationException("Guest never rendered stages "+string.Join(",",missing));
        Plugin.Record("Guest rendered every stage 0-10 of the path");
    }
}

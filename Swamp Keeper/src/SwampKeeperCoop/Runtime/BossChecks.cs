using SwampKeeperCoop.Protocol;
using UnityEngine;

namespace SwampKeeperCoop;

// Each of the three bosses: it targets whichever living player is nearer, its attacks land on that actor,
// the guest kills it with credit to the guest only, and the stage ends natively.
internal sealed class BossChecks:Gate
{
    private static readonly int[] Stages={3,7,10};
    // Debug only (boss-from=N in run_pair): start at a later boss; evidence runs fight all three.
    private int index=int.TryParse(Arg("-coop-boss-from"),out int from)?from:0;
    private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0 && i+1<a.Length?a[i+1]:"";}
    private int phase,nearTargeted,farTargeted,samples,guestKills,hostKills;private float next,since;
    private int takenGuest,takenHost;private float diedAt=-1;
    internal override float HostLimit=>820;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>870;
    private void Go(int value,float elapsed,float wait=0){phase=value;since=elapsed;next=elapsed+wait;}
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var gm=GameManager.gm;var h=Plugin.Host!;var r=Plugin.Remote!;var stage=gm.current;
        switch(phase)
        {
            case 0:
                if(elapsed<12)return;
                gm.MoveStage(Stages[index],true);Go(1,elapsed,2);break;
            case 1:
            {
                if(RunTransitions.Moving || stage?.stageIndex!=Stages[index] || UIManager.um.isEventNow){if(elapsed-since>60)throw new TimeoutException("Boss stage "+Stages[index]+" never became playable");next=elapsed+.5f;return;}
                var boss=stage!.TryCast<Stage_Boss>()!.boss;
                if(!boss.gameObject.activeInHierarchy || !boss.isStart){if(elapsed-since>60)throw new TimeoutException("Boss never started");return;}
                Plugin.Record("Boss "+boss.GetIl2CppType().Name+" started with both actors present");
                nearTargeted=farTargeted=samples=0;takenGuest=Taken(r);takenHost=Taken(h);Go(2,elapsed);break;
            }
            case 2:
            case 3:
            {
                // Phase 2: guest close, host far. Phase 3: swapped. The boss must chase and hit the nearer actor.
                var boss=stage!.TryCast<Stage_Boss>()!.boss;var near=phase==2?r:h;var far=phase==2?h:r;
                // Stand in reach and let the boss wind up its attacks; only correct drift. Spots must be walkable:
                // boss AI searches (e.g. the Fox's dash target) never finish around a player standing inside scenery.
                if(Vector3.Distance(near.transform.position,boss.transform.position)>3)TestActions.Place(near,boss.transform.position);
                if(Vector3.Distance(far.transform.position,boss.transform.position)<Vector3.Distance(near.transform.position,boss.transform.position)+6)TestActions.Place(far,TestActions.Away(stage!,boss.transform.position));
                // Attacks already committed against the previous near actor resolve during the first seconds after a swap.
                if(elapsed-since<3){nearTargeted=farTargeted=samples=0;takenGuest=Taken(r);takenHost=Taken(h);return;}
                // The boss's callbacks run against EnemyTargets.For: it must be whichever living actor is nearer right now.
                var target=EnemyTargets.For(boss);samples++;var bp=boss.transform.position;
                var nearest=Vector3.Distance(r.transform.position,bp)<Vector3.Distance(h.transform.position,bp)?r:h;
                if(target && target!.Pointer==nearest.Pointer)nearTargeted++;
                if(nearest.Pointer==near.Pointer)farTargeted++;
                int landed=Taken(near)-(phase==2?takenGuest:takenHost),stray=Taken(far)-(phase==2?takenHost:takenGuest);
                if(elapsed-since<7 || (landed<1 && elapsed-since<48))return;
                Plugin.Record("Boss targeting with "+(phase==2?"guest":"host")+" near: target was the nearer actor in "+nearTargeted+"/"+samples+" samples ("+(phase==2?"guest":"host")+" nearer in "+farTargeted+"); attacks landed near="+landed+" far="+stray);
                if(nearTargeted<samples*.98f || landed<1)throw new InvalidOperationException("Boss did not target and hit the nearer actor");
                nearTargeted=farTargeted=samples=0;takenGuest=Taken(r);takenHost=Taken(h);
                if(phase==2)Go(3,elapsed);else{hostKills=h.killCount;guestKills=r.killCount;TestActions.Place(h,TestActions.Away(stage!,stage!.TryCast<Stage_Boss>()!.boss.transform.position));Go(4,elapsed);}
                break;
            }
            case 4:
            {
                var boss=stage!.TryCast<Stage_Boss>()!.boss;
                if(boss.state==EnemyState.Die)
                {
                    int dh=h.killCount-hostKills,dg=r.killCount-guestKills;
                    // Kill credit lands at the end of the native death sequence.
                    if(dh+dg==0 && diedAt<0)diedAt=elapsed;
                    if(dh+dg==0 && elapsed-diedAt<6)return;
                    diedAt=-1;
                    Plugin.Record("Boss "+boss.GetIl2CppType().Name+" killed by guest: guest kills +"+dg+" host kills +"+dh);
                    if(dg<1 || dh!=0)throw new InvalidOperationException("Boss kill credit went to the wrong player");
                    Go(5,elapsed,.5f);return;
                }
                TestActions.BossStep(r,boss);
                if(elapsed-since>120)throw new TimeoutException("Guest could not kill the boss: "+TestActions.Describe(boss));
                break;
            }
            case 5:
                if(Stages[index]==10)
                {
                    // The final boss ends the run: its death event leads to the run-clear screen.
                    if(UIManager.um.clearPopUp && UIManager.um.clearPopUp.gameObject.activeInHierarchy){Plugin.Record("Final boss death reached the native run-clear screen");Finish();return;}
                    if(elapsed-since>150)throw new TimeoutException("Final boss did not reach the run-clear screen");
                    next=elapsed+1;return;
                }
                if(!stage!.isEnd){if(elapsed-since>60)throw new TimeoutException("Boss stage did not end natively");next=elapsed+.5f;return;}
                if(TeamLife.Dead(h) || TeamLife.Dead(r))throw new InvalidOperationException("An actor was lost in the boss fight");
                Plugin.Record("Boss stage "+Stages[index]+" ended natively with both actors alive");
                index++;Go(0,elapsed,1);break;
        }
    }
    private static int Taken(Player p)=>IntegrationKeepAlive.TakenBy(p);
    private void Finish(){Plugin.Record("BOSS CHECKS PASSED: Bear, Fox and Tiger each targeted and hit the nearer actor, were killed by the guest with the kill credited to the guest actor only, and ended their stage natively");Done=true;}
}

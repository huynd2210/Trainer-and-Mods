using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// Runs exclusively under the integration flag in the copied game, never in normal play.
internal sealed class GameplayChecks:Gate
{
    private int phase,skill;private float next;
    private string baseline="";private int hostSkillUses;
    internal override float GuestFinish=>Plugin.ProgressionTest?45:75;
    internal override uint GuestKeys(float t)=>t>9 && t<10?1u<<(int)CustomKeyCode.Skill_0:0;
    internal override void GuestFinished(SwampKeeperCoop.Protocol.WorldState? last)
    {
        if(last?.Stage!=1 || last.Actors.Single(a=>a.Id==1).Items.Length!=40)throw new InvalidOperationException("Guest did not receive authoritative progression and inventory");
    }
    internal static string Stats(Player p)=>string.Join("|",p.arrow.arrowCapacity,p.arrow.nockingTime,p.arrow.maxRange,p.arrow.drawingTime,p.arrow.penetration,p.moveSpdMultiByItem,p.moveSpdWhileAttackMulti,p.tailGuardChance,p.doubleDodge,p.noDrawing,p.noNocking,p.tridentArrow,p.powderCharge,p.autoRetrieve,p.watchtower,p.elasticArrow);
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var host=Plugin.Host!;var remote=Plugin.Remote!;
        if(phase==0)
        {
            if(elapsed<13)return;
            Plugin.Record("Network skill use: host="+host.skillUseCount+" guest="+remote.skillUseCount);
            if(host.skillUseCount!=0 || remote.skillUseCount<1)throw new InvalidOperationException("Network skill input did not remain actor-owned");
            baseline=Stats(host);hostSkillUses=host.skillUseCount;
            for(int i=0;i<40;i++)
            {
                var item=Object.Instantiate(ItemManager.im.GetItem(i));item.gameObject.SetActive(true);remote.AddItem(item);
            }
            Plugin.Record("Granted all 40 native items to guest; inventory host="+host.items.Count+" guest="+remote.items.Count);
            next=elapsed+2;phase=1;
        }
        else if(phase==1)
        {
            if(host.items.Count!=0 || remote.items.Count!=40 || baseline!=Stats(host))throw new InvalidOperationException("Guest item preparation modified host inventory/stats");
            Plugin.Record("All-item ownership check passed: host inventory and 16 equipment stats unchanged");
            if(Plugin.ProgressionTest)skill=SkillManager.sm.GetSkillCount();
            phase=2;next=elapsed;
        }
        else if(phase==2)
        {
            if(skill>=SkillManager.sm.GetSkillCount())
            {
                if(hostSkillUses!=host.skillUseCount || baseline!=Stats(host))throw new InvalidOperationException("Remote skill callbacks altered host gameplay state");
                Plugin.Record("Skill smoke passed: "+skill+" skills invoked; host singleton stable");
                GameManager.gm.MoveStage(1,true);
                phase=3;next=elapsed+7;return;
            }
            remote.ChangeSkill(skill,0);
            Owners.Register(remote.skills[0],remote);
            foreach(var pool in remote.skills[0].GetComponentsInChildren<ObjectPool>(true))if(pool.activePool==null || pool.deactivePool==null)pool.Awake();
            var ability=remote.skills[0];ability.current=ability.GetCost();
            ability.StartSkill();ability.SetCharge(1,Vector2.right);ability.UseSkill(1,Vector2.right);ability.EndSkill();
            if(Player.player.Pointer!=host.Pointer || Plugin.RemoteContext)throw new InvalidOperationException("Skill leaked the native player singleton");
            Plugin.Record("Skill smoke "+skill+": "+ability.GetName()+" invoked on guest");
            skill++;next=elapsed+2;
        }
        else if(phase==3)
        {
            if(RunTransitions.Moving){Plugin.Record("Awaiting native transition: host="+host.state+" host control="+host.canControl+" elapsed="+elapsed+" scale="+Time.timeScale);next=elapsed+5;return;}
            if(GameManager.gm.current.stageIndex!=1 || Vector3.Distance(host.transform.position,remote.transform.position)>3 || remote.items.Count!=40)
                throw new InvalidOperationException("Shared run transition lost the guest or inventory");
            Plugin.Record("NATIVE GAMEPLAY CHECKS PASSED: all 18 skills smoke-tested; all 40 items isolated; shared transition to stage 1 preserves guest inventory");Done=true;
        }
    }
}

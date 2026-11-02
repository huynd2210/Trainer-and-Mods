using SwampKeeperCoop.Protocol;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// Every item changes only its owner's stats, and by exactly what it changes for the other actor;
// item-triggered and skill effects fired by real guest network input land only on the guest's account.
internal sealed class EffectChecks:Gate
{
    private const float Triggers=44,Skills=72,Window=4;
    private int phase,item,skill=-1;private float next;
    private readonly string[] guestDelta=new string[40];
    private string hostFp="",guestFp="",fpBefore="";
    private int hostHits,guestHits,hostUses,guestUses,hostShots;private int[] hostCharges=Array.Empty<int>();private Vector3 hostAt;
    private readonly List<Enemy> targets=new();
    internal override float HostLimit=>Skills+18*Window+20;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>HostLimit+40;
    internal override uint GuestKeys(float t)
    {
        if(t>Triggers+1 && t<Triggers+11 && (t-Triggers)%1.2f<.5f)return 1u<<(int)CustomKeyCode.Attack;
        float w=(t-Skills)%Window;int k=(int)((t-Skills)/Window);
        return t>Skills && k<18 && w>1 && w<1.6f?1u<<(int)CustomKeyCode.Skill_0:0;
    }
    internal static string Fingerprint(Player p)
    {
        var a=p.arrow;
        static string F(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>? v)=>v==null?"-":string.Join("/",v);
        return string.Join("|","capacity="+a.arrowCapacity,"nock="+a.nockingTime,"range="+a.maxRange,"draw="+a.drawingTime,"pen="+a.penetration,"minRange="+a.minRange,"spd="+a.minSpd+"-"+a.maxSpd,"force="+a.minForce+"-"+a.maxForce,"risk="+a.maxRisk,"itemStats="+F(a.stats_Items),"buffStats="+F(a.stats_Buffs),
            "move="+p.moveSpdMultiByItem,"buff="+p.moveSpdMultiByBuff,"multi="+p.moveSpdMultiMulti,"attackMove="+p.moveSpdWhileAttackMulti,"reload="+p.reloadSpdMulti,"dodgeDist="+p.dodgeDistanceMulti,"retrieve="+p.autoRetrievingDis,
            "tailGuard="+p.tailGuardChance,"tail="+p.hasTail,"mutated="+p.mutatedTailCount,"doubleDodge="+p.doubleDodge,"noDraw="+p.noDrawing,"noNock="+p.noNocking,"trident="+p.tridentArrow,"powder="+p.powderCharge,
            "autoRetrieve="+p.autoRetrieve,"watchtower="+p.watchtower,"elastic="+p.elasticArrow,"spirit="+(bool)p.spiritStone,"dummy="+(bool)p.dummy,"tigerEye="+p.tigerEye,"items="+p.items.Count);
    }
    private static string Delta(string before,string after)
    {
        var b=before.Split('|');var a=after.Split('|');
        return string.Join(" ",a.Where((v,i)=>v!=b[i]).Select((v,i)=>v));
    }
    // Buffs are situational (e.g. Tattered Cloth senses enemies around its wearer), so compare only static stats.
    private static string Static(string delta)=>string.Join(" ",delta.Split(' ').Where(f=>!f.StartsWith("buff=") && !f.StartsWith("buffStats=") && !f.StartsWith("multi=")));
    // Situational items sense through trigger boxes; each must sit on the actor that owns the item, never on the other one.
    private static void Sensors(Player owner)
    {
        for(int i=0;i<owner.items.Count;i++)
        {
            var item=owner.items[i];
            if(Owners.Find(item)?.Pointer!=owner.Pointer)throw new InvalidOperationException("Item "+item.index+" is not owned by "+TestActions.Who(owner));
            foreach(var box in item.GetComponentsInChildren<TriggerBox>(true))
                if(box.gameObject.activeInHierarchy && Vector3.Distance(box.transform.position,owner.transform.position)>4)
                    throw new InvalidOperationException("Item "+item.index+" sensor "+box.name+" is "+Vector3.Distance(box.transform.position,owner.transform.position)+" from its owner "+TestActions.Who(owner));
        }
    }
    private static void Grant(Player p,int id){var item=Object.Instantiate(ItemManager.im.GetItem(id));item.gameObject.SetActive(true);p.AddItem(item);}
    private void Targets(Player r)
    {
        for(int i=0;i<3;i++)
        {
            if(i>=targets.Count)targets.Add(TestActions.Spawn(EnemyCode.Swordman,r.transform.position));
            else if(!targets[i] || targets[i].state==EnemyState.Die || !targets[i].gameObject.activeInHierarchy)targets[i]=TestActions.Spawn(EnemyCode.Swordman,r.transform.position);
            TestActions.Place(targets[i],r.transform.position+new Vector3(3+i*.7f,(i-1)*.8f,0));
        }
    }
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<next)return;
        var h=Plugin.Host!;var r=Plugin.Remote!;
        if(phase==0)
        {
            if(elapsed<12)return;
            TestActions.Place(h,r.transform.position+new Vector3(-12,0,0));hostAt=h.transform.position;
            phase=1;item=0;
        }
        if(phase==1 || phase==2)
        {
            // Phase 1 grants item N to the guest, phase 2 the same item to the host, in the same order.
            var owner=phase==1?r:h;var other=phase==1?h:r;
            if(item>0 || fpBefore.Length>0)
            {
                var after=Fingerprint(owner);var delta=Delta(fpBefore,after);
                var otherBefore=phase==1?hostFp:guestFp;var otherNow=Fingerprint(other);
                if(Static(Delta(otherBefore,otherNow)).Length>0)throw new InvalidOperationException("Item "+item+" granted to "+TestActions.Who(owner)+" changed the other actor: ["+Delta(otherBefore,otherNow)+"] was ["+Delta(otherNow,otherBefore)+"]");
                if(phase==1)guestDelta[item]=delta;
                else if(Static(delta)!=Static(guestDelta[item]))throw new InvalidOperationException("Item "+item+" applied differently: guest ["+guestDelta[item]+"] host ["+delta+"]");
                else Plugin.Record("Item "+item+" "+ItemManager.im.GetItem(item).GetIl2CppType().Name+": identical owner-only effect "+(delta.Length>0?delta:"(triggered, no static stat)"));
                item++;fpBefore="";
                if(item==40)
                {
                    if(phase==1){Sensors(r);phase=3;next=Triggers;return;}
                    else{Sensors(h);Plugin.Record("All 40 items: owner-only, identical static effects on guest and host; every item sensor rides on its owner");
                        Plugin.Record("EFFECT CHECKS PASSED: 40 items owner-only and identical on both actors; item-triggered and all 18 skill effects from guest network input credited to the guest only");Done=true;return;}
                }
            }
            hostFp=Fingerprint(h);guestFp=Fingerprint(r);fpBefore=Fingerprint(owner);
            Grant(owner,item);next=elapsed+.25f;return;
        }
        if(phase==3)
        {
            hostHits=DamageCredit.HitsBy(h);guestHits=DamageCredit.HitsBy(r);hostShots=h.shotCount;hostFp=Fingerprint(h);
            Targets(r);phase=4;next=elapsed+.5f;return;
        }
        if(phase==4)
        {
            if(elapsed<Triggers+12){Targets(r);next=elapsed+.5f;return;}
            int gh=DamageCredit.HitsBy(r)-guestHits,hh=DamageCredit.HitsBy(h)-hostHits;
            Plugin.Record("All-item network arrows: guest hits +"+gh+" host hits +"+hh+" host shots +"+(h.shotCount-hostShots));
            if(gh<1 || hh!=0 || h.shotCount!=hostShots || Fingerprint(h)!=hostFp)throw new InvalidOperationException("Item-triggered effects from guest arrows leaked to the host");
            // The host holds no items until the end, so nothing of its own can deal damage during these windows.
            phase=5;next=Skills;return;
        }
        if(phase==5)
        {
            // Window k: arm skill k for the guest at its start, evaluate just before the next one.
            if(skill>=0)Evaluate(h,r);
            skill++;
            if(skill==18){phase=2;item=0;fpBefore="";return;}
            r.ChangeSkill(skill,0);Owners.Register(r.skills[0],r);
            foreach(var pool in r.skills[0].GetComponentsInChildren<ObjectPool>(true))if(pool.activePool==null || pool.deactivePool==null)pool.Awake();
            r.skills[0].current=r.skills[0].GetCost();
            TestActions.Place(h,hostAt);Targets(r);
            hostHits=DamageCredit.HitsBy(h);guestHits=DamageCredit.HitsBy(r);hostUses=h.skillUseCount;guestUses=r.skillUseCount;
            hostCharges=h.skills.Select(s=>s.current).ToArray();hostFp=Fingerprint(h);guestFp=Fingerprint(r);
            next=Skills+(skill+1)*Window-.2f;
        }
    }
    private void Evaluate(Player h,Player r)
    {
        var s=r.skills[0];int gh=DamageCredit.HitsBy(r)-guestHits,hh=DamageCredit.HitsBy(h)-hostHits;
        bool used=r.skillUseCount>guestUses || s.current<s.GetCost();var self=Delta(guestFp,Fingerprint(r));
        Plugin.Record("Skill "+skill+" "+s.GetIl2CppType().Name+": used="+used+" guest uses +"+(r.skillUseCount-guestUses)+" charge "+s.current+"/"+s.GetCost()+" guest hits +"+gh+" host hits +"+hh+" guest self-effect ["+self+"]");
        if(!used)throw new InvalidOperationException("Guest network input did not use skill "+skill);
        if(hh!=0 || h.skillUseCount!=hostUses || !h.skills.Select(x=>x.current).SequenceEqual(hostCharges) || Fingerprint(h)!=hostFp || Vector3.Distance(h.transform.position,hostAt)>.5f)
            throw new InvalidOperationException("Skill "+skill+" used by the guest changed the host: hits +"+hh+" uses "+hostUses+"->"+h.skillUseCount+" charges "+string.Join("/",hostCharges)+"->"+string.Join("/",h.skills.Select(x=>x.current))+" stats ["+Delta(hostFp,Fingerprint(h))+"] moved "+Vector3.Distance(h.transform.position,hostAt));
    }
}

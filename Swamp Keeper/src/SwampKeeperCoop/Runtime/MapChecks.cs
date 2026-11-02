using UnityEngine;

namespace SwampKeeperCoop;

// Records the native run layout so the path and boss gates can enumerate it rather than hard-code it.
internal sealed class MapChecks:Gate
{
    internal override float HostLimit=>30;
    internal override float GuestFinish=>16;
    internal override float GuestLimit=>40;
    internal override void Host(float elapsed)
    {
        if(Done || elapsed<12)return;
        var gm=GameManager.gm;
        for(int i=0;i<gm.stages.Length;i++)
        {
            var s=gm.stages[i];
            if(!s){Plugin.Record("Map stage "+i+": null");continue;}
            var boss=s.TryCast<Stage_Boss>();
            Plugin.Record("Map stage "+i+": "+s.name+" type="+s.GetIl2CppType().Name+" index="+s.stageIndex+" kind="+s.stageType+" maxTime="+s.maxTime+" boss="+(boss && boss!.boss?boss.boss.GetIl2CppType().Name:"-")+" well="+(s.Pointer==gm.well.Pointer));
        }
        var well=gm.well;
        Plugin.Record("Map well: "+well.name+" trials="+(well.trials?.Length??-1)+" bosses="+string.Join(",",well.bosses.Select(b=>b?b.GetIl2CppType().Name:"null"))+" stageIndex="+well.stageIndex);
        for(int i=0;i<SkillManager.sm.GetSkillCount();i++){var k=SkillManager.sm.GetSkill(i);Plugin.Record("Map skill "+i+": "+k.GetIl2CppType().Name+" cost="+k.GetCost()+" charging="+k.chargingSkill+" bow="+k.bowSkill);}
        for(int i=0;i<ItemManager.im.GetItemCount();i++){var it=ItemManager.im.GetItem(i);Plugin.Record("Map item "+i+": "+it.GetIl2CppType().Name);}
        foreach(var b in Resources.FindObjectsOfTypeAll<Boss>())Plugin.Record("Map boss object: "+b.name+" "+b.GetIl2CppType().Name+" scene="+b.gameObject.scene.name+" active="+b.gameObject.activeInHierarchy+" armor="+b.armor);
        foreach(var e in Resources.FindObjectsOfTypeAll<PlayEvent>())Plugin.Record("Map event: "+e.name+" "+e.GetIl2CppType().Name);
        Plugin.Record("Map pools: enemyPools="+EnemyManager.em.enemyPools.Length+" kills host="+Plugin.Host!.killCount);
        Done=true;
    }
}

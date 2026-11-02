using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// Integration-test helpers. Each drives a native game path as the named actor would, never a shortcut that skips it.
internal static class TestActions
{
    internal static string Who(Player? p)=>!p?"none":Plugin.IsRemote(p!)?"guest":"host";
    // The arrow's own damage path (Arrow_Player.DealDamage): armored targets lose armor by penetration, others take the hit,
    // run inside the attacker's ownership scope so native credit (last hit, AddKillCount) resolves to that actor.
    internal static void Strike(Player attacker,Attackedable target,int penetration=3,Vector2? aim=null)
    {
        var state=Owners.Enter(attacker);
        try
        {
            var dir=aim??((Vector2)(target.transform.position-attacker.transform.position)).normalized;if(dir==Vector2.zero)dir=Vector2.right;
            var enemy=target.TryCast<Enemy>();
            // Arrow_Player.DealDamage type-checks special targets first: the Fox and its beads take the hit directly
            // (beads break; the Fox dies once they are gone), since their armor paths ignore arrows.
            bool direct=target.TryCast<Boss_Fox_Bead>()!=null || target.TryCast<Boss_Fox>()!=null;
            if(enemy && enemy!.isArmored && !direct)enemy.DealArmor(penetration,1,dir,true);else target.ApplyDamage(1,dir,true);
        }
        finally{Owners.Exit(state);}
    }
    // Players cannot leave the stage's camera area; positions outside it make some boss AI (e.g. the Fox's dash target search) spin forever.
    internal static Vector3 Arena(Vector3 position)
    {
        // Prefer the nearest navmesh point (where players can stand); else clamp into the camera area.
        if(UnityEngine.AI.NavMesh.SamplePosition(position,out var hit,3,UnityEngine.AI.NavMesh.AllAreas))return new Vector3(hit.position.x,hit.position.y,position.z);
        var area=GameManager.gm?.current?.confiner;
        if(!area || area!.OverlapPoint(position))return position;
        Vector3 edge=area.ClosestPoint(position);var centre=area.bounds.center;
        return edge+(centre-edge).normalized*1.5f;
    }
    // A walkable spot next to a point: step from it towards the middle of the stage.
    internal static Vector3 Beside(Stage stage,Vector3 point)
    {
        var centre=stage.confiner?stage.confiner.bounds.center:point;
        var dir=(Vector2)(centre-point);return point+(Vector3)(dir.sqrMagnitude<.01f?Vector2.right:dir.normalized)*2.5f;
    }
    // A walkable spot far from a point: the stage's own player start, else mirrored across the stage centre.
    internal static Vector3 Away(Stage stage,Vector3 point)
    {
        if(stage.startPos && Vector3.Distance(stage.startPos.position,point)>8)return stage.startPos.position;
        var centre=stage.confiner?stage.confiner.bounds.center:point;return centre+(centre-point)*.6f;
    }
    // exact: stand precisely there (inside a trigger box); otherwise snap to where a player can stand.
    internal static void Place(Player p,Vector3 position,bool exact=false)
    {
        if(!exact)position=Arena(position);position.z=p.transform.position.z;
        p.transform.position=position;
        if(p.po && p.po.rg){p.po.rg.position=position;p.po.rg.velocity=Vector2.zero;}
        Physics2D.SyncTransforms();
    }
    internal static void Place(Enemy e,Vector3 position)
    {
        position=Arena(position);
        e.transform.position=position;
        if(e.po && e.po.rg){e.po.rg.position=position;e.po.rg.velocity=Vector2.zero;}
    }
    // The stage's own spawn (pool, wave bookkeeping, start state), as waves use it.
    internal static Enemy Spawn(EnemyCode code,Vector3 position)
    {
        var stage=GameManager.gm.current;position=Arena(position);
        int before=stage.eList.Count;stage.SpawnEnemy(code,position);
        if(stage.eList.Count<=before)throw new InvalidOperationException("Stage did not spawn "+code);
        return stage.eList[stage.eList.Count-1];
    }
    // Strike until the enemy enters its native death state; invincibility windows are respected.
    internal static bool KillStep(Player attacker,Enemy enemy)
    {
        if(!enemy || enemy.state==EnemyState.Die)return true;
        if(enemy.isStart && !enemy.invincible)Strike(attacker,enemy);
        return enemy.state==EnemyState.Die;
    }
    // A boss fight: break whatever guards the boss (shields, beads, summoned enemies) as a player's arrows would, then the boss.
    private static readonly HashSet<string> struck=new();
    internal static void BossStep(Player attacker,Boss boss)
    {
        foreach(var part in Object.FindObjectsOfType<Attackedable>())
        {
            if(!part.gameObject.activeInHierarchy || part.invincible || part.Pointer==boss.Pointer || part.TryCast<Player>()!=null)continue;
            // The Fox's beads belong to it wherever they float; anything else must be near the fight.
            if(Vector3.Distance(part.transform.position,boss.transform.position)>25 && !part.TryCast<Boss_Fox_Bead>())continue;
            var e=part.TryCast<Enemy>();if(e && e!.state==EnemyState.Die)continue;
            var kind=part.GetIl2CppType().Name+"/"+part.name;if(struck.Add(kind))Plugin.Record("Boss fight strikes "+kind+" ("+(e?e!.state.ToString():"-")+")");
            // Deflectable parts (the Fox's beads) fly along the shot; aim them at the boss as a player would.
            Strike(attacker,part,3,((Vector2)(boss.transform.position-part.transform.position)).normalized);
        }
        if(!boss.invincible && boss.state!=EnemyState.Die){if(struck.Add("boss"))Plugin.Record("Boss fight strikes the boss: "+Describe(boss));Strike(attacker,boss);}
    }
    // Where a player must stand to be inside a trigger: its collider's centre, not its transform (colliders carry offsets).
    internal static Vector3 Inside(TriggerBox box)=>box.col?(Vector3)box.col.bounds.center:box.transform.position;
    internal static Vector3 Inside(Interactables target)=>target.col?(Vector3)target.col.bounds.center:target.transform.position;
    internal static string Describe(Boss boss)
    {
        var text="armor="+boss.currentArmor+" state="+boss.state+" invincible="+boss.invincible+" at="+boss.transform.position;
        var fox=boss.TryCast<Boss_Fox>();
        if(fox && fox!.beadList!=null)text+=" beads="+fox.beadList.Count+" ["+string.Join(";",Enumerable.Range(0,fox.beadList.Count).Select(i=>fox.beadList[i]).Select(b=>{try{return !b?"null":b.name+"/act="+b.gameObject.activeInHierarchy+"/state="+b.state+"/inv="+b.invincible+"/d="+Vector3.Distance(b.transform.position,boss.transform.position).ToString("0.0");}catch(Exception){return "uninitialised";}}))+"]";
        var bear=boss.TryCast<Boss_Bear>();
        if(bear)text+=" shield="+bear!.hasShield+" shields="+(bear.shieldList?.Count??-1);
        return text;
    }
}

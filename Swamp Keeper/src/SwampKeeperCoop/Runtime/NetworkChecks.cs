using SwampKeeperCoop.Protocol;
using UnityEngine;

namespace SwampKeeperCoop;

// Runs behind run_pair.py's latency proxy. The host asks the proxy to cut the link, then to stall it silently
// (half-open), and checks that each side notices, the run pauses, the guest rejoins through the F7 menu and the run resumes intact.
internal sealed class NetworkChecks:Gate
{
    private int phase,drops,rejoins;private float next,since,downAt,lastClick=-99;
    private string runId="";private IntPtr remote;private int[] items=Array.Empty<int>();private int guestKills;
    private Enemy? victim;private bool down;private long sent0;private WorldState? seen,stale;private float sentAt;
    internal override float HostLimit=>150;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>200;
    private static void Proxy(string command)=>File.WriteAllText(Path.Combine(BepInEx.Paths.GameRootPath,"coop-proxy-command.txt"),command);
    internal override void Host(float elapsed)
    {
        if(down){down=false;Resumed(elapsed);}
        if(Done || elapsed<next)return;
        var h=Plugin.Host!;var r=Plugin.Remote!;var driver=Plugin.Driver!;
        if(phase==0)
        {
            if(elapsed<12 || driver.Rtt<0)return;
            if(driver.Rtt<.15f)throw new InvalidOperationException("Latency proxy not in the path: rtt="+driver.Rtt);
            if(victim==null){victim=TestActions.Spawn(EnemyCode.Swordman,r.transform.position+new Vector3(2,0,0));since=elapsed;}
            if(!TestActions.KillStep(r,victim)){if(elapsed-since>15)throw new TimeoutException("Guest could not kill its pre-drop enemy");return;}
            var item=UnityEngine.Object.Instantiate(ItemManager.im.GetItem(5));item.gameObject.SetActive(true);r.AddItem(item);
            runId=Plugin.RunId;remote=r.Pointer;items=Enumerable.Range(0,r.items.Count).Select(i=>r.items[i].index).ToArray();guestKills=r.killCount;
            sent0=driver.Peer!.BytesSent;sentAt=elapsed;phase=1;next=elapsed+6;
            Plugin.Record("Latency play: rtt="+(int)(driver.Rtt*1000)+" ms; guest moved/attacked through the proxy; guest kills="+guestKills+" items="+string.Join(",",items));
        }
        else if(phase==1)
        {
            float kbps=(driver.Peer!.BytesSent-sent0)/1024f/(elapsed-sentAt);
            Plugin.Record("Host upstream "+kbps.ToString("0.0")+" KiB/s over "+(elapsed-sentAt).ToString("0")+" s ("+(kbps*8/1024).ToString("0.00")+" Mbit/s)");
            if(kbps>2048)throw new InvalidOperationException("Snapshot stream exceeds 2 MiB/s");
            guestKills=r.killCount;items=Enumerable.Range(0,r.items.Count).Select(i=>r.items[i].index).ToArray();
            Proxy("cut");Plugin.Record("Proxy instructed to cut the link; guest kills="+guestKills+" items="+string.Join(",",items));phase=2;since=elapsed;next=elapsed+.5f;
        }
        else if(phase==2 || phase==4)
        {
            if(elapsed-since>40)throw new TimeoutException("Link "+(phase==2?"cut":"stall")+" was not detected and recovered");
        }
        else if(phase==3)
        {
            Proxy("stall");Plugin.Record("Proxy instructed to stall the link silently (half-open)");phase=4;since=elapsed;
        }
        else if(phase==5)
        {
            if(elapsed-since<4)return;
            if(TimeManager.tm.isPause || !r.canControl)throw new InvalidOperationException("Run did not resume after recovery");
            Plugin.Record("NETWORK CHECKS PASSED: "+(int)(driver.Rtt*1000)+" ms RTT play; cut and silent stall both detected; run paused, guest rejoined via F7 menu twice; actor, inventory, kills and run identity preserved");Done=true;
        }
    }
    internal override void Disconnected(float elapsed)
    {
        if(Plugin.Guest && down){GuestRejoin();return;}
        if(down)return;
        down=true;downAt=elapsed;drops++;lastClick=Time.realtimeSinceStartup-1;stale=seen;
        if(!Plugin.Guest)
        {
            if(!TimeManager.tm.isPause)throw new InvalidOperationException("Host did not pause the run when the guest dropped");
            Plugin.Record("Drop "+drops+" detected by host "+(downAt-since).ToString("0.0")+" s after the "+(phase==2?"cut":"stall")+"; run paused, input cleared");
        }
        else Plugin.Record("Drop "+drops+" detected by guest");
    }
    private void GuestRejoin()
    {
        // Press Join in the real F7 menu, as the status message tells the player to; retry like a player would.
        var driver=Plugin.Driver!;
        if(!driver.CanReconnect || Time.realtimeSinceStartup-lastClick<2.5f)return;
        if(lastClick>0)Plugin.Record("Rejoin attempt status: "+Plugin.Status);
        lastClick=Time.realtimeSinceStartup;SessionMenu.Instance!.TestConnect(true,Plugin.Address,Plugin.Code);
    }
    private void Resumed(float elapsed)
    {
        var r=Plugin.Remote!;
        if(Plugin.RunId!=runId || r.Pointer!=remote || r.killCount!=guestKills || !Enumerable.Range(0,r.items.Count).Select(i=>r.items[i].index).SequenceEqual(items))
            throw new InvalidOperationException("Reconnect lost state: run "+(Plugin.RunId==runId)+" actor "+(r.Pointer==remote)+" kills "+guestKills+"->"+r.killCount+" items "+string.Join(",",items)+"->"+string.Join(",",Enumerable.Range(0,r.items.Count).Select(i=>r.items[i].index)));
        Plugin.Record("Guest rejoined after drop "+drops+" ("+(elapsed-downAt).ToString("0.0")+" s down); same actor, items, kills and run");
        if(phase==2){phase=3;next=elapsed+3;}else if(phase==4){phase=5;since=elapsed;}
    }
    internal override void Guest(float t,WorldState? world)
    {
        if(world==null)return;
        // The host restarts its snapshot sequence on reconnect; a fresh snapshot object is the signal.
        if(!down)seen=world;
        if(down && !ReferenceEquals(world,stale)){seen=world;down=false;rejoins++;if(world.RunId!=runId && runId!="")throw new InvalidOperationException("Guest rejoined a different run");Plugin.Record("Guest resumed run after rejoin "+rejoins);}
        runId=world.RunId;
    }
    internal override void GuestFinished(WorldState? last)
    {
        if(rejoins!=2)throw new InvalidOperationException("Guest rejoined "+rejoins+" times, expected 2");
    }
}

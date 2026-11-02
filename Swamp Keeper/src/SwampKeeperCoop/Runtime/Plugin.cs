using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Net;
using UnityEngine;
using SwampKeeperCoop.Protocol;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

[BepInPlugin("local.swampkeeper.coop","Swamp Keeper Online Co-op","0.1.0")]
[BepInDependency("local.swampkeeper.mods","1.2.0")]
public sealed class Plugin:BasePlugin
{
    internal const string Build="swamp105-coop-003";
    internal const string NativeHash="A9A9AE88DCF5089F9530DA833ECAB459B7213C55CA5A3CD0E0F3DB0BA1461B4B";
    internal static Plugin Self=null!;
    internal static Player? Host,Remote;
    internal static bool Cloning,RemoteContext,Guest,Test,ProgressionTest;
    internal static string Role="",Address="127.0.0.1",Code="";
    internal static string RunId="";
    internal static int Port=27856;
    internal static ControllerType HostController;
    internal static RuntimeDriver? Driver;
    internal static string Status="F7 opens online co-op. Connect from the title screen.";
    private static bool started,patched;
    internal static string PeerId="";
    internal static readonly ActorInput RemoteInput=new();
    internal static readonly Dictionary<IntPtr,Player> ProjectileOwners=new(),EnemyLastHit=new();
    internal static readonly List<string> Evidence=new();
    public override void Load()
    {
        var args=Environment.GetCommandLineArgs();
        string Arg(string key,string fallback=""){int at=Array.IndexOf(args,key);return at>=0 && at+1<args.Length?args[at+1]:fallback;}
        Self=this;PeerId=Config.Bind("Session","PlayerIdentity",Guid.NewGuid().ToString("N"),"Persistent identity used to reconnect to your co-op actor.").Value;AddComponent<SessionMenu>();
        SwampKeeperMods.CoopIntegration.OpenMenu=()=>SessionMenu.Instance?.Toggle();
        SwampKeeperMods.CoopIntegration.IsMenuOpen=()=>SessionMenu.Open;
        Test=args.Contains("-coop-integration-test");ProgressionTest=args.Contains("-coop-progression-test");
        if(Test){Gates.Load(args);Port=int.Parse(Arg("-coop-port","27856"));}
        Role=Arg("-coop-role");
        // The menu test names its role only for its report; the session itself starts from the F7 menu.
        if(Role!="host" && Role!="guest"){Role=Test?Arg("-coop-menu-role"):"";return;}
        Guest=Role=="guest";
        Address=Arg("-coop-address","127.0.0.1");Code=Arg("-coop-code");
        Port=int.Parse(Arg("-coop-port","27856"));StartRuntime();
    }
    internal static void BeginSession(bool guest,string address,string code)
    {
        if(started && guest && Driver?.CanReconnect==true){Address=address;Code=code;Driver.Reconnect();return;}
        if(started)throw new InvalidOperationException("A session is already starting or connected.");
        if(Player.player)throw new InvalidOperationException("Start co-op from the title screen before entering a run.");
        if((guest && string.IsNullOrWhiteSpace(address)) || address.Length>255 || code.Length is <8 or >128)throw new ArgumentException("Enter the host's address and a session code of 8–128 characters.");
        Role=guest?"guest":"host";Guest=guest;Address=guest?address:"0.0.0.0";Code=code;StartRuntime();
    }
    private static void StartRuntime()
    {
        if(started)throw new InvalidOperationException("A session is already active");
        using(var file=File.OpenRead(Path.Combine(Paths.GameRootPath,"GameAssembly.dll")))
        using(var hash=System.Security.Cryptography.SHA256.Create())
            if(Convert.ToHexString(hash.ComputeHash(file))!=NativeHash)throw new NotSupportedException("Co-op requires the validated Swamp Keeper v1.05 binary");
        if(Code.Length<8)throw new ArgumentException("Use -coop-code with at least 8 characters");
        if(!patched){new Harmony("local.swampkeeper.coop").PatchAll(typeof(Plugin).Assembly);patched=true;}
        SwampKeeperMods.CoopIntegration.Active=true;SwampKeeperMods.CoopIntegration.RenderingGuest=Guest;
        started=true;Status=Guest?"Connecting to host…":"Hosting on TCP "+Port+". Waiting for a guest…";
        Driver=Self.AddComponent<RuntimeDriver>();
    }
    internal static void ResetFailedStart(RuntimeDriver driver)
    {
        if(Driver?.Pointer!=driver.Pointer)return;
        started=false;Driver=null;Role="";Guest=false;
        SwampKeeperMods.CoopIntegration.Active=false;SwampKeeperMods.CoopIntegration.RenderingGuest=false;
        Object.Destroy(driver);
    }
    // The guest's native game stays frozen for the whole session and cannot be brought back to its title screen in place,
    // so leaving starts a fresh process once this one has exited (quitting closes the link; the host pauses for a rejoin).
    internal static void LeaveToTitle()
    {
        Record("Guest left the session; restarting to the title screen");
        if(!Test)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe",RelaunchArguments(Environment.ProcessId,Paths.ExecutablePath,Paths.GameRootPath)){UseShellExecute=false,CreateNoWindow=true});
        Application.Quit();
    }
    internal static string RelaunchArguments(int pid,string exe,string dir)
    {
        static string Quote(string s)=>"'"+s.Replace("'","''")+"'";
        return "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Get-Process -Id "+pid+" -ErrorAction SilentlyContinue | Wait-Process -Timeout 60 -ErrorAction Stop; Start-Process -FilePath "+Quote(exe)+" -WorkingDirectory "+Quote(dir)+"\"";
    }
    internal static bool IsRemote(Player p)=>Cloning || (Remote && p.Pointer==Remote!.Pointer);
    internal static void Record(string text)
    {
        Evidence.Add(text);Self.Log.LogInfo(text);
        File.WriteAllLines(Path.Combine(Paths.GameRootPath,"coop-"+Role+"-report.txt"),Evidence);
    }
    internal static Player Clone(Player host)
    {
        ProjectileOwners.Clear();EnemyLastHit.Clear();Owners.Reset();
        Host=host;RunId=Guid.NewGuid().ToString("N");TeamLife.Reset();Cloning=true;
        try{Remote=Object.Instantiate(host);}
        finally{Cloning=false;Player.player=host;RemoteContext=false;}
        Remote!.name="Coop_Player_2";RunTransitions.Home=host.transform.parent;
        Remote.transform.position=host.transform.position+new Vector3(2,0,0);
        return Remote;
    }
    internal static void FinishClone()
    {
        var r=Remote!;var h=Host!;
        var skills=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Skill>(h.skills.Length);
        for(int i=0;i<skills.Length;i++){skills[i]=Object.Instantiate(h.skills[i]);skills[i].transform.SetParent(r.transform,false);skills[i].current=h.skills[i].current;}
        r.skills=skills;Owners.Register(h,h);Owners.Register(r,r);
        r.arrowCount=r.arrow.arrowCapacity;r.ChangeState(PlayerState.Idle);
        // Remove the clone's own OnEnable join lock, preserving genuine scene/ability locks.
        r.StartControl(r.gameObject);
        Record("Remote native actor ready: independent skills="+(h.skills[0].Pointer!=r.skills[0].Pointer)+" control="+r.canControl);
    }
}

internal sealed class ActorInput
{
    private readonly InputAccumulator accumulator=new();
    private InputState value=>accumulator.Current;
    internal Vector2 Move=>new(value.MoveX,value.MoveY);
    internal Vector2 Aim=>new(value.AimX,value.AimY);
    internal Vector2 Mouse=>new(value.MouseX,value.MouseY);
    internal ControllerType Controller=>(ControllerType)value.Controller;
    internal long Sequence=>accumulator.Sequence;
    internal void BeginFrame()=>accumulator.BeginFrame();
    internal void Set(InputState next)=>accumulator.Accept(next);
    internal void Clear()=>accumulator.Clear();
    internal void Reset()=>accumulator.Reset();
    internal bool Held(CustomKeyCode key)=>((value.Keys|Injected)&(1u<<(int)key))!=0;
    internal bool Down(CustomKeyCode key)
    {
        if((accumulator.Pressed&(1u<<(int)key))!=0)return true;
        if((Injected&(1u<<(int)key))==0)return false;
        if(injectedDown<0)injectedDown=Time.frameCount;return injectedDown==Time.frameCount;
    }
    // Integration tests only: a key press as the guest's native Player.Update would see it.
    private uint injected;private int injectedUntil,injectedDown=-1;
    private uint Injected=>Time.frameCount<=injectedUntil?injected:0;
    internal void Inject(CustomKeyCode key,int frames=4){injected=1u<<(int)key;injectedUntil=Time.frameCount+frames;injectedDown=-1;}
    internal bool Up(CustomKeyCode key)=>(accumulator.Released&(1u<<(int)key))!=0;
}

public sealed class RuntimeDriver:MonoBehaviour
{
    public RuntimeDriver(IntPtr p):base(p){}
    private CoopListener? listener;private NetworkPeer? peer;
    private Task<(NetworkPeer Peer,Welcome Welcome)>? connecting;
    private readonly WorldMirror mirror=new();private readonly WorldCapture capture=new();
    private bool HostChecksPassed=>Gates.AllDone && (!Gates.RequiresBasics || (moved && shot));
    private WorldState? lastWorld;
    internal bool GatesPassed=>Plugin.Test && Gates.Active.Count>0 && HostChecksPassed;
    internal bool Ready=>phase==4 && !Restarting;
    internal bool Restarting {get;private set;}
    private IntPtr restartingHost;
    internal bool StartingGame {get;private set;}
    internal bool IsPreparation=>Ready && (Plugin.Guest?mirror.Stage==-1:GameManager.gm && !GameManager.gm.current);
    internal bool HostReady {get;private set;}
    internal bool GuestReady {get;private set;}
    internal bool LocalReady=>Plugin.Guest?GuestReady:HostReady;
    internal bool PeerReady=>Plugin.Guest?HostReady:GuestReady;
    private int[] hostItems=Array.Empty<int>(),guestItems=Array.Empty<int>();
    private int[] hostSkills={0,1},guestSkills={0,1};private long commandSequence,lastCommand=-1;
    private bool preparedGranted;
    private bool reconnecting,ownsNetworkPause;
    private readonly CancellationTokenSource networkStop=new();
    internal bool LinkUp=>peer?.Connected==true && !reconnecting;
    internal bool CanReconnect=>Plugin.Guest && phase==4 && reconnecting && connecting==null;
    internal void Reconnect()
    {
        if(!CanReconnect)return;
        connecting=CoopConnector.ConnectAsync(Plugin.Address,Plugin.Port,Plugin.Build,Plugin.Code,Plugin.PeerId,"Guest",networkStop.Token);
        Plugin.Status="Reconnecting to your run…";
    }
    // A link that stops delivering (half-open TCP after a VPN/router drop) never errors on its own; heartbeats expose it.
    internal const float LinkTimeout=8;
    private float lastHeard,pingAt;private bool heard;
    internal float Rtt {get;private set;}=-1;
    internal NetworkPeer? Peer=>peer;
    // A send can fail because the link just died; recovery starts next frame. A full queue on a live link is an error.
    private bool Sent(bool ok,string failure){if(ok || peer?.Connected!=true)return ok;throw new IOException(failure);}
    private void Heard(){lastHeard=Time.realtimeSinceStartup;heard=true;}
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private bool Link(Packet packet)
    {
        Heard();
        if(packet.Kind==PacketKind.Ping){if(packet.Data.Length!=8)throw new InvalidDataException("Invalid heartbeat");peer!.Send(PacketKind.Pong,packet.Data);return true;}
        if(packet.Kind!=PacketKind.Pong)return false;
        if(packet.Data.Length!=8)throw new InvalidDataException("Invalid heartbeat");
        double sent=BitConverter.ToDouble(packet.Data);double rtt=Time.realtimeSinceStartupAsDouble-sent;
        if(rtt>=0 && rtt<60)Rtt=(float)rtt;
        return true;
    }
    private void Heartbeat()
    {
        float now=Time.realtimeSinceStartup;
        if(!heard)return;
        if(now-lastHeard>LinkTimeout){Plugin.Record("Peer silent for "+LinkTimeout+" s; closing the dead link");_=peer!.DisposeAsync();return;}
        if(now>=pingAt){peer!.Send(PacketKind.Ping,BitConverter.GetBytes(Time.realtimeSinceStartupAsDouble));pingAt=now+1;}
    }
    private uint pendingPressed,pendingReleased;
    private int phase;private float next,sendAt,testStart,lastInputAt;private long sequence;
    private int snapshots;private Vector3 remoteStart,hostStart;private bool moved,shot,captured,finishSent;
    public void Update()
    {
        try
        {
            Application.runInBackground=true;
            if(!Plugin.Guest && !Plugin.RemoteContext && CustomKeyManager.cm)Plugin.HostController=CustomKeyManager.cm.controllerType;
            if(phase==0)
            {
                if(Plugin.Guest)connecting=CoopConnector.ConnectAsync(Plugin.Address,Plugin.Port,Plugin.Build,Plugin.Code,Plugin.PeerId,"Guest",networkStop.Token);
                else listener=new CoopListener(IPAddress.Any,Plugin.Port,Plugin.Build,Plugin.Code,true);
                Plugin.Record("Session transport starting on "+Plugin.Address+":"+Plugin.Port);
                phase=1;next=Time.realtimeSinceStartup+3;
            }
            if(phase==1 && Time.realtimeSinceStartup>=next)
            {
                if(!Plugin.Test || Gates.Get<MenuChecks>()!=null)
                {
                    if(Plugin.Guest){if(connecting==null || !connecting.IsCompleted)return;(peer,_)=connecting.GetAwaiter().GetResult();}
                    else if(peer==null && (listener==null || !listener.TryAccept(out peer)))return;
                }
                var title=Object.FindObjectOfType<UI_Title>();if(!title)return;
                StartingGame=true;try{title.StartGame();}finally{StartingGame=false;}phase=2;next=Time.realtimeSinceStartup+4;
            }
            if(phase==2 && Time.realtimeSinceStartup>=next)
            {
                if(!Player.player || !GameManager.gm)return;
                if(Plugin.Test && Gates.StartsRun && !GameManager.gm.current){GameManager.gm.MoveStage(0,true);next=Time.realtimeSinceStartup+5;return;}
                Plugin.Host=Player.player;
                if(Plugin.Guest)
                {
                    if(connecting==null || !connecting.IsCompleted)return;
                    if(peer==null)(peer,_)=connecting.GetAwaiter().GetResult();
                    FreezeGuest();Plugin.Record("Guest simulation stopped; authoritative rendering enabled");
                    phase=4;Plugin.Status="Connected. Choose items and skills, then ready up with F7.";
                }
                else
                {
                    if(peer==null && (listener==null || !listener.TryAccept(out peer)))return;
                    Plugin.Record("Authenticated guest "+peer.Name);
                    Plugin.Clone(Player.player);phase=3;next=Time.realtimeSinceStartup+.5f;
                }
            }
            if(phase==3 && Time.realtimeSinceStartup>=next)
            {
                Plugin.FinishClone();hostStart=Plugin.Host!.transform.position;remoteStart=Plugin.Remote!.transform.position;
                phase=4;if(testStart==0)testStart=Time.realtimeSinceStartup;Restarting=false;Plugin.Status="Connected. Choose items and skills, then ready up with F7.";
            }
            if(phase!=4 || peer==null)return;
            if(Restarting)
            {
                var player=Player.player;
                if(!player || player.Pointer==restartingHost || !GameManager.gm || player.skills==null || player.arrow==null || player.items==null)return;
                Plugin.Clone(player);phase=3;next=Time.realtimeSinceStartup+.5f;Plugin.Record("Rebuilding both actors after native run restart");return;
            }
            if(!peer.Connected)
            {
                Plugin.RemoteInput.Clear();
                if(Plugin.Test && captured && (Plugin.Guest?finishSent:HostChecksPassed)){Plugin.Record("INTEGRATION PASSED: peer exited after verification");Application.Quit();return;}
                if(Plugin.Test && Gates.Get<NetworkChecks>()==null)throw new IOException("Session disconnected: "+peer.Error);
                RecoverConnection();
                if(Plugin.Test)foreach(var gate in Gates.Active)gate.Disconnected(Time.realtimeSinceStartup-testStart);
                return;
            }
            if(Plugin.Guest)GuestFrame();else HostFrame();
            if(peer.Connected)Heartbeat();
            if(IsPreparation)Plugin.Status=HostReady && GuestReady?"Both ready. Close this menu; the host can enter the run.":"Choose items and skills, then ready up. Both players must be ready.";
            else if(Ready)Plugin.Status=(Plugin.Guest?mirror.TeamDown:TeamLife.Wiping)?"Run ended. The host can use the restart controls.":"Run active. Inspect your inventory here; change equipment before the next run.";
        }
        catch(Exception ex)
        {
            Plugin.Status="Session error: "+ex.Message;Plugin.Record("SESSION FAILED: "+ex);
            if((!Plugin.Test || Gates.Get<MenuChecks>()!=null) && phase<=1){phase=99;Plugin.ResetFailedStart(this);SessionMenu.Instance?.Show();return;}
            phase=99;if(Plugin.Test)Application.Quit();
        }
    }
    private void RecoverConnection()
    {
        if(!reconnecting)
        {
            reconnecting=true;connecting=null;
            if(!Plugin.Guest && TimeManager.tm && !TimeManager.tm.isPause){TimeManager.tm.PauseGame();ownsNetworkPause=true;}
            Plugin.Status=Plugin.Guest?"Connection lost. Open F7 and join again to resume your run.":"Guest disconnected. Run paused while waiting for the same guest to reconnect.";
            SessionMenu.Instance?.Show();Plugin.Record("Connection lost ("+(peer?.Error??"closed")+"); preserving both actors and run progression");
        }
        NetworkPeer? resumed=null;
        if(Plugin.Guest)
        {
            if(connecting==null || !connecting.IsCompleted)return;
            try{(resumed,_)=connecting.GetAwaiter().GetResult();}
            catch(Exception ex){Plugin.Status="Reconnect failed: "+ex.Message;connecting=null;return;}
        }
        else if(listener==null || !listener.TryAccept(out resumed))return;
        _=peer!.DisposeAsync();peer=resumed;connecting=null;reconnecting=false;heard=false;pingAt=0;
        if(Plugin.Guest)mirror.Rerequest();
        Plugin.RemoteInput.Reset();lastCommand=-1;sequence=0;commandSequence=0;pendingPressed=pendingReleased=0;sendAt=0;
        if(!Plugin.Guest){capture.ResendCatalog();if(ownsNetworkPause && TimeManager.tm)TimeManager.tm.ResumeGame();ownsNetworkPause=false;}
        SessionMenu.Instance?.Close();Plugin.Status="Reconnected to the same run.";Plugin.Record("Reconnected: actor inventories and run identity preserved");
    }
    internal void BeginRestart(bool quick)
    {
        if(Plugin.Guest || peer?.Connected!=true || Restarting)return;
        restartingHost=Plugin.Host?.Pointer??IntPtr.Zero;Restarting=true;preparedGranted=false;HostReady=GuestReady=quick;
        Plugin.RemoteInput.Clear();SessionMenu.Instance?.ResetForRun();Plugin.Record("Native run restart requested: quick="+quick);
    }
    private void FreezeGuest()
    {
        // No native gameplay, coroutines or physics may advance on the rendering peer.
        foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if(b.Pointer==Pointer)continue;
            var name=b.GetIl2CppType().FullName;
            if(name.StartsWith("SwampKeeperCoop."))continue;
            if(b.TryCast<CustomKeyManager>()!=null || name.StartsWith("TMPro.") || name.StartsWith("UnityEngine.UI.") || name.StartsWith("UnityEngine.EventSystems.") || name.StartsWith("UnityEngine.InputSystem.UI."))continue;
            b.StopAllCoroutines();b.enabled=false;
        }
        foreach(var body in Object.FindObjectsOfType<Rigidbody2D>())body.simulated=false;
        foreach(var canvas in Object.FindObjectsOfType<Canvas>())if(canvas.gameObject.name!="CoopSessionMenu")canvas.enabled=false;
        foreach(var r in Object.FindObjectsOfType<SpriteRenderer>())r.enabled=false;
        Screen.SetResolution(1280,800,false);
        mirror.Initialize();
    }
    private void HostFrame()
    {
        CoopCamera.HostFrame();RunTransitions.Regroup();
        Plugin.RemoteInput.BeginFrame();
        while(peer!.TryReceive(out var packet))
        {
            if(Link(packet))continue;
            if(packet.Kind==PacketKind.Input){Plugin.RemoteInput.Set(JsonPackets.Decode<InputState>(packet.Data));lastInputAt=Time.realtimeSinceStartup;}
            else if(packet.Kind==PacketKind.Command)Command(JsonPackets.Decode<RunCommand>(packet.Data));
            else if(packet.Kind==PacketKind.AssetRequest)
            {
                var request=JsonPackets.Decode<SpriteRequest>(packet.Data);
                if(request.Ids==null || request.Ids.Length>128)throw new InvalidDataException("Invalid sprite request");
                foreach(int id in request.Ids)
                    if(!Sent(peer.Send(PacketKind.Asset,JsonPackets.Encode(capture.Export(id))),"Asset queue is full"))return;
            }
            else if(packet.Kind==PacketKind.Disconnect && Plugin.Test)
            {
                if(!HostChecksPassed || (Gates.RequiresBasics && !captured))throw new InvalidOperationException("Guest exited before host checks passed");
                Plugin.Record("HOST INTEGRATION PASSED: authoritative input and "+snapshots+" snapshots");Application.Quit();return;
            }
        }
        if(Time.realtimeSinceStartup-lastInputAt>.5f)Plugin.RemoteInput.Clear();
        if(Time.realtimeSinceStartup>=sendAt)
        {
            var (world,catalog)=capture.Capture(++sequence,Time.realtimeSinceStartup-testStart);
            if(catalog!=null && !Sent(peer.Send(PacketKind.Catalog,WorldPackets.Encode(catalog)),"Catalog queue is full"))return;
            if(!Sent(peer.Send(PacketKind.Snapshot,WorldPackets.Encode(world),true),"Snapshot send failed"))return;
            snapshots++;sendAt=Time.realtimeSinceStartup+.05f;
        }
        if(!Plugin.Test)return;
        var elapsed=Time.realtimeSinceStartup-testStart;
        foreach(var gate in Gates.Active)gate.Host(elapsed);
        if(elapsed>Gates.HostLimit)throw new TimeoutException("Integration gates did not finish: "+string.Join(",",Gates.Active.Where(g=>!g.Done).Select(g=>g.GetType().Name)));
        if(!Gates.RequiresBasics)return;
        if(!moved && elapsed>5)
        {
            float hd=Vector3.Distance(hostStart,Plugin.Host!.transform.position),rd=Vector3.Distance(remoteStart,Plugin.Remote!.transform.position);
            Plugin.Record("Network movement: host distance="+hd+" guest distance="+rd+" received sequence="+Plugin.RemoteInput.Sequence);
            if(hd>.5f || rd<1)throw new InvalidOperationException("Remote input did not move only guest actor");
            moved=true;
        }
        if(!shot && elapsed>9)
        {
            Plugin.Record("Network attacks: host shots="+Plugin.Host!.shotCount+" guest shots="+Plugin.Remote!.shotCount);
            if(Plugin.Host.shotCount!=0 || Plugin.Remote.shotCount<1)throw new InvalidOperationException("Network attack ownership failed");
            shot=true;
        }
        if(!captured && elapsed>10){ScreenCapture.CaptureScreenshot(Path.Combine(Paths.GameRootPath,"coop-host.png"));captured=true;}
    }
    private void GuestFrame()
    {
        Packet? latest=null;
        while(peer!.TryReceive(out var packet))
        {
            if(Link(packet))continue;
            if(packet.Kind==PacketKind.Catalog)mirror.Catalog(WorldPackets.Decode<WorldCatalog>(packet.Data));
            else if(packet.Kind==PacketKind.Asset)mirror.Asset(JsonPackets.Decode<SpriteAsset>(packet.Data));
            else if(packet.Kind==PacketKind.Snapshot)
            {
                latest=packet;
            }
        }
        if(latest!=null)
        {
            var world=WorldPackets.Decode<WorldState>(latest.Data);mirror.Apply(world);snapshots++;lastWorld=world;
            HostReady=world.HostReady;GuestReady=world.GuestReady;
            if(testStart==0){testStart=Time.realtimeSinceStartup;Plugin.Record("First authoritative snapshot: actors="+world.Actors.Length+" sprites="+world.Sprites.Length);}
        }
        var missing=mirror.RequestMissing();
        if(missing.Length>0 && !Sent(peer.Send(PacketKind.AssetRequest,JsonPackets.Encode(new SpriteRequest(missing))),"Asset request queue is full"))return;
        if(Plugin.Test && testStart>0)foreach(var gate in Gates.Active)gate.Guest(mirror.SessionSeconds,lastWorld);
        if(!Plugin.Test && CustomKeyManager.cm && !SessionMenu.Open)
            for(int i=0;i<(int)CustomKeyCode.END;i++)
            {
                if(CustomKeyManager.cm.GetCustomKeyDown((CustomKeyCode)i,true))pendingPressed|=1u<<i;
                if(CustomKeyManager.cm.GetCustomKeyUp((CustomKeyCode)i,true))pendingReleased|=1u<<i;
            }
        // Input starts once the host is simulating (first snapshot); before that nothing reads it and queues would back up.
        if(!finishSent && lastWorld!=null && Time.realtimeSinceStartup>=sendAt)
        {
            InputState input;
            if(Plugin.Test)
            {
                float elapsed=mirror.SessionSeconds;
                var actor=mirror.Guest;
                bool moves=Gates.GuestMoves;
                uint keys=(moves && elapsed>5 && elapsed<6?1u<<(int)CustomKeyCode.Attack:0)|Gates.GuestKeys(elapsed);
                var move=Gates.GuestMove(elapsed);
                input=new InputState{Sequence=++sequence,MoveX=(moves && elapsed>1 && elapsed<3?1:0)+move.x,MoveY=move.y,AimX=1,MouseX=(actor?.X??0)+5,MouseY=actor?.Y??0,Keys=keys};
            }
            else
            {
                var cm=CustomKeyManager.cm;var move=cm.GetMoveDir(true);var aim=cm.GetAimingDir(true);var mouse=cm.GetMouseWorldPoint();uint keys=0;
                var origin=mirror.Guest!=null?new Vector2(mirror.Guest.X,mirror.Guest.Y):Vector2.zero;
                if(cm.controllerType==ControllerType.KeyboardMouse)aim=(mouse-origin).normalized;else mouse=origin+aim*5;
                for(int i=0;i<(int)CustomKeyCode.END;i++)if(cm.GetCustomKey((CustomKeyCode)i,true))keys|=1u<<i;
                if(SessionMenu.Open){move=Vector2.zero;keys=0;pendingPressed=0;}
                uint forwarded=~(1u<<(int)CustomKeyCode.Inventory);
                input=new InputState{Sequence=++sequence,MoveX=move.x,MoveY=move.y,AimX=aim.x,AimY=aim.y,MouseX=mouse.x,MouseY=mouse.y,Keys=keys&forwarded,Pressed=pendingPressed&forwarded,Released=pendingReleased&forwarded,Controller=(int)cm.controllerType};
            }
            if(!Sent(peer.Send(PacketKind.Input,JsonPackets.Encode(input)),"Input queue is full"))return;
            pendingPressed=0;pendingReleased=0;
            sendAt=Time.realtimeSinceStartup+1f/60;
        }
        if(Plugin.Test && testStart>0)
        {
            var elapsed=Time.realtimeSinceStartup-testStart;
            if(!captured && elapsed>11)
            {
                mirror.Verify();ScreenCapture.CaptureScreenshot(Path.Combine(Paths.GameRootPath,"coop-guest.png"));
                Plugin.Record("GUEST INTEGRATION PASSED: snapshots="+snapshots+" replicas="+mirror.Count+" missing sprites="+mirror.Missing);captured=true;
            }
            if((elapsed>Gates.GuestFinish || (captured && lastWorld?.GatesDone==true)) && !finishSent)
            {
                mirror.Verify();
                foreach(var gate in Gates.Active)gate.GuestFinished(lastWorld);
                if(!peer.Send(PacketKind.Disconnect,JsonPackets.Encode("integration-complete")))throw new IOException("Completion send failed");finishSent=true;
            }
            if(elapsed>Gates.GuestLimit)throw new TimeoutException("Host did not acknowledge completion");
        }
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void SetReady(int[] items,int[] skills,bool ready)
    {
        if(!IsPreparation)throw new InvalidOperationException("Equipment can be chosen before a run starts.");
        if(items.Length>40 || items.Any(id=>id<0 || id>=40) || skills.Length!=2 || skills.Any(id=>id<0 || id>=SkillManager.sm.GetSkillCount()))throw new ArgumentException("Invalid loadout");
        SwampKeeperMods.CoopIntegration.SetPreparedItems(items);
        if(!Plugin.Guest){hostItems=items.Distinct().ToArray();hostSkills=skills.ToArray();HostReady=ready;return;}
        SendCommand(new(++commandSequence,CommandKind.PrepareItems,items));
        for(int i=0;i<2;i++)SendCommand(new(++commandSequence,CommandKind.SelectSkill,SkillSlot:i,SkillId:skills[i]));
        SendCommand(new(++commandSequence,CommandKind.Ready,Ready:ready));GuestReady=ready;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void Unready()
    {
        if(!IsPreparation)return;
        if(Plugin.Guest){SendCommand(new(++commandSequence,CommandKind.Ready,Ready:false));GuestReady=false;}else HostReady=false;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void SendCommand(RunCommand command){if(peer==null || !peer.Send(PacketKind.Command,JsonPackets.Encode(command)))throw new IOException("Could not send preparation choice");}
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void Command(RunCommand command)
    {
        if(command.Sequence<0 || command.Sequence<=lastCommand)return;
        if(!IsPreparation)throw new InvalidDataException("Preparation command outside preparation area");
        switch(command.Kind)
        {
            case CommandKind.PrepareItems:
                if(command.Items==null || command.Items.Length>40 || command.Items.Any(id=>id<0 || id>=40))throw new InvalidDataException("Invalid prepared item IDs");
                guestItems=command.Items.Distinct().ToArray();GuestReady=false;break;
            case CommandKind.SelectSkill:
                if(command.SkillSlot is <0 or >1 || command.SkillId<0 || command.SkillId>=SkillManager.sm.GetSkillCount())throw new InvalidDataException("Invalid skill selection");
                guestSkills[command.SkillSlot]=command.SkillId;GuestReady=false;break;
            case CommandKind.Ready:GuestReady=command.Ready;break;
            default:throw new InvalidDataException("Unsupported preparation command");
        }
        lastCommand=command.Sequence;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void ApplyPreparedLoadouts()
    {
        if(preparedGranted)return;
        void Grant(Player player,int[] items,int[] skills)
        {
            foreach(int id in items)
            {
                if(Enumerable.Range(0,player.items.Count).Any(i=>player.items[i].index==id))continue;
                var template=ItemManager.im.GetItem(id);var item=Object.Instantiate(template);item.gameObject.SetActive(true);player.AddItem(item);
                ItemManager.im.dropItemList?.Remove(template);ItemManager.im.lockedItemList?.Remove(template);
            }
            for(int i=0;i<2;i++)player.ChangeSkill(skills[i],i);
        }
        Grant(Plugin.Host!,hostItems,hostSkills);Grant(Plugin.Remote!,guestItems,guestSkills);preparedGranted=true;
        Plugin.Record("Separate prepared loadouts equipped: host="+string.Join(",",hostItems)+" guest="+string.Join(",",guestItems));
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal int[] LocalItems()
    {
        if(Plugin.Guest)return mirror.Guest?.Items??Array.Empty<int>();
        var player=Plugin.Host;return player?Enumerable.Range(0,player!.items.Count).Select(i=>player.items[i].index).ToArray():Array.Empty<int>();
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal int[] LocalSkills()=>Plugin.Guest?mirror.Guest?.Skills??Array.Empty<int>():Plugin.Host?Plugin.Host!.skills.Select(s=>s.index).ToArray():Array.Empty<int>();
    public void OnDestroy()=>Shutdown();
    public void OnApplicationQuit()=>Shutdown();
    private void Shutdown()
    {
        if(!networkStop.IsCancellationRequested)networkStop.Cancel();
        if(peer!=null)_=peer.DisposeAsync();if(listener!=null)_=listener.DisposeAsync();
        if(connecting!=null)_=DisposePending(connecting);
    }
    private static async Task DisposePending(Task<(NetworkPeer Peer,Welcome Welcome)> task)
    {try{var result=await task.ConfigureAwait(false);await result.Peer.DisposeAsync().ConfigureAwait(false);}catch{}}
}


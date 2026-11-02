using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text;
using SwampKeeperCoop.Protocol;

if(args.Contains("--server")){await SeparateServer();return;}
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
async Task Throws<T>(Func<Task> action,string label) where T:Exception
{
    try{await action();}catch(T){Check(true,label);return;}
    throw new Exception("Did not reject: "+label);
}
using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
var token=deadline.Token;
var first=new Packet(PacketKind.Command,Encoding.UTF8.GetBytes("Unicode character: 용"));
var second=new Packet(PacketKind.Ping,Array.Empty<byte>());
using var wire=new MemoryStream();
await Frames.WriteAsync(wire,first,token);await Frames.WriteAsync(wire,second,token);
using(var fragmented=new Fragments(wire.ToArray(),3))
{
    var a=await Frames.ReadAsync(fragmented,token);var b=await Frames.ReadAsync(fragmented,token);
    Check(a?.Kind==first.Kind&&a.Data.SequenceEqual(first.Data),"Fragmented TCP frame reassembles exact UTF-8 payload");
    Check(b?.Kind==PacketKind.Ping&&b.Data.Length==0,"Coalesced next frame and empty ping remain separate");
    Check(await Frames.ReadAsync(fragmented,token)==null,"Clean EOF is recognized");
}
var bytes=wire.ToArray();
await Throws<EndOfStreamException>(()=>Frames.ReadAsync(new MemoryStream(bytes[..8]),token),"Truncated header is rejected");
await Throws<EndOfStreamException>(()=>Frames.ReadAsync(new MemoryStream(bytes[..15]),token),"Truncated payload is rejected");
var bad=bytes[..12].ToArray();BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(8),int.MaxValue);
await Throws<InvalidDataException>(()=>Frames.ReadAsync(new MemoryStream(bad),token),"Oversized frame rejected before allocation");
bad=bytes[..12].ToArray();BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(8),-1);
await Throws<InvalidDataException>(()=>Frames.ReadAsync(new MemoryStream(bad),token),"Negative frame length rejected");
bad=bytes.ToArray();bad[4]=99;
await Throws<InvalidDataException>(()=>Frames.ReadAsync(new MemoryStream(bad),token),"Protocol header mismatch rejected");
bad=bytes.ToArray();bad[5]=255;
await Throws<InvalidDataException>(()=>Frames.ReadAsync(new MemoryStream(bad),token),"Unknown packet type rejected");
var normalized=new InputState{Sequence=4,MoveX=1,MoveY=1,Keys=uint.MaxValue}.Validated();
Check(Math.Abs(normalized.MoveX*normalized.MoveX+normalized.MoveY*normalized.MoveY-1)<.0001f&&normalized.Keys==(1u<<22)-1,"Movement and key mask are bounded");
await Throws<InvalidDataException>(()=>Task.Run(()=>new InputState{MoveX=float.NaN}.Validated()),"NaN input rejected");
await Throws<InvalidDataException>(()=>Task.Run(()=>new InputState{AimX=float.MaxValue}.Validated()),"Extreme finite input rejected");
var inputFrames=new InputAccumulator();inputFrames.BeginFrame();
inputFrames.Accept(new InputState{Sequence=1,Keys=512});inputFrames.Accept(new InputState{Sequence=2,Keys=0});
Check(inputFrames.Current.Keys==0 && inputFrames.Pressed==512 && inputFrames.Released==512,"A complete tap between host frames preserves both input edges");
Check(!inputFrames.Accept(new InputState{Sequence=1,Keys=1024}) && inputFrames.Current.Keys==0,"Replayed or reordered input cannot replace current controls");
inputFrames.BeginFrame();inputFrames.Accept(new InputState{Sequence=3,Keys=512});inputFrames.BeginFrame();
Check(inputFrames.Current.Keys==512 && inputFrames.Pressed==0 && inputFrames.Released==0,"Held input survives frame boundaries without repeating its press");
inputFrames.Clear();Check(inputFrames.Current.Keys==0 && inputFrames.Released==512 && inputFrames.Sequence==3,"Input timeout releases held buttons while preserving replay protection");

var repeated=new string('x',100000);var compressed=WorldPackets.Encode(repeated);
Check(compressed.Length<1000 && WorldPackets.Decode<string>(compressed)==repeated,"World compression substantially reduces repeated snapshot data and round-trips exactly");
await Throws<InvalidDataException>(()=>Task.Run(()=>WorldPackets.Decode<string>(compressed[..3])),"Truncated compression header rejected");
var invalidCompressed=compressed.ToArray();BinaryPrimitives.WriteInt32LittleEndian(invalidCompressed,int.MaxValue);
await Throws<InvalidDataException>(()=>Task.Run(()=>WorldPackets.Decode<string>(invalidCompressed)),"Oversized decoded world rejected before allocation");
invalidCompressed=compressed.ToArray();BinaryPrimitives.WriteInt32LittleEndian(invalidCompressed,10);
await Throws<InvalidDataException>(()=>Task.Run(()=>WorldPackets.Decode<string>(invalidCompressed)),"Compressed world cannot expand beyond declared bound");
invalidCompressed=compressed.ToArray();BinaryPrimitives.WriteInt32LittleEndian(invalidCompressed,100005);
await Throws<InvalidDataException>(()=>Task.Run(()=>WorldPackets.Decode<string>(invalidCompressed)),"Decoded length mismatch rejected");

const string build="test-build-exact-hash";
string code=CoopListener.NewCode();
await using(var host=new CoopListener(IPAddress.Loopback,0,build,code))
{
    await Throws<InvalidOperationException>(async()=>{var c=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,"wrong-code",Guid.NewGuid().ToString("N"),"Guest",token);await c.Peer.DisposeAsync();},"Incorrect session code rejected");
    await Throws<InvalidOperationException>(async()=>{var c=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,"wrong-build",code,Guid.NewGuid().ToString("N"),"Guest",token);await c.Peer.DisposeAsync();},"Mismatched game build rejected");
    var connected=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code.ToLowerInvariant(),Guid.NewGuid().ToString("N"),"Guest",token);
    await using var guest=connected.Peer;
    await using var server=await Accept(host,token);
    Check(connected.Welcome.ActorId==1&&connected.Welcome.SessionId==host.SessionId&&server.Name=="Guest","Authenticated peers agree on session and actor identity");
    await Throws<InvalidOperationException>(async()=>{var c=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code,Guid.NewGuid().ToString("N"),"Extra",token);await c.Peer.DisposeAsync();},"Third player rejected for a two-player session");
    var input=new InputState{Sequence=17,MoveX=.5f,AimX=1,MouseX=9,MouseY=3,Keys=1u<<9};
    Check(guest.Send(PacketKind.Input,JsonPackets.Encode(input)),"Guest input queued without blocking caller");
    var received=await Receive(server,token);
    Check(received.Kind==PacketKind.Input&&JsonPackets.Decode<InputState>(received.Data)==input,"Host receives exact input state");
    var state=new WorldState(125,2,"Game",3,false,81,new[]{new ActorState(1,3,4,0,1,0,0,8,8,true,true,5,new[]{0,1},new[]{1,2},new[]{0,39})},Array.Empty<SpriteState>(),Array.Empty<TilemapState>(),new CameraState(3,4,-10,7));
    Check(server.Send(PacketKind.Snapshot,JsonPackets.Encode(state),true),"Authoritative snapshot queued");
    received=await Receive(guest,token);
    var snapshot=JsonPackets.Decode<WorldState>(received.Data);
    Check(received.Kind==PacketKind.Snapshot&&snapshot.Tick==125&&snapshot.Actors[0].Items.SequenceEqual(new[]{0,39})&&snapshot.Actors[0].Kills==5,"Guest receives actor, inventory and run state");
    guest.Send(PacketKind.Snapshot,JsonPackets.Encode(state));
    await server.Completion.WaitAsync(token);await guest.Completion.WaitAsync(token);
    Check(server.Error?.Contains("role")==true,"Host rejects guest attempt to submit world state");
    var reconnect=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code,Guid.NewGuid().ToString("N"),"Reconnect",token);
    await using var reGuest=reconnect.Peer;await using var reHost=await Accept(host,token);
    Check(reconnect.Welcome.SessionId==host.SessionId,"Disconnected session accepts a new authenticated guest");
    await reGuest.DisposeAsync();await reHost.Completion.WaitAsync(token);
    Check(!reHost.Connected&&!reGuest.Connected&&!reGuest.Send(PacketKind.Ping,Array.Empty<byte>()),"Disconnect cancels both directions and prevents new sends");
}

// A half-open link (VPN/router drop) still looks connected to the host; the same player rejoining must replace it.
await using(var host=new CoopListener(IPAddress.Loopback,0,build,code,true))
{
    string player=Guid.NewGuid().ToString("N");
    var stale=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code,player,"Guest",token);
    await using var staleGuest=stale.Peer;await using var staleHost=await Accept(host,token);
    staleGuest.Send(PacketKind.Input,JsonPackets.Encode(new InputState{Sequence=1}));await Receive(staleHost,token);
    Check(staleHost.BytesReceived>12 && staleGuest.BytesSent==staleHost.BytesReceived,"Byte counters match on both ends of a link");
    await Throws<InvalidOperationException>(async()=>{var c=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code,Guid.NewGuid().ToString("N"),"Intruder",token);await c.Peer.DisposeAsync();},"Another player cannot take over a live guest slot");
    var rejoin=await CoopConnector.ConnectAsync("127.0.0.1",host.Port,build,code,player,"Guest",token);
    await using var rejoinGuest=rejoin.Peer;await using var rejoinHost=await Accept(host,token);
    await staleHost.Completion.WaitAsync(token);
    Check(!staleHost.Connected && rejoinHost.Connected && rejoinHost.PeerId==player,"Same player rejoining replaces the stale half-open link");
    rejoinGuest.Send(PacketKind.Ping,BitConverter.GetBytes(1.5));
    var ping=await Receive(rejoinHost,token);
    Check(ping.Kind==PacketKind.Ping && BitConverter.ToDouble(ping.Data)==1.5,"Heartbeat ping carries its timestamp to the host");
}

var exe=Environment.ProcessPath??throw new Exception("No test executable");
using(var process=Process.Start(new ProcessStartInfo(exe,"--server"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true})??throw new Exception("Cannot start second process"))
{
    try
    {
        var ready=await process.StandardOutput.ReadLineAsync(token);
        if(ready==null||!ready.StartsWith("READY "))throw new Exception("Server failed: "+ready);
        var parts=ready.Split(' ');int port=int.Parse(parts[1]);
        var connected=await CoopConnector.ConnectAsync("127.0.0.1",port,"process-build",parts[2],Guid.NewGuid().ToString("N"),"External",token);
        await using var peer=connected.Peer;
        peer.Send(PacketKind.Input,JsonPackets.Encode(new InputState{Sequence=33,MoveX=.75f,Keys=512}));
        var reply=await Receive(peer,token);
        var echoed=JsonPackets.Decode<InputState>(reply.Data);
        Check(reply.Kind==PacketKind.Asset&&echoed.Sequence==33&&echoed.MoveX==.75f&&echoed.Keys==512,"Separate OS processes exchange authenticated framed game input");
        await process.WaitForExitAsync(token);
        Check(process.ExitCode==0,"Separate server shuts down cleanly");
    }
    finally{if(!process.HasExited){process.Kill();await process.WaitForExitAsync();}}
}
Console.WriteLine($"{checks} network/protocol checks passed");

static async Task<NetworkPeer> Accept(CoopListener listener,CancellationToken token)
{
    while(true){if(listener.TryAccept(out var peer))return peer;await Task.Delay(5,token);}
}
static async Task<Packet> Receive(NetworkPeer peer,CancellationToken token)
{
    while(true){if(peer.TryReceive(out var packet))return packet;if(!peer.Connected)throw new IOException(peer.Error??"Peer disconnected");await Task.Delay(5,token);}
}
static async Task SeparateServer()
{
    using var token=new CancellationTokenSource(TimeSpan.FromSeconds(15));
    string code=CoopListener.NewCode();await using var listener=new CoopListener(IPAddress.Loopback,0,"process-build",code);
    Console.WriteLine($"READY {listener.Port} {code}");Console.Out.Flush();
    await using var peer=await Accept(listener,token.Token);var packet=await Receive(peer,token.Token);
    var input=JsonPackets.Decode<InputState>(packet.Data).Validated();
    peer.Send(PacketKind.Asset,JsonPackets.Encode(input));
    await Task.Delay(250,token.Token);
}

sealed class Fragments:Stream
{
    private readonly MemoryStream data;private readonly int chunk;
    public Fragments(byte[] bytes,int chunk){data=new MemoryStream(bytes);this.chunk=chunk;}
    public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;
    public override long Length=>data.Length;public override long Position{get=>data.Position;set=>throw new NotSupportedException();}
    public override int Read(byte[] buffer,int offset,int count)=>data.Read(buffer,offset,Math.Min(chunk,count));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>data.ReadAsync(buffer[..Math.Min(chunk,buffer.Length)],token);
    public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}

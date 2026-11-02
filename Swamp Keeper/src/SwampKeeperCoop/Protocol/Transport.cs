using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace SwampKeeperCoop.Protocol;

public sealed record Packet(PacketKind Kind,byte[] Data);

public static class Frames
{
    public const int Version=1;
    public const uint Magic=0x50434B53; // SKCP
    public static int MaxLength(PacketKind kind)=>kind switch
    {
        PacketKind.Snapshot or PacketKind.Catalog=>4*1024*1024,
        PacketKind.Asset=>8*1024*1024,
        PacketKind.Command=>16384,
        PacketKind.Hello or PacketKind.Welcome or PacketKind.Reject or PacketKind.Input or PacketKind.AssetRequest=>4096,
        PacketKind.Disconnect=>1024,
        PacketKind.Ping or PacketKind.Pong=>128,
        _=>throw new InvalidDataException("Unknown packet kind")
    };
    public static async Task WriteAsync(Stream stream,Packet packet,CancellationToken token)
    {
        if(packet.Data.Length>MaxLength(packet.Kind))throw new InvalidDataException("Packet exceeds size limit");
        var header=new byte[12];BinaryPrimitives.WriteUInt32LittleEndian(header,Magic);
        header[4]=Version;header[5]=(byte)packet.Kind;BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8),packet.Data.Length);
        await stream.WriteAsync(header,token).ConfigureAwait(false);
        if(packet.Data.Length>0)await stream.WriteAsync(packet.Data,token).ConfigureAwait(false);
    }
    public static async Task<Packet?> ReadAsync(Stream stream,CancellationToken token,int maxLength=int.MaxValue)
    {
        var header=new byte[12];
        if(!await ReadAllAsync(stream,header,true,token).ConfigureAwait(false))return null;
        if(BinaryPrimitives.ReadUInt32LittleEndian(header)!=Magic || header[4]!=Version || header[6]!=0 || header[7]!=0)throw new InvalidDataException("Incompatible packet header");
        var kind=(PacketKind)header[5];int length=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
        if(length<0 || length>Math.Min(MaxLength(kind),maxLength))throw new InvalidDataException("Invalid packet length");
        var body=new byte[length];
        if(length>0)await ReadAllAsync(stream,body,false,token).ConfigureAwait(false);
        return new Packet(kind,body);
    }
    private static async Task<bool> ReadAllAsync(Stream stream,Memory<byte> target,bool eofAllowed,CancellationToken token)
    {
        int offset=0;
        while(offset<target.Length)
        {
            int n=await stream.ReadAsync(target[offset..],token).ConfigureAwait(false);
            if(n==0)
            {
                if(offset==0 && eofAllowed)return false;
                throw new EndOfStreamException("Peer closed in the middle of a frame");
            }
            offset+=n;
        }
        return true;
    }
}

// Reader/writer threads handle bytes only. Unity state is touched exclusively by the caller.
public sealed class NetworkPeer:IAsyncDisposable
{
    private readonly TcpClient socket;
    private readonly CancellationTokenSource stop=new();
    private readonly Channel<Packet> incoming=Channel.CreateBounded<Packet>(new BoundedChannelOptions(32){SingleReader=true,SingleWriter=true,FullMode=BoundedChannelFullMode.Wait});
    private readonly Channel<Packet> reliable=Channel.CreateBounded<Packet>(new BoundedChannelOptions(64){SingleReader=true,SingleWriter=false,FullMode=BoundedChannelFullMode.Wait});
    private readonly SemaphoreSlim wake=new(0,1);
    private readonly bool receivesGuest;
    private Packet? latestSnapshot;
    private readonly Task work;
    private int closed;
    private int disposed;
    public string PeerId {get;}
    public string Name {get;}
    public string? Error {get;private set;}
    public bool Connected=>Volatile.Read(ref closed)==0;
    private long bytesSent,bytesReceived;
    public long BytesSent=>Interlocked.Read(ref bytesSent);
    public long BytesReceived=>Interlocked.Read(ref bytesReceived);
    public Task Completion=>work;
    internal NetworkPeer(TcpClient socket,bool receivesGuest,string peerId,string name)
    {
        this.socket=socket;this.receivesGuest=receivesGuest;PeerId=peerId;Name=name;
        socket.NoDelay=true;
        work=Task.Run(RunAsync);
    }
    public bool TryReceive(out Packet packet)=>incoming.Reader.TryRead(out packet!);
    public bool Send(PacketKind kind,ReadOnlySpan<byte> bytes,bool latest=false)
    {
        if(!Connected)return false;
        if(bytes.Length>Frames.MaxLength(kind))throw new InvalidDataException("Packet exceeds size limit");
        var packet=new Packet(kind,bytes.ToArray());
        if(latest)
        {
            if(kind!=PacketKind.Snapshot)throw new ArgumentException("Only world snapshots may be superseded");
            Interlocked.Exchange(ref latestSnapshot,packet);
        }
        else if(!reliable.Writer.TryWrite(packet))return false;
        try{wake.Release();}catch(SemaphoreFullException){}catch(ObjectDisposedException){return false;}
        return true;
    }
    private async Task RunAsync()
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        var read=ReadAsync(linked.Token);var write=WriteAsync(linked.Token);
        try
        {
            var first=await Task.WhenAny(read,write).ConfigureAwait(false);
            await first.ConfigureAwait(false);
        }
        catch(Exception ex) when(ex is IOException or SocketException or InvalidDataException or OperationCanceledException or ObjectDisposedException)
        {if(!stop.IsCancellationRequested && ex is not OperationCanceledException)Error=ex.Message;}
        finally
        {
            Interlocked.Exchange(ref closed,1);linked.Cancel();socket.Dispose();
            incoming.Writer.TryComplete();reliable.Writer.TryComplete();
            try{await Task.WhenAll(read,write).ConfigureAwait(false);}catch{}
        }
    }
    private async Task ReadAsync(CancellationToken token)
    {
        var stream=socket.GetStream();
        while(!token.IsCancellationRequested)
        {
            var packet=await Frames.ReadAsync(stream,token).ConfigureAwait(false);
            if(packet==null)return;
            Interlocked.Add(ref bytesReceived,12+packet.Data.Length);
            bool allowed=receivesGuest
                ?packet.Kind is PacketKind.Input or PacketKind.Command or PacketKind.AssetRequest or PacketKind.Ping or PacketKind.Pong or PacketKind.Disconnect
                :packet.Kind is PacketKind.Snapshot or PacketKind.Catalog or PacketKind.Asset or PacketKind.Ping or PacketKind.Pong or PacketKind.Disconnect;
            if(!allowed)throw new InvalidDataException("Packet not allowed from this peer role");
            await incoming.Writer.WriteAsync(packet,token).ConfigureAwait(false);
        }
    }
    private async Task WriteAsync(CancellationToken token)
    {
        var stream=socket.GetStream();
        while(!token.IsCancellationRequested)
        {
            while(reliable.Reader.TryRead(out var packet)){await Frames.WriteAsync(stream,packet,token).ConfigureAwait(false);Interlocked.Add(ref bytesSent,12+packet.Data.Length);}
            var snapshot=Interlocked.Exchange(ref latestSnapshot,null);
            if(snapshot!=null){await Frames.WriteAsync(stream,snapshot,token).ConfigureAwait(false);Interlocked.Add(ref bytesSent,12+snapshot.Data.Length);}
            await wake.WaitAsync(token).ConfigureAwait(false);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0){await work.ConfigureAwait(false);return;}
        Interlocked.Exchange(ref closed,1);stop.Cancel();socket.Dispose();
        try{await work.ConfigureAwait(false);}finally{stop.Dispose();wake.Dispose();}
    }
}

public sealed class CoopListener:IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly string code,build,session=Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource stop=new();
    private readonly Channel<NetworkPeer> accepted=Channel.CreateBounded<NetworkPeer>(1);
    private readonly Task work;
    private NetworkPeer? active;
    private int disposed;
    private readonly bool rememberPlayer;
    private string? rememberedPlayer;
    public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
    public string SessionId=>session;
    public string? Error {get;private set;}
    public CoopListener(IPAddress address,int port,string build,string code,bool rememberPlayer=false)
    {
        if(string.IsNullOrWhiteSpace(build)||code.Length<8)throw new ArgumentException("Build identity and session code required");
        this.build=build;this.code=code.ToUpperInvariant();this.rememberPlayer=rememberPlayer;
        listener=new TcpListener(address,port);listener.Start(4);work=Task.Run(AcceptAsync);
    }
    public bool TryAccept(out NetworkPeer peer)=>accepted.Reader.TryRead(out peer!);
    private async Task AcceptAsync()
    {
        using var registration=stop.Token.Register(listener.Stop);
        try
        {
            while(!stop.IsCancellationRequested)
            {
                var socket=await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                await AuthenticateAsync(socket,stop.Token).ConfigureAwait(false);
            }
        }
        catch(Exception ex) when(ex is SocketException or ObjectDisposedException or OperationCanceledException)
        {if(!stop.IsCancellationRequested)Error=ex.Message;}
        finally{accepted.Writer.TryComplete();}
    }
    private async Task AuthenticateAsync(TcpClient socket,CancellationToken token)
    {
        bool owned=false;
        try
        {
            socket.NoDelay=true;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var stream=socket.GetStream();var packet=await Frames.ReadAsync(stream,deadline.Token,4096).ConfigureAwait(false);
            if(packet==null||packet.Kind!=PacketKind.Hello)throw new InvalidDataException("Expected handshake");
            var hello=JsonPackets.Decode<Hello>(packet.Data);
            string? rejection=null;
            if(hello.Protocol!=Frames.Version || hello.Build!=build)rejection="Game/mod versions do not match";
            else if(hello.Code==null || hello.Code.Length>128 || !SameCode(hello.Code,code))rejection="Session code is incorrect";
            else if(!Guid.TryParseExact(hello.PeerId,"N",out _) || hello.Name==null || hello.Name.Length is <1 or >40)rejection="Invalid player identity";
            // The same player reconnecting proves the old link is dead (e.g. half-open after a VPN drop); replace it.
            else if(active?.Connected==true && hello.PeerId!=active.PeerId)rejection="Host already has a guest";
            else if(rememberedPlayer!=null && hello.PeerId!=rememberedPlayer)rejection="This run belongs to the original guest. Reconnect from the same installation.";
            if(rejection!=null){await Frames.WriteAsync(stream,new Packet(PacketKind.Reject,JsonPackets.Encode(new Rejection(rejection))),deadline.Token).ConfigureAwait(false);return;}
            await Frames.WriteAsync(stream,new Packet(PacketKind.Welcome,JsonPackets.Encode(new Welcome(Frames.Version,build,session,1))),deadline.Token).ConfigureAwait(false);
            var peer=new NetworkPeer(socket,true,hello.PeerId,hello.Name!);owned=true;
            if(rememberPlayer)rememberedPlayer??=hello.PeerId;
            var old=Interlocked.Exchange(ref active,peer);if(old!=null)await old.DisposeAsync().ConfigureAwait(false);
            while(accepted.Reader.TryPeek(out var stale)&&!stale.Connected)
                if(accepted.Reader.TryRead(out stale))await stale.DisposeAsync().ConfigureAwait(false);
            if(!accepted.Writer.TryWrite(peer)){await peer.DisposeAsync().ConfigureAwait(false);return;}
        }
        catch(Exception ex) when(ex is IOException or SocketException or InvalidDataException or System.Text.Json.JsonException or OperationCanceledException or ArgumentException){}
        finally{if(!owned)socket.Dispose();}
    }
    private static bool SameCode(string supplied,string expected)=>CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied.ToUpperInvariant()),Encoding.UTF8.GetBytes(expected));
    public static string NewCode()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0){await work.ConfigureAwait(false);return;}
        stop.Cancel();listener.Stop();await work.ConfigureAwait(false);
        var peer=Interlocked.Exchange(ref active,null);if(peer!=null)await peer.DisposeAsync().ConfigureAwait(false);
        stop.Dispose();
    }
}

public static class CoopConnector
{
    public static async Task<(NetworkPeer Peer,Welcome Welcome)> ConnectAsync(string host,int port,string build,string code,string peerId,string name,CancellationToken token=default)
    {
        var socket=new TcpClient();bool owned=false;
        try
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(8));
            await socket.ConnectAsync(host,port,deadline.Token).ConfigureAwait(false);socket.NoDelay=true;
            var stream=socket.GetStream();
            await Frames.WriteAsync(stream,new Packet(PacketKind.Hello,JsonPackets.Encode(new Hello(Frames.Version,build,code,peerId,name))),deadline.Token).ConfigureAwait(false);
            var packet=await Frames.ReadAsync(stream,deadline.Token,4096).ConfigureAwait(false);
            if(packet?.Kind==PacketKind.Reject)throw new InvalidOperationException(JsonPackets.Decode<Rejection>(packet.Data).Reason);
            if(packet?.Kind!=PacketKind.Welcome)throw new InvalidDataException("Host did not accept the session");
            var welcome=JsonPackets.Decode<Welcome>(packet.Data);
            if(welcome.Protocol!=Frames.Version || welcome.Build!=build || welcome.ActorId!=1 || !Guid.TryParseExact(welcome.SessionId,"N",out _))throw new InvalidDataException("Invalid host handshake");
            var peer=new NetworkPeer(socket,false,"host","Host");owned=true;return(peer,welcome);
        }
        finally{if(!owned)socket.Dispose();}
    }
}

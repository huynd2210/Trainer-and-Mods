using System.Text.Json;

namespace SwampKeeperCoop.Protocol;

public enum PacketKind:byte { Hello=1,Welcome=2,Reject=3,Input=4,Command=5,Snapshot=6,Catalog=7,AssetRequest=8,Asset=9,Ping=10,Pong=11,Disconnect=12 }
public enum CommandKind { Ready,PrepareItems,SelectSkill,Interact,Restart,ReturnToPreparation }
public sealed record Hello(int Protocol,string Build,string Code,string PeerId,string Name);
public sealed record Welcome(int Protocol,string Build,string SessionId,int ActorId);
public sealed record Rejection(string Reason);

public sealed record InputState
{
    public long Sequence {get;init;}
    public float MoveX {get;init;} public float MoveY {get;init;}
    public float AimX {get;init;} public float AimY {get;init;}
    public float MouseX {get;init;} public float MouseY {get;init;}
    public uint Keys {get;init;}
    public uint Pressed {get;init;} public uint Released {get;init;}
    public int Controller {get;init;}
    public InputState Validated()
    {
        if(Sequence<0 || Controller is <0 or >3 || !float.IsFinite(MoveX)||!float.IsFinite(MoveY)||!float.IsFinite(AimX)||!float.IsFinite(AimY)||!float.IsFinite(MouseX)||!float.IsFinite(MouseY))
            throw new InvalidDataException("Invalid input values");
        float length=MathF.Sqrt(MoveX*MoveX+MoveY*MoveY);
        float aimLength=MathF.Sqrt(AimX*AimX+AimY*AimY);
        // Reject extreme finite inputs before their squared lengths can overflow.
        if(Math.Abs(MoveX)>100 || Math.Abs(MoveY)>100 || Math.Abs(AimX)>100 || Math.Abs(AimY)>100 || Math.Abs(MouseX)>100000 || Math.Abs(MouseY)>100000)
            throw new InvalidDataException("Input outside world bounds");
        return this with{MoveX=length>1?MoveX/length:MoveX,MoveY=length>1?MoveY/length:MoveY,AimX=aimLength>1?AimX/aimLength:AimX,AimY=aimLength>1?AimY/aimLength:AimY,Keys=Keys&((1u<<22)-1),Pressed=Pressed&((1u<<22)-1),Released=Released&((1u<<22)-1)};
    }
}
public sealed record RunCommand(long Sequence,CommandKind Kind,int[]? Items=null,int SkillSlot=0,int SkillId=0,bool Ready=false);
public sealed record ActorState(int Id,float X,float Y,float Z,float AimX,float AimY,int State,int Arrows,int Capacity,bool HasTail,bool Alive,int Kills,int[] Skills,int[] Charges,int[] Items);
public sealed record SpriteState(int Id,int Sprite,int Material,float X,float Y,float Z,float ScaleX,float ScaleY,float Rotation,int SortingLayer,int Order,bool FlipX,bool FlipY,uint Color,string Name="",int DrawMode=0,float SizeX=1,float SizeY=1,int Mask=0);
public sealed record TilemapState(string Path,bool Active,float X,float Y,float Z,float ScaleX,float ScaleY,float Rotation);
public sealed record CameraState(float X,float Y,float Z,float OrthographicSize);
public sealed record WorldState(long Tick,int Generation,string Scene,int Stage,bool Well,float Timer,ActorState[] Actors,SpriteState[] Sprites,TilemapState[] Tilemaps,CameraState Camera,float SessionSeconds=0,string RunId="",bool HostReady=false,bool GuestReady=false,bool GatesDone=false);
public sealed record SpriteDefinition(int Id,string Name,string Texture,float X,float Y,float Width,float Height,float PivotX,float PivotY,float PixelsPerUnit);
public sealed record MaterialDefinition(int Id,string Name,string Shader);
public sealed record WorldCatalog(int Generation,SpriteDefinition[] Sprites,MaterialDefinition[] Materials,string RunId="");
public sealed record SpriteRequest(int[] Ids);
public sealed record SpriteAsset(int Id,float PivotX,float PivotY,float PixelsPerUnit,byte[] Png);

public static class JsonPackets
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=false,MaxDepth=32};
    public static byte[] Encode<T>(T value)=>JsonSerializer.SerializeToUtf8Bytes(value,Options);
    public static T Decode<T>(ReadOnlySpan<byte> bytes)=>JsonSerializer.Deserialize<T>(bytes,Options)??throw new InvalidDataException("Null packet body");
}

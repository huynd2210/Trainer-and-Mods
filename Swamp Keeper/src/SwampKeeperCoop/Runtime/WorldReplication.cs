using Il2CppInterop.Runtime;
using SwampKeeperCoop.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

internal static class AssetKey
{
    internal static string Sprite(Sprite s){var r=s.rect;return s.texture.name+"/"+s.name+"/"+r.x+","+r.y+","+r.width+","+r.height;}
    internal static string Sprite(SpriteDefinition s)=>s.Texture+"/"+s.Name+"/"+s.X+","+s.Y+","+s.Width+","+s.Height;
    internal static string Path(Transform t){string path=t.name;while(t.parent){t=t.parent;path=t.name+"/"+path;}return path;}
    internal static uint Color(Color c)=>((uint)Math.Clamp((int)(c.r*255),0,255))|((uint)Math.Clamp((int)(c.g*255),0,255)<<8)|((uint)Math.Clamp((int)(c.b*255),0,255)<<16)|((uint)Math.Clamp((int)(c.a*255),0,255)<<24);
    internal static Color Color(uint c)=>new((c&255)/255f,((c>>8)&255)/255f,((c>>16)&255)/255f,((c>>24)&255)/255f);
}
// The generated SpriteRenderer.flipX/flipY getters marshal the native 1-byte bool as a 4-byte BOOL, so leftover
// register bytes read as true and every sprite was mirrored on both axes (drawn upside down) on the guest.
internal static class SpriteFlags
{
    private delegate byte GetFlag(IntPtr renderer);
    private static readonly GetFlag flipX=IL2CPP.ResolveICall<GetFlag>("UnityEngine.SpriteRenderer::get_flipX"),flipY=IL2CPP.ResolveICall<GetFlag>("UnityEngine.SpriteRenderer::get_flipY");
    internal static bool FlipX(SpriteRenderer r)=>flipX(r.Pointer)!=0;
    internal static bool FlipY(SpriteRenderer r)=>flipY(r.Pointer)!=0;
}
internal sealed class WorldCapture
{
    private readonly Dictionary<IntPtr,int> spriteIds=new(),materialIds=new(),objectIds=new();
    private readonly List<SpriteDefinition> sprites=new();private readonly List<MaterialDefinition> materials=new();
    private readonly Dictionary<int,Sprite> nativeSprites=new();private readonly Dictionary<int,SpriteAsset> exported=new();
    private bool changed;
    private string capturedRun="";
    internal void ResendCatalog()=>changed=true;
    private int SpriteId(Sprite sprite)
    {
        if(spriteIds.TryGetValue(sprite.Pointer,out int id))return id;
        id=sprites.Count+1;spriteIds.Add(sprite.Pointer,id);nativeSprites.Add(id,sprite);var r=sprite.rect;var p=sprite.pivot;
        sprites.Add(new(id,sprite.name,sprite.texture.name,r.x,r.y,r.width,r.height,p.x,p.y,sprite.pixelsPerUnit));changed=true;return id;
    }
    internal SpriteAsset Export(int id)
    {
        if(exported.TryGetValue(id,out var asset))return asset;
        if(!nativeSprites.TryGetValue(id,out var sprite))throw new InvalidDataException("Unknown sprite requested");
        // Short-lived runtime sprites can be destroyed before the guest's request arrives; nothing will draw them again.
        asset=sprite?SpriteTransfer.Export(id,sprite):SpriteTransfer.Blank(id);exported.Add(id,asset);return asset;
    }
    private int MaterialId(Material material)
    {
        if(!material)return 0;
        if(materialIds.TryGetValue(material.Pointer,out int id))return id;
        id=materials.Count+1;materialIds.Add(material.Pointer,id);materials.Add(new(id,material.name,material.shader.name));changed=true;return id;
    }
    internal (WorldState,WorldCatalog?) Capture(long tick,float sessionSeconds)
    {
        if(capturedRun!=Plugin.RunId)
        {
            capturedRun=Plugin.RunId;spriteIds.Clear();materialIds.Clear();objectIds.Clear();sprites.Clear();materials.Clear();nativeSprites.Clear();exported.Clear();changed=true;
        }
        var states=new List<SpriteState>();
        foreach(var r in Object.FindObjectsOfType<SpriteRenderer>())
        {
            if(!r.enabled || !r.sprite || !r.gameObject.activeInHierarchy)continue;
            if(!objectIds.TryGetValue(r.Pointer,out int id)){id=objectIds.Count+1;objectIds.Add(r.Pointer,id);}
            var t=r.transform;var p=t.position;var s=t.lossyScale;
            states.Add(new(id,SpriteId(r.sprite),MaterialId(r.sharedMaterial),p.x,p.y,p.z,s.x,s.y,t.eulerAngles.z,r.sortingLayerID,r.sortingOrder,SpriteFlags.FlipX(r),SpriteFlags.FlipY(r),AssetKey.Color(r.color),AssetKey.Path(t),(int)r.drawMode,r.size.x,r.size.y,(int)r.maskInteraction));
        }
        var tiles=new List<TilemapState>();
        foreach(var r in Resources.FindObjectsOfTypeAll<TilemapRenderer>())
        {
            if(!r.gameObject.scene.IsValid())continue;
            var t=r.transform;var p=t.position;var s=t.lossyScale;
            tiles.Add(new(AssetKey.Path(t),r.enabled&&r.gameObject.activeInHierarchy,p.x,p.y,p.z,s.x,s.y,t.eulerAngles.z));
        }
        ActorState Actor(Player p,int id)
        {
            var pos=p.transform.position;
            return new(id,pos.x,pos.y,pos.z,1,0,(int)p.state,p.arrowCount,p.arrow.arrowCapacity,p.hasTail,p.state!=PlayerState.Die,p.killCount,p.skills.Select(s=>s.index).ToArray(),p.skills.Select(s=>s.current).ToArray(),Enumerable.Range(0,p.items.Count).Select(i=>p.items[i].index).ToArray());
        }
        var camera=Camera.main;var cp=camera.transform.position;var gm=GameManager.gm;var stage=gm.current;
        var world=new WorldState(tick,RunTransitions.Generation,SceneManager.GetActiveScene().name,stage?stage.stageIndex:-1,stage && stage.Pointer==gm.well.Pointer,stage?stage.timer:0,new[]{Actor(Plugin.Host!,0),Actor(Plugin.Remote!,1)},states.ToArray(),tiles.ToArray(),new(cp.x,cp.y,cp.z,camera.orthographicSize),sessionSeconds,Plugin.RunId,Plugin.Driver?.HostReady??false,Plugin.Driver?.GuestReady??false,Plugin.Driver?.GatesPassed??false);
        WorldCatalog? catalog=changed?new(RunTransitions.Generation,sprites.ToArray(),materials.ToArray(),Plugin.RunId):null;changed=false;
        return(world,catalog);
    }
}
internal sealed class WorldMirror
{
    private readonly CoopHud hud=new();
    private readonly Dictionary<string,Sprite> localSprites=new();private readonly Dictionary<string,Material> localMaterials=new();
    private readonly Dictionary<int,Sprite> sprites=new();private readonly Dictionary<int,Material> materials=new();
    private readonly Dictionary<int,SpriteRenderer> replicas=new();private readonly Dictionary<string,TilemapRenderer> tilemaps=new();
    private readonly Dictionary<int,SpriteState> last=new();private readonly HashSet<int> missing=new();
    private readonly HashSet<int> requested=new();
    private bool transferTestForced;
    private string catalogRun="";
    internal bool TeamDown {get;private set;}
    internal ActorState? Guest {get;private set;}
    internal float SessionSeconds {get;private set;}
    internal int Stage {get;private set;}=-999;
    internal int Count=>last.Count;internal int Missing=>missing.Count;
    internal void Initialize()
    {
        foreach(var sprite in Resources.FindObjectsOfTypeAll<Sprite>())if(sprite.texture)localSprites.TryAdd(AssetKey.Sprite(sprite),sprite);
        foreach(var material in Resources.FindObjectsOfTypeAll<Material>())localMaterials.TryAdd(material.name,material);
        foreach(var r in Resources.FindObjectsOfTypeAll<TilemapRenderer>())if(r.gameObject.scene.IsValid())tilemaps[AssetKey.Path(r.transform)]=r;
        Plugin.Record("Local asset catalog: sprites="+localSprites.Count+" materials="+localMaterials.Count+" tilemaps="+tilemaps.Count);
    }
    internal void Catalog(WorldCatalog catalog)
    {
        if(catalogRun!=catalog.RunId)
        {
            catalogRun=catalog.RunId;
            foreach(var r in replicas.Values)if(r)Object.Destroy(r.gameObject);
            replicas.Clear();sprites.Clear();materials.Clear();missing.Clear();requested.Clear();last.Clear();
        }
        foreach(var s in catalog.Sprites)
        {
            if(localSprites.TryGetValue(AssetKey.Sprite(s),out var sprite)){sprites[s.Id]=sprite;missing.Remove(s.Id);}
            else if(!sprites.ContainsKey(s.Id))missing.Add(s.Id);
        }
        foreach(var m in catalog.Materials)if(localMaterials.TryGetValue(m.Name,out var mat))materials[m.Id]=mat;
        if(Plugin.ProgressionTest && !transferTestForced)
        {
            var sample=catalog.Sprites.First(s=>s.Width<128 && s.Height<128);
            localSprites.Remove(AssetKey.Sprite(sample));sprites.Remove(sample.Id);missing.Add(sample.Id);transferTestForced=true;
            Plugin.Record("Exercising asset fallback for sprite "+sample.Id+" "+sample.Name);
        }
    }
    // Requests in flight when the link dropped were never answered; ask again on the new link.
    internal void Rerequest()=>requested.Clear();
    internal int[] RequestMissing()
    {
        var ids=missing.Where(id=>!requested.Contains(id)).Take(32).ToArray();foreach(int id in ids)requested.Add(id);return ids;
    }
    internal void Asset(SpriteAsset asset)
    {
        if(!requested.Contains(asset.Id))throw new InvalidDataException("Unsolicited sprite asset");
        if(!sprites.ContainsKey(asset.Id))sprites[asset.Id]=SpriteTransfer.Import(asset);
        missing.Remove(asset.Id);Plugin.Record("Guest imported native sprite "+asset.Id);
    }
    internal void Apply(WorldState world)
    {
        if(world.RunId!=catalogRun)return;
        if(world.Actors.Length!=2 || world.Sprites.Length>100000)throw new InvalidDataException("Invalid authoritative world");
        TeamDown=world.Actors.All(a=>!a.Alive);
        Guest=world.Actors.Single(a=>a.Id==1);
        if(!string.IsNullOrEmpty(world.RunId))SwampKeeperMods.CoopIntegration.ReceiveKills(world.RunId,Guest.Kills);
        SessionSeconds=world.SessionSeconds;
        if(Stage!=world.Stage){Stage=world.Stage;Plugin.Record("Guest received stage="+Stage+" generation="+world.Generation);}
        hud.Apply(world);
        foreach(var r in replicas.Values)r.enabled=false;
        last.Clear();
        foreach(var state in world.Sprites)
        {
            if(!sprites.TryGetValue(state.Sprite,out var sprite)){missing.Add(state.Sprite);continue;}
            if(!replicas.TryGetValue(state.Id,out var r))
            {
                var obj=new GameObject("CoopReplica_"+state.Id);r=obj.AddComponent<SpriteRenderer>();replicas.Add(state.Id,r);
            }
            r.enabled=true;r.sprite=sprite;
            if(materials.TryGetValue(state.Material,out var material))r.sharedMaterial=material;
            var t=r.transform;t.position=new(state.X,state.Y,state.Z);t.localScale=new(state.ScaleX,state.ScaleY,1);t.rotation=Quaternion.Euler(0,0,state.Rotation);
            r.sortingLayerID=state.SortingLayer;r.sortingOrder=state.Order;r.flipX=state.FlipX;r.flipY=state.FlipY;r.color=AssetKey.Color(state.Color);last[state.Id]=state;
            r.drawMode=(SpriteDrawMode)state.DrawMode;r.size=new(state.SizeX,state.SizeY);
        }
        foreach(var r in tilemaps.Values)r.enabled=false;
        foreach(var state in world.Tilemaps)
        {
            if(!state.Active || !tilemaps.TryGetValue(state.Path,out var r))continue;
            r.enabled=true;var t=r.transform;t.position=new(state.X,state.Y,state.Z);t.rotation=Quaternion.Euler(0,0,state.Rotation);
        }
        var camera=Camera.main;
        var follow=Guest.Alive?Guest:world.Actors.FirstOrDefault(a=>a.Alive)??Guest;
        if(camera){camera.orthographicSize=world.Camera.OrthographicSize;camera.transform.position=new(follow.X,follow.Y,world.Camera.Z);}
    }
    internal void Verify()
    {
        File.WriteAllBytes(System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"replica-debug.json"),JsonPackets.Encode(last.Values.ToArray()));
        if(last.Count<10 || Guest==null || missing.Count>0)throw new InvalidOperationException("Incomplete world replication: rendered="+last.Count+" missing="+missing.Count);
        if(last.Values.All(s=>s.FlipY))throw new InvalidOperationException("Every replicated sprite is flipped vertically; the guest would draw the world upside down");
        foreach(var pair in last)
        {
            var p=replicas[pair.Key].transform.position;var s=pair.Value;
            if(Vector3.Distance(p,new(s.X,s.Y,s.Z))>.001f)throw new InvalidOperationException("Replica diverged from authoritative position");
        }
    }
}

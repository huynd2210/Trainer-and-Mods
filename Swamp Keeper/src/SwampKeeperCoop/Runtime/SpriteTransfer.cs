using System.Buffers.Binary;
using SwampKeeperCoop.Protocol;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

internal static class SpriteTransfer
{
    internal static SpriteAsset Export(int id,Sprite sprite)
    {
        var rect=sprite.textureRect;
        int width=Mathf.RoundToInt(rect.width),height=Mathf.RoundToInt(rect.height);
        if(width<1 || height<1 || width>2048 || height>2048)throw new InvalidDataException("Sprite exceeds transfer bounds: "+sprite.name);
        var source=sprite.texture;var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        Texture2D? readable=null;
        try
        {
            rt.filterMode=FilterMode.Point;
            Graphics.Blit(source,rt,new Vector2(rect.width/source.width,rect.height/source.height),new Vector2(rect.x/source.width,rect.y/source.height));
            RenderTexture.active=rt;readable=new Texture2D(width,height,TextureFormat.RGBA32,false);
            readable.ReadPixels(new Rect(0,0,width,height),0,0);readable.Apply();
            var bytes=ImageConversion.EncodeToPNG(readable).ToArray();
            var pivot=sprite.pivot-sprite.textureRectOffset;
            Plugin.Record("Transferred missing native sprite "+id+" "+sprite.name+" "+width+"x"+height+" "+bytes.Length+" bytes");
            return new(id,pivot.x/width,pivot.y/height,sprite.pixelsPerUnit,bytes);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(readable)Object.Destroy(readable);}
    }
    internal static SpriteAsset Blank(int id)
    {
        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try{texture.SetPixel(0,0,new Color(0,0,0,0));texture.Apply();return new(id,.5f,.5f,100,ImageConversion.EncodeToPNG(texture).ToArray());}
        finally{Object.Destroy(texture);}
    }
    internal static Sprite Import(SpriteAsset asset)
    {
        var bytes=asset.Png;
        if(bytes==null || bytes.Length<33 || bytes.Length>4*1024*1024 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Invalid sprite PNG");
        int width=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)),height=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20));
        if(width<1 || height<1 || width>2048 || height>2048 || !float.IsFinite(asset.PivotX) || !float.IsFinite(asset.PivotY) || Math.Abs(asset.PivotX)>10 || Math.Abs(asset.PivotY)>10 || !float.IsFinite(asset.PixelsPerUnit) || asset.PixelsPerUnit<.01f || asset.PixelsPerUnit>10000)
            throw new InvalidDataException("Invalid sprite dimensions/pivot");
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
        if(!ImageConversion.LoadImage(texture,bytes,false)){Object.Destroy(texture);throw new InvalidDataException("Could not decode native sprite");}
        var sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(asset.PivotX,asset.PivotY),asset.PixelsPerUnit);sprite.name="CoopTransferred_"+asset.Id;
        return sprite;
    }
}

using System.Buffers.Binary;
using System.IO.Compression;

namespace SwampKeeperCoop.Protocol;

// Full snapshots are independently decodable, so the transport can discard stale ones.
// Bound the decoded size before allocation; a small compressed frame cannot expand forever.
public static class WorldPackets
{
    public const int MaxDecodedLength=4*1024*1024;
    public static byte[] Encode<T>(T value)
    {
        var json=JsonPackets.Encode(value);
        if(json.Length>MaxDecodedLength)throw new InvalidDataException("World exceeds decoded size limit");
        using var stream=new MemoryStream();
        var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,json.Length);stream.Write(header);
        using(var compressed=new BrotliStream(stream,CompressionLevel.Fastest,true))compressed.Write(json);
        return stream.ToArray();
    }
    public static T Decode<T>(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<5)throw new InvalidDataException("Truncated compressed world");
        int length=BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if(length<2 || length>MaxDecodedLength)throw new InvalidDataException("Invalid decoded world size");
        var json=new byte[length];
        if(!BrotliDecoder.TryDecompress(bytes[4..],json,out int written) || written!=length)throw new InvalidDataException("Invalid compressed world");
        return JsonPackets.Decode<T>(json);
    }
}

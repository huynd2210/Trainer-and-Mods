using System.Text;
using System.Security.Cryptography;

namespace SkullHordeTrainer;

internal enum LuaType : byte
{
    Boolean = 1,
    Number = 3,
    String = 4,
    Table = 5,
    UserData = 7,
    NegativeNumber = 64,
    Hash = 65,
}

internal sealed class LuaKey
{
    public required LuaType Type { get; init; }
    public required object Value { get; init; }

    public string Display => Type switch
    {
        LuaType.String => (string)Value,
        LuaType.Number => ((uint)Value).ToString(),
        LuaType.NegativeNumber => $"-{(uint)Value}",
        LuaType.Hash => $"hash:{(ulong)Value}",
        _ => Value.ToString() ?? "?",
    };
}

internal sealed class LuaValue
{
    public required LuaType Type { get; set; }
    public required object Value { get; set; }

    public string Display => Type switch
    {
        LuaType.Boolean => ((bool)Value) ? "true" : "false",
        LuaType.Number => ((double)Value).ToString("0.###############"),
        LuaType.String => (string)Value,
        LuaType.Table => $"{((LuaTable)Value).Entries.Count} entries",
        LuaType.UserData => "binary userdata",
        _ => Value.ToString() ?? string.Empty,
    };
}

internal sealed record LuaUserData(byte SubType, byte[] Bytes);
internal sealed record LuaEntry(LuaKey Key, LuaValue Value);

internal sealed class LuaTable
{
    public List<LuaEntry> Entries { get; } = [];

    public LuaEntry? Find(string key) => Entries.FirstOrDefault(
        entry => entry.Key.Type == LuaType.String &&
                 string.Equals((string)entry.Key.Value, key, StringComparison.Ordinal));

    public LuaTable? GetTable(string key) => Find(key)?.Value is { Type: LuaType.Table } value
        ? (LuaTable)value.Value
        : null;

    public LuaTable GetOrCreateTable(string key)
    {
        LuaEntry? existing = Find(key);
        if (existing is not null)
        {
            if (existing.Value.Type != LuaType.Table)
                throw new InvalidDataException($"'{key}' exists but is not a table.");
            return (LuaTable)existing.Value.Value;
        }

        var table = new LuaTable();
        Entries.Add(new LuaEntry(
            new LuaKey { Type = LuaType.String, Value = key },
            new LuaValue { Type = LuaType.Table, Value = table }));
        return table;
    }

    public double? GetNumber(string key) => Find(key)?.Value is { Type: LuaType.Number } value
        ? (double)value.Value
        : null;

    public void SetNumber(string key, double number)
    {
        if (!double.IsFinite(number))
            throw new ArgumentOutOfRangeException(nameof(number));

        LuaEntry? existing = Find(key);
        if (existing is not null)
        {
            existing.Value.Type = LuaType.Number;
            existing.Value.Value = number;
            return;
        }

        Entries.Add(new LuaEntry(
            new LuaKey { Type = LuaType.String, Value = key },
            new LuaValue { Type = LuaType.Number, Value = number }));
    }

    public void SetBoolean(string key, bool state)
    {
        SetValue(key, LuaType.Boolean, state);
    }

    public void SetString(string key, string text)
    {
        SetValue(key, LuaType.String, text);
    }

    private void SetValue(string key, LuaType type, object value)
    {
        LuaEntry? existing = Find(key);
        if (existing is not null)
        {
            existing.Value.Type = type;
            existing.Value.Value = value;
            return;
        }

        Entries.Add(new LuaEntry(
            new LuaKey { Type = LuaType.String, Value = key },
            new LuaValue { Type = type, Value = value }));
    }

    public void AppendArrayTable(LuaTable value)
    {
        uint next = Entries
            .Where(e => e.Key.Type == LuaType.Number)
            .Select(e => (uint)e.Key.Value)
            .DefaultIfEmpty(0u)
            .Max() + 1;
        Entries.Add(new LuaEntry(
            new LuaKey { Type = LuaType.Number, Value = next },
            new LuaValue { Type = LuaType.Table, Value = value }));
    }

    public int SetBooleanLeaves(bool value)
    {
        int changed = 0;
        foreach (LuaEntry entry in Entries)
        {
            if (entry.Value.Type == LuaType.Boolean)
            {
                if ((bool)entry.Value.Value != value)
                {
                    entry.Value.Value = value;
                    changed++;
                }
            }
            else if (entry.Value.Type == LuaType.Table)
            {
                changed += ((LuaTable)entry.Value.Value).SetBooleanLeaves(value);
            }
        }
        return changed;
    }
}

internal sealed record DefoldSave(uint Version, LuaTable Root);

internal static class DefoldSaveCodec
{
    private const uint Magic = 0x42544448; // HDTB in little endian

    public static DefoldSave Read(string path) => Parse(File.ReadAllBytes(path));

    public static DefoldSave Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("This is not a Defold serialized-table save.");

        uint version = reader.ReadUInt32();
        if (version is not (4 or 5))
            throw new InvalidDataException($"Unsupported Defold save version {version}.");

        LuaTable root = ReadTable(reader);
        if (stream.Position != stream.Length)
            throw new InvalidDataException($"Save has {stream.Length - stream.Position} unread bytes.");
        return new DefoldSave(version, root);
    }

    public static byte[] Serialize(DefoldSave save)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(save.Version);
        WriteTable(writer, save.Root);
        writer.Flush();
        return stream.ToArray();
    }

    public static void SelfTest(string path)
    {
        byte[] original = File.ReadAllBytes(path);
        DefoldSave parsed = Parse(original);
        byte[] reproduced = Serialize(parsed);
        if (!original.AsSpan().SequenceEqual(reproduced))
        {
            int difference = FirstDifference(original, reproduced);
            throw new InvalidDataException(
                $"Round-trip differs at byte {difference}; original={original.Length}, reproduced={reproduced.Length}.");
        }
        _ = Parse(reproduced);
    }

    public static void MutationSelfTest(string path)
    {
        byte[] original = File.ReadAllBytes(path);
        byte[] originalHash = SHA256.HashData(original);
        DefoldSave parsed = Parse(original);
        LuaTable savefile = parsed.Root.GetTable("savefile")
            ?? throw new InvalidDataException("Missing savefile table.");

        LuaTable points = savefile.GetOrCreateTable("skill_points");
        points.SetNumber("trainer_test_character", 12_345);
        LuaTable achievements = savefile.GetOrCreateTable("achievements");
        achievements.SetBooleanLeaves(true);

        byte[] mutated = Serialize(parsed);
        DefoldSave verified = Parse(mutated);
        LuaTable verifiedSave = verified.Root.GetTable("savefile")
            ?? throw new InvalidDataException("Mutation lost the savefile table.");
        double? value = verifiedSave.GetTable("skill_points")?.GetNumber("trainer_test_character");
        if (value != 12_345)
            throw new InvalidDataException("Added numeric progression value did not survive serialization.");

        byte[] afterHash = SHA256.HashData(File.ReadAllBytes(path));
        if (!originalHash.AsSpan().SequenceEqual(afterHash))
            throw new IOException("The read-only mutation test unexpectedly changed the source save.");
    }

    private static int FirstDifference(byte[] left, byte[] right)
    {
        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
            if (left[i] != right[i]) return i;
        return count;
    }

    private static LuaTable ReadTable(BinaryReader reader)
    {
        uint count = reader.ReadUInt32();
        var table = new LuaTable();
        if (count > 1_000_000)
            throw new InvalidDataException($"Unreasonable table size: {count}.");

        for (uint i = 0; i < count; i++)
        {
            LuaType keyType = (LuaType)reader.ReadByte();
            LuaType valueType = (LuaType)reader.ReadByte();
            LuaKey key = ReadKey(reader, keyType);
            LuaValue value = ReadValue(reader, valueType);
            table.Entries.Add(new LuaEntry(key, value));
        }
        return table;
    }

    private static LuaKey ReadKey(BinaryReader reader, LuaType type) => type switch
    {
        LuaType.String => new LuaKey { Type = type, Value = ReadString(reader) },
        LuaType.Number or LuaType.NegativeNumber => new LuaKey { Type = type, Value = reader.ReadUInt32() },
        LuaType.Hash => new LuaKey { Type = type, Value = reader.ReadUInt64() },
        _ => throw new InvalidDataException($"Unsupported key type {(byte)type}."),
    };

    private static LuaValue ReadValue(BinaryReader reader, LuaType type)
    {
        object value = type switch
        {
            LuaType.Boolean => reader.ReadByte() != 0,
            LuaType.Number => ReadNumber(reader),
            LuaType.String => ReadString(reader),
            LuaType.Table => ReadTable(reader),
            LuaType.UserData => ReadUserData(reader),
            _ => throw new InvalidDataException($"Unsupported value type {(byte)type}."),
        };
        return new LuaValue { Type = type, Value = value };
    }

    private static double ReadNumber(BinaryReader reader)
    {
        AlignForRead(reader.BaseStream, 4);
        return reader.ReadDouble();
    }

    private static string ReadString(BinaryReader reader)
    {
        uint length = reader.ReadUInt32();
        if (length > int.MaxValue || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException($"Invalid string length {length}.");
        byte[] data = reader.ReadBytes((int)length);
        return new UTF8Encoding(false, true).GetString(data);
    }

    private static LuaUserData ReadUserData(BinaryReader reader)
    {
        byte subtype = reader.ReadByte();
        AlignForRead(reader.BaseStream, 4);
        int size = subtype switch
        {
            0 => 12, // vector3
            1 => 16, // vector4
            2 => 16, // quaternion
            3 => 64, // matrix4
            4 => 8,  // hash
            5 => 24, // URL (three 64-bit hashes)
            _ => throw new InvalidDataException($"Unsupported userdata subtype {subtype}."),
        };
        return new LuaUserData(subtype, reader.ReadBytes(size));
    }

    private static void WriteTable(BinaryWriter writer, LuaTable table)
    {
        writer.Write((uint)table.Entries.Count);
        foreach (LuaEntry entry in table.Entries)
        {
            writer.Write((byte)entry.Key.Type);
            writer.Write((byte)entry.Value.Type);
            WriteKey(writer, entry.Key);
            WriteValue(writer, entry.Value);
        }
    }

    private static void WriteKey(BinaryWriter writer, LuaKey key)
    {
        switch (key.Type)
        {
            case LuaType.String:
                WriteString(writer, (string)key.Value);
                break;
            case LuaType.Number:
            case LuaType.NegativeNumber:
                writer.Write((uint)key.Value);
                break;
            case LuaType.Hash:
                writer.Write((ulong)key.Value);
                break;
            default:
                throw new InvalidDataException($"Unsupported key type {(byte)key.Type}.");
        }
    }

    private static void WriteValue(BinaryWriter writer, LuaValue value)
    {
        switch (value.Type)
        {
            case LuaType.Boolean:
                writer.Write((byte)((bool)value.Value ? 1 : 0));
                break;
            case LuaType.Number:
                AlignForWrite(writer, 4);
                writer.Write((double)value.Value);
                break;
            case LuaType.String:
                WriteString(writer, (string)value.Value);
                break;
            case LuaType.Table:
                WriteTable(writer, (LuaTable)value.Value);
                break;
            case LuaType.UserData:
                var data = (LuaUserData)value.Value;
                writer.Write(data.SubType);
                AlignForWrite(writer, 4);
                writer.Write(data.Bytes);
                break;
            default:
                throw new InvalidDataException($"Unsupported value type {(byte)value.Type}.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    private static void AlignForRead(Stream stream, int alignment)
    {
        long aligned = (stream.Position + alignment - 1) & ~(alignment - 1);
        if (aligned > stream.Length)
            throw new EndOfStreamException();
        stream.Position = aligned;
    }

    private static void AlignForWrite(BinaryWriter writer, int alignment)
    {
        while (writer.BaseStream.Position % alignment != 0)
            writer.Write((byte)0);
    }
}

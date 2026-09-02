using System.Text;

namespace TearsOfMetal.Trainer;

internal sealed class SaveGame
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private const string EntrySeparator = "\f\v";
    private const string ValueMarker = ", mscorlib:";

    private readonly List<string> _entries;
    private readonly bool _hasBom;

    private SaveGame(string path, List<string> entries, bool hasBom)
    {
        Path = path;
        _entries = entries;
        _hasBom = hasBom;
    }

    public static string DefaultSavePath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(
                local,
                "..",
                "LocalLow",
                "Paper Cult",
                "Tears of Metal",
                "SaveData_ToM.txt"));
        }
    }

    public string Path { get; }
    public int EntryCount => _entries.Count;

    public static SaveGame Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The Tears of Metal save file was not found.", path);
        }

        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        var offset = hasBom ? Utf8Bom.Length : 0;
        var payload = bytes.AsSpan(offset).ToArray();

        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] ^= 0x01;
        }

        var decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(payload);

        if (!decoded.StartsWith("SAVE_FILE:", StringComparison.Ordinal))
        {
            throw new InvalidDataException("This file is not a recognized Tears of Metal save.");
        }

        var entries = decoded.Split(EntrySeparator, StringSplitOptions.None).ToList();
        if (entries.Count < 10)
        {
            throw new InvalidDataException("The save contains too few entries and may be damaged.");
        }

        return new SaveGame(path, entries, hasBom);
    }

    public int GetInt32(string key)
    {
        var (index, entry) = FindEntry(key);
        _ = index;
        var value = ExtractValue(entry);

        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidDataException($"Save field '{key}' does not contain a valid 32-bit integer.");
        }

        return parsed;
    }

    public void SetInt32(string key, int value)
    {
        var (index, entry) = FindEntry(key);
        if (!entry.StartsWith($"{key}:System.Int32", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Save field '{key}' is not a 32-bit integer.");
        }

        var markerIndex = entry.IndexOf(ValueMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidDataException($"Save field '{key}' has an unrecognized format.");
        }

        _entries[index] = entry[..(markerIndex + ValueMarker.Length)] + value;
    }

    public byte[] Encode()
    {
        var decoded = string.Join(EntrySeparator, _entries);
        var payload = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(decoded);

        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] ^= 0x01;
        }

        if (!_hasBom)
        {
            return payload;
        }

        var result = new byte[Utf8Bom.Length + payload.Length];
        Utf8Bom.CopyTo(result, 0);
        payload.CopyTo(result, Utf8Bom.Length);
        return result;
    }

    public string Save(bool createBackup)
    {
        string? backupPath = null;
        if (createBackup)
        {
            backupPath = CreateBackup();
        }

        var tempPath = $"{Path}.trainer.tmp";
        try
        {
            File.WriteAllBytes(tempPath, Encode());
            _ = Load(tempPath);
            File.Move(tempPath, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return backupPath ?? string.Empty;
    }

    public string CreateBackup()
    {
        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new InvalidOperationException("The save directory could not be determined.");
        var name = System.IO.Path.GetFileNameWithoutExtension(Path);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var backupPath = System.IO.Path.Combine(directory, $"{name}.trainer-backup.{timestamp}.txt");
        File.Copy(Path, backupPath, overwrite: false);
        return backupPath;
    }

    public static void RestoreBackup(string backupPath, string destinationPath)
    {
        _ = Load(backupPath);
        File.Copy(backupPath, destinationPath, overwrite: true);
        _ = Load(destinationPath);
    }

    private (int Index, string Entry) FindEntry(string key)
    {
        var prefix = $"{key}:";
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].StartsWith(prefix, StringComparison.Ordinal))
            {
                return (index, _entries[index]);
            }
        }

        throw new KeyNotFoundException($"Save field '{key}' was not found. The game version may be unsupported.");
    }

    private static string ExtractValue(string entry)
    {
        var markerIndex = entry.IndexOf(ValueMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidDataException("The save entry has an unrecognized format.");
        }

        return entry[(markerIndex + ValueMarker.Length)..];
    }
}

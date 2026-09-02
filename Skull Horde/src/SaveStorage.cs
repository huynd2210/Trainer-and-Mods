using System.Globalization;

namespace SkullHordeTrainer;

internal sealed record RestoreResult(string RestoredBackup, string SafetyBackup);

internal static class SaveStorage
{
    public static string WriteWithBackup(DefoldSave save, string saveFile, string backupDirectory)
    {
        if (!File.Exists(saveFile))
            throw new FileNotFoundException("The save file no longer exists.", saveFile);

        Directory.CreateDirectory(backupDirectory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string backup = Path.Combine(backupDirectory, $"skull_horde_savefile.{stamp}.bak");
        File.Copy(saveFile, backup, overwrite: false);

        string temp = saveFile + $".trainer.{Guid.NewGuid():N}.tmp";
        try
        {
            byte[] data = DefoldSaveCodec.Serialize(save);
            _ = DefoldSaveCodec.Parse(data);
            File.WriteAllBytes(temp, data);
            File.Move(temp, saveFile, overwrite: true);
            DefoldSaveCodec.SelfTest(saveFile);
            return backup;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static RestoreResult RestoreLatest(string saveFile, string backupDirectory)
    {
        string latest = Directory.Exists(backupDirectory)
            ? Directory.GetFiles(backupDirectory, "*.bak")
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                ?? throw new FileNotFoundException("No trainer backups exist yet.")
            : throw new DirectoryNotFoundException("No trainer backups exist yet.");

        _ = DefoldSaveCodec.Read(latest);
        string safety = Path.Combine(backupDirectory,
            $"before_restore.{DateTime.Now:yyyyMMdd_HHmmss_fff}.bak");
        File.Copy(saveFile, safety, overwrite: false);
        string temp = saveFile + $".trainer.restore.{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(latest, temp, overwrite: false);
            File.Move(temp, saveFile, overwrite: true);
            DefoldSaveCodec.SelfTest(saveFile);
            return new RestoreResult(latest, safety);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static void WorkflowSelfTest(string sourceSave)
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "SkullHordeTrainerTests", Guid.NewGuid().ToString("N"));
        string testSave = Path.Combine(testRoot, "skull_horde_savefile");
        string backups = Path.Combine(testRoot, "trainer_backups");
        Directory.CreateDirectory(testRoot);
        try
        {
            byte[] original = File.ReadAllBytes(sourceSave);
            File.WriteAllBytes(testSave, original);
            DefoldSave edited = DefoldSaveCodec.Read(testSave);
            LuaTable savefile = edited.Root.GetTable("savefile")
                ?? throw new InvalidDataException("Missing savefile table.");
            savefile.GetOrCreateTable("skill_points").SetNumber("workflow_test", 54_321);

            string backup = WriteWithBackup(edited, testSave, backups);
            if (!File.ReadAllBytes(backup).AsSpan().SequenceEqual(original))
                throw new IOException("The pre-write backup does not match the original save.");
            double? written = DefoldSaveCodec.Read(testSave).Root.GetTable("savefile")?
                .GetTable("skill_points")?.GetNumber("workflow_test");
            if (written != 54_321)
                throw new InvalidDataException("The tested write did not preserve the mutation.");

            RestoreResult result = RestoreLatest(testSave, backups);
            if (!File.ReadAllBytes(testSave).AsSpan().SequenceEqual(original))
                throw new IOException("Restore did not reproduce the original save.");
            if (!File.Exists(result.SafetyBackup))
                throw new IOException("Restore did not create its safety backup.");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }
}

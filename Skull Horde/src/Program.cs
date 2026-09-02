namespace SkullHordeTrainer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--unlock-everything", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (System.Diagnostics.Process.GetProcessesByName("SkullHorde").Length > 0)
                    throw new InvalidOperationException("Close Skull Horde before unlocking the save.");
                DefoldSave save = DefoldSaveCodec.Read(TrainerPaths.SaveFile);
                SaveEdits.UnlockEverything(save);
                string backup = SaveStorage.WriteWithBackup(save, TrainerPaths.SaveFile, TrainerPaths.BackupDirectory);
                Console.WriteLine($"PASS: unlocked all known content and characters. Backup: {backup}");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex}");
                Environment.ExitCode = 1;
            }
            return;
        }

        if (args.Length > 0 && args[0].Equals("--verify-unlocked", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string path = args.Length > 1 ? args[1] : TrainerPaths.SaveFile;
                SaveEdits.VerifyEverythingUnlocked(DefoldSaveCodec.Read(path));
                Console.WriteLine($"PASS: all known content, achievements, and five characters are unlocked in {path}.");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex}");
                Environment.ExitCode = 1;
            }
            return;
        }

        if (args.Length > 0 && (args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("--full-mod-test", StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("--mutation-test", StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("--workflow-test", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                string path = args.Length > 1 ? args[1] : TrainerPaths.SaveFile;
                if (args[0].Equals("--full-mod-test", StringComparison.OrdinalIgnoreCase))
                {
                    SaveEdits.SelfTest(path);
                    Console.WriteLine($"PASS: full unlock and unlimited-skill edits serialize and re-parse without touching {path}.");
                }
                else if (args[0].Equals("--workflow-test", StringComparison.OrdinalIgnoreCase))
                {
                    SaveStorage.WorkflowSelfTest(path);
                    Console.WriteLine($"PASS: backup, write, validation, and restore workflow using a temporary copy of {path}.");
                }
                else if (args[0].Equals("--mutation-test", StringComparison.OrdinalIgnoreCase))
                {
                    DefoldSaveCodec.MutationSelfTest(path);
                    Console.WriteLine($"PASS: mutations serialize and re-parse without touching {path}.");
                }
                else
                {
                    DefoldSaveCodec.SelfTest(path);
                    Console.WriteLine($"PASS: parsed and reproduced {path} byte-for-byte.");
                }
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex}");
                Environment.ExitCode = 1;
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal static class TrainerPaths
{
    public static string SaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "8bitskull_skull_horde");

    public static string SaveFile => Path.Combine(SaveDirectory, "skull_horde_savefile");
    public static string BackupDirectory => Path.Combine(SaveDirectory, "trainer_backups");
}

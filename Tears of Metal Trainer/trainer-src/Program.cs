using System.Diagnostics;

namespace TearsOfMetal.Trainer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunSelfTest(args.Skip(1).FirstOrDefault());
            return;
        }

        if (args.Length > 0 && args[0].Equals("--patch-self-test", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunPatchSelfTest(args.Skip(1).FirstOrDefault());
            return;
        }

        if (args.Length > 0 && args[0].Equals("--patch-status", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunPatchStatus(args.Skip(1).FirstOrDefault());
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrainerForm());
    }

    private static int RunSelfTest(string? requestedPath)
    {
        try
        {
            var sourcePath = requestedPath ?? SaveGame.DefaultSavePath;
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var source = SaveGame.Load(sourcePath);
            var roundTrip = source.Encode();

            if (!sourceBytes.SequenceEqual(roundTrip))
            {
                throw new InvalidDataException("A no-change decode/encode did not reproduce the original save.");
            }

            var testRoot = Path.Combine(Path.GetTempPath(), $"tom-trainer-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);
            var testPath = Path.Combine(testRoot, "SaveData_ToM.txt");
            File.Copy(sourcePath, testPath);

            try
            {
                var testSave = SaveGame.Load(testPath);
                var original = testSave.GetInt32("Wallet_COIN");
                var testValue = original == int.MaxValue ? original - 1 : original + 1;
                testSave.SetInt32("Wallet_COIN", testValue);
                testSave.Save(createBackup: true);

                var reloaded = SaveGame.Load(testPath);
                if (reloaded.GetInt32("Wallet_COIN") != testValue)
                {
                    throw new InvalidDataException("The edited currency did not survive a save reload.");
                }

                if (!Directory.EnumerateFiles(testRoot, "*.trainer-backup.*.txt").Any())
                {
                    throw new InvalidDataException("The automatic backup was not created.");
                }
            }
            finally
            {
                Directory.Delete(testRoot, recursive: true);
            }

            Console.WriteLine($"PASS: validated {source.EntryCount:N0} save entries, byte-perfect round trip, edit, reload, and backup.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
    }

    private static int RunPatchSelfTest(string? requestedProcessId)
    {
        try
        {
            var processId = string.IsNullOrWhiteSpace(requestedProcessId)
                ? DamageBlockPatch.FindGameProcessId()
                : int.Parse(requestedProcessId);
            if (processId is null)
            {
                throw new InvalidOperationException("Tears of Metal is not running.");
            }

            var patch = new DamageBlockPatch();
            if (patch.GetState(processId) != DamageBlockState.Disabled)
            {
                throw new InvalidOperationException("The ReceiveHit HP-subtraction instruction was not in its original state.");
            }

            patch.Enable(processId);
            if (patch.GetState(processId) != DamageBlockState.Enabled)
            {
                throw new InvalidOperationException("Enable verification failed.");
            }

            patch.Disable(processId);
            if (patch.GetState(processId) != DamageBlockState.Disabled)
            {
                throw new InvalidOperationException("Disable verification failed.");
            }

            Console.WriteLine(
                $"PASS: blocked, verified, unblocked, and restored ReceiveHit HP subtraction in process {processId}.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
    }

    private static int RunPatchStatus(string? requestedProcessId)
    {
        try
        {
            var processId = string.IsNullOrWhiteSpace(requestedProcessId)
                ? DamageBlockPatch.FindGameProcessId()
                : int.Parse(requestedProcessId);
            var patch = new DamageBlockPatch();
            Console.WriteLine(patch.GetDiagnosticSummary(processId));
            return processId is null ? 2 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
    }
}

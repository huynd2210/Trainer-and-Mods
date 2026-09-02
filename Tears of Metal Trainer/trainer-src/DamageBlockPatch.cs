using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TearsOfMetal.Trainer;

internal enum DamageBlockState
{
    GameNotRunning,
    Disabled,
    Enabled,
    Unsupported
}

internal sealed class DamageBlockPatch
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint PageExecuteReadWrite = 0x40;

    // PlayerDamageReceiver.ReceiveHit(HitInfo), build 56935:
    //   subss xmm0, dword ptr [rdi+4]  ; current HP -= resolved hit damage
    // Replacing only this instruction with a length-matched NOP preserves hit
    // reactions, networking, healing, initialization, and HUD updates.
    private const long TargetRva = 0xECBB30;
    private static readonly byte[] OriginalBytes = [0xF3, 0x0F, 0x5C, 0x47, 0x04];
    private static readonly byte[] PatchedBytes = [0x0F, 0x1F, 0x44, 0x00, 0x00];

    public int? PatchedProcessId { get; private set; }

    public DamageBlockState GetState(int? processId = null)
    {
        var pid = processId ?? FindGameProcessId();
        if (pid is null)
        {
            PatchedProcessId = null;
            return DamageBlockState.GameNotRunning;
        }

        try
        {
            var current = ReadTargetBytes(pid.Value);
            if (current.SequenceEqual(OriginalBytes))
            {
                PatchedProcessId = null;
                return DamageBlockState.Disabled;
            }

            if (current.SequenceEqual(PatchedBytes))
            {
                PatchedProcessId = pid;
                return DamageBlockState.Enabled;
            }

            return DamageBlockState.Unsupported;
        }
        catch (ArgumentException)
        {
            PatchedProcessId = null;
            return DamageBlockState.GameNotRunning;
        }
        catch (InvalidOperationException)
        {
            PatchedProcessId = null;
            return DamageBlockState.GameNotRunning;
        }
    }

    public string GetDiagnosticSummary(int? processId = null)
    {
        var pid = processId ?? FindGameProcessId();
        if (pid is null)
        {
            return "Game not running.";
        }

        using var process = Process.GetProcessById(pid.Value);
        var address = GetTargetAddress(process);
        var bytes = ReadTargetBytes(pid.Value);
        var state = bytes.SequenceEqual(PatchedBytes)
            ? DamageBlockState.Enabled
            : bytes.SequenceEqual(OriginalBytes)
                ? DamageBlockState.Disabled
                : DamageBlockState.Unsupported;
        return $"PID {pid.Value} | GameAssembly+0x{TargetRva:X} | 0x{address.ToInt64():X} | {state} | {Convert.ToHexString(bytes)}";
    }

    public void Enable(int? processId = null)
    {
        var pid = processId ?? FindGameProcessId()
            ?? throw new InvalidOperationException("Tears of Metal is not running or GameAssembly.dll is not loaded yet.");
        var state = GetState(pid);
        if (state == DamageBlockState.Enabled)
        {
            PatchedProcessId = pid;
            return;
        }

        if (state != DamageBlockState.Disabled)
        {
            throw new NotSupportedException(
                "The ReceiveHit health-subtraction signature does not match game build 56935. No memory was changed.");
        }

        WriteTargetBytes(pid, PatchedBytes);
        if (GetState(pid) != DamageBlockState.Enabled)
        {
            TryWriteTargetBytes(pid, OriginalBytes);
            throw new InvalidOperationException("The HP-subtraction patch could not be verified.");
        }

        PatchedProcessId = pid;
    }

    public void Disable(int? processId = null)
    {
        var pid = processId ?? PatchedProcessId ?? FindGameProcessId();
        if (pid is null)
        {
            PatchedProcessId = null;
            return;
        }

        var state = GetState(pid.Value);
        if (state == DamageBlockState.Disabled)
        {
            PatchedProcessId = null;
            return;
        }

        if (state != DamageBlockState.Enabled)
        {
            throw new NotSupportedException(
                "The ReceiveHit health-subtraction bytes changed unexpectedly. No memory was changed.");
        }

        WriteTargetBytes(pid.Value, OriginalBytes);
        if (GetState(pid.Value) != DamageBlockState.Disabled)
        {
            throw new InvalidOperationException("The original HP-subtraction instruction could not be restored.");
        }

        PatchedProcessId = null;
    }

    public static int? FindGameProcessId()
    {
        var processes = Process.GetProcessesByName("ToM");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.Modules
                        .Cast<ProcessModule>()
                        .Any(item => item.ModuleName.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        return process.Id;
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited while it was being inspected.
                }
                catch (Win32Exception)
                {
                    // Ignore inaccessible launch stubs and continue to the real game process.
                }
            }

            return null;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static byte[] ReadTargetBytes(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var address = GetTargetAddress(process);
        var handle = OpenProcess(
            ProcessQueryInformation | ProcessVmRead,
            inheritHandle: false,
            processId);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the game process.");
        }

        try
        {
            var bytes = new byte[OriginalBytes.Length];
            if (!ReadProcessMemory(handle, address, bytes, (nuint)bytes.Length, out var read)
                || read != (nuint)bytes.Length)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not read the ReceiveHit health-subtraction instruction.");
            }

            return bytes;
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    private static void WriteTargetBytes(int processId, byte[] bytes)
    {
        using var process = Process.GetProcessById(processId);
        var address = GetTargetAddress(process);
        var handle = OpenProcess(
            ProcessQueryInformation | ProcessVmOperation | ProcessVmRead | ProcessVmWrite,
            inheritHandle: false,
            processId);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the game process for editing.");
        }

        try
        {
            if (!VirtualProtectEx(handle, address, (nuint)bytes.Length, PageExecuteReadWrite, out var originalProtection))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not unlock the ReceiveHit health-subtraction instruction.");
            }

            try
            {
                if (!WriteProcessMemory(handle, address, bytes, (nuint)bytes.Length, out var written)
                    || written != (nuint)bytes.Length)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Could not write the ReceiveHit health-subtraction instruction.");
                }

                if (!FlushInstructionCache(handle, address, (nuint)bytes.Length))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Could not refresh the game code cache.");
                }
            }
            finally
            {
                _ = VirtualProtectEx(
                    handle,
                    address,
                    (nuint)bytes.Length,
                    originalProtection,
                    out _);
            }
        }
        finally
        {
            _ = CloseHandle(handle);
        }

        var verified = ReadTargetBytes(processId);
        if (!verified.SequenceEqual(bytes))
        {
            throw new InvalidOperationException("The ReceiveHit instruction write could not be verified.");
        }
    }

    private static void TryWriteTargetBytes(int processId, byte[] bytes)
    {
        try
        {
            WriteTargetBytes(processId, bytes);
        }
        catch
        {
            // Preserve the original failure while making a best effort to roll back.
        }
    }

    private static IntPtr GetTargetAddress(Process process)
    {
        process.Refresh();
        var module = process.Modules
            .Cast<ProcessModule>()
            .FirstOrDefault(item => item.ModuleName.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("GameAssembly.dll is not loaded yet.");
        return IntPtr.Add(module.BaseAddress, checked((int)TargetRva));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        byte[] buffer,
        nuint size,
        out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtectEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        uint newProtection,
        out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

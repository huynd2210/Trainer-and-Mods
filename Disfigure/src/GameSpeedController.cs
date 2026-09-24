using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using UnityEngine;

namespace DisfigureTrainer;

// Disfigure treats timeScale == 1 as a gameplay/input gate. Hook only the
// IL2CPP script accessors: scripts retain their original scale, while Unity's
// engine clock receives that scale multiplied by the user's speed. No OS
// clock hooks, frame-order races, or patches to individual weapons are needed.
internal sealed class GameSpeedController : IDisposable
{
    public const float MinSpeed = 0.2f;
    public const float MaxSpeed = 3f;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate float GetScale(IntPtr methodInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SetScale(float value, IntPtr methodInfo);

    private readonly INativeDetour _getHook;
    private readonly INativeDetour _setHook;
    private readonly GetScale _originalGet;
    private readonly SetScale _originalSet;
    private float _gameScale;
    private float _speed = 1f;
    private bool _disposed;

    public float Speed => _speed;

    public GameSpeedController()
    {
        // Initialize Unity's wrappers before installing either hook.
        _gameScale = Time.timeScale;
        IntPtr get = AccessorPointer("get_timeScale");
        IntPtr set = AccessorPointer("set_timeScale");
        try
        {
            _getHook = INativeDetour.Create<GetScale>(get, ReadGameScale);
            _originalGet = _getHook.GenerateTrampoline<GetScale>();
            _setHook = INativeDetour.Create<SetScale>(set, WriteGameScale);
            _originalSet = _setHook.GenerateTrampoline<SetScale>();
            // All callbacks and trampolines are wired before enabling hooks.
            _getHook.Apply();
            _setHook.Apply();
        }
        catch
        {
            _setHook?.Dispose();
            _getHook?.Dispose();
            throw;
        }
    }

    internal static IntPtr AccessorPointer(string accessor)
    {
        // Resolve through generated interop metadata, not version-specific RVAs.
        var field = typeof(Time).GetField(
            accessor == "get_timeScale"
                ? "NativeMethodInfoPtr_get_timeScale_Public_Static_get_Single_0"
                : "NativeMethodInfoPtr_set_timeScale_Public_Static_set_Void_Single_0",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(typeof(Time).FullName, accessor);
        var info = (IntPtr)field.GetValue(null);
        if (info == IntPtr.Zero || Marshal.ReadIntPtr(info) == IntPtr.Zero)
            throw new InvalidOperationException($"Unity {accessor} has no native method pointer.");
        return Marshal.ReadIntPtr(info);
    }

    private float ReadGameScale(IntPtr methodInfo) => _gameScale;

    private void WriteGameScale(float value, IntPtr methodInfo)
    {
        // Keep pause (0), resume (1), and the game's slow-motion effects intact.
        // Ignore invalid values just as we reject them in the speed control.
        if (!float.IsFinite(value) || value < 0f)
            return;
        _originalSet(value * _speed, methodInfo);
        _gameScale = value;
    }

    public void SetSpeed(float speed)
    {
        if (_disposed || !float.IsFinite(speed))
            return;
        _speed = Math.Clamp(MathF.Round(speed, 2), MinSpeed, MaxSpeed);
        _originalSet(_gameScale * _speed, IntPtr.Zero);
    }

    // Keep the original fixed step: physics, cooldowns and coroutines advance
    // together in game time. The bounded 3x maximum limits extra physics work.
    public void Restore() => SetSpeed(1f);

    internal float EngineScale => _originalGet(IntPtr.Zero);

    public void Dispose()
    {
        if (_disposed)
            return;
        Restore();
        _setHook.Dispose();
        _getHook.Dispose();
        _disposed = true;
    }
}

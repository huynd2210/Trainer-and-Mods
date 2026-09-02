using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatMailCo.SpeedBoost;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("CatMailCo.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.catmailco.speedboost";
    public const string Name = "Speed Boost";
    public const string Version = "1.2.0";

    internal static Plugin Instance { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        SpeedBoostState.Initialize(Config);

        AddComponent<SpeedBoostOverlay>();
        Harmony.CreateAndPatchAll(typeof(PlayerSpeedPatches), Guid);

        Log.LogInfo($"{Name} {Version} loaded. Press {SpeedBoostState.CycleKeyName} to cycle walk speed.");
    }

    public override bool Unload()
    {
        SpeedBoostState.ResetSpeed();
        Harmony.UnpatchID(Guid);
        return true;
    }
}

internal static class SpeedBoostState
{
    private static readonly float[] CycleMultipliers = { 1f, 2f, 3f, 5f };
    private static int _index;

    private static ConfigFile _config = null!;
    private static ConfigEntry<string> _cycleKeyName = null!;
    private static ConfigEntry<bool> _overlayVisible = null!;
    private static ConfigEntry<float> _overlayX = null!;
    private static ConfigEntry<float> _overlayY = null!;

    // True while we drive a recompute ourselves so the patch below does not
    // scale the value a second time.
    internal static bool SuppressRecomputeScale;

    internal static string CycleKeyName => _cycleKeyName.Value;
    internal static Key CycleKey { get; private set; } = Key.F6;
    internal static float Multiplier => CycleMultipliers[_index];

    internal static bool OverlayVisible
    {
        get => _overlayVisible.Value;
        set => _overlayVisible.Value = value;
    }

    internal static float OverlayX => _overlayX.Value;
    internal static float OverlayY => _overlayY.Value;

    internal static void Initialize(ConfigFile config)
    {
        _config = config;
        _cycleKeyName = config.Bind("Speed", "Key", "F6",
            "Key (a UnityEngine.InputSystem Key name, e.g. F6 or V) that cycles the walk-speed multiplier through 1x, 2x, 3x, 5x. Hold Left Shift while pressing it to show or hide the overlay.");
        _overlayVisible = config.Bind("Overlay", "Visible", true, "Show the current speed multiplier in-game.");
        _overlayX = config.Bind("Overlay", "PositionX", 20f, "Overlay distance from the left edge in pixels.");
        _overlayY = config.Bind("Overlay", "PositionY", 240f, "Overlay distance from the top edge in pixels.");
        ParseKey();
    }

    private static void ParseKey()
    {
        if (Enum.TryParse(_cycleKeyName.Value, true, out Key parsed))
        {
            CycleKey = parsed;
        }
        else
        {
            CycleKey = Key.F6;
            Plugin.Instance.Log.LogWarning($"Unknown key \"{_cycleKeyName.Value}\" in the Speed Boost config; falling back to F6.");
        }
    }

    internal static void Cycle()
    {
        _index = (_index + 1) % CycleMultipliers.Length;
        Apply();
        LogCurrentState();
    }

    internal static void ResetSpeed()
    {
        _index = 0;
        Apply();
    }

    // Recomputes the game's canonical max grounded speed (base * parcel-weight
    // reduction) through its own method, then scales the cached value the
    // movement code actually reads. Writing this scalar is safe: unlike scaling
    // the KCC motor's BaseVelocity, it cannot compound and leaves jump/gravity
    // velocity untouched.
    internal static void Apply()
    {
        try
        {
            foreach (var controller in UnityEngine.Object.FindObjectsOfType<PlayerController>())
            {
                if (controller == null)
                    continue;

                SuppressRecomputeScale = true;
                try
                {
                    controller.ComputeCurrentMaximumGroundedSpeed();
                }
                finally
                {
                    SuppressRecomputeScale = false;
                }
                controller._currentMaximumGroundedSpeed *= Multiplier;
            }
        }
        catch (Exception exception)
        {
            Plugin.Instance.Log.LogWarning($"Could not update the player walk speed: {exception.Message}");
        }
    }

    // One-line runtime check of the value the game actually moves with, so the
    // log proves whether the multiplier reached the player.
    private static void LogCurrentState()
    {
        var builder = new StringBuilder($"{Plugin.Name} speed is now {Multiplier:0.##}x.");
        try
        {
            var foundAny = false;
            foreach (var controller in UnityEngine.Object.FindObjectsOfType<PlayerController>())
            {
                foundAny = true;
                var motor = controller?.Motor;
                var cached = controller != null ? controller._currentMaximumGroundedSpeed : 0f;
                var baseVel = motor != null ? motor.BaseVelocity.magnitude : 0f;
                builder.Append($" | cachedMax={cached:0.##} BaseVelocity={baseVel:0.##}");
            }
            if (!foundAny)
                builder.Append(" | no PlayerController found");
        }
        catch (Exception exception)
        {
            builder.Append($" | diagnostic failed: {exception.Message}");
        }
        Plugin.Instance.Log.LogInfo(builder.ToString());
    }

    internal static void Save() => _config?.Save();
}

internal static class PlayerSpeedPatches
{
    // Whenever the game recomputes its cached max grounded speed (parcel pickup,
    // sprint change, respawn, ...), re-apply the multiplier so the boost
    // survives the game overwriting our value. Suppressed while we drive a
    // recompute ourselves in Apply().
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.ComputeCurrentMaximumGroundedSpeed))]
    private static void AfterComputeCurrentMaximumGroundedSpeed(PlayerController __instance)
    {
        if (SpeedBoostState.SuppressRecomputeScale)
            return;

        __instance._currentMaximumGroundedSpeed *= SpeedBoostState.Multiplier;
    }
}

internal sealed class SpeedBoostOverlay : MonoBehaviour
{
    private GUIStyle _style;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        var keyControl = keyboard[SpeedBoostState.CycleKey];
        if (keyControl == null || !keyControl.wasPressedThisFrame)
            return;

        if (keyboard.shiftKey.isPressed)
        {
            SpeedBoostState.OverlayVisible = !SpeedBoostState.OverlayVisible;
            SpeedBoostState.Save();
            Plugin.Instance.Log.LogInfo($"{Plugin.Name} overlay {(SpeedBoostState.OverlayVisible ? "shown" : "hidden")}.");
        }
        else
        {
            SpeedBoostState.Cycle();
        }
    }

    private void OnGUI()
    {
        if (!SpeedBoostState.OverlayVisible)
            return;

        EnsureStyles();
        var text = $"SPEED  {SpeedBoostState.Multiplier:0.##}x";
        var panel = new Rect(SpeedBoostState.OverlayX, SpeedBoostState.OverlayY, 150f, 38f);
        GUI.Box(panel, string.Empty);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 26f), text, _style);
    }

    private void EnsureStyles()
    {
        if (_style != null)
            return;

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _style.normal.textColor = Color.white;
    }
}

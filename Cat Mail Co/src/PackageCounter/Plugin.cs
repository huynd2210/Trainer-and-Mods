using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatMailCo.PackageCounter;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("CatMailCo.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.catmailco.packagecounter";
    public const string Name = "Package Counter";
    public const string Version = "1.1.0";

    internal static Plugin Instance { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        PackageCounterState.Initialize(Config);

        AddComponent<PackageCounterOverlay>();
        Harmony.CreateAndPatchAll(typeof(RecapPatches), Guid);

        Log.LogInfo($"{Name} {Version} loaded. Press F8 to show or hide the counter.");
    }

    public override bool Unload()
    {
        PackageCounterState.Save();
        Harmony.UnpatchID(Guid);
        return true;
    }
}

internal static class PackageCounterState
{
    private static ConfigFile _config = null!;
    private static ConfigEntry<int> _allTime = null!;
    private static ConfigEntry<int> _allTimeCustomer = null!;
    private static ConfigEntry<int> _allTimeBoat = null!;
    private static ConfigEntry<int> _allTimeHome = null!;
    private static ConfigEntry<int> _observedCustomer = null!;
    private static ConfigEntry<int> _observedBoat = null!;
    private static ConfigEntry<int> _observedHome = null!;
    private static ConfigEntry<bool> _showOverlay = null!;
    private static ConfigEntry<float> _overlayX = null!;
    private static ConfigEntry<float> _overlayY = null!;

    internal static int AllTime => _allTime.Value;
    internal static int AllTimeCustomer => _allTimeCustomer.Value;
    internal static int AllTimeBoat => _allTimeBoat.Value;
    internal static int AllTimeHome => _allTimeHome.Value;
    internal static int EarlierUnsplit => Math.Max(0, AllTime - AllTimeCustomer - AllTimeBoat - AllTimeHome);
    internal static int ShiftCustomer => _observedCustomer.Value;
    internal static int ShiftBoat => _observedBoat.Value;
    internal static int ShiftHome => _observedHome.Value;
    internal static int ShiftTotal => ShiftCustomer + ShiftBoat + ShiftHome;

    internal static bool ShowOverlay
    {
        get => _showOverlay.Value;
        set => _showOverlay.Value = value;
    }

    internal static float OverlayX => _overlayX.Value;
    internal static float OverlayY => _overlayY.Value;

    internal static void Initialize(ConfigFile config)
    {
        _config = config;
        _allTime = config.Bind("Counter", "AllTimeProcessed", 0,
            "Total accepted packages processed since the counter was installed.");
        _allTimeCustomer = config.Bind("Counter", "AllTimeCustomerPackages", 0,
            "Persistent all-time total of accepted customer-counter packages.");
        _allTimeBoat = config.Bind("Counter", "AllTimeBoatPackages", 0,
            "Persistent all-time total of accepted outgoing boat packages.");
        _allTimeHome = config.Bind("Counter", "AllTimeHomeDeliveryPackages", 0,
            "Persistent all-time total of accepted home-delivery packages.");
        _observedCustomer = config.Bind("Current Shift", "CustomerPackages", 0,
            "Internal checkpoint used to prevent double counting after a restart.");
        _observedBoat = config.Bind("Current Shift", "BoatPackages", 0,
            "Internal checkpoint used to prevent double counting after a restart.");
        _observedHome = config.Bind("Current Shift", "HomeDeliveryPackages", 0,
            "Internal checkpoint used to prevent double counting after a restart.");
        _showOverlay = config.Bind("Overlay", "Visible", true, "Show the in-game package counter.");
        _overlayX = config.Bind("Overlay", "PositionX", 20f, "Overlay distance from the left edge in pixels.");
        _overlayY = config.Bind("Overlay", "PositionY", 20f, "Overlay distance from the top edge in pixels.");
    }

    internal static void Reconcile(RecapManager recap)
    {
        try
        {
            var customer = Math.Max(0, recap.CurrentShiftCustomerParcelAmount);
            var boat = Math.Max(0, recap.CurrentShiftBoatParcelAmount);
            var home = Math.Max(0, recap.CurrentShiftHomeDeliveryParcelAmount);

            var customerAdded = ReconcileSource(_observedCustomer, customer);
            var boatAdded = ReconcileSource(_observedBoat, boat);
            var homeAdded = ReconcileSource(_observedHome, home);
            var added = customerAdded + boatAdded + homeAdded;

            if (added <= 0)
                return;

            _allTimeCustomer.Value = checked(_allTimeCustomer.Value + customerAdded);
            _allTimeBoat.Value = checked(_allTimeBoat.Value + boatAdded);
            _allTimeHome.Value = checked(_allTimeHome.Value + homeAdded);
            _allTime.Value = checked(_allTime.Value + added);
            Save();
            Plugin.Instance.Log.LogInfo($"Recorded {added} processed package(s); all-time total is {_allTime.Value}.");
        }
        catch (Exception exception)
        {
            Plugin.Instance.Log.LogError($"Could not update the package count: {exception}");
        }
    }

    private static int ReconcileSource(ConfigEntry<int> observed, int current)
    {
        if (current < observed.Value)
        {
            // A different/new shift can be loaded without the normal reset callback.
            observed.Value = current;
            return 0;
        }

        var delta = current - observed.Value;
        observed.Value = current;
        return delta;
    }

    internal static void BeginNewShift()
    {
        _observedCustomer.Value = 0;
        _observedBoat.Value = 0;
        _observedHome.Value = 0;
        Save();
    }

    internal static void ResetAllTime()
    {
        _allTime.Value = 0;
        _allTimeCustomer.Value = 0;
        _allTimeBoat.Value = 0;
        _allTimeHome.Value = 0;
        Save();
        Plugin.Instance.Log.LogInfo("The all-time package counter was reset.");
    }

    internal static void Save()
    {
        _config?.Save();
    }
}

internal sealed class PackageCounterOverlay : MonoBehaviour
{
    private const float ConfirmationSeconds = 5f;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private float _resetConfirmationUntil;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame)
            return;

        var shiftHeld = keyboard.shiftKey.isPressed;
        if (!shiftHeld)
        {
            PackageCounterState.ShowOverlay = !PackageCounterState.ShowOverlay;
            PackageCounterState.Save();
            return;
        }

        if (Time.realtimeSinceStartup <= _resetConfirmationUntil)
        {
            PackageCounterState.ResetAllTime();
            _resetConfirmationUntil = 0f;
        }
        else
        {
            _resetConfirmationUntil = Time.realtimeSinceStartup + ConfirmationSeconds;
            PackageCounterState.ShowOverlay = true;
        }
    }

    private void OnGUI()
    {
        if (!PackageCounterState.ShowOverlay)
            return;

        EnsureStyles();

        var showResetPrompt = Time.realtimeSinceStartup <= _resetConfirmationUntil;
        var showEarlierUnsplit = PackageCounterState.EarlierUnsplit > 0;
        var height = 158f + (showEarlierUnsplit ? 22f : 0f) + (showResetPrompt ? 28f : 0f);
        var panel = new Rect(PackageCounterState.OverlayX, PackageCounterState.OverlayY, 330f, height);

        GUI.Box(panel, string.Empty);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 28f),
            "PACKAGES PROCESSED", _titleStyle);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 40f, panel.width - 28f, 118f),
            $"All time: {PackageCounterState.AllTime:N0}\n" +
            $"This shift: {PackageCounterState.ShiftTotal:N0}\n" +
            "All-time routes:\n" +
            $"Customer {PackageCounterState.AllTimeCustomer:N0}  •  Boat {PackageCounterState.AllTimeBoat:N0}  •  Home {PackageCounterState.AllTimeHome:N0}\n" +
            (showEarlierUnsplit ? $"Earlier unsplit: {PackageCounterState.EarlierUnsplit:N0}\n" : string.Empty) +
            "F8: hide", _bodyStyle);

        if (showResetPrompt)
        {
            GUI.Label(new Rect(panel.x + 14f, panel.y + height - 28f, panel.width - 28f, 24f),
                "Press Shift + F8 again to reset all-time.", _bodyStyle);
        }
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        _titleStyle.normal.textColor = Color.white;

        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft
        };
        _bodyStyle.normal.textColor = Color.white;
    }

    private void OnApplicationQuit() => PackageCounterState.Save();
    private void OnDestroy() => PackageCounterState.Save();
}

internal static class RecapPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.CustomerManager_OnCustomerRequestSatisfied))]
    private static void CustomerProcessed(RecapManager __instance) => PackageCounterState.Reconcile(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.ComputeBoatScores))]
    private static void BoatProcessed(RecapManager __instance) => PackageCounterState.Reconcile(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.ComputeHomeDeliveriesScore))]
    private static void HomeDeliveryProcessed(RecapManager __instance) => PackageCounterState.Reconcile(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.UpdateSummary))]
    private static void SummaryUpdated(RecapManager __instance) => PackageCounterState.Reconcile(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.UpdateSummaryFromNetwork))]
    private static void NetworkSummaryUpdated(RecapManager __instance) => PackageCounterState.Reconcile(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecapManager), nameof(RecapManager.ResetValues))]
    private static void ShiftReset() => PackageCounterState.BeginNewShift();
}

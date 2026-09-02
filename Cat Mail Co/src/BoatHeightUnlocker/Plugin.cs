using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace CatMailCo.BoatHeightUnlocker;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("CatMailCo.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.catmailco.boatheightunlocker";
    public const string Name = "Boat Height Unlocker";
    public const string Version = "1.2.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ConfigEntry<float> MaximumHeight { get; private set; } = null!;
    internal static ConfigEntry<bool> LogPlacements { get; private set; } = null!;

    internal static void LogInfo(string message) => Instance.Log.LogInfo(message);

    public override void Load()
    {
        Instance = this;
        MaximumHeight = Config.Bind(
            "Boat",
            "MaximumHeight",
            1000f,
            "Height at which the boat refuses to depart, in game units. Parcels can always be " +
            "stacked past it, exactly as in the unmodded game -- the boat simply will not sail " +
            "until the stack is back under this height. The default 1000 is unreachable, which " +
            "removes the restriction entirely.");
        LogPlacements = Config.Bind(
            "Debug",
            "LogPlacements",
            false,
            "Log every boat placement attempt with the live heights. Diagnostic only.");

        Harmony.CreateAndPatchAll(typeof(BoatStoragePatches), Guid);
        Log.LogInfo($"{Name} {Version} loaded; boat departure height limit is {MaximumHeight.Value}.");
    }

    public override bool Unload()
    {
        Harmony.UnpatchID(Guid);
        return true;
    }
}

internal static class BoatStorage
{
    // Stacking stays unrestricted at every configured limit. Capping placement is
    // what made a low limit feel broken: the unmodded game lets parcels pile up and
    // withholds departure instead, and that division is the whole point of the mod.
    private const float UnrestrictedPlacementHeight = 1000f;

    // Boat heights in this build sit well under 1.0, so fractional limits have to
    // be allowed or no configured value could ever bind.
    internal static float DepartureLimit => Math.Max(0.01f, Plugin.MaximumHeight.Value);

    internal static bool IsBoatStore(EntityInteractableStore store)
    {
        if (store == null)
            return false;

        try
        {
            if (store.StorageType == StorageType.Boat)
                return true;

            var root = store.GetRootStore(false);
            return root != null && root.StorageType == StorageType.Boat;
        }
        catch
        {
            return false;
        }
    }

    // A parcel placed into a nested store is height-checked against the stack it
    // ultimately belongs to, so the ceilings go on the root as well as on the
    // store that received the call.
    internal static void ApplyCeilings(EntityInteractableStore store)
    {
        if (!IsBoatStore(store))
            return;

        ApplyCeilingsToKnownBoatStore(store);

        try
        {
            ApplyCeilingsToKnownBoatStore(store.GetRootStore(false));
            ApplyCeilingsToKnownBoatStore(store.GetRootStore(true));
        }
        catch
        {
            // A store queried before its hierarchy is wired up; the next call covers it.
        }
    }

    internal static void ApplyCeilingsToKnownBoatStore(EntityInteractableStore store)
    {
        if (store == null)
            return;

        // ParcelDeliverer supplies the authoritative boat storage object, even
        // on builds where its serialized StorageType is assigned a frame later.
        //
        // IsHeightApproved is deliberately not written. The game's own
        // UpdateApprovedHeight compares the live height against the approved
        // ceiling and raises the on-boat indicator; forcing the flag true made
        // every stack departable regardless of the configured maximum.
        store.StorageMaximumHeight = UnrestrictedPlacementHeight;
        store.StorageMaximumApprovedHeight = DepartureLimit;
    }

    private static readonly HashSet<int> ReportedStores = new();

    internal static void ReportStockCeilings(EntityInteractableStore store)
    {
        if (!Plugin.LogPlacements.Value || store == null)
            return;

        try
        {
            if (!ReportedStores.Add(store.GetInstanceID()))
                return;

            Plugin.LogInfo(
                $"boat store {store.name} stock ceilings: " +
                $"placement {store.StorageMaximumHeight:0.####}, " +
                $"approved {store.StorageMaximumApprovedHeight:0.####}");
        }
        catch
        {
            // Diagnostics must never break placement.
        }
    }
}

internal static class BoatStoragePatches
{
    // Postfix, not prefix: the game's own Awake assigns the serialized ceilings,
    // so a prefix would be overwritten before the store is ever used.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityInteractableStore), nameof(EntityInteractableStore.Awake))]
    private static void AfterStoreAwake(EntityInteractableStore __instance)
    {
        if (BoatStorage.IsBoatStore(__instance))
            BoatStorage.ReportStockCeilings(__instance);

        BoatStorage.ApplyCeilings(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityInteractableStore), nameof(EntityInteractableStore.CanStoreEntity))]
    private static void BeforeParcelPlacement(EntityInteractableStore __instance) => BoatStorage.ApplyCeilings(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityInteractableStore), nameof(EntityInteractableStore.PreviewStorage))]
    private static void BeforeStoragePreview(EntityInteractableStore __instance) => BoatStorage.ApplyCeilings(__instance);

    // Runs before the game recomputes approval, so it compares the live height
    // against the configured departure limit rather than the serialized one.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityInteractableStore), nameof(EntityInteractableStore.UpdateApprovedHeight))]
    private static void BeforeHeightApprovalUpdate(EntityInteractableStore __instance) => BoatStorage.ApplyCeilings(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ParcelDeliverer), nameof(ParcelDeliverer.Initialize))]
    private static void AfterBoatInitialize(EntityInteractableStore __0) => BoatStorage.ApplyCeilingsToKnownBoatStore(__0);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ParcelDeliverer), nameof(ParcelDeliverer.InitializeFromNetwork))]
    private static void AfterNetworkBoatInitialize(EntityInteractableStore __0) => BoatStorage.ApplyCeilingsToKnownBoatStore(__0);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityInteractableStore), nameof(EntityInteractableStore.CanStoreEntity))]
    private static void AfterParcelPlacement(EntityInteractableStore __instance, StoreResult __result)
    {
        if (!Plugin.LogPlacements.Value || !BoatStorage.IsBoatStore(__instance))
            return;

        Plugin.LogInfo(
            $"boat placement -> {__result}; height {__instance.CurrentHeight:0.###}, " +
            $"Height {__instance.Height:0.###}, " +
            $"placement ceiling {__instance.StorageMaximumHeight:0.###}, " +
            $"departure ceiling {__instance.StorageMaximumApprovedHeight:0.###}, " +
            $"approved {__instance.IsHeightApproved}");
    }
}

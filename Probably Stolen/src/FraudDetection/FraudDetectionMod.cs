using HarmonyLib;
using UnityEngine.UI;

namespace FraudDetection
{
    public class FraudDetectionMod : IMod
    {
        internal const string Id = "FraudDetection";

        // Options > Game Settings > "Fraud Detection": switches the mod off and on mid-run.
        // Kept in the game's settings file, so it survives a restart.
        private const string EnabledSaveKey = "mod_FraudDetection_enabled";
        internal static bool Active = true;

        public void Init(ModManifest manifest)
        {
            Active = !ES3.KeyExists(EnabledSaveKey) || ES3.Load<bool>(EnabledSaveKey);
        }

        public void OnEnable()
        {
            ModHook.OnCreateTooltipLate += TruthTooltip.Add;
            ModHelper.AddGameOptionToggle(Id, "gameplay", "enabled",
                () => "Fraud Detection",
                () => Active,
                SetActive,
                () => "Shows what items really are, the red X / green + badges, and pre-ticks confront answers. Turn off to inspect by yourself.");
        }

        public void OnDisable()
        {
            ModHook.OnCreateTooltipLate -= TruthTooltip.Add;
            ModHelper.RemoveGameOptions(Id);
        }

        private static void SetActive(bool value)
        {
            bool changed = Active != value;
            Active = value;
            if (!ES3.KeyExists(EnabledSaveKey) || ES3.Load<bool>(EnabledSaveKey) != value)
                ES3.Save(EnabledSaveKey, value);
            if (changed)
                ItemBadge.RefreshVisible();
        }
    }

    // Opening a confront panel: tick the answer. The panel's own code has just set up the
    // toggles; ticking one is exactly what the player's click would do.
    internal static class ConfrontPrefill
    {
        internal static void Tick(InspectionUIManager ui, GameItem item, Inspectable kind)
        {
            if (!FraudDetectionMod.Active || ui == null || item == null || !kind.StillOpen(item))
                return;

            Toggle[] toggles = { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
            if (kind.ClaimedGenuine(item))
            {
                // Seller says genuine: tick the flaw it really has, if any.
                int issue = kind.IssueIndex(item);
                if (issue >= 0 && toggles[issue] != null && toggles[issue].gameObject.activeSelf)
                    toggles[issue].isOn = true;
            }
            else if (kind.ReallyGenuine(item) && toggles[0] != null && toggles[0].gameObject.activeSelf)
            {
                // Seller says counterfeit but it is real: toggle1 is "genuine".
                toggles[0].isOn = true;
            }
        }
    }

    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenCigaretteConfrontPanel))]
    internal static class CigaretteConfrontPatch
    {
        private static void Postfix(InspectionUIManager __instance, GameItem gameItem) =>
            ConfrontPrefill.Tick(__instance, gameItem, Inspectable.Cigarette);
    }

    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenStampConfrontPanel))]
    internal static class StampConfrontPatch
    {
        private static void Postfix(InspectionUIManager __instance, GameItem gameItem) =>
            ConfrontPrefill.Tick(__instance, gameItem, Inspectable.Stamp);
    }

    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenInjectorConfrontPanel))]
    internal static class InjectorConfrontPatch
    {
        private static void Postfix(InspectionUIManager __instance, GameItem gameItem) =>
            ConfrontPrefill.Tick(__instance, gameItem, Inspectable.Injector);
    }
}

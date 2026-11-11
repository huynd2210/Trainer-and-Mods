using HarmonyLib;

namespace AutoAppraisal
{
    public class AutoAppraisalMod : IMod
    {
        internal const string Id = "AutoAppraisal";

        // Options > Game Settings > "Auto Appraisal": switches the mod off and on mid-run.
        // Kept in the game's settings file, so it survives a restart.
        private const string EnabledSaveKey = "mod_AutoAppraisal_enabled";
        internal static bool Active = true;

        public void Init(ModManifest manifest)
        {
            Active = !ES3.KeyExists(EnabledSaveKey) || ES3.Load<bool>(EnabledSaveKey);
        }

        public void OnEnable()
        {
            ModHook.OnCreateTooltipLate += AddTrueValue;
            ModHelper.AddGameOptionToggle(Id, "gameplay", "enabled",
                () => "Auto Appraisal",
                () => Active,
                SetActive,
                () => "Shows appraisal items' true value and pre-fills the appraisal box. Turn off to appraise by yourself.");
        }

        public void OnDisable()
        {
            ModHook.OnCreateTooltipLate -= AddTrueValue;
            ModHelper.RemoveGameOptions(Id);
        }

        private static void SetActive(bool value)
        {
            Active = value;
            if (!ES3.KeyExists(EnabledSaveKey) || ES3.Load<bool>(EnabledSaveKey) != value)
                ES3.Save(EnabledSaveKey, value);
        }

        // Appraisal items: the exact value the customer is looking for.
        private static void AddTrueValue(RichTextBuilder builder, GameItem item)
        {
            if (!Active || builder == null || item == null || !item.IsTag("APPRAISAL_ITEM"))
                return;
            int correct = AppraisalHelper.GetCorrectBaseValue(item);
            if (correct > 0)
                builder.AddLine("True value: " + correct, ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange, bold: true);
        }
    }

    // Opening an appraisal item: put the exact value in the estimate box. The box shows
    // the item's APPRAISAL_DEFAULT_VALUE tag every frame (InspectionUIManager.UpdateAppraisalPanel),
    // which is also what typing into it writes, so you can still change it before pressing Estimate.
    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OnInspectItem))]
    internal static class AppraisalPrefillPatch
    {
        private static void Postfix(GameItem gameItem)
        {
            if (!AutoAppraisalMod.Active || gameItem == null || !gameItem.IsTag("APPRAISAL_ITEM"))
                return;
            StoreClient client = PlayerStore.Instance.currentClientInstance?.GetClientBlueprint();
            if (client == null || client.clientIntent != StoreClient.ClientIntent.APPRAISAL_SERVICE)
                return;

            int correct = AppraisalHelper.GetCorrectBaseValue(gameItem);
            if (correct <= 0)
                return;
            gameItem.ModifyTag("APPRAISAL_DEFAULT_VALUE", state => state.Enable().SetInt(correct));
        }
    }
}

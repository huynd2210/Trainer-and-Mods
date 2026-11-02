using HarmonyLib;

namespace AutoAppraisal
{
    public class AutoAppraisalMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            ModHook.OnCreateTooltipLate += AddTrueValue;
        }

        public void OnDisable()
        {
            ModHook.OnCreateTooltipLate -= AddTrueValue;
        }

        // Appraisal items: the exact value the customer is looking for.
        private static void AddTrueValue(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null || !item.IsTag("APPRAISAL_ITEM"))
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
            if (gameItem == null || !gameItem.IsTag("APPRAISAL_ITEM"))
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

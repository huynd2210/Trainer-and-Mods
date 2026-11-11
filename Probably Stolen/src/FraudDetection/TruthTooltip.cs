namespace FraudDetection
{
    // Adds what an item really is to its hover tooltip. Nothing is added for items whose
    // labels are honest, so the extra lines only appear where there is something to catch.
    internal static class TruthTooltip
    {
        private const RenderHandler.ColorPalette Worse = RenderHandler.ColorPalette.Red;
        private const RenderHandler.ColorPalette Better = RenderHandler.ColorPalette.LightGreen;

        internal static void Add(RichTextBuilder builder, GameItem item)
        {
            if (!FraudDetectionMod.Active || builder == null || item == null)
                return;
            AddHiddenFeatures(builder, item);
            AddConfrontHint(builder, item);
        }

        // Every feature still showing a label that differs from its real condition.
        private static void AddHiddenFeatures(RichTextBuilder builder, GameItem item)
        {
            if (item.itemFeatures == null)
                return;
            foreach (ItemFeature feature in item.itemFeatures)
            {
                if (!IsLie(feature))
                    continue;
                string real = feature.GetBaseRealDisplay();
                string modifier = feature.useCondition
                    ? feature.realCondition.GetModValueFormated()
                    : Signed(feature.GetActualValueModifier());
                builder.AddLine("Really: " + real + " (" + modifier + "%)", ignoreEmpty: true,
                    feature.IsRealFeatureCheaper() ? Worse : Better, bold: true);
            }
        }

        internal static bool IsLie(ItemFeature feature)
        {
            if (feature == null || feature.isDisabled || feature.IsExposed() || feature.IsFeatureMatch())
                return false;
            if (feature.useCondition)
                return feature.realCondition != null && feature.fakeCondition != null
                    && feature.realCondition.identifier != feature.fakeCondition.identifier;
            string real = feature.GetBaseRealDisplay();
            if (string.IsNullOrEmpty(real))
                return false;
            return feature.GetPublicHidden()
                || real != feature.GetBaseFakeDisplay()
                || feature.GetActualValueModifier() != feature.GetFakeValueModifier();
        }

        // Inspectable items: which confront answer is right.
        private static void AddConfrontHint(RichTextBuilder builder, GameItem item)
        {
            Inspectable kind = Inspectable.Of(item);
            if (kind == null || !kind.StillOpen(item))
                return;

            if (kind.ClaimedGenuine(item))
            {
                int issue = kind.IssueIndex(item);
                if (issue >= 0)
                    builder.AddLine("Counterfeit - confront: " + LocHelper.GetLocalizedUI(kind.IssueLabelKeys[issue]),
                        ignoreEmpty: true, Worse, bold: true);
            }
            else if (kind.ReallyGenuine(item))
            {
                builder.AddLine("Actually genuine - tell them: " + LocHelper.GetLocalizedUI("inspection_genuine"),
                    ignoreEmpty: true, Better, bold: true);
            }
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}

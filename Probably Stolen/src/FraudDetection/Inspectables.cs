using System.Linq;

namespace FraudDetection
{
    // The three items with a confront panel in InspectionUIManager. The panel shows five
    // issue toggles (toggle1..toggle5) when the seller claims the item is genuine; the
    // issue tags and labels below are in that toggle order, as InspectionUIManager.On*Confront
    // and Open*ConfrontPanel use them. When the seller admits it is counterfeit, the panel
    // shows only toggle1, labelled "genuine".
    internal class Inspectable
    {
        public string ItemTag;
        public string FeatureCategory;
        public string GenuineId;
        public string[] IssueTags;
        public string[] IssueLabelKeys;

        internal static readonly Inspectable Cigarette = new Inspectable
        {
            ItemTag = "CIGARETTE_TAG",
            FeatureCategory = "CATEGORY_GENUINE_CIGARETTE",
            GenuineId = "genuineCigarette",
            IssueTags = new[] { "CIGARETTE_LETTERING_ISSUE", "CIGARETTE_COLOR_ISSUE", "CIGARETTE_SEAL_ISSUE", "CIGARETTE_LOGO_ISSUE", "CIGARETTE_BOX_ISSUE" },
            IssueLabelKeys = new[] { "inspection_lettering_issues", "inspection_color_issues", "inspection_seal_issues", "inspection_logo_issues", "inspection_box_issues" },
        };

        internal static readonly Inspectable Stamp = new Inspectable
        {
            ItemTag = "FOOD_STAMP_TAG",
            FeatureCategory = "CATEGORY_GENUINE_STAMP",
            GenuineId = "genuineStamp",
            IssueTags = new[] { "STAMP_LETTERING_ISSUE", "STAMP_LOGO_ISSUE", "STAMP_SEAL_ISSUE", "STAMP_AMOUNT_ISSUE", "STAMP_CHIP_ISSUE" },
            IssueLabelKeys = new[] { "inspection_lettering_issues", "inspection_logo_issues", "inspection_seal_issues", "inspection_value_issues", "inspection_chip_issues" },
        };

        internal static readonly Inspectable Injector = new Inspectable
        {
            ItemTag = "INS_INJECTOR_TAG",
            FeatureCategory = "CATEGORY_GENUINE_INJECTOR",
            GenuineId = "genuineInjector",
            IssueTags = new[] { "ISSUE_INDICATOR", "ISSUE_LABEL", "ISSUE_SERIAL", "ISSUE_BODY", "ISSUE_COLOR" },
            IssueLabelKeys = new[] { "inspection_indicator_issues", "inspection_label_issues", "inspection_serial_issues", "inspection_body_issues", "inspection_color_issues" },
        };

        internal static readonly Inspectable[] All = { Cigarette, Stamp, Injector };

        internal static Inspectable Of(GameItem item) => All.FirstOrDefault(i => item.IsTag(i.ItemTag));

        internal ItemFeature Feature(GameItem item) => item.FindItemFeatureByCategory(FeatureCategory);

        // What the seller claims: the condition shown until the feature is exposed.
        internal bool ClaimedGenuine(GameItem item) =>
            Feature(item)?.GetCurrentItemCondition()?.identifier == GenuineId;

        internal bool ReallyGenuine(GameItem item) =>
            Feature(item)?.realCondition?.identifier == GenuineId;

        // Index (0-4) of the first issue the item really has, or -1.
        internal int IssueIndex(GameItem item)
        {
            for (int i = 0; i < IssueTags.Length; i++)
            {
                if (item.IsTag(IssueTags[i]))
                    return i;
            }
            return -1;
        }

        internal bool StillOpen(GameItem item)
        {
            ItemFeature feature = Feature(item);
            return feature != null && !feature.IsExposed() && !item.IsTag("ALREADY_CONFRONTED_TAG");
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreRunStats
{
    // A "Shady Deals" tab at the end of the run summary's tab row (RunSummaryUI), drawn
    // with the summary's own helpers so it looks like the Security tab next to it.
    internal static class ShadyDealsTab
    {
        // The game's mod scanner reports two harmless patterns as the reflection-external
        // capability: an AccessTools lookup on a Type held in a field ("computed type"), and
        // an AccessTools generic over a system type (MethodDelegate<Action<...>>). So typeof
        // is written inline at every call and delegates come from MethodInfo.CreateDelegate.
        private static readonly Action<CustomUIBuilder, float> BeginBox =
            (Action<CustomUIBuilder, float>)AccessTools.Method(typeof(RunSummaryUI), "BeginBox").CreateDelegate(typeof(Action<CustomUIBuilder, float>));
        private static readonly Action<CustomUIBuilder, string> SectionTitle =
            (Action<CustomUIBuilder, string>)AccessTools.Method(typeof(RunSummaryUI), "SectionTitle").CreateDelegate(typeof(Action<CustomUIBuilder, string>));
        private static readonly Action<CustomUIBuilder, string, string, float, bool> KeyValueRow =
            (Action<CustomUIBuilder, string, string, float, bool>)AccessTools.Method(typeof(RunSummaryUI), "KeyValueRow").CreateDelegate(typeof(Action<CustomUIBuilder, string, string, float, bool>));
        private static readonly Func<CustomUIBuilder, string, float, Color, bool, FontLoader.FontRole?, bool, CustomUIElement> Label =
            (Func<CustomUIBuilder, string, float, Color, bool, FontLoader.FontRole?, bool, CustomUIElement>)AccessTools.Method(typeof(RunSummaryUI), "Label").CreateDelegate(typeof(Func<CustomUIBuilder, string, float, Color, bool, FontLoader.FontRole?, bool, CustomUIElement>));
        private static readonly Func<long, string> Money =
            (Func<long, string>)AccessTools.Method(typeof(RunSummaryUI), "Money").CreateDelegate(typeof(Func<long, string>));
        private static readonly Func<int, string> Number =
            (Func<int, string>)AccessTools.Method(typeof(RunSummaryUI), "Number").CreateDelegate(typeof(Func<int, string>));
        private static readonly Action<CustomUIManager, CustomUIBuilder, RectTransform, Action<CustomUIBuilder>> AddPage =
            (Action<CustomUIManager, CustomUIBuilder, RectTransform, Action<CustomUIBuilder>>)AccessTools.Method(typeof(RunSummaryUI), "AddPage").CreateDelegate(typeof(Action<CustomUIManager, CustomUIBuilder, RectTransform, Action<CustomUIBuilder>>));
        private static readonly Action<int> SelectPage =
            (Action<int>)AccessTools.Method(typeof(RunSummaryUI), "SelectPage").CreateDelegate(typeof(Action<int>));

        private static List<CustomUIElement> Tabs => (List<CustomUIElement>)AccessTools.Field(typeof(RunSummaryUI), "tabs").GetValue(null);
        private static List<Sprite> TabSprites => (List<Sprite>)AccessTools.Field(typeof(RunSummaryUI), "tabNormalSprites").GetValue(null);
        private static List<GameObject> Pages => (List<GameObject>)AccessTools.Field(typeof(RunSummaryUI), "pages").GetValue(null);
        private static Color Teal => (Color)AccessTools.Field(typeof(RunSummaryUI), "TEAL").GetValue(null);

        // The summary's main builder, as RunSummaryUI.Build passes it to AddPage.
        internal static CustomUIBuilder MainBuilder;

        private const float HalfWidth = 588f;
        private const float RowSize = 20f;

        private static string T(string key, string english, params object[] args) =>
            LocHelper.GetLocalizedUIOrDefault("ui_more_run_stats_" + key, english, args);

        internal static void Add()
        {
            List<CustomUIElement> tabs = Tabs;
            List<GameObject> pages = Pages;
            CustomUIBuilder main = MainBuilder;
            if (main == null || tabs.Count == 0 || pages.Count == 0 || tabs[0]?.root == null || pages[0] == null)
                return;

            int index = pages.Count;
            var tabRow = new CustomUIBuilder(CustomUIManager.Instance, main.Handle, (RectTransform)tabs[0].root.transform.parent);
            tabRow.AddButton(T("tab", "Shady Deals"), () => SelectPage(index));
            CustomUIElement tab = tabRow.Last;
            tabs.Add(tab);
            TabSprites.Add(tab.button != null && tab.button.image != null ? tab.button.image.sprite : null);
            AddPage(CustomUIManager.Instance, main, (RectTransform)pages[0].transform.parent, Fill);
        }

        private static void Fill(CustomUIBuilder b)
        {
            b.BeginRow(24f);
            BuildContraband(b);
            BuildScamsAndStolen(b);
            b.End();
        }

        private static void BuildContraband(CustomUIBuilder b)
        {
            BeginBox(b, HalfWidth);
            SectionTitle(b, T("contraband_title", "Contraband Sold"));
            int totalCount = 0;
            long totalCredits = 0;
            foreach (ContrabandKind kind in RunLedger.Kinds)
            {
                int count = RunLedger.Get(kind.CountKey);
                if (count <= 0)
                    continue;
                int credits = RunLedger.Get(kind.CreditsKey);
                totalCount += count;
                totalCredits += credits;
                KeyValueRow(b, kind.Label, CreditsAndCount(credits, count), RowSize, false);
            }
            if (totalCount == 0)
            {
                Label(b, LocHelper.GetLocalizedMechanic("sec_data_none"), RowSize, Teal, false, null, true);
            }
            else
            {
                b.AddSeparator();
                KeyValueRow(b, LocHelper.GetLocalizedUI("ui_nego_total"), CreditsAndCount(totalCredits, totalCount), 24f, true);
            }
            b.End();
        }

        private static void BuildScamsAndStolen(CustomUIBuilder b)
        {
            BeginBox(b, HalfWidth);
            SectionTitle(b, T("scams_title", "Scams & Stolen Goods"));
            KeyValueRow(b, T("scam_profit", "Scam Profit"), Money(RunLedger.Get(RunLedger.ScamCredits)), RowSize, false);
            KeyValueRow(b, T("scam_count", "Mislabelled Items Sold"), Number(RunLedger.Get(RunLedger.ScamCount)), RowSize, false);
            int biggest = RunLedger.Get(RunLedger.BiggestScamValue);
            if (biggest > 0)
                KeyValueRow(b, T("scam_biggest", "Biggest Scam"), RunLedger.GetText(RunLedger.BiggestScamItem) + " +" + Money(biggest), RowSize, false);
            b.AddSeparator();
            KeyValueRow(b, T("stolen_sold", "Stolen Goods Sold"),
                CreditsAndCount(RunLedger.Get(RunLedger.StolenCredits), RunLedger.Get(RunLedger.StolenCount)), RowSize, false);
            b.End();
        }

        private static string CreditsAndCount(long credits, int count) =>
            T("credits_and_count", "{0} ({1} items)", Money(credits), Number(count));
    }

    [HarmonyPatch(typeof(RunSummaryUI), "AddPage")]
    internal static class SummaryPageBuilderPatch
    {
        private static void Prefix(CustomUIBuilder b) => ShadyDealsTab.MainBuilder = b;
    }

    // Build runs inside RunSummaryUI.Open's try/catch, which throws the whole summary away
    // on an exception, so a failure here must stay here.
    [HarmonyPatch(typeof(RunSummaryUI), "Build")]
    internal static class SummaryBuildPatch
    {
        private static void Prefix() => ShadyDealsTab.MainBuilder = null;

        private static void Postfix() => SaleTracker.Guard("Adding the Shady Deals tab", ShadyDealsTab.Add);
    }
}

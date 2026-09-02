using System;
using System.Text;

namespace HicDraft
{
    /// <summary>
    /// Smoke test for the open sequence, so the plugin can be checked without a human at the keyboard.
    /// Launch the game with HIC_DRAFT_TEST=1: once the compendium exists it opens the draft menu, writes
    /// what it found to the BepInEx log, and closes again. Off in normal play.
    /// </summary>
    internal static class DraftSelfTest
    {
        public static bool Enabled { get; } =
            Environment.GetEnvironmentVariable("HIC_DRAFT_TEST") == "1";

        private const float StartAfterSeconds = 20f;
        private const float ReportAfterOpenSeconds = 2f;

        private static float _elapsed;
        private static bool _opened;
        private static bool _reported;

        public static void Tick(float deltaTime)
        {
            if (!Enabled || _reported) return;

            _elapsed += deltaTime;
            if (_elapsed < StartAfterSeconds) return;

            if (!_opened)
            {
                Report("before open");
                DraftController.Open();
                _opened = true;
                _elapsed = StartAfterSeconds;
                return;
            }

            if (_elapsed < StartAfterSeconds + ReportAfterOpenSeconds) return;

            Report("after open");
            _reported = true;
            DraftController.Close();
        }

        private static void Report(string phase)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Draft][selftest] --- {phase} ---");

            var controls = GameRefs.Controls;
            var compendium = GameRefs.Compendium;
            var overworld = GameRefs.Overworld;
            var items = GameRefs.Items;

            sb.AppendLine($"[Draft][selftest] PlayerControlsManager: {(controls == null ? "null" : "found")}");
            sb.AppendLine($"[Draft][selftest] ItemCompendium:        {(compendium == null ? "null" : "found")}");
            sb.AppendLine($"[Draft][selftest] OverworldUIManager:    {(overworld == null ? "null" : "found")}");
            sb.AppendLine($"[Draft][selftest] ItemManager:           {(items == null ? "null" : "found")}");
            sb.AppendLine($"[Draft][selftest] Armed: {DraftController.Armed}");

            TryLine(sb, "ControlsState", () => controls.GetControlsState().ToString());
            TryLine(sb, "item prefabs", () => items.GetAllItemPrefabs().Count.ToString());
            TryLine(sb, "compendium entries", () => compendium.itemChooseEntries.Count.ToString());
            TryLine(sb, "compendiumPage active", () => compendium.compendiumPage.activeInHierarchy.ToString());
            TryLine(sb, "foregroundPage active", () => compendium.foregroundPage.activeInHierarchy.ToString());
            TryLine(sb, "pageTitle", () => compendium.pageTitle.text);

            Plugin.Log.LogInfo(sb.ToString().TrimEnd());
        }

        private static void TryLine(StringBuilder sb, string label, Func<string> read)
        {
            string value;
            try { value = read() ?? "null"; }
            catch (Exception e) { value = "unavailable (" + e.GetType().Name + ")"; }
            sb.AppendLine($"[Draft][selftest] {label}: {value}");
        }
    }
}

using System;

namespace ShopkeepingSuite
{
    internal static class Counterfeit
    {
        internal static Func<InspectionUIManager, bool> PanelActive = panel => panel.confrontPanel != null && panel.confrontPanel.activeInHierarchy;
        internal static Func<InspectionUIManager, string> ReadHeading = panel => panel.confrontTitleTMP.text;
        internal static Action<InspectionUIManager, string> WriteHeading = (panel, text) => panel.confrontTitleTMP.text = text;
        private static InspectionUIManager changedPanel;
        private static string originalHeading, writtenHeading;
        internal static void Reset()
        {
            try
            {
                if (!ReferenceEquals(changedPanel, null) && ReadHeading(changedPanel) == writtenHeading)
                    WriteHeading(changedPanel, originalHeading);
            }
            catch (Exception error) { SuiteMod.Fault("counterfeit heading cleanup", error); }
            finally { changedPanel = null; originalHeading = writtenHeading = null; }
        }
        internal static string Line(GameItem item)
        {
            if (item == null || !Settings.On("enabled") || !Settings.On("counterfeit_check")) return null;
            var issues = Rules.CounterfeitIssues(tag => Marks.Tag(item, tag));
            return issues == null ? null : Texts.Get("counterfeit_line", issues.Count == 0 ? Texts.Get("counterfeit_sound") : string.Join(", ", issues));
        }
        internal static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            try { string line = Line(item); if (line != null) SuiteRuntime.AddLine(builder, line); }
            catch (Exception error) { SuiteMod.Fault("counterfeit tooltip", error); }
        }
        internal static void Postfix(InspectionUIManager __instance, GameItem __0)
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("counterfeit_check") || !PanelActive(__instance)) return;
                string line = Line(__0);
                if (line == null) return;
                string heading = ReadHeading(__instance), next = Rules.CounterfeitHeading(heading, line);
                if (next != heading)
                {
                    Reset();
                    changedPanel = __instance; originalHeading = heading; writtenHeading = next;
                    WriteHeading(__instance, next);
                }
            }
            catch (Exception error) { SuiteMod.Fault("counterfeit panel", error); }
        }
    }
}

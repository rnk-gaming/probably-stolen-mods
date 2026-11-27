using LocalMods;
using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    internal enum MarkKind { None, SameKind, Stolen, Contraband, Inspection, Wanted, TooDear }
    internal sealed class ItemFacts
    {
        internal bool Outside, Same, Owned, Stolen, Contraband, Flaggable, Concealed, Security, Buyer;
        internal bool EngineColour = false;
        internal int Demand, Heat, Appraisal, InspectionCount, WantedCount, InsideDemand;
        internal string Level;
        internal bool AppraisalItem, Appraiser;
    }
    internal static class Rules
    {
        internal static bool CounterfeitVisible(bool enabled, bool aid, bool panelActive, bool hasItem, List<string> issues, bool checkActive)
            => enabled && aid && panelActive && hasItem && issues != null && !checkActive;
        internal static List<string> CounterfeitIssues(Func<string, bool> tag)
        {
            string[] issues, names;
            if (tag("INS_INJECTOR_TAG"))
            {
                issues = new[] { "ISSUE_INDICATOR", "ISSUE_LABEL", "ISSUE_SERIAL", "ISSUE_BODY", "ISSUE_COLOR" };
                names = new[] { TextKeys.CounterfeitIndicator, TextKeys.CounterfeitLabel, TextKeys.CounterfeitSerial, TextKeys.CounterfeitBody, TextKeys.CounterfeitColor };
            }
            else if (tag("CIGARETTE_TAG"))
            {
                issues = new[] { "CIGARETTE_LETTERING_ISSUE", "CIGARETTE_COLOR_ISSUE", "CIGARETTE_SEAL_ISSUE", "CIGARETTE_LOGO_ISSUE", "CIGARETTE_BOX_ISSUE" };
                names = new[] { TextKeys.CounterfeitLettering, TextKeys.CounterfeitColor, TextKeys.CounterfeitSeal, TextKeys.CounterfeitLogo, TextKeys.CounterfeitBox };
            }
            else if (tag("FOOD_STAMP_TAG"))
            {
                issues = new[] { "STAMP_LETTERING_ISSUE", "STAMP_LOGO_ISSUE", "STAMP_SEAL_ISSUE", "STAMP_AMOUNT_ISSUE", "STAMP_CHIP_ISSUE" };
                names = new[] { TextKeys.CounterfeitLettering, TextKeys.CounterfeitLogo, TextKeys.CounterfeitSeal, TextKeys.CounterfeitAmount, TextKeys.CounterfeitChip };
            }
            else return null;
            var result = new List<string>();
            for (int i = 0; i < issues.Length; i++) if (tag(issues[i])) result.Add(TextTable.Get(names[i]));
            return result;
        }

        internal static string CounterfeitHeading(string heading, string line)
        {
            if (line == null) return heading;
            string addition = "\n<size=80%>" + line + "</size>";
            return (heading ?? "").EndsWith(addition, StringComparison.Ordinal) ? heading : heading + addition;
        }
        internal static MarkKind Mark(ItemFacts f, Func<string, bool> on)
        {
            if (!on("enabled") || f == null || f.Outside || f.EngineColour) return MarkKind.None;
            if (on("mark_same_kind") && f.Same) return MarkKind.SameKind;
            if (on("mark_risk") && !f.Owned)
            {
                if (f.Stolen) return MarkKind.Stolen;
                if (f.Contraband) return MarkKind.Contraband;
            }
            if (on("mark_inspection") && f.Owned && f.Security && ((f.Flaggable && !f.Concealed) || f.InspectionCount > 0)) return MarkKind.Inspection;
            if (on("mark_wanted") && f.Owned && f.Buyer)
            {
                if (f.Demand == 2) return MarkKind.Wanted;
                if (f.Demand == 1) return MarkKind.TooDear;
                if (on("mark_wanted_inside"))
                {
                    if (f.InsideDemand == 2) return MarkKind.Wanted;
                    if (f.InsideDemand == 1) return MarkKind.TooDear;
                }
            }
            return MarkKind.None;
        }
        internal static bool Inspection(bool flaggable, bool behindLock, bool hidden, bool locked) => flaggable && !behindLock && !hidden && !locked;
        internal static List<string> Tooltip(ItemFacts f, Func<string, bool> on)
        {
            var result = new List<string>();
            if (!on("enabled") || f.Outside) return result;
            if (on("tooltip_wanted") && f.Owned && f.Buyer && f.Demand > 0) result.Add(TextTable.Get(f.Demand == 2 ? TextKeys.Wanted : TextKeys.TooDear));
            if (on("tooltip_wanted") && on("mark_wanted") && on("mark_wanted_inside") && f.Owned && f.Buyer && f.WantedCount > 0)
                result.Add(TextTable.Get(TextKeys.WantedInside, f.WantedCount));
            if (on("tooltip_risk") && !f.Owned)
            {
                if (f.Stolen) result.Add(TextTable.Get(TextKeys.Stolen, f.Heat));
                if (f.Contraband && f.Level != "CONTRABAND_LEVEL_NONE")
                    result.Add(string.IsNullOrEmpty(f.Level) ? TextTable.Get(TextKeys.ContrabandPlain) : TextTable.Get(TextKeys.Contraband, f.Level.Replace("CONTRABAND_LEVEL_", "").ToLowerInvariant()));
            }
            if (on("tooltip_risk") && on("mark_inspection") && f.Owned && f.Security && f.InspectionCount > 0)
                result.Add(TextTable.Get(TextKeys.InspectionInside, f.InspectionCount));
            if (on("tooltip_appraisal") && f.AppraisalItem && f.Appraiser && f.Appraisal > 0) result.Add(TextTable.Get(TextKeys.Worth, f.Appraisal));
            return result;
        }
        internal static string Readable(string id, string prefix)
        {
            string text = (id ?? "");
            if (text.StartsWith(prefix, StringComparison.Ordinal)) text = text.Substring(prefix.Length);
            text = text.Replace('_', ' ').ToLowerInvariant();
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        internal static string Intent(string intent)
        {
            return intent switch
            {
                "BUY" => TextTable.Get(TextKeys.IntentBuy), "SELL" => TextTable.Get(TextKeys.IntentSell), "SELLNBUY" => TextTable.Get(TextKeys.IntentSellnbuy),
                "INSPECTION" => TextTable.Get(TextKeys.IntentInspection), "APPRAISAL_SERVICE" => TextTable.Get(TextKeys.IntentAppraisal),
                _ => Readable(intent, "")
            };
        }
        internal static HashSet<GameItem> Containers(IEnumerable<GameItem> items, GameItem dragged,
            Func<GameItem, bool> owned, Func<GameItem, IEnumerable<GameItem>> contents)
        {
            var result = new HashSet<GameItem>();
            if (dragged == null || string.IsNullOrEmpty(dragged.identifier)) return result;
            foreach (GameItem item in items)
            {
                if (item == null || ReferenceEquals(item, dragged) || !owned(item)) continue;
                foreach (GameItem child in contents(item))
                    if (child != null && child.identifier == dragged.identifier) { result.Add(item); break; }
            }
            return result;
        }
    }
}

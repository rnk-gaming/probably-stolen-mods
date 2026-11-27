using LocalMods;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShopkeepingSuite
{
    internal sealed class WindowRow
    {
        internal string Label, Value, Colour, Group, ServiceId;
        internal bool Sum;
        internal string Start, Today, Now;
        internal bool StandingRow, Header, Caption;
        internal bool Section, Note, DailyStanding;
        internal float Y;
        internal long Amount;
        internal WindowRow(string label, string value, string group, string colour = null, bool sum = false)
        { Label = label ?? ""; Value = value ?? ""; Group = group; Colour = colour; Sum = sum; }
    }
    internal sealed class WindowPage
    {
        internal int Tab, Part;
        internal string Title;
        internal List<WindowRow> Rows;
        internal float Pitch = 14;
    }
    internal static partial class WindowRules
    {
        internal const string Green = "2f6b3a", Red = "8a2f2f";
        internal const int RowsPerPage = 12;
        private static IReadOnlyDictionary<string, string> pageLabels;
        internal static void ClearLabels() => pageLabels = null;
        private static string Number(double value, string format = "0.##") => value.ToString(Math.Abs(value) >= 1e12 ? "0.##E+0" : format, CultureInfo.InvariantCulture);
        internal static List<WindowPage> Pages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally)
        {
            string[] keys = { TextKeys.Today, TextKeys.Run, TextKeys.Customer, TextKeys.Standing, TextKeys.MoneyInLabel, TextKeys.MoneyOutLabel, TextKeys.NetLabel, TextKeys.ServiceName, TextKeys.DaysLabel, TextKeys.DayLabel,
                TextKeys.Yesterday, TextKeys.LastDay, TextKeys.StandingToday, TextKeys.StandingStart, TextKeys.StandingNow, TextKeys.Nobody, TextKeys.NameLabel, TextKeys.FactionLabel, TextKeys.IntentLabel, TextKeys.BudgetLabel, TextKeys.NoBudget, TextKeys.WantedLabel, TextKeys.AffordableLabel, TextKeys.NoStanding, TextKeys.OtherServices, TextKeys.AndMore,
                TextKeys.SectionSales, TextKeys.SectionPurchases, TextKeys.SectionCosts, TextKeys.SectionOtherCosts, TextKeys.SectionResult, TextKeys.DailyGained, TextKeys.DailyLost, TextKeys.DailyChange, TextKeys.NoChangeToday };
            // Resolve translations at the boundary; composition below only uses supplied figures and text.
            pageLabels ??= keys.Concat(LedgerRules.Keys).Concat(StandingRules.LabelKeys).Distinct().ToDictionary(k => k, k => TextTable.Get(k, "{0}"));
            return ComposePages(ledger, customer, standing, tally, pageLabels);
        }
        internal static List<WindowPage> ComposePages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally, IReadOnlyDictionary<string, string> labels)
        {
            var factions = standing.ToList();
            string[] titles = { TextKeys.Today, TextKeys.Run, TextKeys.Customer, TextKeys.Standing };
            var pages = new List<WindowPage>();
            for (int tab = 0; tab < 4; tab++)
            {
                var rows = Rows(tab, ledger, customer, factions, tally, labels, true);
                if (tab == 3 && factions.Count > 0) rows.Insert(0, StandingHeader(labels));
                var page = new WindowPage { Tab = tab, Part = 0, Title = labels[titles[tab]], Rows = rows };
                if (tab < 2) PaperRules.Layout(page, labels[TextKeys.OtherServices], labels[TextKeys.AndMore]);
                else
                {
                    page.Rows = tab == 3 ? PaperRules.FoldStanding(rows, labels[TextKeys.AndMore]) : rows;
                    for (int i = 0; i < page.Rows.Count; i++) page.Rows[i].Y = 62 + 14 * i;
                }
                pages.Add(page);
            }
            return pages;
        }
        internal static List<WindowPage> PlainPages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally)
        {
            if (pageLabels == null) Pages(ledger, customer, standing, tally);
            return ComposePlainPages(ledger, customer, standing, tally, pageLabels);
        }
        private static WindowRow StandingHeader(IReadOnlyDictionary<string, string> labels) =>
            new WindowRow(labels[TextKeys.FactionLabel], labels[TextKeys.StandingStart] + " | " + labels[TextKeys.Today] + " | " + labels[TextKeys.StandingNow], "standing")
                { StandingRow = true, Header = true, Start = labels[TextKeys.StandingStart], Today = labels[TextKeys.Today], Now = labels[TextKeys.StandingNow] };
        private static List<WindowPage> ComposePlainPages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally, IReadOnlyDictionary<string, string> labels)
        {
            var result = new List<WindowPage>();
            string[] titles = { TextKeys.Today, TextKeys.Run, TextKeys.Customer, TextKeys.Standing };
            var factions = standing.ToList();
            for (int tab = 0; tab < 4; tab++)
            {
                List<WindowRow> rows = Rows(tab, ledger, customer, factions, tally, labels);
                for (int offset = 0, part = 0; offset < rows.Count; part++)
                {
                    int count = Math.Min(tab == 3 && factions.Count > 0 ? RowsPerPage - 1 : RowsPerPage, rows.Count - offset);
                    // A description belongs to the name immediately above it, even at a page boundary.
                    if (offset + count < rows.Count && rows[offset + count].Caption) count--;
                    // Keep a section heading with its first row on the following page.
                    if (count > 1 && rows[offset + count - 1].Section) count--;
                    var pageRows = rows.Skip(offset).Take(count).ToList();
                    if (tab == 3 && factions.Count > 0)
                        pageRows.Insert(0, new WindowRow(labels[TextKeys.FactionLabel], labels[TextKeys.StandingStart] + " | " + labels[TextKeys.Today] + " | " + labels[TextKeys.StandingNow], "standing")
                            { StandingRow = true, Header = true, Start = labels[TextKeys.StandingStart], Today = labels[TextKeys.Today], Now = labels[TextKeys.StandingNow] });
                    result.Add(new WindowPage { Tab = tab, Part = part, Title = labels[titles[tab]] + (part == 0 ? "" : " (" + (part + 1).ToString(CultureInfo.InvariantCulture) + ")"), Rows = pageRows });
                    offset += count;
                }
            }
            return result;
        }
        private static List<WindowRow> Rows(int tab, LedgerState ledger, CustomerView customer, List<StandingView> standing, bool tally, IReadOnlyDictionary<string, string> labels, bool paper = false)
        {
            if (tab < 2) return SettlementRows(tab, ledger, standing, tally, labels, paper);
            string Text(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, labels[key], values);
            var rows = new List<WindowRow>();
            void Add(string label, string value, string group) => rows.Add(new WindowRow(label, value, group));
            if (tab == 2)
            {
                if (!customer.Present) Add(Text(TextKeys.Nobody), "", "customer");
                else
                {
                    Add(Text(TextKeys.NameLabel), customer.Name, "identity");
                    Add(Text(TextKeys.FactionLabel), customer.Faction, "identity");
                    Add(Text(TextKeys.IntentLabel), customer.Intent, "identity");
                    Add(customer.HasBudget ? Text(TextKeys.BudgetLabel) : Text(TextKeys.NoBudget), customer.HasBudget ? Number(customer.Budget) : "", "budget");
                    Add(Text(TextKeys.WantedLabel), Number(customer.Wanted), "wanted");
                    Add(Text(TextKeys.AffordableLabel), Number(customer.Affordable), "wanted");
                }
            }
            else
            {
                foreach (StandingView faction in standing)
                {
                    StandingTally changes = null;
                    if (faction.Id != null) ledger.Standing.TryGetValue(faction.Id, out changes);
                    string Decimal(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("F1", CultureInfo.InvariantCulture);
                    double net = Math.Round(tally ? (changes?.Gained ?? 0) - (changes?.Lost ?? 0) : 0, 1, MidpointRounding.AwayFromZero);
                    var row = new WindowRow(StandingRules.Clean(faction.Faction), "", "standing", net > 0 ? Green : net < 0 ? Red : null)
                    {
                        StandingRow = true, Start = tally ? Decimal(changes?.HasStart == true ? changes.Start : faction.Value) : "-",
                        Today = net == 0 ? "-" : (net > 0 ? "+" : "") + Decimal(net), Now = Decimal(faction.Value)
                    };
                    row.Value = row.Start + " | " + (row.Colour == null ? row.Today : "<color=#" + row.Colour + ">" + row.Today + "</color>") + " | " + row.Now;
                    rows.Add(row);
                }
                if (tally)
                    foreach (StandingView faction in standing)
                    {
                        if (faction.Id == null || !ledger.Standing.TryGetValue(faction.Id, out StandingTally start) || !start.HasStart) continue;
                        foreach (StandingChange change in StandingRules.Changes(faction, start.Start, faction.Value, labels))
                        {
                            Add(StandingRules.Clean(faction.Faction) + ": " + change.Text, "", "standing_changes");
                            string description = paper ? change.FullDescription : change.Description;
                            if (description.Length > 0) rows.Add(new WindowRow(description, "", "standing_changes") { Caption = true });
                        }
                    }
                if (rows.Count == 0) Add(Text(TextKeys.NoStanding), "", "standing");
            }
            return rows;
        }
    }
}

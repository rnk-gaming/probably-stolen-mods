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
            string[] keys = { "today", "run", "customer", "standing", "money_in_label", "money_out_label", "net_label", "service_name", "days_label", "day_label",
                "yesterday", "last_day", "standing_today", "standing_start", "standing_now", "nobody", "name_label", "faction_label", "intent_label", "budget_label", "no_budget", "wanted_label", "affordable_label", "no_standing", "other_services", "and_more",
                "section_sales", "section_purchases", "section_costs", "section_other_costs", "section_result", "daily_gained", "daily_lost", "daily_change", "no_change_today" };
            // Resolve translations at the boundary; composition below only uses supplied figures and text.
            pageLabels ??= keys.Concat(LedgerRules.Keys).Concat(StandingRules.TextKeys).Distinct().ToDictionary(k => k, k => Texts.Get(k, "{0}"));
            return ComposePages(ledger, customer, standing, tally, pageLabels);
        }
        internal static List<WindowPage> ComposePages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally, IReadOnlyDictionary<string, string> labels)
        {
            var factions = standing.ToList();
            string[] titles = { "today", "run", "customer", "standing" };
            var pages = new List<WindowPage>();
            for (int tab = 0; tab < 4; tab++)
            {
                var rows = Rows(tab, ledger, customer, factions, tally, labels, true);
                if (tab == 3 && factions.Count > 0) rows.Insert(0, StandingHeader(labels));
                var page = new WindowPage { Tab = tab, Part = 0, Title = labels[titles[tab]], Rows = rows };
                if (tab < 2) PaperRules.Layout(page, labels["other_services"], labels["and_more"]);
                else
                {
                    page.Rows = tab == 3 ? PaperRules.FoldStanding(rows, labels["and_more"]) : rows;
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
            new WindowRow(labels["faction_label"], labels["standing_start"] + " | " + labels["today"] + " | " + labels["standing_now"], "standing")
                { StandingRow = true, Header = true, Start = labels["standing_start"], Today = labels["today"], Now = labels["standing_now"] };
        private static List<WindowPage> ComposePlainPages(LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing, bool tally, IReadOnlyDictionary<string, string> labels)
        {
            var result = new List<WindowPage>();
            string[] titles = { "today", "run", "customer", "standing" };
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
                        pageRows.Insert(0, new WindowRow(labels["faction_label"], labels["standing_start"] + " | " + labels["today"] + " | " + labels["standing_now"], "standing")
                            { StandingRow = true, Header = true, Start = labels["standing_start"], Today = labels["today"], Now = labels["standing_now"] });
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
                if (!customer.Present) Add(Text("nobody"), "", "customer");
                else
                {
                    Add(Text("name_label"), customer.Name, "identity");
                    Add(Text("faction_label"), customer.Faction, "identity");
                    Add(Text("intent_label"), customer.Intent, "identity");
                    Add(customer.HasBudget ? Text("budget_label") : Text("no_budget"), customer.HasBudget ? Number(customer.Budget) : "", "budget");
                    Add(Text("wanted_label"), Number(customer.Wanted), "wanted");
                    Add(Text("affordable_label"), Number(customer.Affordable), "wanted");
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
                if (rows.Count == 0) Add(Text("no_standing"), "", "standing");
            }
            return rows;
        }
    }
}

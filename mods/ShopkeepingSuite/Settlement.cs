using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShopkeepingSuite
{
    internal static partial class WindowRules
    {
        // Only the supplied ledger, faction readings and translations are used here.
        private static List<WindowRow> SettlementRows(int tab, LedgerState ledger, List<StandingView> factions, bool tally, IReadOnlyDictionary<string, string> labels, bool paper)
        {
            var rows = new List<WindowRow>();
            Totals sums = tab == 0 ? ledger.Today : LedgerRules.Combined(ledger);
            void Heading(string key) => rows.Add(new WindowRow(labels[key].ToUpperInvariant(), "", "section") { Section = true });
            void Total(string key, bool note = false) => rows.Add(new WindowRow(labels[key], Number(sums.Get(key)), "details") { Note = note });
            Heading("section_sales"); Total("items_sold"); Total("sales");
            Heading("section_purchases"); Total("items_bought"); Total("purchases");
            string[] costs = tab == 0 ? new[] { "services", "rent", "upgrades", "mortgage", "fees" } : new[] { "rent", "upgrades", "services", "mortgage", "fees" };
            if (tab == 1 || costs.Any(k => sums.Get(k) != 0))
            {
                Heading(tab == 0 ? "section_other_costs" : "section_costs");
                foreach (string key in costs)
                    if (tab == 1 || sums.Get(key) != 0)
                    {
                        // Retain the less common ledger categories without adding empty rows.
                        if ((key == "mortgage" || key == "fees") && sums.Get(key) == 0) continue;
                        Total(key);
                    }
                if (tab == 1)
                    foreach (var pair in sums.Sums.OrderBy(p => p.Key, StringComparer.Ordinal))
                        if (pair.Key.StartsWith("service_", StringComparison.Ordinal) && pair.Value != 0)
                            rows.Add(new WindowRow(string.Format(CultureInfo.InvariantCulture, labels["service_name"], paper ? LedgerRules.ServiceName(pair.Key) : Rules.Readable(LedgerRules.ServiceName(pair.Key), "")), Number(pair.Value), "services") { Amount = pair.Value, ServiceId = LedgerRules.ServiceName(pair.Key) });
            }
            Heading("section_result");
            rows.Add(new WindowRow(labels["money_in_label"], Number(LedgerRules.MoneyIn(sums)), "money"));
            rows.Add(new WindowRow(labels["money_out_label"], Number(LedgerRules.MoneyOut(sums)), "money"));
            int net = LedgerRules.Net(sums);
            rows.Add(new WindowRow(labels["net_label"].ToUpperInvariant(), Number(net), "sum", net >= 0 ? Green : Red, true));
            Total("margin", true);
            if (tab == 1) rows.Add(new WindowRow(labels["days_label"], Number(LedgerRules.Cap((long)ledger.Days + (ledger.Day >= 0 ? 1 : 0))), "context") { Note = true });
            else if (ledger.YesterdayDay >= 0)
                rows.Add(new WindowRow(labels[ledger.YesterdayDay == ledger.Day - 1 ? "yesterday" : "last_day"] + " (" + string.Format(CultureInfo.InvariantCulture, labels["day_label"], ledger.YesterdayDay) + ")", Number(LedgerRules.Net(ledger.Yesterday)), "context") { Note = true });
            foreach (string key in new[] { "other", "seized", "burgled" }) if (sums.Get(key) != 0) Total(key, true);
            if (tab == 0 && tally)
            {
                Heading("standing_today");
                var changed = ledger.Standing.Where(p => p.Value.Gained != 0 || p.Value.Lost != 0).OrderBy(p => p.Value.Name, StringComparer.Ordinal).ToList();
                if (changed.Count == 0) rows.Add(new WindowRow(labels["no_change_today"], "", "standing_changes"));
                else
                {
                    WindowRow Table(string name, string gained, string lost, string change, string colour, bool header = false)
                    {
                        string Paint(string value, string ink) => value == "-" || header ? value : "<color=#" + ink + ">" + value + "</color>";
                        return new WindowRow(name, Paint(gained, Green) + " | " + Paint(lost, Red) + " | " + (colour == null ? change : Paint(change, colour)), "daily_standing", colour)
                        { StandingRow = true, DailyStanding = true, Header = header, Start = gained, Today = lost, Now = change };
                    }
                    rows.Add(Table(labels["faction_label"], labels["daily_gained"], labels["daily_lost"], labels["daily_change"], null, true));
                    foreach (var pair in changed)
                    {
                        double change = pair.Value.Gained - pair.Value.Lost;
                        string Signed(double value) => value == 0 ? "-" : (value > 0 ? "+" : "") + Number(value);
                        rows.Add(Table(pair.Value.Name, Signed(pair.Value.Gained), Signed(-pair.Value.Lost), Signed(change), change > 0 ? Green : change < 0 ? Red : null));
                    }
                }
                foreach (StandingView faction in factions)
                    if (faction.Id != null && ledger.Standing.TryGetValue(faction.Id, out StandingTally start) && start.HasStart)
                        foreach (StandingChange change in StandingRules.Changes(faction, start.Start, faction.Value, labels))
                        {
                            rows.Add(new WindowRow(StandingRules.Clean(faction.Faction) + ": " + change.Text, "", "standing_changes"));
                            string description = paper ? change.FullDescription : change.Description;
                            if (description.Length > 0) rows.Add(new WindowRow(description, "", "standing_changes") { Caption = true });
                        }
            }
            return rows;
        }
    }
}

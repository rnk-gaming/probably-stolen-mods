using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShopkeepingSuite
{
    internal sealed class PriceEntry
    {
        internal string Tag, Name;
        internal int Value, Group;
    }
    internal sealed class PriceAnswer
    {
        internal int Total;
        internal double Factor;
        internal float MarketUntil = float.PositiveInfinity;
        internal List<PriceEntry> Events;
        internal string SignType;
        internal bool MarketRead;
        internal int MarketCount, MarketSeen;
    }
    internal static class PriceRules
    {
        internal static int EventShift(IEnumerable<PriceEntry> entries, Func<string, bool> hasTag)
        {
            long sum = 0;
            foreach (PriceEntry entry in entries) if (hasTag(entry.Tag)) sum += entry.Value;
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, sum));
        }
        internal static bool ValidFactor(double factor) => factor > 0 && !double.IsInfinity(factor) && !double.IsNaN(factor);
        internal static double Factor(double factor) => ValidFactor(factor) ? factor : 1;
        // Bound the displayed change to -100% .. +10000%; never overflow a UI integer.
        internal static int Total(int events, double factor) => Rounded(((1 + events / 100.0) * Factor(factor) - 1) * 100);
        // Remove binary floating-point noise at decimal half-percent boundaries.
        private static int Rounded(double value) => (int)Math.Round(Math.Round(Math.Max(-100, Math.Min(10000, value)), 8), MidpointRounding.AwayFromZero);
        internal static int Mark(int total, int margin, int strong)
        {
            margin = Math.Max(1, Math.Min(100, margin)); strong = Math.Max(margin, Math.Min(300, strong));
            long magnitude = Math.Abs((long)total);
            return magnitude < margin ? 0 : Math.Sign(total) * (magnitude >= strong ? 2 : 1);
        }
        internal static string Name(string name)
        {
            var clean = new StringBuilder(); bool tag = false, space = false;
            foreach (char c in name ?? "")
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (tag || char.IsControl(c) && !char.IsWhiteSpace(c)) continue;
                if (char.IsWhiteSpace(c)) { space = clean.Length > 0; continue; }
                if (space) { clean.Append(' '); space = false; }
                clean.Append(c);
            }
            string value = clean.ToString();
            return value.Length <= 24 ? value : value.Substring(0, char.IsHighSurrogate(value[23]) ? 23 : 24);
        }
        private static string Signed(long value) => (value > 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture) + "%";
        internal static string Tooltip(PriceAnswer answer, IDictionary<string, string> text)
        {
            if (answer == null || answer.Total == 0 && answer.SignType == null) return "";
            bool sign = answer.SignType != null;
            var events = answer.Events.GroupBy(e => (e.Group, e.Name))
                .Select(g => new { Name = Name(g.Key.Name), Value = g.Sum(e => (long)e.Value) }).Where(e => e.Value != 0).ToList();
            var parts = events.Take(3).Select(e => (e.Name.Length == 0 ? text["price_event"] : e.Name) + " " + Signed(e.Value)).ToList();
            if (events.Count > 3) parts.Add(string.Format(CultureInfo.InvariantCulture, text["price_more"], events.Count - 3));
            int market = Total(0, answer.Factor);
            if (market != 0) parts.Add(sign
                ? string.Format(CultureInfo.InvariantCulture, text["price_sign_market"], Signed(market), answer.MarketCount)
                : text["price_market"] + " " + Signed(market));
            string line = sign
                ? string.Format(CultureInfo.InvariantCulture, text["price_sign_now"], text[SignPrices.NameKey(answer.SignType)], answer.Total == 0 ? text["price_sign_usual"] : Signed(answer.Total))
                : string.Format(CultureInfo.InvariantCulture, text["price_now"], Signed(answer.Total));
            line += parts.Count == 0 ? "" : " (" + string.Join(", ", parts) + ")";
            if (sign && answer.MarketRead && answer.MarketCount == 0)
                line += "; " + text[answer.MarketSeen == 0 ? "price_sign_empty" : "price_sign_unknown"];
            return line;
        }
    }
}

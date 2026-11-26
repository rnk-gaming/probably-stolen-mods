using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopkeepingSuite
{
    internal sealed class TabletPage
    {
        internal string Id, Faction, Caption;
        internal double Amount;
        internal string Text => Id == null ? Caption : Faction + "\n" + Math.Round(Amount, 2, MidpointRounding.AwayFromZero).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
    }
    internal sealed class TabletRectangle
    {
        internal double X, Y, Width, Height;
    }
    // No engine types or calls. Width and measure must use the same units.
    internal static class TabletRules
    {
        internal static List<TabletPage> InspectionPages(List<string> issues, Func<string, double> measure, double width)
        {
            if (issues == null) return new List<TabletPage>();
            // The heading always owns the first line. Subsequent pages contain two issue lines.
            var lines = new List<string> { Texts.Get(issues.Count == 0 ? "counterfeit_no_faults" : "counterfeit_faults") };
            lines.AddRange(Wrap(issues.Count == 0 ? Texts.Get("counterfeit_sound") : string.Join(", ", issues), measure, width));
            return LinePages(lines);
        }
        internal static bool IsTablet(string name) => name == "negociation_tablet" || (name?.EndsWith("_negociation_tablet", StringComparison.Ordinal) ?? false);
        internal static TabletRectangle Layout(double width, double height, double pivotX, double pivotY, double pixelsPerUnit, bool flipX, bool flipY)
        {
            if (width <= 0 || height <= 0 || pixelsPerUnit <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            return new TabletRectangle
            {
                X = (714.5 * width / 960 - pivotX) / pixelsPerUnit * (flipX ? -1 : 1),
                Y = (334.5 * height / 540 - pivotY) / pixelsPerUnit * (flipY ? -1 : 1),
                Width = 67 * width / 960 / pixelsPerUnit, Height = 33 * height / 540 / pixelsPerUnit
            };
        }
        internal static List<TabletPage> Pages(string id, string faction, double amount, IEnumerable<string> crossed, Func<string, double> measure, double width)
        {
            if (measure == null || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var pages = new List<TabletPage> { new TabletPage { Id = id, Faction = StandingRules.Clean(faction), Amount = amount } };
            foreach (string caption in crossed)
            {
                pages.AddRange(LinePages(Wrap(caption, measure, width)));
            }
            return pages;
        }
        private static List<TabletPage> LinePages(List<string> lines)
        {
            var pages = new List<TabletPage>();
            for (int i = 0; i < lines.Count; i += 2)
                pages.Add(new TabletPage { Caption = lines[i] + (i + 1 < lines.Count ? "\n" + lines[i + 1] : "") });
            return pages;
        }
        private static List<string> Wrap(string caption, Func<string, double> measure, double width)
        {
            if (measure == null || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var lines = new List<string>(); string line = "";
            foreach (string word in StandingRules.Clean(caption).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && measure(line + " " + word) <= width) { line += " " + word; continue; }
                if (line.Length > 0) { lines.Add(line); line = ""; }
                // Split a word that is too wide; always make progress for a wide glyph.
                foreach (char c in word)
                {
                    if (line.Length > 0 && measure(line + c) > width) { lines.Add(line); line = ""; }
                    line += c;
                }
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }
    }
    internal sealed class TabletQueue
    {
        // The presentation clock advances only after a successful draw. Hold timing uses
        // the same clock, so invisibility and magnifier pre-emption cannot double-count.
        private double clock, lastTime;
        private bool timed, shown = true;
        private void Advance(double now)
        {
            if (!timed) { clock = now; timed = true; }
            else if (shown) clock += Math.Max(0, now - lastTime);
            lastTime = now;
        }
        internal void Shown(double now, bool visible) { Advance(now); shown = visible; }
        internal object HeldItem;
        private List<TabletPage> held;
        private double heldAt, pausedAt;
        internal void Hold(object item, List<TabletPage> pages, double now)
        {
            Advance(now); now = clock;
            if (ReferenceEquals(HeldItem, item)) return;
            if (item != null && (pages == null || pages.Count == 0)) throw new ArgumentException("A held item needs pages", nameof(pages));
            if (HeldItem == null && item != null) pausedAt = now;
            if (HeldItem != null && item == null && current != null) until += Math.Max(0, now - pausedAt);
            HeldItem = item; held = pages; heldAt = now;
        }
        internal readonly List<TabletPage> Waiting = new List<TabletPage>();
        private TabletPage current;
        private double until;
        internal bool HasPages => HeldItem != null || current != null || Waiting.Count > 0;
        internal void Add(TabletPage page)
        {
            if (page.Id != null)
            {
                TabletPage waiting = Waiting.Find(p => p.Id == page.Id);
                if (waiting != null) { waiting.Amount += page.Amount; return; }
            }
            Waiting.Add(page);
            if (Waiting.Count > 12) Waiting.RemoveAt(0);
        }
        internal TabletPage Step(double now)
        {
            Advance(now); now = clock;
            if (HeldItem != null) return held[(int)(Math.Max(0, Math.Floor((now - heldAt) / 3)) % held.Count)];
            if (current != null && now < until) return current;
            current = null;
            if (Waiting.Count > 0) { current = Waiting[0]; Waiting.RemoveAt(0); until = now + 3; }
            return current;
        }
        internal void Reset() { current = null; Waiting.Clear(); until = 0; HeldItem = null; held = null; heldAt = pausedAt = 0; clock = lastTime = 0; timed = false; shown = true; }
    }
}

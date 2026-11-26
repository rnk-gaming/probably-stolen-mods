using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShopkeepingSuite
{
    // These decisions use no Unity or game calls.
    internal static class PaperRules
    {
        internal const int RowsPerPage = 20;
        internal const string Ink = "3a3848", Green = "3c6848", Red = "b45460";
        internal const float FlipLeftX = 95, FlipRightX = 203;
        internal static bool CanTurn(float width, float height, bool fixedAnchors) => fixedAnchors && width > 0 && height > 0 && !float.IsInfinity(width) && !float.IsInfinity(height);
        internal static float[] CentreShift(float width, float height, float pivotX, float pivotY) => new[] { (.5f - pivotX) * width, (.5f - pivotY) * height };
        // x/y are top-left coordinates. The stamp sits in the empty foot of the sheet, lower left, with air
        // to the edges: 78 by 54 about (62, 352), turned it spans about 17 to 107
        // across and 315 to 389 down on a sheet of 314 by 402. The lettering centre is 18 below the frame centre.
        internal const float StampWidth = 78, StampHeight = 54, StampCentreX = 62, StampCentreY = 352, LetteringBelow = 18, LetteringHeight = 14;
        internal const float StampX = StampCentreX - StampWidth / 2, StampY = StampCentreY - StampHeight / 2;
        internal const float LetteringX = StampX, LetteringY = StampCentreY + LetteringBelow - LetteringHeight / 2;
        internal static float[] StampGeometry()
        {
            double angle = 17 * Math.PI / 180;
            return new[] { StampX, StampY, StampWidth, StampHeight, -17f, StampCentreX - LetteringBelow * (float)Math.Sin(angle), StampCentreY + LetteringBelow * (float)Math.Cos(angle) };
        }
        internal static bool Fits(List<WindowPage> pages) => pages.Count == 4 && pages.All(p => p.Tab < 2
            ? p.Pitch >= 12 && p.Rows.All(r => r.Y >= 62 && r.Y + p.Pitch <= 344)
            : p.Rows.Count <= RowsPerPage);
        // Complete settlement layout: folding, omissions, pitch and positions. No engine calls.
        internal static void Layout(WindowPage page, string otherTemplate, string moreTemplate)
        {
            var rows = new List<WindowRow>(page.Rows);
            float Air() => (Math.Max(0, rows.Count(r => r.Section) - 1) + rows.Count(r => r.Sum)) * 3;
            bool FitsAt(float pitch) => rows.Count * pitch + Air() <= 282;
            if (!FitsAt(14))
            {
                int capacity = (int)((282 - Air()) / 12);
                rows = FoldTo(rows, otherTemplate, capacity);
                int omitted = 0;
                while (rows.Count * 12 + Air() + (omitted > 0 ? 12 : 0) > 282)
                {
                    int index = rows.FindLastIndex(r => r.Caption);
                    if (index < 0) index = rows.FindLastIndex(r => r.Group == "standing_changes");
                    if (index < 0) index = rows.FindLastIndex(r => r.DailyStanding && !r.Header);
                    if (index < 0) break; // Caller uses the plain report when essential totals cannot fit.
                    rows.RemoveAt(index); omitted++;
                }
                if (omitted > 0) rows.Add(new WindowRow(string.Format(CultureInfo.InvariantCulture, moreTemplate, omitted), "", "standing_changes"));
            }
            page.Pitch = FitsAt(14) ? 14 : 12;
            float y = 62;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0 && rows[i].Section) y += 3;
                rows[i].Y = y; y += page.Pitch + (rows[i].Sum ? 3 : 0);
            }
            page.Rows = rows;
        }
        internal static List<WindowRow> FoldStanding(List<WindowRow> rows, string moreTemplate)
        {
            if (rows.Count <= RowsPerPage) return rows;
            var result = new List<WindowRow>(rows);
            // Reserve a row for the omission count. The existing order of change rows
            // is retained; descriptions go first, then changes from the front.
            while (result.Count >= RowsPerPage)
            {
                int index = result.FindIndex(r => r.Caption);
                if (index < 0) index = result.FindIndex(r => r.Group == "standing_changes");
                if (index < 0) return rows; // Too many faction totals: plain report for this opening.
                result.RemoveAt(index);
            }
            result.Add(new WindowRow(string.Format(CultureInfo.InvariantCulture, moreTemplate, rows.Count - result.Count), "", "standing_changes"));
            return result;
        }
        internal static List<WindowRow> FoldServices(List<WindowRow> rows, string otherTemplate)
            => FoldTo(rows, otherTemplate, RowsPerPage);
        private static List<WindowRow> FoldTo(List<WindowRow> rows, string otherTemplate, int capacity)
        {
            int excess = rows.Count - capacity;
            if (excess <= 0) return rows;
            var services = rows.Where(r => r.Group == "services").OrderBy(r => Math.Abs(r.Amount)).ThenBy(r => r.Label, StringComparer.Ordinal).ToList();
            int count = Math.Min(services.Count, excess + 1);
            if (count < 2) return rows;
            var folded = new HashSet<WindowRow>(services.Take(count));
            var result = new List<WindowRow>();
            long amount = services.Take(count).Sum(r => r.Amount);
            bool inserted = false;
            foreach (var row in rows)
            {
                if (!folded.Contains(row)) result.Add(row);
                else if (!inserted)
                {
                    result.Add(new WindowRow(string.Format(CultureInfo.InvariantCulture, otherTemplate, count), amount.ToString(CultureInfo.InvariantCulture), "services") { Amount = amount });
                    inserted = true;
                }
            }
            return result;
        }
        // Conservative serif advance estimates; shrink first, then ellipsise at the minimum.
        internal static float Width(string text, float size) => (text ?? "").Sum(c => "il.,' :;!|".Contains(c) ? .3f : "MW@%".Contains(c) ? .9f : .6f) * size;
        internal static float LabelSize(string text, float width) => Math.Max(7, Math.Min(9, 9 * width / Math.Max(width, Width(text, 9))));
        internal static string Label(string text, float width)
        {
            text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ');
            float size = LabelSize(text, width);
            if (Width(text, size) <= width) return text;
            while (text.Length > 0 && Width(text + "\u2026", 7) > width) text = text.Substring(0, text.Length - 1);
            return text + "\u2026";
        }
        internal static string Hint(string template, string key) => string.Format(CultureInfo.InvariantCulture, template, key);
    }
    internal sealed class PaperDayState
    {
        private int day = -1, closed = -1, pending = -1, notified = -1;
        internal void Loaded(int current, bool storeClosed) { Observe(current); if (storeClosed) ShutterClosed(current); }
        internal void CancelHint() => pending = -1;
        internal void Observe(int current)
        {
            if (day == current) return;
            day = current; closed = pending = notified = -1;
        }
        internal void ShutterClosed(int current) { Observe(current); if (current >= 0) closed = current; }
        internal bool Stamp(int current) { Observe(current); return current >= 0 && closed == current; }
        internal void AutoOpened(int current) { Observe(current); if (current >= 0) pending = current; }
        internal bool TakeHint(int current)
        {
            Observe(current);
            if (current < 0 || pending != current || notified == current) return false;
            pending = -1; notified = current; return true;
        }
    }
}

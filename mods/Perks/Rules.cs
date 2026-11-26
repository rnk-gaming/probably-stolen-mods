using System;
using System.Globalization;

namespace Perks
{
    // All balancing numbers live here. Price adjustments use decimal arithmetic.
    internal static class Rules
    {
        internal const int SummaryRowSize = 8;
        internal static int[][] SummaryRows(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var rows = new int[count == 0 ? 0 : 1 + (count - 1) / SummaryRowSize][];
            int index = 0;
            for (int row = 0; row < rows.Length; row++)
            {
                rows[row] = new int[Math.Min(SummaryRowSize, count - index)];
                for (int column = 0; column < rows[row].Length; column++) rows[row][column] = index++;
            }
            return rows;
        }
        internal const int ValuerPercent = 25, AdvertChance = 25, BlackoutPercent = 115, LuxuryPercent = 90,
            LuxuryBudgetPercent = 200, TrustedPercent = 10, GreedyPercent = 10, ReputationPercent = 15,
            BargainLimit = 10, CertainChance = 100, NightVisits = 3, WindowBonus = 50, WealthPercent = 125,
            OpeningPercent = 10, OpeningCap = 25, OpeningSales = 3,
            ExtraSlots = 12, SpecialBonus = 15, SpecialPenalty = 10, CheapValue = 50,
            DearValue = 200, LargeArea = 6, SmallArea = 2, PremiumSale = 5, PremiumPurchase = 10,
            HeatPercent = 125, InspectionPercent = 75, InspectionMultiplier = 2, InspectionFloorMin = 1, InspectionFloorMax = 2,
            ShabbyLoss = 150, RenovationPercent = 125, TightPercent = 85, WoundBonus = 5, WoundCap = 100,
            AugChance = 25, AugLimit = 2, SearchChance = 25, ServicePercent = 25, SupplierInterval = 4,
            FeePercent = 5, DailyFeeCap = 10, WeekLength = 7, CoolingPercent = 85;
        private const uint HashOffset = 2166136261, HashPrime = 16777619;
        internal static long Percent(long value, decimal percent) => value <= 0 ? value :
            (long)Math.Min(long.MaxValue, Math.Max(1m, Math.Round(value * percent / 100m, MidpointRounding.AwayFromZero)));
        private static int Cap(decimal value) => (int)Math.Min(int.MaxValue, Math.Max(int.MinValue, value));
        internal static int Bargain(int value, int percent) => value > 0 && value < CertainChance && Math.Abs((long)percent) <= BargainLimit ? CertainChance : value;
        internal static float Wound(float value) => value > 0 ? Math.Min(WoundCap, value + WoundBonus) : value;
        internal static int Week(int day, int start) => (int)(Math.Max(0L, (long)day - start) / WeekLength);
        internal static int WeekDay(int day, int start) => (int)(Math.Max(0L, (long)day - start) % WeekLength) + 1;
        internal static int LuxuryDay(string run, int week)
        {
            uint hash = HashOffset;
            foreach (char c in (run ?? "") + "|" + week.ToString(CultureInfo.InvariantCulture)) hash = unchecked((hash ^ c) * HashPrime);
            return (int)(hash % WeekLength) + 1;
        }
        internal static long Sale(long value, bool ordinary, bool luxury, bool premium, int specialisation, bool technology, long baseValue, long area, bool contents, bool blackout)
        {
            if (ordinary)
            {
                int percent = 100 + (premium ? PremiumSale : 0);
                if (!contents)
                {
                    if (specialisation == 1) percent += technology ? SpecialBonus : -SpecialPenalty;
                    if (specialisation == 2) percent += baseValue <= CheapValue ? SpecialBonus : baseValue >= DearValue ? -SpecialPenalty : 0;
                    if (specialisation == 3) percent += area >= LargeArea ? SpecialBonus : area <= SmallArea ? -SpecialPenalty : 0;
                }
                value = Percent(value, percent);
                if (blackout) value = Percent(value, BlackoutPercent);
            }
            return luxury ? Percent(value, LuxuryPercent) : value;
        }
        internal static long Purchase(long value, bool ordinary, bool trusted, bool greedy, bool premium) => ordinary ?
            Percent(value, 100 - (trusted ? TrustedPercent : 0) + (greedy ? GreedyPercent : 0) + (premium ? PremiumPurchase : 0)) : value;
        internal static int Wealth(int value) => Cap(Percent(value, WealthPercent));
        internal static int Tight(int value) => value <= 0 ? value : Math.Max(1, Cap(decimal.Floor(value * (decimal)TightPercent / 100)));
        internal static int Attractiveness(int value, bool shabby, bool window, bool goods)
        {
            if (shabby) value = (int)Math.Max(0L, (long)value - ShabbyLoss);
            return window && goods ? Cap((decimal)value + WindowBonus) : value;
        }
        internal static int Renovation(int value) => value <= 0 ? value : Cap(decimal.Ceiling(value * (decimal)RenovationPercent / 100));
        internal static int Heat(int value) => value <= 0 ? value : Cap(decimal.Ceiling(value * (decimal)HeatPercent / 100));
        internal static int Cool(int value) => Math.Max(0, Cap(Math.Round(value * (decimal)CoolingPercent / 100, MidpointRounding.AwayFromZero)));
        private static int Inspection(int value, bool friend, bool marked) => Cap(decimal.Floor(value *
            (friend ? (decimal)InspectionMultiplier : 1m) * (marked ? InspectionPercent : 100m) / 100));
        internal static int InspectionMin(int value, bool friend, bool marked) => Math.Max(InspectionFloorMin, Inspection(value, friend, marked));
        internal static int InspectionMax(int value, int min, bool friend, bool marked) => Math.Max(min, Math.Max(InspectionFloorMax, Inspection(value, friend, marked)));
        internal static int Service(int value) => value <= 0 ? 0 : Cap(decimal.Ceiling(value * (decimal)ServicePercent / 100));
        internal static int Appraisal(int value) => value <= 0 ? 0 : (int)Percent(value, ValuerPercent);
        internal static double Reputation(double value) => value > 0 ? value * ReputationPercent / 100 : 0;
        internal static bool Withhold(int count) => count > 0 && count % SupplierInterval == 0;
        internal static int Visits(int value) => Math.Max(value, NightVisits);
        internal static int LuxuryAverage(long total, int count, int fallback)
        {
            decimal average = count <= 0 ? fallback : total / (decimal)count;
            return average <= 0 ? Cap(average) : Cap(Math.Max(1, Math.Round(average * LuxuryBudgetPercent / 100, MidpointRounding.AwayFromZero)));
        }
        internal static bool IsService(string id)
        {
            switch ((id ?? "").TrimEnd('\0'))
            {
                case "POWER_SERVICE": case "JANITORIAL_SERVICE": case "INSURANCE_SERVICE": case "BUY_REFERAL": case "SELL_REFERAL": return true;
                default: return false;
            }
        }
        internal static bool Ordinary(bool canReturn, bool security, bool wanted, string source, string id) => canReturn && !security && !wanted
            && string.IsNullOrEmpty(source) && !(id ?? "").StartsWith("retired_", StringComparison.Ordinal) && !(id ?? "").StartsWith("rnk.perks.", StringComparison.Ordinal);
        internal static Settlement Settle(DailyState before, long paid, bool brisk, bool skim)
        {
            var next = before.Copy();
            int opening = brisk && before.Sales < OpeningSales ? (int)Math.Min(OpeningCap, Math.Max(0m, decimal.Floor(paid * (decimal)OpeningPercent / 100))) : 0;
            int fee = skim ? (int)Math.Max(0m, Math.Min(DailyFeeCap - before.Fees, decimal.Floor(paid * (decimal)FeePercent / 100))) : 0;
            next.Sales = before.Sales == int.MaxValue ? int.MaxValue : before.Sales + 1;
            next.Fees += fee;
            return new Settlement { State = next, Opening = opening, Fee = fee };
        }
    }
    internal sealed class Settlement
    {
        internal DailyState State;
        internal int Opening, Fee;
        internal int Delta => Opening - Fee;
    }
    // One write per record. Invalid or old records mean a fresh state, never a partial one.
    internal sealed class DailyState
    {
        internal int Day, Sales, Fees;
        internal DailyState Copy() => (DailyState)MemberwiseClone();
        internal string Text() => Day + ";" + Sales + ";" + Fees;
        internal static DailyState Parse(string text, int day)
        {
            string[] p = (text ?? "").Split(';');
            if (p.Length == 3 && int.TryParse(p[0], out int d) && d == day && int.TryParse(p[1], out int sales) && sales >= 0
                && int.TryParse(p[2], out int fees) && fees >= 0 && fees <= Rules.DailyFeeCap)
                return new DailyState { Day = day, Sales = sales, Fees = fees };
            return new DailyState { Day = day };
        }
    }
    internal sealed class AugState
    {
        internal int Day;
        internal bool ExtraTried;
        internal string Text() => Day + ";" + (ExtraTried ? "1" : "0");
        internal static AugState Parse(string text, int day)
        {
            string[] p = (text ?? "").Split(';');
            if (p.Length == 2 && int.TryParse(p[0], out int d) && d == day && (p[1] == "0" || p[1] == "1"))
                return new AugState { Day = day, ExtraTried = p[1] == "1" };
            return new AugState { Day = day };
        }
    }
}

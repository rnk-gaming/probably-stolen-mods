using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShopkeepingSuite
{
    internal sealed class Totals
    {
        internal readonly Dictionary<string, int> Sums = new Dictionary<string, int>(StringComparer.Ordinal);
        internal int Get(string key) => Sums.TryGetValue(key, out int value) ? value : 0;
        internal void Add(string key, int amount) => Sums[key] = LedgerRules.Cap((long)Get(key) + amount);
        internal Totals Copy() { var t = new Totals(); foreach (var p in Sums) t.Sums.Add(p.Key, p.Value); return t; }
    }
    internal sealed class LedgerState
    {
        internal int Day = -1, YesterdayDay = -1, Days;
        internal Totals Today = new Totals(), Run = new Totals(), Yesterday = new Totals();
        internal int StandingDay = -1;
        internal Dictionary<string, StandingTally> Standing = new Dictionary<string, StandingTally>(StringComparer.Ordinal);
    }
    internal sealed class StandingTally
    {
        internal string Name;
        internal double Gained, Lost;
        internal double Start;
        internal bool HasStart;
        internal StandingTally Copy() => new StandingTally { Name = Name, Gained = Gained, Lost = Lost, Start = Start, HasStart = HasStart };
    }
    internal static class LedgerRules
    {
        internal const int MaxEntries = 45; // Thirteen totals and at most thirty-two service ids.
        internal static readonly string[] Keys = { "sales", "items_sold", "purchases", "items_bought", "services", "rent", "mortgage", "fees", "seized", "burgled", "other", "upgrades", "margin" };
        internal static int Cap(long value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
        internal static Totals Event(string name, int amount, int boughtFor, string service)
        {
            var result = new Totals();
            amount = Math.Max(0, amount);
            switch (name)
            {
                case "OnItemSold":
                    result.Add("sales", amount); result.Add("items_sold", 1);
                    if (boughtFor > 0) result.Add("margin", Cap((long)amount - boughtFor));
                    break;
                case "OnItemBought": result.Add("purchases", amount); result.Add("items_bought", 1); break;
                case "OnServicePaid":
                    result.Add("services", amount);
                    if (!string.IsNullOrEmpty(service) && service.Length <= 96) result.Add(ServiceKey(service), amount);
                    break;
                case "OnRentPaid": result.Add("rent", amount); break;
                case "OnMortgagePayment": result.Add("mortgage", amount); break;
                case "OnPermitBought": case "OnPermitBribe": case "OnInspectorBribe": result.Add("fees", amount); break;
                case "OnInspection": result.Add("seized", amount); break;
                case "OnBurglary": result.Add("burgled", amount); break;
                case "OnVendingSale": case "OnFountainSale": result.Add("other", amount); break;
                case "OnWildUpgradeBought": result.Add("upgrades", amount); break;
            }
            return result;
        }
        internal static LedgerState Roll(LedgerState state, int day)
        {
            var next = new LedgerState { Day = state.Day, YesterdayDay = state.YesterdayDay, Days = state.Days, Today = state.Today.Copy(), Run = state.Run.Copy(), Yesterday = state.Yesterday.Copy() };
            next.StandingDay = state.StandingDay;
            if (day >= 0 && state.StandingDay >= 0 && day != state.StandingDay) next.StandingDay = day;
            else foreach (var pair in state.Standing) next.Standing.Add(pair.Key, pair.Value.Copy());
            if (day < 0 || day <= state.Day) return next;
            if (state.Day >= 0)
            {
                Merge(next.Run, next.Today); next.Yesterday = next.Today; next.YesterdayDay = state.Day; next.Today = new Totals();
                next.Days = Cap((long)next.Days + 1);
            }
            next.Day = day;
            return next;
        }
        private static void Merge(Totals into, Totals from)
        {
            foreach (var p in from.Sums)
                if (CanAdd(into, p.Key)) into.Add(p.Key, p.Value);
        }
        internal static bool CanAdd(Totals totals, string key) => !key.StartsWith("service_", StringComparison.Ordinal) || totals.Sums.ContainsKey(key) || totals.Sums.Keys.Count(k => k.StartsWith("service_", StringComparison.Ordinal)) < 32;
        internal static Totals Combined(LedgerState state) { var total = state.Run.Copy(); Merge(total, state.Today); return total; }
        internal static int MoneyIn(Totals totals) => Cap((long)totals.Get("sales") + totals.Get("other"));
        internal static int MoneyOut(Totals totals) => Cap((long)totals.Get("purchases") + totals.Get("services") + totals.Get("rent") + totals.Get("mortgage") + totals.Get("fees") + totals.Get("upgrades"));
        internal static int Net(Totals totals) => Cap((long)totals.Get("sales") + totals.Get("other") - totals.Get("purchases") - totals.Get("services") - totals.Get("rent") - totals.Get("mortgage") - totals.Get("fees") - totals.Get("upgrades"));
        internal static string Serialise(LedgerState state) => "v1|" + state.Day.ToString(CultureInfo.InvariantCulture) + "|" + state.Days.ToString(CultureInfo.InvariantCulture)
            + "|" + Pack(state.Today) + "|" + Pack(state.Run) + "|" + Pack(state.Yesterday) + "|" + state.YesterdayDay.ToString(CultureInfo.InvariantCulture);
        internal static string SerialiseStanding(LedgerState state) => state.StandingDay < 0 ? "" : "v1|" + state.StandingDay.ToString(CultureInfo.InvariantCulture) + "|" + PackStanding(state.Standing);
        internal static void ParseStanding(LedgerState state, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                if (text.Length > 524288) throw new FormatException();
                string[] p = text.Split('|');
                if (p.Length != 3 || p[0] != "v1" || !int.TryParse(p[1], out int day) || day < 0) throw new FormatException();
                var values = UnpackStanding(p[2]);
                state.StandingDay = day; state.Standing = values;
            }
            catch (Exception error) { SuiteMod.Fault("saved standing tally", error); }
        }
        private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));
        private static string Decode(string text) => Encoding.UTF8.GetString(Convert.FromBase64String(text));
        private static string PackStanding(Dictionary<string, StandingTally> values) => string.Join(",", values.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => Encode(p.Key) + ":" + Encode(p.Value.Name) + ":" + p.Value.Gained.ToString("R", CultureInfo.InvariantCulture) + ":" + p.Value.Lost.ToString("R", CultureInfo.InvariantCulture)
                + ":" + (p.Value.HasStart ? p.Value.Start.ToString("R", CultureInfo.InvariantCulture) : "")));
        private static Dictionary<string, StandingTally> UnpackStanding(string text)
        {
            var result = new Dictionary<string, StandingTally>(StringComparer.Ordinal);
            if (text.Length == 0) return result;
            foreach (string entry in text.Split(','))
            {
                string[] p = entry.Split(':');
                if (p.Length != 4 && p.Length != 5) throw new FormatException();
                string id = Decode(p[0]), name = Decode(p[1]);
                if (id.Length < 1 || id.Length > 128 || name.Length > 128 || result.Count >= 256 ||
                    !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double gained) || !Finite(gained) || gained < 0 ||
                    !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double lost) || !Finite(lost) || lost < 0) throw new FormatException();
                bool hasStart = p.Length == 5 && p[4].Length > 0;
                double start = 0;
                if (hasStart && (!double.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out start) || !Finite(start))) throw new FormatException();
                result.Add(id, new StandingTally { Name = name, Gained = gained, Lost = lost, Start = start, HasStart = hasStart });
            }
            return result;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void ObserveStanding(LedgerState state, int day, IEnumerable<StandingView> reading, IEnumerable<KeyValuePair<string, double>> delta)
        {
            if (day < 0) return;
            if (state.StandingDay != day) { state.Standing.Clear(); state.StandingDay = day; }
            var existing = new HashSet<string>(state.Standing.Where(p => p.Value.HasStart).Select(p => p.Key), StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (StandingView faction in reading)
            {
                if (string.IsNullOrEmpty(faction.Id) || faction.Id.Length > 128 || !Finite(faction.Value)) continue;
                names[faction.Id] = faction.Faction;
                if (!state.Standing.TryGetValue(faction.Id, out StandingTally tally))
                {
                    if (state.Standing.Count >= 256) continue;
                    string name = faction.Faction ?? faction.Id;
                    tally = new StandingTally { Name = name.Length <= 128 ? name : name.Substring(0, 128) };
                    state.Standing.Add(faction.Id, tally);
                }
                if (!tally.HasStart) { tally.Start = faction.Value; tally.HasStart = true; }
            }
            // The first reading on a day is the baseline, including after loading an older save.
            Tally(state, day, delta.Where(p => existing.Contains(p.Key)), id => names.TryGetValue(id, out string name) ? name : id);
        }
        internal static void Tally(LedgerState state, int day, IEnumerable<KeyValuePair<string, double>> delta, Func<string, string> name)
        {
            if (day < 0) return;
            if (state.StandingDay != day) { state.Standing.Clear(); state.StandingDay = day; }
            foreach (var pair in delta)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Key.Length > 128 || !Finite(pair.Value) || pair.Value == 0) continue;
                if (!state.Standing.TryGetValue(pair.Key, out var tally))
                {
                    if (state.Standing.Count >= 256) continue;
                    string label = name(pair.Key) ?? pair.Key;
                    tally = new StandingTally { Name = label.Length <= 128 ? label : label.Substring(0, 128) };
                    state.Standing.Add(pair.Key, tally);
                }
                if (pair.Value > 0) tally.Gained = Math.Min(double.MaxValue, tally.Gained + pair.Value);
                else tally.Lost = Math.Min(double.MaxValue, tally.Lost - pair.Value);
            }
        }
        private static string Pack(Totals totals) => string.Join(",", totals.Sums.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value.ToString(CultureInfo.InvariantCulture)));
        private static Totals Unpack(string text)
        {
            var result = new Totals();
            if (text.Length == 0) return result;
            foreach (string line in text.Split(','))
            {
                int at = line.IndexOf(':'); if (at < 1) throw new FormatException();
                string key = line.Substring(0, at);
                if (!Keys.Contains(key) && (!key.StartsWith("service_", StringComparison.Ordinal) || ServiceName(key).Length > 96)) throw new FormatException();
                if (!int.TryParse(line.Substring(at + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) || value < 0 && key != "margin") throw new FormatException();
                result.Sums.Add(key, value);
                if (result.Sums.Count > MaxEntries || result.Sums.Keys.Count(k => k.StartsWith("service_", StringComparison.Ordinal)) > 32) throw new FormatException();
            }
            return result;
        }
        internal static LedgerState Parse(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text) || text.Length > 65536) return new LedgerState();
                string[] p = text.Split('|');
                if ((p.Length != 6 && p.Length != 7) || p[0] != "v1" || !int.TryParse(p[1], out int day) || day < 0 || !int.TryParse(p[2], out int days) || days < 0) return new LedgerState();
                int yesterdayDay = -1; // Older notes did not keep this date; do not guess across gaps.
                if (p.Length == 7 && (!int.TryParse(p[6], out yesterdayDay) || yesterdayDay < -1)) return new LedgerState();
                if (yesterdayDay >= day) yesterdayDay = -1;
                var state = new LedgerState { Day = day, YesterdayDay = yesterdayDay, Days = days, Today = Unpack(p[3]), Run = Unpack(p[4]), Yesterday = Unpack(p[5]) };
                return state;
            }
            catch (Exception) { return new LedgerState(); }
        }
        internal static string ServiceKey(string id) => "service_" + Convert.ToBase64String(Encoding.UTF8.GetBytes(id ?? ""));
        internal static string ServiceName(string key) => Encoding.UTF8.GetString(Convert.FromBase64String(key.Substring(8)));
    }
    internal static class Ledger
    {
        internal static Func<int> Day = () => StoreStation.GetDayCounter();
        internal static Func<string, string, string> Read = (key, fallback) => ModHelper.GetModData(SuiteMod.Id, key, fallback);
        internal static Action<string, string> Write = (key, text) => ModHelper.SetModData(SuiteMod.Id, key, text);
        private static bool tallyFailed;
        internal const string StandingKey = "standing_day_v1";
        private static bool clearedDisabledTally;
        internal static LedgerState Snapshot()
        {
            var state = LedgerRules.Parse(Read("ledger_v1", ""));
            LedgerRules.ParseStanding(state, Read(StandingKey, ""));
            return LedgerRules.Roll(state, Day());
        }
        internal static void CheckTallySwitch()
        {
            if (Settings.On("standing_tally")) { clearedDisabledTally = false; return; }
            if (clearedDisabledTally || tallyFailed || !SuiteRuntime.HasRun()) return;
            try
            {
                if (Read(StandingKey, "").Length > 0) Write(StandingKey, "");
                clearedDisabledTally = true; SuiteWindow.Dirty = true;
            }
            catch (Exception error) { tallyFailed = true; SuiteMod.Fault("standing tally", error); }
        }
        internal static void Loaded() => clearedDisabledTally = false;
        internal static void ObserveStanding(List<StandingView> reading, List<KeyValuePair<string, double>> delta)
        {
            if (tallyFailed || !Settings.On("enabled") || !Settings.On("standing_tally") || !Settings.On("ledger_data")) return;
            try
            {
                int day = Day(); if (day < 0) return;
                var state = new LedgerState();
                LedgerRules.ParseStanding(state, Read(StandingKey, ""));
                string before = LedgerRules.SerialiseStanding(state);
                LedgerRules.ObserveStanding(state, day, reading, delta);
                string after = LedgerRules.SerialiseStanding(state);
                if (before != after) { Write(StandingKey, after); SuiteWindow.Dirty = true; }
            }
            catch (Exception error) { tallyFailed = true; SuiteMod.Fault("standing tally", error); }
        }
        internal static void Record(string name, int amount, int boughtFor = 0, string service = null)
        {
            if (!Settings.On("enabled") || !Settings.On("ledger")) return;
            if (Day() < 0) return;
            LedgerState state = LedgerRules.Roll(LedgerRules.Parse(Read("ledger_v1", "")), Day());
            foreach (var pair in LedgerRules.Event(name, amount, boughtFor, service).Sums)
                if (LedgerRules.CanAdd(state.Today, pair.Key)) state.Today.Add(pair.Key, pair.Value);
            Write("ledger_v1", LedgerRules.Serialise(state));
            SuiteWindow.Dirty = true;
        }
        internal static void Touch()
        {
            if (!Settings.On("enabled") || !Settings.On("ledger") || Day() < 0) return;
            string old = Read("ledger_v1", "");
            string next = LedgerRules.Serialise(LedgerRules.Roll(LedgerRules.Parse(old), Day()));
            if (old != next) Write("ledger_v1", next);
        }
    }
}

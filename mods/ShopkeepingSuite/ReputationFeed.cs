using LocalMods;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShopkeepingSuite
{
    // Game-independent readings and changes both carry a faction id and a value.
    internal sealed class ReputationChanges
    {
        private Dictionary<string, double> previous = new Dictionary<string, double>(StringComparer.Ordinal);
        internal List<KeyValuePair<string, double>> Exact = new List<KeyValuePair<string, double>>();
        internal List<KeyValuePair<string, double>> Observe(IEnumerable<KeyValuePair<string, double>> reading)
        {
            var changes = new List<KeyValuePair<string, double>>();
            Exact = new List<KeyValuePair<string, double>>();
            var next = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var faction in reading)
            {
                if (previous.TryGetValue(faction.Key, out double value) && faction.Value != value)
                {
                    var difference = new KeyValuePair<string, double>(faction.Key, faction.Value - value);
                    Exact.Add(difference);
                    if (Math.Abs(difference.Value) >= .005) changes.Add(difference);
                }
                next[faction.Key] = faction.Value;
            }
            previous = next;
            return changes;
        }
        internal void Reset() { previous.Clear(); Exact.Clear(); }
        internal static string Format(IEnumerable<KeyValuePair<string, double>> changes, Func<string, string> name)
        {
            var parts = new List<string>();
            foreach (var change in changes)
            {
                double amount = Math.Round(change.Value, 2, MidpointRounding.AwayFromZero);
                parts.Add(TextTable.Get(TextKeys.ReputationChange, name(change.Key), amount.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture)));
            }
            return string.Join(", ", parts);
        }
    }
    internal static class ReputationFeed
    {
        internal static Func<List<StoreReputation>> Read = () => PlayerStore.Instance.storeReputations;
        internal static Func<StoreReputation, double> Value = faction => faction.GetReputationExact();
        internal static Func<string, string> Name = id => StoreReputation.GetFactionNameByID(id);
        internal static Action<string, string> Notify = (message, colour) =>
        {
            ReputationUIManager panel = ReputationUIManager.Instance;
            if (panel != null) panel.Notify(message, colour);
        };
        private static readonly ReputationChanges changes = new ReputationChanges();
        internal static List<StandingView> Latest = new List<StandingView>();
        private static string changeTemplate;
        private static float nextPoll = float.NegativeInfinity;
        internal static void Reset() { changes.Reset(); Latest = new List<StandingView>(); nextPoll = float.NegativeInfinity; changeTemplate = null; }
        internal static void Poll() => PollCore(false);
        internal static void ReadForWindow() => PollCore(true);
        private static void PollCore(bool opening)
        {
            try
            {
                Ledger.CheckTallySwitch();
                if (!Settings.On("enabled") || !SuiteRuntime.HasRun()) return;
                if (!opening && !Settings.On("reputation_feed") && !Settings.On("tablet_feed") && !Settings.On("standing_tally") && !Settings.On("window")) return;
                float now = SuiteRuntime.Clock();
                if (!opening && now < nextPoll) return;
                nextPoll = now + 1;
                var factions = AllStanding.Read();
                // Freeze the reading: a provider may reuse its objects on subsequent polls.
                Latest = factions.Select(f => new StandingView { Id = f.Id, Faction = f.Faction, Value = f.Value, Status = f.Status, Steps = f.Steps }).ToList();
                changeTemplate ??= TextTable.Get(TextKeys.ReputationChange, "{0}", "{1}");
                var labels = StandingRules.Labels();
                var reading = new List<KeyValuePair<string, double>>();
                var names = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (StandingView faction in factions)
                {
                    reading.Add(new KeyValuePair<string, double>(faction.Id, faction.Value));
                    names[faction.Id] = faction.Faction;
                }
                changes.Observe(reading);
                var delta = changes.Exact;
                Func<string, string> label = id => names.TryGetValue(id, out string name) ? name : id;
                Ledger.ObserveStanding(factions, delta);
                if ((Settings.On("reputation_feed") || Settings.On("tablet_feed")) && delta.Count > 0)
                {
                    var lines = new List<string>();
                    foreach (var change in delta)
                    {
                        StandingView faction = factions.First(f => f.Id == change.Key);
                        string amount = string.Format(CultureInfo.InvariantCulture, changeTemplate, StandingRules.Clean(label(change.Key)),
                            Math.Round(change.Value, 2, MidpointRounding.AwayFromZero).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture));
                        var crossed = StandingRules.Changes(faction, faction.Value - change.Value, faction.Value, labels);
                        if (Math.Abs(change.Value) >= .005 || crossed.Count > 0)
                        {
                            lines.Add(amount + (crossed.Count == 0 ? "" : ". " + string.Join(". ", crossed.Select(c => c.Text))));
                            if (Settings.On("tablet_feed")) TabletScreen.Enqueue(change.Key, label(change.Key), change.Value, crossed.Select(c => c.Text));
                        }
                    }
                    if (Settings.On("reputation_feed") && lines.Count > 0) Notify(string.Join("\n", lines), "default");
                }
            }
            catch (Exception error) { SuiteMod.Fault("standing feed", error); }
        }
    }
}

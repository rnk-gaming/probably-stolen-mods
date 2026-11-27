using LocalMods;
using System;

namespace ShopkeepingSuite
{
    // Opaque keys are compared by reference; this class knows no game types.
    internal sealed class HandsRules
    {
        private sealed class Seen
        {
            internal object Key;
            internal string Name;
            internal bool Warned, Finished;
            internal float NextTry = float.NegativeInfinity;
        }
        private readonly Seen[] previous = new Seen[3];
        // 0: wait, 1: try the hand, 2: warn about the backpack without moving it.
        internal int Observe(int slot, object key, string name, bool atHome, float now)
        {
            if (!atHome) { Reset(); return 0; }
            Seen last = previous[slot];
            if (key == null) { previous[slot] = null; return 0; }
            if (last == null || !ReferenceEquals(last.Key, key))
            {
                previous[slot] = new Seen { Key = key, Name = name };
                return 0;
            }
            last.Name = name;
            return last.Finished ? 0 : slot == 2 ? 2 : now < last.NextTry ? 0 : 1;
        }
        internal string Result(int slot, int unitsLeft, int unitsBefore, float now)
        {
            Seen last = previous[slot];
            if (last == null || last.Finished || unitsBefore <= 0) return null;
            if (slot == 2) { last.Finished = true; return TextKeys.HandsBackpack; }
            if (unitsLeft == 0) { last.Finished = true; return TextKeys.HandsBrought; }
            last.NextTry = now + 5;
            if (last.Warned) return null;
            last.Warned = true;
            return TextKeys.HandsFull;
        }
        internal string Name(int slot) => previous[slot]?.Name;
        internal void Reset() { Array.Clear(previous, 0, previous.Length); }
    }

    internal static class HandsHome
    {
        internal static Func<bool> AtHome = () => MapUIManager.Instance != null && !MapUIManager.Instance.IsOutside && EmporiumEntry.Instance != null;
        internal static Func<int, GameItem> Read = slot =>
        {
            EmporiumEntry home = EmporiumEntry.Instance;
            GameSlotInventory inventory = slot == 0 ? home.afterhourPocketSlotInvLeft : slot == 1 ? home.afterhourPocketSlotInvRight : home.afterhourPocketSlotInvBackpack;
            return inventory?.childItem;
        };
        internal static Func<GameItem, string> Name = item => item.GetDisplayName(false);
        internal static Func<GameItem, int> Count = item => item.unitCount;
        internal static Func<int, GameItem, int> Place = (root, item) =>
        {
            EmporiumEntry home = EmporiumEntry.Instance;
            GraphNodeStorage inventory = root == 0 ? home.backInvinvElement : root == 1 ? home.invElement : home.showcaseElement;
            return inventory == null ? item.unitCount : GraphUtils.TryAcceptAll(inventory, item, -1);
        };
        private static readonly HandsRules rules = new HandsRules();
        private static float nextPoll = float.NegativeInfinity;
        internal static void Poll()
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("hands_home") || !SuiteRuntime.HasRun() || !AtHome()) { Reset(); return; }
                float now = SuiteRuntime.Clock();
                if (now < nextPoll) return;
                nextPoll = now + 1;
                for (int slot = 0; slot < 3; slot++)
                {
                    GameItem item = Read(slot);
                    int action = rules.Observe(slot, item, item == null ? null : ReadName(item), true, now);
                    if (action == 0) continue;
                    int before = action == 2 ? 1 : Count(item), left = before;
                    if (action == 1 && before > 0)
                        for (int root = 0; root < 3 && left > 0; root++) left = Place(root, item);
                    string notice = rules.Result(slot, left, before, now);
                    if (notice != null) ReputationFeed.Notify(TextTable.Get(notice, rules.Name(slot)), "default");
                }
            }
            catch (Exception error) { SuiteMod.Fault("hands at home", error); }
        }
        private static string ReadName(GameItem item)
        {
            try
            {
                string name = Name(item);
                if (!string.IsNullOrWhiteSpace(name)) return name;
                throw new InvalidOperationException("the item name is empty");
            }
            catch (Exception error) { SuiteMod.Fault("reading hand item name", error); }
            return TextTable.Get(TextKeys.HandsItem);
        }
        internal static void Reset()
        {
            try { rules.Reset(); nextPoll = float.NegativeInfinity; }
            catch (Exception error) { SuiteMod.Fault("resetting hands at home", error); }
        }
        internal static void SettingsChanged()
        {
            try { if (!Settings.On("enabled") || !Settings.On("hands_home")) Reset(); }
            catch (Exception error) { SuiteMod.Fault("hands setting", error); }
        }
    }
}

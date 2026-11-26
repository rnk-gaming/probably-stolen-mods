using System.Collections.Generic;

namespace Stacking
{
    // Accepted items arrive in the game's delivery order, last owned item first.
    internal static class RequestRules
    {
        internal struct Item
        {
            internal int Units;
            internal bool MayHandle, OwnRule;
            internal Item(int units, bool mayHandle, bool ownRule)
            { Units = units; MayHandle = mayHandle; OwnRule = ownRule; }
        }
        internal struct Take
        {
            internal int Index, Units;
            internal bool Destroy;
        }
        internal sealed class Plan
        {
            internal bool CanFill, LeaveOriginal = true;
            internal readonly List<Take> Takes = new List<Take>();
        }
        internal static Plan Build(IList<Item> items, int required)
        {
            var plan = new Plan();
            long total = 0;
            bool stack = false, ownStack = false;
            foreach (Item item in items)
            {
                bool handled = item.Units > 1 && item.MayHandle && !item.OwnRule;
                stack |= handled;
                ownStack |= item.Units > 1 && item.OwnRule;
                total += handled ? item.Units : 1;
            }
            plan.CanFill = required > 0 && total >= required;
            plan.LeaveOriginal = !stack || ownStack;
            if (plan.LeaveOriginal || !plan.CanFill) return plan;
            int left = required;
            for (int i = 0; i < items.Count && left > 0; i++)
            {
                Item item = items[i];
                int units = item.MayHandle && !item.OwnRule ? System.Math.Max(1, item.Units) : 1;
                int take = System.Math.Min(units, left);
                plan.Takes.Add(new Take { Index = i, Units = take, Destroy = take == units });
                left -= take;
            }
            return plan;
        }
    }
}

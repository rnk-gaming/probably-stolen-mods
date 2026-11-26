using System.Collections.Generic;

namespace Stacking
{
    internal static class BarterRules
    {
        internal sealed class Plan
        {
            internal bool CanFill, LeaveOriginal = true;
            internal readonly List<int> Takes = new List<int>();
        }

        // Rows are conditions, columns are objects, both in the game's order.
        internal static Plan Build(IList<RequestRules.Item> items, bool[][] accepts)
        {
            var plan = new Plan();
            int[] left = new int[items.Count];
            bool stackUsed = false, foreign = false, complete = accepts.Length > 0;
            for (int i = 0; i < items.Count; i++)
                left[i] = items[i].MayHandle && !items[i].OwnRule ? System.Math.Max(1, items[i].Units) : 1;
            foreach (bool[] row in accepts)
            {
                int first = -1;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!row[i]) continue;
                    foreign |= items[i].Units > 1 && items[i].OwnRule;
                    if (first < 0 && left[i] > 0) first = i;
                }
                if (first < 0) { complete = false; continue; }
                left[first]--;
                stackUsed |= items[first].Units > 1 && items[first].MayHandle && !items[first].OwnRule;
                plan.Takes.Add(first);
            }
            plan.CanFill = complete;
            plan.LeaveOriginal = !stackUsed || foreign;
            if (!complete || plan.LeaveOriginal) plan.Takes.Clear();
            return plan;
        }
    }
}

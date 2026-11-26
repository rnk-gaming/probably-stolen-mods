using System;
using System.Runtime.CompilerServices;

namespace ShopkeepingSuite
{
    internal static class Wanted
    {
        internal static Func<StoreClient, GameItem, bool> Wants = (client, item) => client.IsClientBuyingThisItem(item);
        // Use plain readings because another mod may make HasBudgetLeftToBuy split unaffordable stacks.
        internal static Func<StoreClient, GameItem, bool> Affords = (client, item) =>
            !client.useClientBudget || AffordRule(item.GetNegociatedValue(), client.GetBudget());
        // The game's own rule, for a stack as for one item: its whole value against the budget.
        internal static bool AffordRule(long value, int budget) => value <= budget;
        internal static Func<StoreClient, int> Budget = client => client.GetBudget();
        private sealed class Answer { internal int Value, Count; internal long UnitValue; internal float At; }
        private static ConditionalWeakTable<GameItem, Answer> cache = new ConditionalWeakTable<GameItem, Answer>();
        private static StoreClient known;
        private static int budget;
        internal static int Get(StoreClient client, GameItem item)
        {
            if (client == null || item == null) return 0;
            if (!ReferenceEquals(client, known)) Observe(client);
            float now = SuiteRuntime.Clock();
            if (cache.TryGetValue(item, out Answer answer) && answer.Count == item.unitCount && answer.UnitValue == item.unitValue && now >= answer.At && now - answer.At < 3f) return answer.Value;
            int value = Wants(client, item) ? Affords(client, item) ? 2 : 1 : 0;
            cache.Remove(item);
            cache.Add(item, new Answer { Value = value, Count = item.unitCount, UnitValue = item.unitValue, At = now });
            return value;
        }
        internal static bool Observe(StoreClient client)
        {
            int next = client == null ? 0 : Budget(client);
            if (ReferenceEquals(client, known) && next == budget) return false;
            cache = new ConditionalWeakTable<GameItem, Answer>(); known = client; budget = next;
            return true;
        }
        internal static void Reset() { cache = new ConditionalWeakTable<GameItem, Answer>(); known = null; budget = 0; }
    }
}

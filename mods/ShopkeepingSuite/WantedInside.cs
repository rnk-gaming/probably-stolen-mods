using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    internal sealed class WantedContents
    {
        internal int Own, Count, Demand;
    }
    internal static class WantedInside
    {
        internal static bool Available = false;
        internal static Func<GameItem, bool> HasContents = item => item.children.Count > 0;
        internal static Func<GameItem, IEnumerable<GameItem>> Children = item => GraphUtils.FindChildrenType<GameItem>(item);
        private static readonly Dictionary<GameItem, WantedContents> contents = new Dictionary<GameItem, WantedContents>();
        private static readonly WantedContents Empty = new WantedContents();
        private static StoreClient known, polledClient;
        private static float cachedAt = float.NegativeInfinity, nextRefresh = float.NegativeInfinity, previous = float.NegativeInfinity;
        internal static bool Batch;
        internal const float RefreshSeconds = 3f;
        internal static WantedContents Get(GameItem item, StoreClient client, bool buyer)
        {
            if (item == null || client == null || !buyer || !Available || !Settings.On("enabled")
                || !Settings.On("wanted_data") || !Settings.On("mark_wanted") || !Settings.On("mark_wanted_inside")) return Empty;
            try
            {
                if (!ReferenceEquals(known, client)) { Reset(); known = client; }
                float now = SuiteRuntime.Clock();
                if (now < cachedAt || !Batch && now - cachedAt >= 1f) Clear();
                // The cheap graph check avoids starting a search for ordinary goods.
                if (!HasContents(item)) return Empty;
                if (contents.TryGetValue(item, out WantedContents cached)) return cached;
                if (float.IsNegativeInfinity(cachedAt)) cachedAt = now;
                return Read(item, client);
            }
            catch (Exception error)
            {
                Available = false;
                // Also clear any container marks already drawn before this fault.
                Marks.RequestAll();
                SuiteMod.Fault("wanted contents traversal; container marks disabled", error);
                return Empty;
            }
        }
        private sealed class Walk
        {
            internal GameItem Item;
            internal WantedContents Answer;
            internal IEnumerator<GameItem> Children;
        }
        private static WantedContents Read(GameItem item, StoreClient client)
        {
            var todo = new Stack<Walk>(); var active = new HashSet<GameItem>();
            int visited = 0;
            void Push(GameItem node)
            {
                var answer = new WantedContents { Own = Wanted.Get(client, node) };
                // Locks, hidden containers and Smuggler's Bays are the player's
                // accessible stock, unlike the concealment rules for a search.
                var walk = new Walk { Item = node, Answer = answer };
                if (HasContents(node)) walk.Children = Children(node).GetEnumerator();
                todo.Push(walk); active.Add(node);
            }
            void Add(WantedContents into, WantedContents child)
            {
                into.Count = (int)Math.Min(int.MaxValue, (long)into.Count + child.Count + (child.Own > 0 ? 1 : 0));
                into.Demand = Math.Max(into.Demand, Math.Max(child.Own, child.Demand));
            }
            try
            {
                Push(item);
                while (todo.Count > 0)
                {
                    Walk current = todo.Peek();
                    if (current.Children == null || !current.Children.MoveNext())
                    {
                        todo.Pop(); current.Children?.Dispose(); active.Remove(current.Item);
                        contents[current.Item] = current.Answer;
                        if (todo.Count == 0) return current.Answer;
                        Add(todo.Peek().Answer, current.Answer);
                        continue;
                    }
                    if (++visited >= 2000) throw new InvalidOperationException("wanted contents exceeded the 2000-item search limit");
                    GameItem child = current.Children.Current;
                    if (child == null) continue;
                    if (active.Contains(child)) throw new InvalidOperationException("wanted contents contain a cycle");
                    if (contents.TryGetValue(child, out WantedContents cached)) Add(current.Answer, cached);
                    else Push(child);
                }
                return Empty;
            }
            finally { while (todo.Count > 0) todo.Pop().Children?.Dispose(); }
        }
        private static void Clear() { contents.Clear(); cachedAt = float.NegativeInfinity; }
        internal static void Reset() { Clear(); known = null; }
        internal static void Poll()
        {
            if (!Available || Marks.Client == null || !Marks.Buyer || Marks.OutsideShop || Marks.ShutterClosed
                || !Settings.On("enabled") || !Settings.On("mark_wanted") || !Settings.On("mark_wanted_inside") || !Settings.On("wanted_data"))
            { polledClient = null; return; }
            float now = SuiteRuntime.Clock();
            // Finish a large redraw before replacing the shared subtree answers.
            if (!Batch && (!ReferenceEquals(polledClient, Marks.Client) || now < previous || now >= nextRefresh))
            {
                // A drag that ends redraws the marks at once (Patches). This slower refresh
                // is for goods that move without a drag; every second, as the search marks
                // do for the short visit of an inspector, would redraw a full shop all
                // through a buyer's long visit.
                polledClient = Marks.Client; nextRefresh = now + RefreshSeconds;
                Marks.RequestAll();
            }
            previous = now;
        }
    }
}

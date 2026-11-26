using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ShopkeepingSuite
{
    internal sealed class InspectionAnswer
    {
        internal bool Found;
        internal int Holds;
        internal float At;
    }
    internal static class InspectionSearch
    {
        internal static bool Available = false;
        internal static Func<GameItem, IEnumerable<GameItem>> Children = item => GraphUtils.FindChildrenType<GameItem>(item);
        internal static Func<GameItem, GameItem> Parent = item => item.parentInventory?.GetParentItem();
        internal static Func<GameItem, string> Id = item => item.identifier;
        private static ConditionalWeakTable<GameItem, InspectionAnswer> cache = new ConditionalWeakTable<GameItem, InspectionAnswer>();
        private static readonly Dictionary<GameItem, InspectionContents> contents = new Dictionary<GameItem, InspectionContents>();
        private static float cachedAt = float.NegativeInfinity;
        internal static bool Batch;
        private static StoreClient known;
        private static StoreClient polledClient;
        private static float nextRefresh = float.NegativeInfinity, previous = float.NegativeInfinity;
        private static readonly InspectionAnswer Empty = new InspectionAnswer();
        internal static InspectionAnswer Get(GameItem item, StoreClient client, bool security)
        {
            if (item == null || client == null || !security || !Settings.On("enabled") || !Settings.On("mark_inspection")) return Empty;
            if (!ReferenceEquals(known, client)) { Reset(); known = client; }
            if (!Available) return Own(item);
            float now = SuiteRuntime.Clock();
            // A redraw may span several frames. Its descendants must reuse the
            // same subtree counts even when working through the queue takes a second.
            if (now < cachedAt || !Batch && now - cachedAt >= 1f) Clear();
            if (cache.TryGetValue(item, out InspectionAnswer answer)) return answer;
            try
            {
                if (float.IsNegativeInfinity(cachedAt)) cachedAt = now;
                InspectionContents result = InspectionRules.Read(item, Marks.Tag, Id, Marks.Flaggable, Children, Parent, contents);
                answer = new InspectionAnswer
                {
                    Found = result.Found, Holds = result.Holds, At = now
                };
                cache.Remove(item); cache.Add(item, answer);
                return answer;
            }
            catch (Exception error)
            {
                Available = false; Reset();
                SuiteMod.Fault("inspection traversal; using the item's own tags only", error);
                return Own(item);
            }
        }
        private static InspectionAnswer Own(GameItem item)
            => new InspectionAnswer { Found = Rules.Inspection(Marks.Flaggable(item), Marks.Tag(item, "ITEM_BEHIND_LOCK_TAG"), Marks.Tag(item, "ITEM_HIDDEN_TAG"), Marks.Tag(item, "LOCKED_TAG")) };
        private static void Clear()
        {
            cache = new ConditionalWeakTable<GameItem, InspectionAnswer>(); contents.Clear(); cachedAt = float.NegativeInfinity;
        }
        internal static void Reset() { Clear(); known = null; }
        internal static void Poll()
        {
            if (Marks.Client == null || !Marks.Security || Marks.OutsideShop || Marks.ShutterClosed
                || !Settings.On("enabled") || !Settings.On("mark_inspection"))
            { polledClient = null; return; }
            float now = SuiteRuntime.Clock();
            if (!ReferenceEquals(polledClient, Marks.Client) || now < previous || now >= nextRefresh)
            {
                polledClient = Marks.Client; nextRefresh = now + 1f;
                Marks.RequestAll();
            }
            previous = now;
        }
    }
}

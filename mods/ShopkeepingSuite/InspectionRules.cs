using System;
using System.Collections.Generic;

namespace ShopkeepingSuite
{
    internal sealed class InspectionContents
    {
        internal bool Found;
        internal int Holds;
    }
    // Pure boundary: stand-in trees can exercise this without loading the engine.
    internal static class InspectionRules
    {
        internal static bool Blocks<T>(T item, Func<T, string, bool> tag, Func<T, string> id) where T : class
        {
            if (tag(item, "ITEM_HIDDEN_TAG")) return true;
            string name = id(item);
            return name == "smuggler_bay" || name == "smuggler_bay_mod" || name == "smuggler_bay_mini";
        }
        private static bool Reachable<T>(T item, Func<T, string, bool> tag, Func<T, string> id, Func<T, T> parent) where T : class
        {
            if (Blocks(item, tag, id)) return false;
            for (int height = 0; height < 16; height++)
            {
                item = parent(item);
                if (item == null) return true;
                if (Blocks(item, tag, id)) return false;
            }
            // Unknown ancestry must not promise safety. This also bounds cycles.
            return true;
        }
        internal static bool Found<T>(T item, Func<T, string, bool> tag, Func<T, string> id,
            Func<T, bool> flaggable, Func<T, T> parent) where T : class
            => Reachable(item, tag, id, parent) && OwnFound(item, tag, flaggable);
        private static bool OwnFound<T>(T item, Func<T, string, bool> tag, Func<T, bool> flaggable) where T : class
            => Rules.Inspection(flaggable(item), tag(item, "ITEM_BEHIND_LOCK_TAG"), tag(item, "ITEM_HIDDEN_TAG"), tag(item, "LOCKED_TAG"));
        internal static int Holds<T>(T item, Func<T, string, bool> tag, Func<T, string> id,
            Func<T, bool> flaggable, Func<T, IEnumerable<T>> children, Func<T, T> parent) where T : class
            => Read(item, tag, id, flaggable, children, parent, new Dictionary<T, InspectionContents>()).Holds;

        // The memo contains subtree facts only. Reachability is checked once for
        // each requested item, so a cached descendant cannot bypass a hidden parent.
        internal static InspectionContents Read<T>(T item, Func<T, string, bool> tag, Func<T, string> id,
            Func<T, bool> flaggable, Func<T, IEnumerable<T>> children, Func<T, T> parent,
            IDictionary<T, InspectionContents> memo) where T : class
        {
            if (!Reachable(item, tag, id, parent)) return new InspectionContents();
            if (memo.TryGetValue(item, out InspectionContents cached)) return cached;
            var todo = new Stack<Walk<T>>();
            var active = new HashSet<T>();
            int visited = 0;
            void Push(T node)
            {
                var answer = new InspectionContents { Found = OwnFound(node, tag, flaggable) };
                todo.Push(new Walk<T> { Item = node, Answer = answer, Children = children(node).GetEnumerator() });
                active.Add(node);
            }
            void Add(InspectionContents into, InspectionContents child)
                => into.Holds = (int)Math.Min(int.MaxValue, (long)into.Holds + child.Holds + (child.Found ? 1 : 0));
            try
            {
                Push(item);
                while (todo.Count > 0)
                {
                    var current = todo.Peek();
                    if (!current.Children.MoveNext())
                    {
                        todo.Pop(); current.Children.Dispose(); active.Remove(current.Item);
                        memo[current.Item] = current.Answer;
                        if (todo.Count == 0) return current.Answer;
                        Add(todo.Peek().Answer, current.Answer);
                        continue;
                    }
                    T child = current.Children.Current;
                    visited++;
                    if (child != null && !Blocks(child, tag, id))
                    {
                        if (memo.TryGetValue(child, out cached)) Add(current.Answer, cached);
                        else if (active.Contains(child)) return Unfinished(todo, memo);
                        else if (visited < 2000) Push(child);
                    }
                    if (visited >= 2000) return Unfinished(todo, memo);
                }
                return new InspectionContents();
            }
            finally { while (todo.Count > 0) todo.Pop().Children.Dispose(); }
        }
        private sealed class Walk<T>
        {
            internal T Item;
            internal InspectionContents Answer;
            internal IEnumerator<T> Children;
        }
        private static InspectionContents Unfinished<T>(Stack<Walk<T>> todo, IDictionary<T, InspectionContents> memo)
        {
            // A cap or cycle warns for every unfinished ancestor. Never remember
            // an incomplete subtree as empty; completed siblings remain reusable.
            InspectionContents root = null;
            while (todo.Count > 0)
            {
                Walk<T> entry = todo.Peek();
                entry.Answer.Holds = Math.Max(1, entry.Answer.Holds);
                memo[entry.Item] = root = entry.Answer;
                todo.Pop().Children.Dispose();
            }
            return root;
        }
    }
}

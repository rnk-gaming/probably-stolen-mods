using System;
using System.Collections.Generic;
using System.Text;

namespace ShelfCompat
{
    internal sealed class Diagnostics
    {
        // Never reset on a settings change or disable/enable within the same session.
        private readonly HashSet<int> seen = new HashSet<int>();

        internal void Report<TItem, TNode>(TItem item, IList<TNode> above, bool ready, int grids, string back,
            Func<TItem, int> id, Func<TItem, string> label, Func<TNode, string> word,
            Action<string> log, Action<Exception> fault)
        {
            if (seen.Count >= 5) return;
            try
            {
                int uniqueId = id(item);
                if (!seen.Add(uniqueId)) return;
                var line = new StringBuilder("[Shelf Compat] ");
                line.Append(label(item)).Append('#').Append(uniqueId)
                    .Append(" is not in the shop, on a shelf or in the back room. Above it: ");
                if (above == null || above.Count == 0) line.Append("none");
                else
                    for (int i = 0; i < above.Count; i++)
                    {
                        if (i != 0) line.Append(", ");
                        line.Append(word(above[i]));
                    }
                line.Append(". shelves ready=").Append(ready).Append(" grids=").Append(grids)
                    .Append("; back room=").Append(back).Append('.');
                log(line.ToString());
            }
            catch (Exception error)
            {
                try { fault(error); } catch (Exception) { }
            }
        }
    }

    internal static class Rules
    {
        internal static bool After<TItem, TNode>(bool already, TItem item, Func<bool> enabled,
            Func<bool> waterEnabled, Func<bool> backEnabled, Func<bool> ready, Func<TNode[]> grids,
            Func<TNode> backRoom, Func<TItem, IList<TNode>> above,
            Action<TItem, IList<TNode>, bool, int, string> denied, Action<Exception> fault)
            where TItem : class where TNode : class
        {
            try
            {
                if (already || item == null || !enabled()) return already;
                bool shelfOn = waterEnabled(), backOn = backEnabled();
                bool shelfReady = ready();
                TNode[] shelves = grids();
                TNode back = backOn ? backRoom() : null;
                IList<TNode> nodes = above(item);
                if (nodes != null)
                    for (int n = 0; n < nodes.Count; n++)
                    {
                        TNode node = nodes[n];
                        if (node == null) continue;
                        if (back != null && ReferenceEquals(node, back)) return true;
                        if (shelfOn && shelfReady && shelves != null)
                            for (int g = 0; g < shelves.Length; g++)
                                if (ReferenceEquals(node, shelves[g])) return true;
                    }
                denied(item, nodes, shelfReady, shelves?.Length ?? 0, backOn ? "not found" : "off");
                return already;
            }
            catch (Exception error)
            {
                try { fault(error); } catch (Exception) { }
                return already;
            }
        }
    }
}

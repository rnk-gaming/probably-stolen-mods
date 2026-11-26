// Compiled separately into each mod. No state crosses assembly boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalMods
{
    internal static partial class StorageRules
    {
        internal const int Sane = 100;
        internal static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }

    internal static partial class Storage
    {
        internal const string ColumnsTag = "MACHINE_UPGRADES_COLUMNS_INT", RowsTag = "MACHINE_UPGRADES_ROWS_INT";
        private static readonly HashSet<string> reported = new HashSet<string>();
        internal static string Prefix = "[" + typeof(Storage).Assembly.GetName().Name + "]";
        internal static Action<string> Log = message => Debug.LogWarning(message);
        internal static Func<bool> InCombat = () => GameMaster.current != null && GameMaster.current.InCombat();
        internal static Func<GameItem, bool> Owned = item => GeneralHelper.IsItemOwned(item);
        internal static Func<GameItem, bool> Locked = item => LockHelper.IsCurrentlyLocked(item);
        internal static Func<GameItem, bool> Empty = IsEmpty;
        internal static Func<GameItem, string, int> Read = (item, key) => item.GetTagReadonly(key).GetInt();
        internal static Action<GameItem, string, int> Write = (item, key, value) => item.ModifyTag(key, state => state.Enable().SetInt(value));
        // Unlike the game's search, this stops at items as well as found grids,
        // including bags reached through pocket slots rather than through a page.
        internal static List<GameGridInventory> OwnGrids(GraphNodeStorage root)
        {
            var grids = new List<GameGridInventory>();
            foreach (GraphNodeStorage node in GridWalk.Pages(root, node => node.children, node => node is GameItem, node => node is GameGridInventory))
                grids.Add((GameGridInventory)node);
            return grids;
        }
        internal static Func<GameItem, int[][]> Sizes = GridSizes;
        internal static Action<GameItem, int, int> Resize = ResizeGrids;
        internal static Action<RichTextBuilder, string> Line = (builder, text) => builder.AddLine(text, true, RenderHandler.ColorPalette.LightCyan);
        internal static long Faults;

        internal static void Fault(string where, Exception error)
        {
            unchecked { Faults++; }
            try { if (reported.Add(where)) Log?.Invoke(Prefix + " a fault in " + where + "; leaving the game's own behaviour. " + error.GetType().Name + ": " + error.Message); }
            catch (Exception) { }
        }

        internal static void Guard(string where, Action action)
        {
            try { action(); }
            catch (Exception error) { Fault(where, error); }
        }

        private static bool IsEmpty(GameItem item)
        {
            foreach (GameItem child in GraphUtils.FindAllChildrenType<GameItem>(item))
                if (child != null && child != item) return false;
            return true;
        }

        private static int[][] GridSizes(GameItem item)
        {
            if (item.contentWindow == null) return Array.Empty<int[]>();
            var sizes = new List<int[]>();
            foreach (GameGridInventory grid in OwnGrids(item.contentWindow))
                sizes.Add(new[] { grid.inventoryShape.width, grid.inventoryShape.height });
            return sizes.ToArray();
        }

        private static void ResizeGrids(GameItem item, int width, int height)
        {
            if (item.contentWindow == null) return;
            var grids = OwnGrids(item.contentWindow);
            // Validate all shapes before changing the first page.
            foreach (GameGridInventory grid in grids)
                if (grid.inventoryShape.width > width || grid.inventoryShape.height > height)
                    throw new InvalidOperationException("Storage resize would shrink a page");
            foreach (GameGridInventory grid in grids)
                if (grid.inventoryShape.width < width || grid.inventoryShape.height < height) grid.SetShape(width, height);
            item.contentWindow.Validate();
        }

    }

    internal static class GridWalk
    {
        internal static List<T> Pages<T>(T root, Func<T, IEnumerable<T>> children, Func<T, bool> item, Func<T, bool> grid) where T : class
        {
            var result = new List<T>();
            var queue = new Queue<T>();
            var seen = new HashSet<T>();
            if (root != null) queue.Enqueue(root);
            while (queue.Count > 0)
            {
                T node = queue.Dequeue();
                if (node == null || !seen.Add(node) || item(node)) continue;
                if (grid(node)) { result.Add(node); continue; }
                IEnumerable<T> next = children(node);
                if (next != null) foreach (T child in next) queue.Enqueue(child);
            }
            return result;
        }
    }

    // Using up an upgrade material. The game's own routine destroys the item it is
    // handed, including the whole stack when another mod has stacked the material.
    // Take one unit, with the game's Tinkerer chance of keeping it.
    internal static class Material
    {
        // Replaced by the tests; in the game they are the game's own calls.
        internal static Func<bool> Kept = () => StartingPerk.IsTinkerer() && RNG.Roll(20);
        internal static Func<GameItem, int> Units = item => item.unitCount;
        internal static Action<GameItem, int> SetUnits = (item, count) => item.SetUnitCount(count);
        internal static Action<GameItem> Destroy = item => item.Destroy();
        internal static Action<GameItem, GameItem> Stow = StowInGrid;

        // Set when the dropped item no longer exists after an upgrade.
        internal static bool Gone;
        private sealed class Payment
        {
            internal GameItem Source;
            internal bool Kept, Paid, Used;
        }
        private static Payment payment;
        internal static bool Paying(GameItem source) => payment != null && payment.Source == source;

        internal static bool Grew(int[][] before, int[][] after)
        {
            if (before == null || after == null || before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; i++)
                if (after[i][0] > before[i][0] || after[i][1] > before[i][1]) return true;
            return false;
        }

        // A step reserves a unit before changing saved progress. The
        // last unit is destroyed only at the existing commit point. A failed plan
        // returns its reservation; nested drops own independent payment frames.
        internal static bool Step(GameItem source, GameItem target, Func<GameItem, GameItem, bool> ready,
            Func<GameItem, GameItem, bool> apply, bool tinkerer = true)
        {
            Payment previous = payment;
            int before = 0;
            bool committed = false;
            var pending = new Payment { Source = source };
            try
            {
                if (Spent(source) || !ready(source, target)) return false;
                before = Units(source);
                if (before < 1) return false;
                pending.Kept = tinkerer && Kept();
                payment = pending;
                if (!pending.Kept && before > 1)
                {
                    // SetUnits can fail after changing the count. The finally
                    // block checks the real count before returning a reservation.
                    SetUnits(source, before - 1);
                    pending.Paid = true;
                }
                committed = apply(source, target);
                return committed;
            }
            catch (Exception error) { Storage.Fault("paying for a stack upgrade", error); return false; }
            finally
            {
                if (!committed && before > 1 && !pending.Kept)
                    Storage.Guard("returning stack upgrade material", () =>
                    {
                        if (Units(source) == before - 1) SetUnits(source, before);
                    });
                payment = previous;
            }
        }

        private static bool Reserved(GameItem item)
        {
            if (payment == null || payment.Source != item || payment.Used) return false;
            payment.Used = true;
            if (!payment.Paid && !payment.Kept) UseRawUnit(item);
            return true;
        }

        internal static void UseOne(GameItem item)
        {
            if (Reserved(item)) return;
            if (Kept()) return;
            UseRawUnit(item);
        }

        // Joined containers do not get the Tinkerer chance.
        internal static void UseUnit(GameItem item)
        {
            if (Reserved(item)) return;
            UseRawUnit(item);
        }

        private static void UseRawUnit(GameItem item)
        {
            int units = Units(item);
            if (units > 1) SetUnits(item, units - 1);
            else
            {
                Gone = true;
                if (spent.ContainsKey(item)) return;
                Frame owner = holding > 0 ? frame : null;
                spent[item] = owner;
                if (owner != null) return;
                try { Destroy(item); spent.Remove(item); }
                catch (Exception error) { Storage.Fault("removing a used-up stack", error); }
            }
        }

        // Station Economy notes a stack before a drop and, when it finds the item
        // destroyed afterwards, puts all but one unit back. A stack that a drop uses
        // up therefore keeps its last unit until every patch on that call of the
        // game's routine has run; the count then differs from the noted one and that
        // mod leaves it alone. Each call owns a frame, so a call inside another one
        // never removes what the outer call holds. A held item is spent: it pays for
        // nothing more, and a removal that fails is tried again at the next call.
        internal sealed class Frame { internal Frame Previous; }
        private static Frame frame;
        private static readonly Dictionary<GameItem, Frame> spent = new Dictionary<GameItem, Frame>();
        private static int holding;

        internal static bool Spent(GameItem item) => item != null && spent.ContainsKey(item);

        internal static Frame Enter()
        {
            frame = new Frame { Previous = frame };
            return frame;
        }

        internal static void Leave(Frame mine)
        {
            if (mine == null) return;
            try
            {
                bool open = false;
                for (Frame f = frame; f != null; f = f.Previous) if (f == mine) open = true;
                if (open) frame = mine.Previous;
                var due = new List<GameItem>();
                foreach (KeyValuePair<GameItem, Frame> entry in spent)
                    if (entry.Value == mine || entry.Value == null) due.Add(entry.Key);
                foreach (GameItem item in due)
                {
                    try { Destroy(item); spent.Remove(item); }
                    catch (Exception error) { spent[item] = null; Storage.Fault("removing a used-up stack", error); }
                }
            }
            catch (Exception error) { Storage.Fault("ending a drop", error); }
        }

        internal static bool Held(GameItem source, Func<bool> drop)
        {
            if (Spent(source)) return false;
            bool hold = false;
            try { hold = Units(source) > 1; }
            catch (Exception error) { Storage.Fault("counting a dropped stack", error); }
            if (!hold) return drop();
            holding++;
            try { return drop(); }
            finally { holding--; }
        }

        // `Many`: one drop takes one step, also with `stack_steps` off and for a
        // single item. A step that needs several units of
        // the dropped material takes them from the stack, never more than the stack
        // held; `more` says whether that step still waits. `Dropped` with `stackSteps`:
        // a stack takes every step it covers. What is left
        // stays a stack; for a backpack it is put into the backpack when there is room.
        internal static bool Many(GameItem source, GameItem target, Func<GameItem, GameItem, bool> apply, Func<bool> more, bool restIntoTarget)
            => Run(source, target, apply, more, restIntoTarget, false, null, true);

        internal static bool Dropped(GameItem source, GameItem target, Func<GameItem, GameItem, bool> apply, Func<bool> more, bool restIntoTarget,
            bool stackSteps, Func<GameItem, GameItem, bool> ready, bool tinkerer = true)
            => Run(source, target, apply, more, restIntoTarget, stackSteps, ready, tinkerer);

        private static bool Run(GameItem source, GameItem target, Func<GameItem, GameItem, bool> apply, Func<bool> more, bool restIntoTarget,
            bool stackSteps, Func<GameItem, GameItem, bool> ready, bool tinkerer)
        {
            bool previous = Gone, any = false, gone = false;
            try
            {
                int tries = Math.Max(1, Units(source));
                bool repeat = stackSteps && tries > 1;
                Gone = false;
                for (int i = 0; i < tries; i++)
                {
                    Gone = false;
                    long faults = Storage.Faults;
                    if (!(repeat ? Step(source, target, ready, apply, tinkerer) : apply(source, target))) break;
                    any = true;
                    gone = Gone;
                    if (gone || Storage.Faults != faults || (!repeat && !more())) break;
                }
            }
            catch (Exception error) { Storage.Fault("upgrading with a dropped stack", error); }
            finally
            {
                if (any && restIntoTarget && !gone) Storage.Guard("putting the rest into the backpack", () => Stow(source, target));
                Gone = previous;
            }
            return any;
        }

        private static void StowInGrid(GameItem source, GameItem target)
        {
            if (source == null || target == null || target.contentWindow == null) return;
            int left = source.unitCount;
            foreach (GameGridInventory grid in Storage.OwnGrids(target.contentWindow))
            {
                if (left < 1) return;
                left = GraphUtils.TryAcceptAll(grid, source, left);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace StationEconomyCompat
{
    // All inventory access is supplied by the adapter; offline tests use plain objects.
    internal sealed class FurnaceTransfer
    {
        internal Func<object, object[]> Items;
        internal Func<object, string> Identifier;
        internal Func<object, int> Units;
        internal Func<object, bool> IsHopper;
        internal Func<object, object, bool> InSource;
        internal Func<object, object, int> Place;
        internal Action<object, object> Prepare;
        internal Func<object, object, object[], object, int> RecordNew;
    }

    internal static class FurnaceRules
    {
        internal static bool Allows(string id, int mode, List<string> list) => mode == 1
            ? list != null && list.Contains(id) : mode != 2 || list == null || !list.Contains(id);
        internal static int Allowance(int need, long available, int keep) =>
            (int)Math.Min(Math.Max(0, need), Math.Max(0L, available - Math.Max(0, keep)));

        internal static bool Before(bool active, bool installed, Func<bool> on, object src, object inv,
            string[] ids, int need, int mode, List<string> list, int keep, object mine,
            FurnaceTransfer port, Action<Exception> fault, ref int result)
        {
            int moved = 0;
            object[] before = null;
            bool counted = false;
            // Keep a pending snapshot until recording succeeds. If placement or recording
            // fails after moving, the catch can finish the ledger without pulling twice.
            try
            {
                if (!active || !installed || !on()) return true;
                if (need > 0)
                    foreach (string id in ids)
                    {
                        if (moved >= need) break;
                        if (!Allows(id, mode, list)) continue;
                        object[] items = port.Items(src);
                        var candidates = new List<object>();
                        long available = 0;
                        foreach (object item in items)
                        {
                            if (item == null || port.Identifier(item) != id || port.IsHopper(item)) continue;
                            candidates.Add(item);
                            available += Math.Max(1, port.Units(item));
                        }
                        int remaining = Allowance(need - moved, available, keep);
                        bool blocked = false;
                        foreach (object item in candidates)
                        {
                            // Revisit a stack after every split; its last unit moves the
                            // item itself, at which point it must not be tried again.
                            while (remaining > 0 && port.InSource(item, src))
                            {
                                port.Prepare(inv, mine); // Reserve ledger space before moving.
                                before = port.Items(inv);
                                counted = false;
                                int left = port.Place(inv, item);
                                if (left == 0) { moved++; counted = true; remaining--; }
                                int added = port.RecordNew(inv, src, before, mine);
                                if (!counted && added > 0) { moved++; counted = true; }
                                before = null;
                                if (left != 0) { blocked = true; break; }
                            }
                            if (remaining <= 0 || blocked) break;
                        }
                    }
                result = moved;
                return false;
            }
            catch (Exception error)
            {
                if (before != null)
                {
                    try
                    {
                        int added = port.RecordNew(inv, src, before, mine);
                        if (!counted && added > 0) moved++;
                    }
                    catch (Exception recording) { Report(fault, recording); }
                }
                Report(fault, error);
                if (moved == 0) return true;
                result = moved;
                return false;
            }
        }

        private static void Report(Action<Exception> fault, Exception error)
        {
            try { fault(error); } catch (Exception) { }
        }
    }
}

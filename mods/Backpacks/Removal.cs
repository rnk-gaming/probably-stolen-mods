using System;
using LocalMods;

namespace Backpacks
{
    internal static class Removal
    {
        internal static RemovalContainer Describe(GameItem item)
        {
            if (item == null) return null;
            bool military = Military.IsMilitary(item.identifier);
            if (!military && Storage.Read(item, Storage.RowsTag) <= 0 && Storage.Read(item, Storage.ColumnsTag) <= 0
                && Storage.Read(item, Backpacks.TabsTag) <= 0 && Storage.Read(item, Pockets.PocketsTag) <= 0 && Storage.Read(item, Pockets.ToolboxTag) <= 0) return null;
            if (!BackpackRules.Bags.TryGetValue(Military.Canonical(item.identifier) ?? "", out int[] basis))
                return RemovalList.Capture(item, null, 1, false);
            return RemovalList.Capture(item, new[] { new[] { basis[0], basis[1] } }, 1, military);
        }
    }
}

using System;
using System.Collections.Generic;

using LocalMods;

namespace Backpacks
{
    // Entries in the game's debug menu, so that an upgraded backpack does not have to be
    // built step by step for every test. A backpack
    // is made by writing the same tags the upgrade steps write and letting the mod's own
    // restore build the window. The starting perks share this builder.
    internal static class DebugTools
    {
        internal static string Category => TextTable.Early(TextKeys.DebugCategory);

        // What a fully upgraded backpack of this id carries, as tag and value. Pure.
        internal static List<KeyValuePair<string, int>> FullTags(string id)
        {
            id = Military.Canonical(id);
            var tags = new List<KeyValuePair<string, int>>();
            if (id == null || !BackpackRules.Bags.TryGetValue(id, out int[] bag)) return tags;
            void Add(string key, int value) { if (value > 0) tags.Add(new KeyValuePair<string, int>(key, value)); }
            Add(Storage.RowsTag, bag[2] - bag[1]);
            Add(Storage.ColumnsTag, BackpackRules.Widths[id] - bag[0]);
            Add(Backpacks.TabsTag, BackpackRules.MaxTabs(id));
            Add(Backpacks.StackTag, 4);
            int bags = PocketRules.Maximum(id);
            bool toolbox = PocketRules.Toolbox(id);
            Add(Pockets.PocketsTag, bags);
            if (toolbox) Add(Pockets.ToolboxTag, 2);
            if (bags > 0 || toolbox) Add(Pockets.LayoutTag, 2);
            return tags;
        }

        // Whether the window has what the full tags ask for: every page, at full size, and
        // the side bag slots. `layout` is what Pockets.Layout reports (index 2: bag slots).
        internal static bool Built(string id, int[][] pages, int[] layout)
        {
            id = Military.Canonical(id);
            if (id == null || !BackpackRules.Bags.TryGetValue(id, out int[] bag) || pages == null) return false;
            if (pages.Length != BackpackRules.MaxTabs(id) + 1) return false;
            foreach (int[] page in pages)
                if (page == null || page.Length != 2 || page[0] < BackpackRules.Widths[id] || page[1] < bag[2]) return false;
            int bags = PocketRules.Maximum(id);
            return (bags == 0 && !PocketRules.Toolbox(id)) || (layout != null && layout.Length >= 5 && layout[2] >= bags
                && (!PocketRules.Toolbox(id) || layout[4] > 0));
        }

        internal static Func<string, GameItem> Create = id => DirectoryMaster.Item(id);
        internal static Action<GameItem> Place = item => GraphUtils.EmporiumTryAdd(item);
        internal static Action<string, string, Action> AddEntry = (id, name, action) => ModHelper.AddDebugFunction(id, Category, name, action);

        internal static GameItem MakeFull(string id)
        {
            id = Military.Canonical(id);
            if (id == null || !BackpackRules.Bags.TryGetValue(id, out int[] bag)) return null;
            return Make(id, bag[2] - bag[1], BackpackRules.Widths[id] - bag[0], BackpackRules.MaxTabs(id), 4,
                PocketRules.Maximum(id), PocketRules.Toolbox(id) ? 2 : 0);
        }

        internal static GameItem Make(string id, int rows, int columns, int compartments, int stacks, int sideBags, int toolbox)
        {
            id = Military.Canonical(id);
            if (id == null || !BackpackRules.Bags.TryGetValue(id, out int[] bag)) return null;
            rows = StorageRules.Clamp(rows, 0, bag[2] - bag[1]);
            columns = StorageRules.Clamp(columns, 0, BackpackRules.Widths[id] - bag[0]);
            compartments = StorageRules.Clamp(compartments, 0, BackpackRules.MaxTabs(id));
            stacks = StorageRules.Clamp(stacks, 0, 4);
            sideBags = StorageRules.Clamp(sideBags, 0, PocketRules.Maximum(id));
            toolbox = PocketRules.Toolbox(id) && toolbox > 0 ? 2 : 0;
            GameItem item = null;
            try
            {
                item = Create(id);
                if (item == null) return null;
                string[] keys = { Storage.RowsTag, Storage.ColumnsTag, Backpacks.TabsTag, Backpacks.StackTag, Pockets.PocketsTag, Pockets.ToolboxTag, Pockets.LayoutTag };
                int[] values = { rows, columns, compartments, stacks, sideBags, toolbox, sideBags > 0 || toolbox > 0 ? 2 : 0 };
                for (int i = 0; i < keys.Length; i++) if (values[i] > 0) Storage.Write(item, keys[i], values[i]);
                // The same routine a loaded backpack goes through: pages, slots, sizes.
                Backpacks.Restore(item);
                // That routine keeps its faults to itself, so the result is looked at: a
                // backpack that did not get its pages, its size or its slots is not handed out.
                int[][] actual = Storage.Sizes(item);
                int[] layout = Pockets.Layout(item);
                if (actual == null || actual.Length != compartments + 1) throw new InvalidOperationException("The backpack was not built completely");
                foreach (int[] page in actual)
                    if (page == null || page.Length != 2 || page[0] < bag[0] + columns || page[1] < bag[1] + rows)
                        throw new InvalidOperationException("The backpack was not built completely");
                if ((sideBags > 0 || toolbox > 0) && (layout == null || layout.Length < 5 || layout[2] < sideBags || (toolbox > 0 && layout[4] <= 0)))
                    throw new InvalidOperationException("The backpack was not built completely");
                // The pages get their names as they do when a page is bought.
                int pages = compartments + 1;
                if (pages > 1) Storage.Guard("naming a debug backpack's compartments", () => Backpacks.NamePages(item, pages));
                return item;
            }
            catch
            {
                if (item != null) Storage.Guard("discarding an unfinished debug backpack", () => Material.Destroy(item));
                throw;
            }
        }

        internal static void Spawn(string id)
        {
            Storage.Guard("debug backpack", () =>
            {
                GameItem item = MakeFull(id);
                if (item == null) return;
                try
                {
                    Place(item);
                    if (item.parentInventory == null) throw new InvalidOperationException("The debug backpack found no room");
                }
                catch
                {
                    if (item.parentInventory == null) Storage.Guard("discarding an unplaced debug backpack", () => Material.Destroy(item));
                    throw;
                }
            });
        }

        internal static void Register(string modId)
        {
            Storage.Guard("debug entries", () =>
            {
                foreach (string id in new[] { "backpack_small", "backpack_medium", "backpack_large", Military.Medium, Military.Large })
                {
                    string bag = id;
                    string label = bag == Military.Medium ? TextTable.Early(TextKeys.ScavengersPackName)
                        : bag == Military.Large ? TextTable.Early(TextKeys.HaulersPackName) : bag;
                    AddEntry(modId, TextTable.Early(TextKeys.DebugFullyUpgraded, label), () => Spawn(bag));
                }
            });
        }
    }
}

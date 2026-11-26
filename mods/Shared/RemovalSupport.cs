// Compiled independently into the mods that enlarge containers.
using System;
using System.Collections.Generic;

namespace LocalMods
{
    internal sealed class RemovalPage
    {
        internal int BaseWidth, BaseHeight, Width, Height;
        internal bool Added;
        // Position, extent and number of objects including contents of a nested bag.
        internal int[][] Items = Array.Empty<int[]>();
    }

    internal sealed class RemovalContainer
    {
        internal string Name, Location;
        internal bool All;
        internal int SlotItems;
        internal RemovalPage[] Pages = Array.Empty<RemovalPage>();
        internal int[] AffectedIds;
    }

    internal static class RemovalRules
    {
        internal static bool Affected(bool all, RemovalPage page, int[] item) => all || page.Added || item[0] < 0 || item[1] < 0
            || (long)item[0] + item[2] > page.BaseWidth || (long)item[1] + item[3] > page.BaseHeight;

        internal static long Total(IEnumerable<RemovalContainer> containers)
        {
            var ids = new HashSet<int>(); long unnumbered = 0;
            foreach (RemovalContainer container in containers)
            {
                long count = Count(container);
                if (count < 0) continue;
                if (container.AffectedIds == null) unnumbered += count + (container.All ? 1 : 0);
                else foreach (int id in container.AffectedIds) ids.Add(id);
            }
            return unnumbered + ids.Count;
        }
        // -1 means cannot tell. Arithmetic is widened before adding coordinates.
        internal static long Count(RemovalContainer container)
        {
            if (container == null || container.Pages == null || container.Pages.Length == 0 || container.SlotItems < 0) return -1;
            long total = container.SlotItems;
            foreach (RemovalPage page in container.Pages)
            {
                if (page == null || page.Items == null || page.Width <= 0 || page.Height <= 0
                    || (!container.All && !page.Added && (page.BaseWidth <= 0 || page.BaseHeight <= 0))) return -1;
                foreach (int[] item in page.Items)
                {
                    if (item == null || item.Length != 5 || item[2] <= 0 || item[3] <= 0 || item[4] <= 0) return -1;
                    if (Affected(container.All, page, item)) total += item[4];
                }
            }
            return total;
        }
    }

    internal static class RemovalList
    {
        private static string modId, modName;
        internal static Func<bool> RunLoaded = () => PlayerStore.HasInstance;
        internal static Func<bool> InShop = () => EmporiumEntry.Instance != null && MapUIManager.Instance != null
            && !MapUIManager.Instance.IsOutside && !MapUIManager.Instance.IsInTransit;
        internal static Func<bool, IEnumerable<GameItem>> FindItems = owned => PlayerStore.Instance.FindAllItem(owned);
        internal static Func<IEnumerable<GameItem>> Items = AllItems;
        internal static Func<GameItem, RemovalContainer> Describe = item => null;
        internal static Func<GameItem, int[][], int, bool, RemovalContainer> Capture = (item, basis, pages, all) => Snapshot(item, basis, pages, all);
        internal static Action<string, string, List<string>> Show = Render;
        internal static Action<string> Close = id => { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow(id); };

        internal static void Register(string id, string name, string group, Func<GameItem, RemovalContainer> describe)
        {
            modId = id; modName = name; Describe = describe;
            Storage.Guard("removal list option", () =>
            {
                string section = OptionsRules.SectionId(id, group);
                var option = GameOptionRegistry.Toggle(OptionsRules.OptionId(id, "before_removing"),
                    () => "Before removing this mod: list what to move", () => false, Request,
                    () => "Lists affected containers in the loaded run. Moves nothing. Apply to open the list.");
                option.source = id;
                GameOptionRegistry.GetOrAddSection(section, () => OptionsRules.SectionLabel(name, group), id);
                GameOptionRegistry.Add(section, option);
            });
        }

        internal static void Disable()
        {
            Storage.Guard("removal list cleanup", () =>
            {
                GameOptionRegistry.Remove(OptionsRules.OptionId(modId, "before_removing"));
                Close(modId + ".before-removing");
            });
        }

        internal static void Request(bool value) { if (value) Open(); }

        private static IEnumerable<GameItem> AllItems()
        {
            var seen = new HashSet<GameItem>();
            foreach (bool owned in new[] { true, false })
                foreach (GameItem item in FindItems(owned))
                    if (item != null && seen.Add(item)) yield return item;
        }

        internal static void Open()
        {
            var lines = new List<string>();
            try
            {
                if (!RunLoaded()) lines.Add("No run is loaded. Load a run and return to the shop first.");
                else if (!InShop()) lines.Add("Return to the shop to list what must be moved.");
                else
                {
                    bool unknown = false;
                    var listed = new List<RemovalContainer>();
                    var seen = new HashSet<GameItem>();
                    foreach (GameItem item in Items())
                    {
                        if (item == null || !seen.Add(item)) continue;
                        RemovalContainer container;
                        try { container = Describe(item); }
                        catch (Exception error)
                        {
                            unknown = true;
                            lines.Add((item.name ?? item.identifier ?? "Container") + ": cannot tell (could not inspect this container)");
                            Storage.Fault("inspecting a removal container", error); continue;
                        }
                        if (container == null) continue;
                        long count = RemovalRules.Count(container);
                        if (count == 0 && !container.All) continue;
                        unknown |= count < 0;
                        listed.Add(container);
                        if (container.All)
                            lines.Add(container.Name + ": this backpack itself is removed with the mod"
                                + (count < 0 ? "; cannot tell how many items are in it" : count > 0 ? "; " + count + " items in it" : "")
                                + " | " + container.Location);
                        else lines.Add(container.Name + " | " + container.Location + " | " + (count < 0 ? "cannot tell" : count + " items to move"));
                    }
                    if (seen.Count == 0)
                    {
                        unknown = true;
                        lines.Add("Cannot tell: no items were found in the loaded run.");
                    }
                    if (lines.Count == 0) lines.Add("Nothing to move: this mod can be removed");
                    else lines.Add("Total: " + RemovalRules.Total(listed) + " items" + (unknown ? "; cannot tell for some containers" : ""));
                }
            }
            catch (Exception error)
            {
                lines.Add("Cannot finish the list: cannot tell what remains. Keep this mod enabled and try again.");
                Storage.Fault("listing containers before removal", error);
            }
            Storage.Guard("opening removal list", () =>
            {
                string id = modId + ".before-removing";
                Close(id);
                Show(id, modName + ": Before removing this mod", lines);
            });
        }

        // An unavailable base remains explicit; never infer it from a live enlarged grid.
        internal static RemovalContainer Snapshot(GameItem item, int[][] basis, int basePages, bool all,
            IEnumerable<GameGridInventory> inventories = null, bool includeSlots = true)
        {
            var result = new RemovalContainer { Name = item.name ?? item.identifier ?? "Container", All = all, Location = Location(item) };
            var pages = new List<RemovalPage>();
            var affected = new HashSet<int>();
            if (all) affected.Add(item.uniqueId);
            foreach (GameGridInventory grid in inventories ?? Storage.OwnGrids(item.contentWindow))
            {
                if (grid == null || grid.inventoryShape == null) throw new InvalidOperationException("Container grid missing");
                int index = pages.Count;
                int[] size = basis == null || basis.Length == 0 ? null : basis[Math.Min(index, basis.Length - 1)];
                var page = new RemovalPage { BaseWidth = size == null ? 0 : size[0], BaseHeight = size == null ? 0 : size[1],
                    Width = grid.inventoryShape.width, Height = grid.inventoryShape.height, Added = index >= basePages };
                var entries = new List<int[]>();
                foreach (GameItem child in grid.childItems)
                {
                    GridShape shape = grid.GetItemInventorySlot(child)?.itemGridShape;
                    if (shape == null) throw new InvalidOperationException("Item position missing");
                    int[] entry = { shape.minX, shape.minY, shape.globalWidth, shape.globalHeight, ContentsCount(child) };
                    entries.Add(entry);
                    if (RemovalRules.Affected(all, page, entry)) foreach (int id in ContentIds(child)) affected.Add(id);
                }
                page.Items = entries.ToArray(); pages.Add(page);
            }
            result.Pages = pages.ToArray();
            if (includeSlots)
                foreach (GraphNodeStorage node in GridWalk.Pages<GraphNodeStorage>(item.contentWindow,
                    value => value.children, value => value is GameItem, value => value is GameInventory))
                    if (node is GameSlotInventory slot && slot.childItem != null)
                    {
                        result.SlotItems = checked(result.SlotItems + ContentsCount(slot.childItem));
                        foreach (int id in ContentIds(slot.childItem)) affected.Add(id);
                    }
            result.AffectedIds = new int[affected.Count]; affected.CopyTo(result.AffectedIds);
            return result;
        }

        private static HashSet<int> ContentIds(GameItem root)
        {
            var ids = new HashSet<int>();
            foreach (GraphNodeStorage node in ContentNodes(root)) if (node is GameItem item) ids.Add(item.uniqueId);
            return ids;
        }

        private static int ContentsCount(GameItem root)
        {
            int count = 0;
            foreach (GraphNodeStorage node in ContentNodes(root)) if (node is GameItem) count++;
            return count;
        }

        private static IEnumerable<GraphNodeStorage> ContentNodes(GameItem root)
        {
            var queue = new Queue<GraphNodeStorage>(); var seen = new HashSet<GraphNodeStorage>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                GraphNodeStorage node = queue.Dequeue();
                if (node == null || !seen.Add(node)) continue;
                if (seen.Count > 100000) throw new InvalidOperationException("Container graph too large");
                yield return node;
                if (node.children != null) foreach (GraphNodeStorage child in node.children) queue.Enqueue(child);
            }
        }

        private static string Location(GameItem item)
        {
            GameInventory inventory = item.parentInventory;
            if (inventory == null) return "location unknown";
            GameItem parent = inventory.GetParentItem();
            return parent != null ? parent.name ?? parent.identifier ?? "container" : inventory.identifier ?? "inventory";
        }

        private static void Render(string id, string title, List<string> lines)
        {
            if (CustomUIManager.Instance == null) throw new InvalidOperationException("Custom UI is unavailable");
            var builder = CustomUIManager.Instance.CreateWindow(id, title, "meta").SetSize(620, 600).Center().SetDraggable(true).SetCloseOnEscape();
            builder.BeginScroll(490);
            foreach (string line in lines) builder.AddLabel(line);
            builder.End().AddCloseButton(() => Storage.Guard("closing removal list", () => Close(id))).Show();
        }
    }
}

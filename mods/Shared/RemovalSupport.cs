// Compiled independently into the mods that enlarge containers.
using System;
using System.Collections.Generic;

namespace LocalMods
{
    internal static class RemovalTextKeys
    {
        // Each linking mod includes these in its TextKeys and in its own English table.
        internal const string RemovalOption = "removal_option";
        internal const string RemovalOptionDesc = "removal_option_desc";
        internal const string RemovalNoRun = "removal_no_run";
        internal const string RemovalReturn = "removal_return";
        internal const string RemovalEmptyWalk = "removal_empty_walk";
        internal const string RemovalSafe = "removal_safe";
        internal const string RemovalFailed = "removal_failed";
        internal const string RemovalContainer = "removal_container";
        internal const string RemovalLocationUnknown = "removal_location_unknown";
        internal const string RemovalParentContainer = "removal_parent_container";
        internal const string RemovalInventory = "removal_inventory";
        internal const string RemovalInspectFailed = "removal_inspect_failed";
        internal const string RemovalPackUnknown = "removal_pack_unknown";
        internal const string RemovalPackContents = "removal_pack_contents";
        internal const string RemovalPackEmpty = "removal_pack_empty";
        internal const string RemovalContainerUnknown = "removal_container_unknown";
        internal const string RemovalContainerCount = "removal_container_count";
        internal const string RemovalTotalUnknown = "removal_total_unknown";
        internal const string RemovalTotal = "removal_total";
        internal const string RemovalTitle = "removal_title";
    }

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
                    TextTable.Later(RemovalTextKeys.RemovalOption), () => false, Request,
                    TextTable.Later(RemovalTextKeys.RemovalOptionDesc));
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
                if (!RunLoaded()) lines.Add(TextTable.Get(RemovalTextKeys.RemovalNoRun));
                else if (!InShop()) lines.Add(TextTable.Get(RemovalTextKeys.RemovalReturn));
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
                            lines.Add(TextTable.Get(RemovalTextKeys.RemovalInspectFailed, item.name ?? item.identifier ?? TextTable.Get(RemovalTextKeys.RemovalContainer)));
                            Storage.Fault("inspecting a removal container", error); continue;
                        }
                        if (container == null) continue;
                        long count = RemovalRules.Count(container);
                        if (count == 0 && !container.All) continue;
                        unknown |= count < 0;
                        listed.Add(container);
                        if (container.All)
                            lines.Add(TextTable.Get(count < 0 ? RemovalTextKeys.RemovalPackUnknown
                                : count > 0 ? RemovalTextKeys.RemovalPackContents : RemovalTextKeys.RemovalPackEmpty, container.Name, container.Location, count));
                        else lines.Add(TextTable.Get(count < 0 ? RemovalTextKeys.RemovalContainerUnknown : RemovalTextKeys.RemovalContainerCount, container.Name, container.Location, count));
                    }
                    if (seen.Count == 0)
                    {
                        unknown = true;
                        lines.Add(TextTable.Get(RemovalTextKeys.RemovalEmptyWalk));
                    }
                    if (lines.Count == 0) lines.Add(TextTable.Get(RemovalTextKeys.RemovalSafe));
                    else lines.Add(TextTable.Get(unknown ? RemovalTextKeys.RemovalTotalUnknown : RemovalTextKeys.RemovalTotal, RemovalRules.Total(listed)));
                }
            }
            catch (Exception error)
            {
                lines.Add(TextTable.Get(RemovalTextKeys.RemovalFailed));
                Storage.Fault("listing containers before removal", error);
            }
            Storage.Guard("opening removal list", () =>
            {
                string id = modId + ".before-removing";
                Close(id);
                Show(id, TextTable.Get(RemovalTextKeys.RemovalTitle, modName), lines);
            });
        }

        // An unavailable base remains explicit; never infer it from a live enlarged grid.
        internal static RemovalContainer Snapshot(GameItem item, int[][] basis, int basePages, bool all,
            IEnumerable<GameGridInventory> inventories = null, bool includeSlots = true)
        {
            var result = new RemovalContainer { Name = item.name ?? item.identifier ?? TextTable.Get(RemovalTextKeys.RemovalContainer), All = all, Location = Location(item) };
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
            if (inventory == null) return TextTable.Get(RemovalTextKeys.RemovalLocationUnknown);
            GameItem parent = inventory.GetParentItem();
            return parent != null ? parent.name ?? parent.identifier ?? TextTable.Get(RemovalTextKeys.RemovalParentContainer) : inventory.identifier ?? TextTable.Get(RemovalTextKeys.RemovalInventory);
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

using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using LocalMods;

namespace Backpacks
{
    // Values only, including the priority shared with the older backpack upgrades.
    internal static class PocketRules
    {
        internal static readonly string[] Materials = { "wire", "nuts_metal", "module_bay_expansion_kit", "metal_ingot", "scrap_metal" };
        private static readonly int[] Sewn = { 2, 1, 0, 0, 0 }, Kit = { 0, 0, 1, 0, 0 }, Tools = { 0, 0, 0, 1, 2 };
        private static readonly Dictionary<string, int[][]> Recipes = new Dictionary<string, int[][]>
        {
            { "kids_backpack", new[] { Sewn } }, { "kids_backpack_large", new[] { Sewn } },
            { "backpack_small", new[] { Sewn } },
            { "backpack_medium", new[] { Sewn, Kit } }, { Military.Medium, new[] { Sewn, Kit } },
            { "backpack_large", new[] { Kit, Kit } }, { Military.Large, new[] { Kit, Kit } }
        };

        internal static int Maximum(string id) => id != null && Recipes.TryGetValue(Military.Canonical(id), out int[][] recipes) ? recipes.Length : 0;
        internal static bool Toolbox(string id) => id == "backpack_medium" || id == "backpack_large"
            || Military.IsMilitary(id);
        internal static int[] Recipe(string id, int level, bool toolbox)
        {
            id = Military.Canonical(id);
            if (toolbox) return Toolbox(id) && level == 0 ? (int[])Tools.Clone() : null;
            return level >= 0 && level < Maximum(id) ? (int[])Recipes[id][level].Clone() : null;
        }

        internal static int Choose(int earlier, bool pockets, bool toolbox) => earlier != 0 ? earlier : pockets ? 5 : toolbox ? 6 : 0;
        internal static int[] Drop(int[] recipe, int[] progress, string material)
        {
            if (recipe == null || progress == null || recipe.Length != Materials.Length || progress.Length != recipe.Length) return null;
            for (int i = 0; i < recipe.Length; i++) if (progress[i] < 0 || progress[i] > recipe[i]) return null;
            int index = Array.IndexOf(Materials, material);
            if (index < 0 || progress[index] >= recipe[index]) return null;
            int[] next = (int[])progress.Clone(); next[index]++;
            return next;
        }

        internal static bool Complete(int[] recipe, int[] progress)
        {
            if (recipe == null || progress == null || recipe.Length != progress.Length) return false;
            for (int i = 0; i < recipe.Length; i++) if (recipe[i] != progress[i]) return false;
            return true;
        }

        // Whether `target` is met walking upward from `start`. A walk that is cut short
        // by the limit answers yes: when in doubt the bag is refused.
        internal static bool Above<T>(T start, T target, Func<T, IEnumerable<T>> parents, int limit) where T : class
        {
            var seen = new HashSet<T>();
            var queue = new Queue<T>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                T node = queue.Dequeue();
                if (node == null || !seen.Add(node)) continue;
                if (node == target) return true;
                if (seen.Count >= limit) return true;
                IEnumerable<T> up = parents(node);
                if (up != null) foreach (T parent in up) queue.Enqueue(parent);
            }
            return false;
        }

        internal static bool PocketItem(string id, bool owned, bool self) => owned && !self
            && (id == "fanny_pack" || id == "satchel" || id == "kids_backpack" || id == "kids_backpack_large" || id == "backpack_small");
        // The Hauler's Pack alone also takes the game's Backpack (Basic), id backpack_medium,
        // as a side bag, to distinguish the two military packs.
        internal const string LargeSideBagHost = Military.Large, LargeSideBag = "backpack_medium";
        internal static bool PocketItem(string hostId, string id, bool owned, bool self) =>
            PocketItem(id, owned, self) || (owned && !self && Military.Canonical(hostId) == LargeSideBagHost && id == LargeSideBag);
        internal static bool ToolboxItem(bool tagged, bool owned, bool self) => tagged && owned && !self;

        internal static int NewLayout(int layout, int pockets, int toolbox) =>
            layout == 0 && pockets == 0 && toolbox == 0 ? 2 : layout;

        internal static bool CanMove(int layout, int pockets, int toolbox, bool empty) =>
            (layout == 0 || layout == 1) && (pockets > 0 || toolbox > 0) && empty;

        internal static bool SlotsEmpty(int expected, bool[] occupied)
        {
            if (occupied == null || occupied.Length != expected) return false;
            foreach (bool slot in occupied) if (slot) return false;
            return true;
        }

        // Layout 2: cell numbers 0 to 3 are the side bags and 4 is the toolbox slot. The
        // bags lie two wide in a grid of their own: 0 and 1 on its first row, 2 and 3 on
        // its second. The toolbox slot lies under that grid (see NestedGrid).
        // Saves of the old layout lose side-bag and toolbox contents on load unless migrated.
        internal const int AttachedColumns = 2, AttachedGridRows = 3;
        internal static int CellColumn(int cell) => cell % AttachedColumns;
        internal static int CellRow(int cell) => cell / AttachedColumns;

        internal static int SlotRow(string id, int pockets, int entry) => entry == -1 ? Math.Max(Maximum(id), pockets) : entry;

        internal static int AttachedRows(string id, int pockets, int layout) => layout == 2 ? 5
            : Math.Max(Maximum(id), pockets) + (Toolbox(id) ? 1 : 0);

        internal static int ExpectedSlots(string id, int pockets, int toolbox, int layout) => StorageRules.Clamp(pockets, 0, 4)
            + (layout == 1 || layout == 2 ? (Toolbox(id) ? 1 : 0) : toolbox > 0 ? 1 : 0);

        // The callbacks differ deliberately: append uses minimum sizes in layout 1,
        // whereas cell always names a cell in layout 2. Share this choice with tests.
        internal static bool PlaceAttached(string id, int pockets, int entry, int layout,
            Func<int, bool> append, Func<int, bool> cell) => layout == 2 ? cell(entry == -1 ? 4 : entry)
                : append(SlotRow(id, pockets, entry));

        // -1 denotes the toolbox, reserved before purchase in layouts 1 and 2.
        internal static int[] AttachOrder(string id, int pockets, int toolbox, int layout)
        {
            var order = new List<int>();
            if (layout == 1 && Toolbox(id)) order.Add(-1);
            for (int n = 0; n < StorageRules.Clamp(pockets, 0, 4); n++) order.Add(n);
            if ((layout == 0 && toolbox > 0) || (layout == 2 && Toolbox(id))) order.Add(-1);
            return order.ToArray();
        }

        internal static string Structure(int layout, int toolbox) => layout == 1 || layout == 2 ? "attached"
            : layout != 0 ? "unknown" : toolbox == 1 ? "legacy-wide" : toolbox >= 2 ? "legacy-narrow" : "legacy";

        // Keep panel mutations testable without constructing the game's windows.
        internal static Action ReplacePanels<T>(Func<IEnumerable<T>> panels, Func<T, string> role,
            Func<T, bool> detach, Action<T> add, T replacement)
        {
            var before = new List<T>(panels());
            bool SlotPanel(T panel) => role(panel) == Pockets.PocketRole || role(panel) == Pockets.ToolboxRole || role(panel) == Pockets.AttachedRole;
            before.RemoveAll(panel => !SlotPanel(panel));
            before.Sort((left, right) => string.CompareOrdinal(role(left), role(right)));
            Action rollback = () =>
            {
                if (new List<T>(panels()).Contains(replacement) && !detach(replacement))
                    throw new InvalidOperationException("Panel rollback detach failed");
                foreach (T panel in before)
                    if (!new List<T>(panels()).Contains(panel)) add(panel);
                var restored = new List<T>(panels());
                if (restored.Contains(replacement) || before.Exists(panel => !restored.Contains(panel)))
                    throw new InvalidOperationException("Panel rollback restore failed");
            };
            try
            {
                // Toolbox first leaves a prefix of the old order if a detach fails;
                // rollback can then append the missing panels without detaching storage.
                for (int i = before.Count - 1; i >= 0; i--)
                    if (!detach(before[i]))
                        throw new InvalidOperationException("Old panel detach failed");
                add(replacement);
                var after = new List<T>(panels());
                if (!after.Contains(replacement) || after.Exists(panel => SlotPanel(panel) && !EqualityComparer<T>.Default.Equals(panel, replacement)))
                    throw new InvalidOperationException("Attached panel replacement failed");
                return rollback;
            }
            catch
            {
                try { rollback(); }
                catch (Exception error) { Storage.Fault("putting backpack panels back", error); }
                throw;
            }
        }

        // The game search cannot stop at items when searching for grids. Breadth
        // first is also the game's save order; stop at both items and found grids.
        internal static List<T> Pages<T>(T root, Func<T, IEnumerable<T>> children, Func<T, bool> item, Func<T, bool> grid) where T : class
            => GridWalk.Pages(root, children, item, grid);

    }

    internal static class Pockets
    {
        internal const string PocketsTag = "MACHINE_UPGRADES_POCKETS_INT", ToolboxTag = "MACHINE_UPGRADES_TOOLBOX_INT";
        internal const string PocketRole = "mu_pockets", ToolboxRole = "mu_toolbox";
        internal const string LayoutTag = "MACHINE_UPGRADES_LAYOUT_INT", AttachedRole = "mu_attached";
        private static readonly ConditionalWeakTable<GameItem, GameSlotInventory> reservedToolboxes = new ConditionalWeakTable<GameItem, GameSlotInventory>();
        internal static readonly string[] PocketProgressTags = ProgressTags("POCKETS"), ToolboxProgressTags = ProgressTags("TOOLBOX");
        private static string[] ProgressTags(string kind)
        {
            var tags = new string[PocketRules.Materials.Length];
            for (int i = 0; i < tags.Length; i++) tags[i] = "MACHINE_UPGRADES_" + kind + "_" + PocketRules.Materials[i].ToUpperInvariant() + "_INT";
            return tags;
        }

        // Counts: row, slot panel, side bag slots, toolbox panel, toolbox slots.
        // Layouts 1 and 2's last two values describe the reserved slot in ATTACHED.
        // All window construction is behind replaceable boundaries for the harness.
        internal static Func<GameItem, int[]> Layout = ReadLayout;
        internal static Action<GameItem, int, int> AddAttachedPanel = (item, pockets, toolbox) => Row(item).Add(AttachedPanel(item, pockets, Storage.Read(item, LayoutTag)));
        internal static Action<GameItem, int> AddAttachedSlot = (item, index) =>
        {
            var grid = Inner(Row(item).GetPanel(AttachedRole)) as GridPixelElement;
            if (grid == null) throw new InvalidOperationException("Attached panel differs");
            GameSlotInventory slot = Slot(item, false);
            // Layout 1 deliberately keeps the three-argument Attach overload:
            // those numbers are minimum sizes; its first-free cells are saved.
            bool attached = PocketRules.PlaceAttached(item.identifier, Storage.Read(item, PocketsTag), index, Storage.Read(item, LayoutTag),
                row => grid.Attach(slot, 0, row), cell => PlaceCell(grid, slot, cell));
            if (!attached) throw new InvalidOperationException("Side bag slot was not attached");
        };
        internal static Action<GameItem> UpdateToolbox = item =>
        {
            if (reservedToolboxes.TryGetValue(item, out GameSlotInventory slot))
                slot.SetTooltipName(ToolboxName(Storage.Read(item, ToolboxTag)));
        };
        internal static Func<GameItem, bool> SlotsEmpty = EmptySlots;
        // Builds off-row, then replaces panels. The returned action restores the
        // exact original panel order if validation or writing the layout tag fails.
        internal static Func<GameItem, int, int, Action> MovePanels = ReplacePanels;
        internal static Action<GameItem> AddPocketPanel = item => Row(item).Add(MachineHelper.Panel(TextTable.Get(TextKeys.PanelAttached), new GridPixelElement(1, 4, false), PocketRole));
        internal static Action<GameItem, int> AddPocketSlot = (item, index) =>
        {
            var grid = Inner(Row(item).GetPanel(PocketRole)) as GridPixelElement;
            if (grid == null || !grid.Attach(Slot(item, false), 0, index)) throw new InvalidOperationException("Pocket slot was not attached");
        };
        internal static Action<GameItem> AddToolboxPanel = item => Row(item).Add(MachineHelper.Panel(TextTable.Get(TextKeys.PanelToolbox), Slot(item, true), ToolboxRole));
        // A slot standing alone in a panel makes the panel several times wider than the
        // slot; inside a grid it takes the slot's own
        // width, as the pocket slots do. The grid is one more inventory level, which
        // changes the numbers a save uses, so toolbox tag value 1 keeps the standalone
        // structure and value 2 uses this grid.
        internal static Action<GameItem> AddToolboxPanelNarrow = item =>
        {
            var grid = new GridPixelElement(1, 1, false);
            if (!grid.Attach(Slot(item, true), 0, 0)) throw new InvalidOperationException("Toolbox slot was not attached");
            Row(item).Add(MachineHelper.Panel(TextTable.Get(TextKeys.PanelToolbox), grid, ToolboxRole));
        };
        internal static Action<GameItem> Validate = item =>
        {
            MachineryPanelRow row = Row(item);
            // Show ATTACHED (or the legacy side bag panel) left of storage.
            // Display order never changes the held order that saves number.
            var order = new List<int>();
            for (int i = 0; i < row.Panels.Count; i++) if (row.Panels[i].role == PocketRole || row.Panels[i].role == AttachedRole) order.Add(i);
            for (int i = 0; i < row.Panels.Count; i++) if (row.Panels[i].role != PocketRole && row.Panels[i].role != ToolboxRole && row.Panels[i].role != AttachedRole) order.Add(i);
            for (int i = 0; i < row.Panels.Count; i++) if (row.Panels[i].role == ToolboxRole) order.Add(i);
            row.SetColumns(2);
            row.SetDisplayOrder(order.ToArray());
            row.Validate(); item.contentWindow.Validate();
        };
        internal static Func<GameItem, bool> IsToolbox = item => item.IsTag("TOOLBOX_TAG");
        // Whether `inner` lies somewhere inside `outer`. A bag that already holds this
        // backpack must not go into the backpack's own slot: the two would hold each
        // other and drop out of everything else.
        internal static Func<GameItem, GameItem, bool> Inside = (inner, outer) =>
            PocketRules.Above<GraphNodeStorage>(inner, outer, node => node.parents, 2000);

        // The game's panel helper wraps an inventory or a grid of them in an intake
        // frame, so a panel's content is that frame and ours lies inside it.
        private static PixelElement Inner(MachineryPanel panel) =>
            panel == null ? null : panel.content is MachineryIntake intake ? intake.content : panel.content;

        private static MachineryPanelRow Row(GameItem item) => item.contentWindow?.child as MachineryPanelRow
            ?? throw new InvalidOperationException("Missing backpack panel row");

        private static int SlotCount(GraphNodeStorage root)
        {
            var nodes = PocketRules.Pages(root, node => node.children, node => node is GameItem, node => node is GameSlotInventory);
            return nodes.Count;
        }

        private static int[] ReadLayout(GameItem item)
        {
            if (!(item.contentWindow?.child is MachineryPanelRow row)) return new int[5];
            MachineryPanel attached = row.GetPanel(AttachedRole);
            if (attached != null)
            {
                if (!(Inner(attached) is GridPixelElement grid)) throw new InvalidOperationException("Attached panel differs");
                int reserved = PocketRules.Toolbox(item.identifier) ? 1 : 0;
                return new[] { 1, 1, SlotCount(grid) - reserved, reserved, reserved };
            }
            MachineryPanel pocket = row.GetPanel(PocketRole), toolbox = row.GetPanel(ToolboxRole);
            if (pocket != null && !(Inner(pocket) is GridPixelElement)) throw new InvalidOperationException("Pocket panel differs");
            if (toolbox != null && !(Inner(toolbox) is GameSlotInventory) && !(Inner(toolbox) is GridPixelElement)) throw new InvalidOperationException("Toolbox panel differs");
            return new[] { 1, pocket == null ? 0 : 1, pocket == null ? 0 : SlotCount((GridPixelElement)Inner(pocket)),
                toolbox == null ? 0 : 1, toolbox == null ? 0 : 1 };
        }

        internal static bool Accept(GameItem candidate, GameItem backpack, bool toolbox)
        {
            try
            {
                if (candidate == null || Inside(backpack, candidate)) return false;
                return toolbox ? PocketRules.ToolboxItem(IsToolbox(candidate), Storage.Owned(candidate), candidate == backpack)
                    : PocketRules.PocketItem(backpack?.identifier, candidate.identifier, Storage.Owned(candidate), candidate == backpack);
            }
            catch (Exception error) { Storage.Fault("checking backpack slot item", error); return false; }
        }

        // Our side bag slots, for the patch that lets a bag keep the way it was held.
        internal static readonly ConditionalWeakTable<GameSlotInventory, object> SideBagSlots = new ConditionalWeakTable<GameSlotInventory, object>();

        private static GameSlotInventory Slot(GameItem backpack, bool toolbox)
        {
            var slot = new GameSlotInventory(2, 2);
            // The game gives the machinery look to the slots a panel holds when the panel is
            // made. Give later slots the same look when they are created.
            Storage.Guard("styling a backpack slot", () => slot.SetMachineryStyle());
            slot.SetTooltipName(SlotName(toolbox));
            if (!toolbox) slot.SetBackgroundFadeSprite("Items/items_backpack2", "fanny_pack");
            if (!toolbox) Storage.Guard("marking a side bag slot", () => SideBagSlots.Add(slot, new object()));
            // A closed-container drop can land here before targeting: an eligible
            // small bag still becomes a Compartment. Open the backpack to pocket it.
            slot.mayInventoryAddItemFunc = (candidate, inventory) => Accept(candidate, backpack, toolbox);
            return slot;
        }

        internal static string ToolboxName(int bought) => bought > 0 ? TextTable.Get(TextKeys.SlotToolbox) : TextTable.Get(TextKeys.SlotToolboxUnbuilt);
        internal static string SlotName(bool toolbox) => toolbox ? TextTable.Get(TextKeys.SlotToolbox) : TextTable.Get(TextKeys.SlotSideBag);

        internal static bool AcceptAttachedToolbox(GameItem candidate, GameItem backpack)
        {
            try { return Storage.Read(backpack, ToolboxTag) > 0 && Accept(candidate, backpack, true); }
            catch (Exception error) { Storage.Fault("checking reserved toolbox slot", error); return false; }
        }

        // Layout 2: the side bags lie in a grid of their own, two wide, and
        // that grid and the toolbox slot lie one under the other in a centred column, so
        // that the toolbox slot is centred under the bags. The nesting determines
        // saved inventory numbers. Changing it requires a distinct layout number
        // and a migration, as for layouts 0 and 1.
        private static GridPixelElement NestedGrid()
        {
            var outer = new GridPixelElement(1, 2, true);
            if (!outer.AttachPos(new GridPixelElement(PocketRules.AttachedColumns, 2, true), 0, 0)) throw new InvalidOperationException("Side bag grid was not attached");
            return outer;
        }

        private static bool PlaceCell(GridPixelElement outer, GameSlotInventory slot, int cell)
        {
            if (cell == 4) return outer.AttachPos(slot, 0, 1);
            return outer.GetElement(0, 0) is GridPixelElement bags && bags.AttachPos(slot, PocketRules.CellColumn(cell), PocketRules.CellRow(cell));
        }

        private static MachineryPanel AttachedPanel(GameItem item, int pockets, int layout)
        {
            // Null cells have zero row height and no row padding in the game's
            // grid. Leave them null, with no minimum size or placeholder element.
            // Layout 2 is two wide to keep the window short; see NestedGrid.
            var grid = layout == 2 ? NestedGrid()
                : new GridPixelElement(1, PocketRules.AttachedRows(item.identifier, pockets, layout), false);
            GameSlotInventory reserved = null;
            foreach (int entry in PocketRules.AttachOrder(item.identifier, pockets, 0, layout))
            {
                bool toolbox = entry == -1;
                GameSlotInventory slot = Slot(item, toolbox);
                if (toolbox)
                {
                    reserved = slot;
                    slot.mayInventoryAddItemFunc = (candidate, inventory) => AcceptAttachedToolbox(candidate, item);
                }
                // Keep the first-free overload for layout 1; changing it would
                // renumber inventories in saved games.
                bool attached = PocketRules.PlaceAttached(item.identifier, pockets, entry, layout,
                    row => grid.Attach(slot, 0, row), cell => PlaceCell(grid, slot, cell));
                if (!attached) throw new InvalidOperationException("Attached slot was not attached");
            }
            MachineryPanel panel = MachineHelper.Panel(TextTable.Get(TextKeys.PanelAttached), grid, AttachedRole);
            reservedToolboxes.Remove(item);
            if (reserved != null) reservedToolboxes.Add(item, reserved);
            return panel;
        }

        private static List<GraphNodeStorage> Slots(GraphNodeStorage root) =>
            PocketRules.Pages(root, node => node.children, node => node is GameItem, node => node is GameSlotInventory);

        private static bool EmptySlots(GameItem item)
        {
            MachineryPanelRow row = Row(item);
            int layout = Storage.Read(item, LayoutTag);
            int expected = PocketRules.ExpectedSlots(item.identifier, Storage.Read(item, PocketsTag), Storage.Read(item, ToolboxTag), layout);
            var occupied = new List<bool>();
            foreach (string role in new[] { PocketRole, ToolboxRole, AttachedRole })
            {
                foreach (GraphNodeStorage node in Slots(Inner(row.GetPanel(role)) as GraphNodeStorage))
                {
                    occupied.Add(((GameSlotInventory)node).childItem != null);
                }
            }
            // A missing slot is a damaged rebuild, not an empty backpack.
            return PocketRules.SlotsEmpty(expected, occupied.ToArray());
        }

        private static Action ReplacePanels(GameItem item, int pockets, int toolbox)
        {
            reservedToolboxes.TryGetValue(item, out GameSlotInventory previous);
            void RestoreReference()
            {
                reservedToolboxes.Remove(item);
                if (previous != null) reservedToolboxes.Add(item, previous);
            }
            MachineryPanel replacement = AttachedPanel(item, pockets, 2);
            MachineryPanelRow row = Row(item);
            try
            {
                Action rollback = PocketRules.ReplacePanels(() => row.Panels, panel => panel.role, panel => row.Detach(panel), panel => row.Add(panel), replacement);
                return () => { rollback(); RestoreReference(); };
            }
            catch
            {
                if (!new List<MachineryPanel>(row.Panels).Contains(replacement)) RestoreReference();
                try { Validate(item); }
                catch (Exception error) { Storage.Fault("validating restored backpack panels", error); }
                throw;
            }
        }

        internal static bool TryMove(GameItem item)
        {
            Action rollback = null;
            int oldLayout = 0;
            try
            {
                oldLayout = Storage.Read(item, LayoutTag);
                int pockets = StorageRules.Clamp(Storage.Read(item, PocketsTag), 0, 4);
                int toolbox = StorageRules.Clamp(Storage.Read(item, ToolboxTag), 0, 2);
                if ((oldLayout != 0 && oldLayout != 1) || (pockets == 0 && toolbox == 0)) return false;
                // Do not discard an old slot that today's table cannot represent.
                if (toolbox > 0 && !PocketRules.Toolbox(item.identifier)) return false;
                if (!PocketRules.CanMove(oldLayout, pockets, toolbox, SlotsEmpty(item))) return false;
                rollback = MovePanels(item, pockets, toolbox);
                Validate(item);
                Storage.Write(item, LayoutTag, 2);
                Storage.Guard("naming reserved toolbox slot", () => UpdateToolbox(item));
                return true;
            }
            catch (Exception error)
            {
                if (rollback != null)
                {
                    bool restored = false;
                    try
                    {
                        rollback();
                        restored = true;
                    }
                    catch (Exception restoreError) { Storage.Fault("putting backpack panels back", restoreError); }
                    if (restored)
                    {
                        try { Storage.Write(item, LayoutTag, oldLayout); }
                        catch (Exception tagError) { Storage.Fault("putting backpack layout tag back", tagError); }
                    }
                    try { Validate(item); }
                    catch (Exception validateError) { Storage.Fault("validating restored backpack panels", validateError); }
                }
                Storage.Fault("moving backpack attached slots", error);
                return false;
            }
        }

        internal sealed class Plan
        {
            internal string[] Keys;
            internal int[] Values;
            internal int Needed;
            internal bool Open, Complete;
        }

        internal static Plan Prepare(GameItem source, GameItem target, bool toolbox)
        {
            if (!Backpacks.Switch(toolbox ? "backpack_toolbox_enabled" : "backpack_pockets_enabled")) return null;
            string tag = toolbox ? ToolboxTag : PocketsTag;
            int count = Math.Max(0, Storage.Read(target, tag));
            int[] recipe = PocketRules.Recipe(target.identifier, count, toolbox);
            int ingredient = Array.IndexOf(PocketRules.Materials, source.identifier);
            if (recipe == null || ingredient < 0 || recipe[ingredient] == 0) return null;
            string[] progressTags = toolbox ? ToolboxProgressTags : PocketProgressTags;
            int[] progress = Progress(target, progressTags);
            // A saved progress outside the recipe (an edited save, a changed table) is
            // read as the nearest sound value.
            for (int i = 0; i < progress.Length; i++) progress[i] = StorageRules.Clamp(progress[i], 0, recipe[i]);
            int[] next = PocketRules.Drop(recipe, progress, source.identifier);
            if (next == null) return null;
            bool complete = PocketRules.Complete(recipe, next);
            bool first = complete && Storage.Read(target, PocketsTag) == 0 && Storage.Read(target, ToolboxTag) == 0;
            var keys = new string[progressTags.Length + 1 + (first ? 1 : 0)];
            var values = new int[keys.Length];
            keys[0] = tag; values[0] = complete ? (toolbox ? 2 : count + 1) : count;
            for (int i = 0; i < next.Length; i++) { keys[i + 1] = progressTags[i]; values[i + 1] = complete ? 0 : next[i]; }
            if (first) { keys[keys.Length - 1] = LayoutTag; values[keys.Length - 1] = PocketRules.NewLayout(Storage.Read(target, LayoutTag), 0, 0); }
            return new Plan { Complete = complete, Needed = recipe[ingredient] - progress[ingredient],
                Open = !complete && PocketRules.Drop(recipe, next, source.identifier) != null, Keys = keys, Values = values };
        }

        private static int[] Progress(GameItem item, string[] keys)
        {
            var values = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++) values[i] = Storage.Read(item, keys[i]);
            return values;
        }

        internal static void Ensure(GameItem item, int pockets, int toolbox)
        {
            pockets = StorageRules.Clamp(pockets, 0, 4); toolbox = StorageRules.Clamp(toolbox, 0, 2);
            string structure = PocketRules.Structure(Storage.Read(item, LayoutTag), toolbox);
            if (structure == "unknown")
            {
                Storage.Fault("unknown backpack slot layout", new InvalidOperationException("Unknown backpack slot layout"));
                return;
            }
            if (pockets == 0 && toolbox == 0) return;
            int[] layout = Layout(item);
            int[] before = (int[])layout.Clone();
            if (layout[0] == 0)
            {
                if (!Backpacks.TabsAvailable()) throw new InvalidOperationException("Storage tab method differs");
                Backpacks.AddTabs(item, 0);
                if (Layout(item)[0] == 0) throw new InvalidOperationException("Backpack did not become a panel row");
            }
            if (structure == "attached")
            {
                if (Layout(item)[1] == 0) AddAttachedPanel(item, pockets, toolbox);
                for (int n = Layout(item)[2]; n < pockets; n++)
                {
                    AddAttachedSlot(item, n);
                    if (Layout(item)[2] <= n) throw new InvalidOperationException("Side bag slots did not grow");
                }
                if (Layout(item)[2] < pockets || (PocketRules.Toolbox(item.identifier) && Layout(item)[4] == 0))
                    throw new InvalidOperationException("Attached slots did not grow");
                bool changed = false;
                layout = Layout(item);
                for (int i = 0; i < layout.Length; i++) changed |= layout[i] > before[i];
                if (changed) Validate(item);
                Storage.Guard("naming reserved toolbox slot", () => UpdateToolbox(item));
                return;
            }
            if (Layout(item)[1] == 0)
            {
                // Reserve this position even for toolbox first. Appending pockets
                // after the toolbox would give different saved inventory numbers.
                if (Layout(item)[3] != 0) throw new InvalidOperationException("Backpack panel order differs");
                AddPocketPanel(item);
            }
            for (int n = Layout(item)[2]; n < pockets; n++)
            {
                AddPocketSlot(item, n);
                if (Layout(item)[2] <= n) throw new InvalidOperationException("Pocket slots did not grow");
            }
            if (toolbox > 0 && Layout(item)[4] == 0) { if (structure == "legacy-narrow") AddToolboxPanelNarrow(item); else AddToolboxPanel(item); }
            layout = Layout(item);
            if (layout[2] < pockets || layout[4] < Math.Min(1, toolbox)) throw new InvalidOperationException("Backpack slots did not grow");
            for (int i = 0; i < layout.Length; i++)
                if (layout[i] > before[i]) { Validate(item); break; }
        }

        internal static bool Changed(GameItem item, int[] before)
        {
            try
            {
                int[] after = Layout(item);
                for (int i = 0; i < after.Length; i++) if (after[i] > before[i]) return true;
            }
            catch (Exception error) { Storage.Fault("checking backpack panels after a fault", error); }
            return false;
        }

        internal static void Restore(GameItem item) => Ensure(item, Storage.Read(item, PocketsTag), Storage.Read(item, ToolboxTag));

        private static string Name(string id) => UpgradeViewRules.Name(id);
        internal static string Tooltip(string id, int count, int[] progress, bool toolbox)
        {
            int[] recipe = PocketRules.Recipe(id, count, toolbox);
            var needs = new List<string>();
            if (recipe != null)
                for (int i = 0; i < recipe.Length; i++) if (recipe[i] > 0)
                    needs.Add(TextTable.Get(TextKeys.MaterialProgress, Name(PocketRules.Materials[i]), progress[i], recipe[i]));
            string detail = recipe == null ? TextTable.Get(TextKeys.TooltipLimit) : TextTable.Get(TextKeys.TooltipNext, string.Join(", ", needs));
            return toolbox ? TextTable.Get(count > 0 ? TextKeys.TooltipToolboxYes : TextKeys.TooltipToolboxNo, detail)
                : TextTable.Get(TextKeys.TooltipSideBags, count, PocketRules.Maximum(id), detail);
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (item == null || !Backpacks.Supported(item.identifier)) return;
                foreach (bool toolbox in new[] { false, true })
                {
                    if (toolbox ? !PocketRules.Toolbox(item.identifier) : PocketRules.Maximum(item.identifier) == 0) continue;
                    string key = toolbox ? "backpack_toolbox_enabled" : "backpack_pockets_enabled";
                    Storage.Line(builder, Backpacks.Disabled(key, Tooltip(item.identifier, StorageRules.Clamp(Storage.Read(item, toolbox ? ToolboxTag : PocketsTag), 0, toolbox ? 1 : 4),
                        Progress(item, toolbox ? ToolboxProgressTags : PocketProgressTags), toolbox)));
                }
            }
            catch (Exception error) { Storage.Fault("backpack slots tooltip", error); }
        }
    }

    // A slot of the game stands every item upright: when it offers a place for a dragged
    // item it turns the item's shape to the slot's own fixed orientation. Side bag
    // slots keep the held orientation so bags can lie down and the panel stays lower.
    // For our side bag slots only, the place the game
    // offers is given the orientation the dragged bag has. Nothing else about the place
    // is changed, and the game's own checks have already passed for it.
    [HarmonyPatch(typeof(GameSlotInventory), nameof(GameSlotInventory.TryInventorySlot),
        new[] { typeof(GameItem), typeof(int), typeof(UnityEngine.Vector2), typeof(GridShape), typeof(TagSystem) })]
    internal static class SideBagOrientationPatch
    {
        internal static Func<GameSlotInventory, bool> Ours = slot => slot != null && Pockets.SideBagSlots.TryGetValue(slot, out _);
        internal static Func<bool> On = () => Backpacks.Switch("backpack_side_bags_keep_orientation");

        // Whether the offered place is turned: our slot, a real offer, and the bag held
        // another way than the slot would stand it.
        internal static bool Turn(bool on, bool ours, int transfer, int held, int slot) => on && ours && transfer >= 1 && ((held % 4) + 4) % 4 != ((slot % 4) + 4) % 4;

        private static void Postfix(GameSlotInventory __instance, GameItem item, GridShape tempShape, SlotMarker __result)
        {
            try
            {
                if (__result == null || item == null || !Ours(__instance) || !On()) return;
                GridShape shape = tempShape ?? item.modifiedShape;
                if (shape == null || __result.itemGridShape == null) return;
                var held = new GridShapeBuilder(shape);
                if (!Turn(true, true, __result.numTransfer, held.orientation, __instance.defaultOrientation)) return;
                __result.itemGridShape = held.SetTransform(0, 0, false, held.orientation);
            }
            catch (Exception error) { Storage.Fault("keeping a side bag's orientation", error); }
        }
    }
}

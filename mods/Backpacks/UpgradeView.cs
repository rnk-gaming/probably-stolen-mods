using System;
using System.Collections.Generic;
using UnityEngine;

using LocalMods;
using Material = LocalMods.Material;

namespace Backpacks
{
    // Plain values in and out. Track numbers match Backpacks' apply plans.
    internal static class UpgradeViewRules
    {
        internal sealed class Block
        {
            internal int Kind, Step;
            internal string Heading, Next, After, Contents, Note;
            internal bool Button;
            internal readonly Dictionary<string, int> Needs = new Dictionary<string, int>();
        }

        internal const string Help = "Drop a material on the closed backpack, or use what lies in it. A dropped material goes to the first upgrade in this list that still needs it.";

        internal static string Name(string id)
        {
            id = Military.Canonical(id);
            if (id == Military.Medium) return Texts.Get("scavengers_pack_name");
            if (id == Military.Large) return Texts.Get("haulers_pack_name");
            string fallback = id == "kotton_fabric" ? "Kotton Fabric" : id == "printer_plastic" ? "3D Printing Filament"
            : id == "wire" ? "Wire" : id == "nuts_metal" ? "Metal Screws" : id == "metal_ingot" ? "Metal Ingot"
            : id == "scrap_metal" ? "Scrap Metal" : id == "fanny_pack" ? "Fanny Pack" : id == "satchel" ? "Satchel"
            : id == "backpack_small" ? "Small Backpack" : id == "backpack_medium" ? "Medium Backpack"
            : id == "backpack_large" ? "Large Backpack" : id == "kids_backpack" ? "Kids' Backpack"
            : id == "storage_bay_expansion_kit" ? "Storage Bay Expansion Kit" : "Module Bay Expansion Kit";
            return Backpacks.Supported(id) && !Military.IsMilitary(id) ? Texts.ItemName(id, fallback) : fallback;
        }

        internal static List<Block> Build(string id, Dictionary<string, int> tags, Dictionary<string, int> contents,
            Dictionary<string, bool> switches, int[] current)
        {
            var blocks = new List<Block>();
            id = Military.Canonical(id);
            if (id == null || !BackpackRules.Bags.TryGetValue(id, out int[] bag)) return blocks;
            int Tag(string key) => tags != null && tags.TryGetValue(key, out int value) ? Math.Max(0, value) : 0;
            bool On(string key) => switches == null || !switches.TryGetValue(key, out bool value) || value;
            bool enabled = On("backpacks_enabled") && On("backpack_view_enabled")
                && (!Military.IsMilitary(id) || On("own_backpacks_enabled"));

            void Add(int kind, int step, string heading, string key, string[] materials, int[] recipe, int[] paid, List<string> later, bool donor = false)
            {
                var block = new Block { Kind = kind, Step = step, Heading = heading, Next = "Limit reached" };
                bool active = enabled && On(key);
                if (!active) block.Note = "New upgrades disabled";
                if (recipe != null)
                {
                    var progress = new List<string>(); var held = new List<string>();
                    for (int i = 0; i < recipe.Length; i++)
                    {
                        if (recipe[i] <= 0) continue;
                        int p = StorageRules.Clamp(paid[i], 0, recipe[i]);
                        progress.Add(Name(materials[i]) + (donor ? "" : " " + p + "/" + recipe[i]));
                        int need = recipe[i] - p;
                        if (need <= 0) continue;
                        block.Needs[materials[i]] = need;
                        int count = contents != null && contents.TryGetValue(materials[i], out int n) ? Math.Max(0, n) : 0;
                        if (count > 0) held.Add(Name(materials[i]) + " " + count);
                    }
                    block.Next = "Next: " + string.Join(", ", progress);
                    if (donor) block.Note = "Drop the bag on the closed backpack" + (active ? "" : ". New upgrades disabled");
                    else
                    {
                        if (held.Count > 0) block.Contents = "In this backpack: " + string.Join(", ", held);
                        block.Button = kind != 2 && active && held.Count > 0;
                        if (kind != 2 && held.Count == 0)
                            block.Note = "Nothing in this backpack to use: put the material in it, or drop it on the closed backpack."
                                + (active ? "" : " New upgrades disabled");
                    }
                    if (kind == 2 && !donor) block.Note = "Drop the kit on the closed backpack" + (active ? "" : ". New upgrades disabled");
                }
                if (later.Count > 0) block.After = "After that: " + string.Join(", then ", later);
                blocks.Add(block);
            }

            void Dimension(bool row)
            {
                int basis = row ? bag[1] : bag[0], maximum = row ? bag[2] : BackpackRules.Widths[id];
                int extra = Tag(row ? Storage.RowsTag : Storage.ColumnsTag);
                int size = Math.Max(basis + StorageRules.Clamp(extra, 0, StorageRules.Sane - basis),
                    current != null && current.Length >= 2 ? current[row ? 1 : 0] : basis);
                int step = row ? size - basis + 1 : StorageRules.Clamp(extra, 0, StorageRules.Sane) + 1;
                (string Material, int Units) Cost(int n) => row ? ("kotton_fabric", n) : BackpackRules.ColumnCost(id, n);
                var cost = Cost(step);
                var later = new List<string>();
                if (size < maximum)
                    for (int n = step + 1; n <= maximum - basis; n++)
                    {
                        var after = Cost(n);
                        later.Add(n == step + 1 ? after.Units + " " + Name(after.Material)
                            : after.Material != Cost(n - 1).Material ? Name(after.Material) + " " + after.Units : after.Units.ToString());
                    }
                Add(row ? 1 : 4, size, (row ? "Rows: " : "Columns: ") + size + " of " + Math.Max(size, maximum),
                    row ? "backpack_rows_enabled" : "backpack_width_enabled", new[] { cost.Material },
                    size >= maximum ? null : new[] { cost.Units },
                    new[] { StorageRules.Clamp(Tag(row ? Backpacks.RowProgressTag : Backpacks.ColumnProgressTag), 0, Math.Max(0, cost.Units - 1)) }, later);
            }

            Dimension(true); Dimension(false);
            int tabs = Math.Max(Tag(Backpacks.TabsTag), current != null && current.Length > 2 ? current[2] - 1 : 0);
            int maxTabs = BackpackRules.MaxTabs(id);
            if (maxTabs > 0)
            {
                string next = BackpackRules.NextTab(id, tabs);
                var later = new List<string>();
                for (int n = 0; n < maxTabs; n++) if (n > tabs) later.Add(Name(BackpackRules.NextTab(id, n)));
                Add(2, tabs, "Compartments: " + ((long)tabs + 1) + " of " + (maxTabs + 1), "backpack_tabs_enabled",
                    new[] { next }, next == null ? null : new[] { 1 }, new[] { 0 }, later, Backpacks.Supported(next));
            }

            string Cost(string[] materials, int[] recipe)
            {
                var words = new List<string>();
                for (int i = 0; i < recipe.Length; i++)
                    if (recipe[i] > 0) words.Add((recipe[i] == 1 ? "" : recipe[i] + " ") + Name(materials[i]));
                return string.Join(", ", words);
            }

            int level = StorageRules.Clamp(Tag(Backpacks.StackTag), 0, 4);
            var stackLater = new List<string>();
            for (int n = level + 1; n < 4; n++) stackLater.Add(Cost(BackpackRules.Materials, BackpackRules.Ingredients(n)));
            var stackPaid = new int[Backpacks.ProgressTags.Length];
            for (int i = 0; i < stackPaid.Length; i++) stackPaid[i] = Tag(Backpacks.ProgressTags[i]);
            Add(3, level, "Stack size: x" + (level + 1) + " of x5", "backpack_stacking_enabled",
                BackpackRules.Materials, BackpackRules.Ingredients(level), stackPaid, stackLater);

            void Slots(bool toolbox)
            {
                int maximum = toolbox ? (PocketRules.Toolbox(id) ? 1 : 0) : PocketRules.Maximum(id);
                if (maximum == 0) return;
                int count = Tag(toolbox ? Pockets.ToolboxTag : Pockets.PocketsTag);
                string[] keys = toolbox ? Pockets.ToolboxProgressTags : Pockets.PocketProgressTags;
                var paid = new int[keys.Length];
                for (int i = 0; i < paid.Length; i++) paid[i] = Tag(keys[i]);
                var later = new List<string>();
                for (int n = 0; n < maximum; n++) if (n > count) later.Add(Cost(PocketRules.Materials, PocketRules.Recipe(id, n, toolbox)));
                Add(toolbox ? 6 : 5, count, toolbox ? "Toolbox slot: " + (count > 0 ? "yes" : "no") : "Side bag slots: " + count + " of " + maximum,
                    toolbox ? "backpack_toolbox_enabled" : "backpack_pockets_enabled", PocketRules.Materials,
                    PocketRules.Recipe(id, count, toolbox), paid, later);
            }
            Slots(false); Slots(true);
            return blocks;
        }
    }

    internal static class UpgradeView
    {
        internal static Action<GameItem> EnableConfigure = item => item.EnableTag("CONFIGURE_TAG");
        internal static Func<GameItem, string> WindowId = item => "machine_upgrades_backpack_" + item.uniqueId;
        internal static Action<string, string, List<UpgradeViewRules.Block>, Action<int>, Action> Render = BuildWindow;
        internal static Action<string> Close = id => CustomUIManager.Instance.CloseWindow(id);

        private sealed class View
        {
            internal GameItem Item;
            internal string Id;
            internal int Revision;
        }
        private static readonly Dictionary<GameItem, View> open = new Dictionary<GameItem, View>();
        private static readonly Action<GameItem, GameInventory, SlotMarker> configure = ConfigureItem;

        internal static void Attach(GameItem item, string id)
        {
            Storage.Guard("preparing backpack upgrades view", () =>
            {
                if (item == null || !Backpacks.Supported(id) || !Backpacks.Switch("backpack_view_enabled")) return;
                if (item.onConfigureSlotItemFunc != null)
                    foreach (Delegate handler in item.onConfigureSlotItemFunc.GetInvocationList()) if (handler.Equals(configure)) return;
                EnableConfigure(item);
                item.onConfigureSlotItemFunc += configure;
            });
        }

        private static void ConfigureItem(GameItem item, GameInventory inventory, SlotMarker slot) => Open(item);
        private static bool Usable(GameItem item) => item != null && !Material.Spent(item) && Drops.Alive(item) && Storage.Owned(item);
        private static bool Allowed(GameItem item) => BackpacksMod.Running && Usable(item) && Backpacks.Supported(item.identifier)
            && Backpacks.Switch("backpack_view_enabled") && !Storage.InCombat();

        internal static void Open(GameItem item)
        {
            Storage.Guard("opening backpack upgrades view", () =>
            {
                if (!Allowed(item)) { Dismiss(item); return; }
                if (!open.TryGetValue(item, out View view))
                {
                    view = new View { Item = item, Id = WindowId(item) };
                    open.Add(item, view);
                }
                Refresh(view);
            });
        }

        private static void Dismiss(GameItem item)
        {
            if (item == null || !open.TryGetValue(item, out View view)) return;
            open.Remove(item);
            Close(view.Id);
        }

        internal static void Disable()
        {
            var views = new List<View>(open.Values);
            open.Clear();
            foreach (View view in views)
                Storage.Guard("closing stopped backpack upgrades view", () => Close(view.Id));
        }

        internal static List<UpgradeViewRules.Block> Snapshot(GameItem item)
        {
            var tags = new Dictionary<string, int>();
            foreach (string key in new[] { Storage.RowsTag, Storage.ColumnsTag, Backpacks.RowProgressTag, Backpacks.ColumnProgressTag,
                Backpacks.TabsTag, Backpacks.StackTag, Pockets.PocketsTag, Pockets.ToolboxTag }) tags[key] = Storage.Read(item, key);
            foreach (string[] keys in new[] { Backpacks.ProgressTags, Pockets.PocketProgressTags, Pockets.ToolboxProgressTags })
                foreach (string key in keys) tags[key] = Storage.Read(item, key);
            var counts = new Dictionary<string, int>();
            var seen = new HashSet<GameItem>();
            foreach (GameItem payer in Drops.Contents(item))
            {
                if (!Usable(payer) || !seen.Add(payer) || payer.identifier == null) continue;
                int n = Math.Max(0, Material.Units(payer));
                counts.TryGetValue(payer.identifier, out int previous);
                counts[payer.identifier] = (int)Math.Min(int.MaxValue, (long)previous + n);
            }
            var switches = new Dictionary<string, bool>();
            foreach (string key in new[] { "backpacks_enabled", "backpack_view_enabled", "backpack_rows_enabled", "backpack_width_enabled",
                "backpack_tabs_enabled", "backpack_stacking_enabled", "backpack_pockets_enabled", "backpack_toolbox_enabled" }) switches[key] = Backpacks.Switch(key);
            switches["own_backpacks_enabled"] = !Military.IsMilitary(item.identifier) || Military.Enabled();
            int width = 0, height = 0;
            int[][] pages = Storage.Sizes(item);
            foreach (int[] page in pages) { width = Math.Max(width, page[0]); height = Math.Max(height, page[1]); }
            return UpgradeViewRules.Build(item.identifier, tags, counts, switches, new[] { width, height, pages.Length });
        }

        private static UpgradeViewRules.Block Find(GameItem item, int kind)
        {
            foreach (UpgradeViewRules.Block block in Snapshot(item)) if (block.Kind == kind) return block;
            return null;
        }

        private static void Refresh(View view)
        {
            int revision = ++view.Revision;
            Render(view.Id, view.Item.name + ": Upgrades", Snapshot(view.Item),
                kind => Storage.Guard("using backpack upgrade contents", () =>
                {
                    if (open.TryGetValue(view.Item, out View active) && active == view && view.Revision == revision) Use(view, kind);
                }),
                () => Storage.Guard("closing backpack upgrades view", () =>
                {
                    if (open.TryGetValue(view.Item, out View active) && active == view && view.Revision == revision) open.Remove(view.Item);
                }));
        }

        private static void Use(View view, int kind)
        {
            GameItem item = view.Item;
            if (!Allowed(item)) { Dismiss(item); return; }
            UpgradeViewRules.Block initial = Find(item, kind);
            if (initial == null || !initial.Button) { Refresh(view); return; }
            int cap = 0;
            foreach (int need in initial.Needs.Values) cap += need;
            // Every payment counts, even when Tinkerer keeps it. Re-read after each
            // payment, stop at the initial step, and never borrow a donor bag.
            for (int n = 0; n < cap && Allowed(item); n++)
            {
                UpgradeViewRules.Block block = Find(item, kind);
                if (block == null || !block.Button || block.Step != initial.Step) break;
                GameItem source = null;
                foreach (GameItem payer in Drops.Contents(item))
                    if (Usable(payer) && payer.identifier != null && Material.Units(payer) > 0 && block.Needs.ContainsKey(payer.identifier)) { source = payer; break; }
                if (source == null || !Backpacks.ApplyFor(source, item, kind)) break;
            }
            if (Allowed(item)) Refresh(view); else Dismiss(item);
        }

        // All CustomUIManager access lives behind Render/Close, replaced offline.
        private static void BuildWindow(string id, string title, List<UpgradeViewRules.Block> blocks, Action<int> use, Action closed)
        {
            var manager = CustomUIManager.Instance;
            var previous = manager.GetWindow(id);
            Vector2? position = previous != null && previous.IsAlive ? previous.Rect.anchoredPosition : (Vector2?)null;
            var builder = manager.CreateWindow(id, title, "rear").SetSize(560, 680).SetDraggable(true).SetCloseOnEscape()
                .UseMachineryStyle().OnClose(closed);
            if (position.HasValue) builder.SetPosition(position.Value); else builder.Center();
            builder.BeginScroll(570);
            foreach (UpgradeViewRules.Block block in blocks)
            {
                builder.AddHeader(block.Heading).AddLabel(block.Next);
                if (block.After != null) builder.AddLabel(block.After);
                if (block.Contents != null) builder.AddLabel(block.Contents);
                if (block.Note != null) builder.AddLabel(block.Note);
                if (block.Button) { int kind = block.Kind; builder.AddButton("Use from backpack", () => use(kind)); }
                builder.AddSeparator();
            }
            builder.AddLabel(UpgradeViewRules.Help).End();
            builder.Show();
        }
    }
}

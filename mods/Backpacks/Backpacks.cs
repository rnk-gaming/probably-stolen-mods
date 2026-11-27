using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

using LocalMods;

namespace Backpacks
{
    // Values only. Page counts are extra pages; row levels are extra rows.
    internal static class BackpackRules
    {
        internal static readonly Dictionary<string, int[]> Bags = new Dictionary<string, int[]>
        {
            { "fanny_pack", new[] { 3, 3, 5 } },
            { "satchel", new[] { 6, 3, 5 } },
            { "kids_backpack", new[] { 5, 5, 7 } },
            { "kids_backpack_large", new[] { 5, 6, 8 } },
            { "backpack_small", new[] { 6, 6, 9 } },
            { "backpack_medium", new[] { 6, 9, 9 } },
            { "backpack_large", new[] { 8, 9, 9 } },
            { Military.Medium, new[] { 6, 9, 9 } },
            { Military.Large, new[] { 7, 11, 11 } },
        };
        internal static readonly Dictionary<string, int> Widths = new Dictionary<string, int>
        {
            { "fanny_pack", 5 }, { "satchel", 8 }, { "kids_backpack", 7 }, { "kids_backpack_large", 8 },
            { "backpack_small", 9 }, { "backpack_medium", 12 }, { "backpack_large", 15 },
            { Military.Medium, 16 }, { Military.Large, 17 }
        };
        private static readonly Dictionary<string, string[]> Tabs = new Dictionary<string, string[]>
        {
            { "kids_backpack", new[] { "fanny_pack" } },
            { "kids_backpack_large", new[] { "fanny_pack" } },
            { "backpack_small", new[] { "fanny_pack" } },
            { "backpack_medium", new[] { "satchel", "backpack_small" } },
            { "backpack_large", new[] { "backpack_small", "backpack_medium", "module_bay_expansion_kit", "storage_bay_expansion_kit" } },
            { Military.Medium, new[] { "satchel", "backpack_small" } },
            { Military.Large, new[] { "backpack_small", "backpack_medium", "module_bay_expansion_kit", "storage_bay_expansion_kit" } },
        };
        internal static readonly string[] Materials = { "wire", "nuts_metal", "metal_ingot" };
        private static readonly int[][] Recipes = { new[] { 2, 0, 0 }, new[] { 2, 1, 0 }, new[] { 3, 2, 0 }, new[] { 0, 2, 1 } };

        internal static int NextRow(string id, int extra, int current)
        {
            id = Military.Canonical(id);
            if (id == null || !Bags.TryGetValue(id, out int[] bag)) return 0;
            long height = Math.Max((long)bag[1] + Math.Max(0, extra), current);
            return height >= bag[2] ? 0 : (int)height + 1;
        }

        internal static int NextColumn(string id, int extra, int current)
        {
            id = Military.Canonical(id);
            if (id == null || !Bags.TryGetValue(id, out int[] bag)) return 0;
            long width = Math.Max((long)bag[0] + Math.Max(0, extra), current);
            return width >= Widths[id] ? 0 : (int)width + 1;
        }

        // Step is the saved extra-column count plus one, independent of live size.
        // The four larger bags spend their former row costs on width first.
        internal static (string Material, int Units) ColumnCost(string id, int step)
        {
            id = Military.Canonical(id);
            if (id == null || !Bags.TryGetValue(id, out int[] bag) || step < 1 || step > Widths[id] - bag[0]) return (null, 0);
            int fabricSteps = id == Military.Medium ? 3
                : id == "backpack_medium" || id == "backpack_large" || id == Military.Large ? 2 : 0;
            return step <= fabricSteps ? ("kotton_fabric", step)
                : ("printer_plastic", fabricSteps == 0 ? step : Math.Min(5, step - fabricSteps));
        }

        internal static int MaxTabs(string id) => id != null && Tabs.TryGetValue(Military.Canonical(id), out string[] pages) ? pages.Length : 0;
        internal static string NextTab(string id, int extra) => extra >= 0 && extra < MaxTabs(id) ? Tabs[Military.Canonical(id)][extra] : null;
        internal static int Choose(bool rows, bool width, bool tabs, bool stacking) => rows ? 1 : width ? 4 : tabs ? 2 : stacking ? 3 : 0;
        internal static int[] Ingredients(int level) => level >= 0 && level < Recipes.Length ? (int[])Recipes[level].Clone() : null;

        internal static int[] Drop(int level, int[] progress, string material)
        {
            int[] recipe = Ingredients(level);
            if (recipe == null || progress == null || progress.Length != Materials.Length) return null;
            for (int i = 0; i < progress.Length; i++) if (progress[i] < 0 || progress[i] > recipe[i]) return null;
            int index = Array.IndexOf(Materials, material);
            if (index < 0 || progress[index] >= recipe[index]) return null;
            int[] next = (int[])progress.Clone();
            next[index]++;
            return next;
        }

        internal static bool Complete(int level, int[] progress)
        {
            int[] recipe = Ingredients(level);
            if (recipe == null || progress == null || progress.Length != recipe.Length) return false;
            for (int i = 0; i < recipe.Length; i++) if (progress[i] != recipe[i]) return false;
            return true;
        }

        internal static int StackLimit(int category, int maximum, int level, bool needsUpgrade)
        {
            int cap = Math.Min(Math.Max(1, Math.Min(99, category)), Math.Max(1, Math.Min(5, maximum)));
            return needsUpgrade ? Math.Min(cap, 1 + Math.Max(0, Math.Min(4, level))) : cap;
        }
    }

    internal static class Backpacks
    {
        internal const string TabsTag = "STORAGE_EXPANSION_TABS_INT", StackTag = "MACHINE_UPGRADES_STACKING_INT";
        internal const string RowProgressTag = "MACHINE_UPGRADES_ROW_PROGRESS_INT", ColumnProgressTag = "MACHINE_UPGRADES_COLUMN_PROGRESS_INT";
        internal static readonly string[] ProgressTags =
        {
            "MACHINE_UPGRADES_STACKING_WIRE_INT", "MACHINE_UPGRADES_STACKING_NUTS_METAL_INT", "MACHINE_UPGRADES_STACKING_METAL_INGOT_INT"
        };
        private static readonly Dictionary<string, ConfigEntry<bool>> switches = new Dictionary<string, ConfigEntry<bool>>();
        internal static Func<string, bool> Switch = key => !switches.TryGetValue(key, out ConfigEntry<bool> entry) || entry.Value;
        internal static Func<MethodInfo> FindAddTabs = () => AccessTools.Method(typeof(StorageExpansionHelper), "AddTabs", new[] { typeof(GameItem), typeof(int) });
        internal static Func<bool> TabsAvailable = CheckTabs;
        internal static Action<GameItem, int> AddTabs = (item, count) =>
        {
            MethodInfo method = FindAddTabs();
            if (!Fits(method)) throw new InvalidOperationException("Storage tab method differs");
            method.Invoke(null, new object[] { item, count });
        };
        // Backpack pages are called Compartments. A page
        // without a name gets one; a name the player has given is left alone.
        internal static string PageName(int index) => index == 0 ? TextTable.Get(TextKeys.TabMain) : TextTable.Get(TextKeys.TabCompartment, index + 1);
        internal static Action<GameItem, int> NamePages = (item, pages) =>
        {
            for (int i = 0; i < pages; i++)
                if (StorageExpansionHelper.GetTabName(item, i) == null) StorageExpansionHelper.SetTabName(item, i, PageName(i));
        };
        internal static Action<GameItem> Consume = item =>
        {
            // A bag sewn in as a page is used up whole; a material one unit at a time.
            if (Supported(item.identifier)) { Material.Gone = true; item.Destroy(); }
            else Material.UseOne(item);
        };

        internal static void Configure(string id)
        {
            ModConfig config = ModConfig.For(id);
            switches["stack_steps"] = config.Define("stack_steps", true, TextTable.Early(TextKeys.SectionBackpacks),
                TextTable.Later(TextKeys.OptionStackSteps),
                TextTable.Later(TextKeys.OptionStackStepsDesc));
            switches[TabHover.SettingKey] = config.Define(TabHover.SettingKey, TabHover.Default, TextTable.Early(TextKeys.SectionBackpacks),
                TextTable.Later(TextKeys.TabHoverName), TextTable.Later(TextKeys.TabHoverDescription));
            switches["carry_empty_bags"] = config.Define("carry_empty_bags", true, TextTable.Early(TextKeys.SectionBackpacks), TextTable.Later(TextKeys.OptionCarryEmptyBags),
                TextTable.Later(TextKeys.OptionCarryEmptyBagsDesc));
            foreach (string key in new[] { "backpacks_enabled", "backpack_rows_enabled", "backpack_width_enabled", "backpack_tabs_enabled", "backpack_stacking_enabled", "backpack_pockets_enabled", "backpack_toolbox_enabled", "backpack_view_enabled" })
            {
                switches[key] = config.Define(key, true, TextTable.Early(TextKeys.SectionBackpacks), () => SettingTitle(key),
                    () => key == "backpack_view_enabled" ? TextTable.Get(TextKeys.OptionBackpackViewEnabledDesc)
                        : TextTable.Get(TextKeys.OptionUpgradesDesc));
            }
        }

        internal static string SettingTitle(string key) => TextTable.Get(key == "backpacks_enabled" ? TextKeys.OptionBackpacksEnabled
            : key == "backpack_rows_enabled" ? TextKeys.OptionBackpackRowsEnabled : key == "backpack_width_enabled" ? TextKeys.OptionBackpackWidthEnabled
            : key == "backpack_tabs_enabled" ? TextKeys.OptionBackpackTabsEnabled : key == "backpack_pockets_enabled" ? TextKeys.OptionBackpackPocketsEnabled
            : key == "backpack_toolbox_enabled" ? TextKeys.OptionBackpackToolboxEnabled : key == "backpack_view_enabled" ? TextKeys.OptionBackpackViewEnabled
            : TextKeys.OptionBackpackStackingEnabled);

        internal static bool Fits(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(void)) return false;
            ParameterInfo[] args = method.GetParameters();
            return args.Length == 2 && args[0].ParameterType == typeof(GameItem) && args[1].ParameterType == typeof(int);
        }

        private static bool CheckTabs()
        {
            if (Fits(FindAddTabs())) return true;
            Storage.Fault("backpack tabs shape", new InvalidOperationException("Storage tab method differs"));
            return false;
        }

        internal static bool Supported(string id) => id != null && BackpackRules.Bags.ContainsKey(Military.Canonical(id));
        internal static bool Pair(GameItem source, GameItem target) => source != null && target != null && source != target
            && source.identifier != null && Supported(target.identifier);

        private static int[] Progress(GameItem item)
        {
            var progress = new int[ProgressTags.Length];
            for (int i = 0; i < progress.Length; i++) progress[i] = Storage.Read(item, ProgressTags[i]);
            return progress;
        }

        private static int[] Current(GameItem item)
        {
            int[][] pages = Storage.Sizes(item);
            if (pages == null || pages.Length == 0) return null;
            int width = 0, height = 0;
            foreach (int[] page in pages)
            {
                if (page == null || page.Length != 2 || page[0] <= 0 || page[1] <= 0) throw new InvalidOperationException("Invalid backpack shape");
                width = Math.Max(width, page[0]); height = Math.Max(height, page[1]);
            }
            return new[] { width, height, pages.Length };
        }

        private sealed class Plan
        {
            internal Pockets.Plan Slots;
            internal int Kind, Width, Height, Tabs, Needed;
            internal bool Open;
            internal string[] Keys;
            internal int[] Values;
        }

        private static Plan Prepare(GameItem source, GameItem target) => PrepareFor(source, target, 0);

        // Zero preserves drop priority; a view payment may only select its own track.
        private static Plan PrepareFor(GameItem source, GameItem target, int selected)
        {
            if (!BackpacksMod.Running || !Pair(source, target) || Material.Spent(source) || source.unitCount < 1 || !Switch("backpacks_enabled") || Storage.InCombat()
                || (Military.IsMilitary(target.identifier) && !Military.Enabled())
                || !Storage.Owned(source) || !Storage.Owned(target)) return null;
            int[] current = Current(target);
            if (current == null) return null;
            int[] bag = BackpackRules.Bags[Military.Canonical(target.identifier)];
            int rows = Storage.Read(target, Storage.RowsTag), tabs = Storage.Read(target, TabsTag), level = Storage.Read(target, StackTag);
            int nextRow = BackpackRules.NextRow(target.identifier, rows, current[1]);
            int columns = Storage.Read(target, Storage.ColumnsTag);
            int nextColumn = BackpackRules.NextColumn(target.identifier, columns, current[0]);
            var columnCost = BackpackRules.ColumnCost(target.identifier, StorageRules.Clamp(columns, 0, StorageRules.Sane) + 1);
            // A saved progress outside what the step costs (a changed table, an edited
            // save) is read as the nearest sound value, so the step can still be taken.
            int rowProgress = StorageRules.Clamp(Storage.Read(target, RowProgressTag), 0, Math.Max(0, nextRow - bag[1] - 1));
            int columnProgress = StorageRules.Clamp(Storage.Read(target, ColumnProgressTag), 0, Math.Max(0, columnCost.Units - 1));
            string nextTab = BackpackRules.NextTab(target.identifier, Math.Max(tabs, current[2] - 1));
            int[] progress = BackpackRules.Drop(level, Progress(target), source.identifier);
            // Earlier steps get first choice of each ingredient; full ingredients
            // fall through even when their recipe still waits for another material.
            bool Selected(int track) => selected == 0 || selected == track;
            int kind = BackpackRules.Choose(Selected(1) && Switch("backpack_rows_enabled") && source.identifier == "kotton_fabric" && nextRow > 0 && rowProgress >= 0 && rowProgress < nextRow - bag[1],
                Selected(4) && Switch("backpack_width_enabled") && source.identifier == columnCost.Material && nextColumn > 0 && columnProgress < columnCost.Units,
                Selected(2) && Switch("backpack_tabs_enabled") && nextTab != null && source.identifier == nextTab,
                Selected(3) && Switch("backpack_stacking_enabled") && progress != null);
            Pockets.Plan pockets = null, toolbox = null;
            if (kind == 0)
            {
                pockets = Selected(5) ? Pockets.Prepare(source, target, false) : null;
                toolbox = Selected(6) ? Pockets.Prepare(source, target, true) : null;
                kind = PocketRules.Choose(kind, pockets != null, toolbox != null);
            }
            if (kind == 0) return null;
            var plan = new Plan { Kind = kind,
                Width = Math.Max(current[0], bag[0] + StorageRules.Clamp(columns, 0, StorageRules.Sane - bag[0])),
                Height = Math.Max(current[1], bag[1] + StorageRules.Clamp(rows, 0, StorageRules.Sane - bag[1])) };
            if (kind == 5 || kind == 6)
            {
                plan.Slots = kind == 5 ? pockets : toolbox;
                plan.Needed = plan.Slots.Needed;
                plan.Keys = plan.Slots.Keys; plan.Values = plan.Slots.Values; plan.Open = plan.Slots.Open;
            }
            else if (kind == 1 || kind == 4)
            {
                bool row = kind == 1;
                int next = row ? nextRow : nextColumn, basis = row ? bag[1] : bag[0];
                int cost = row ? next - basis : columnCost.Units;
                int paid = (row ? rowProgress : columnProgress) + 1;
                plan.Needed = cost - paid + 1;
                bool complete = paid == cost;
                if (complete) { if (row) plan.Height = next; else plan.Width = next; }
                plan.Keys = new[] { row ? Storage.RowsTag : Storage.ColumnsTag, row ? RowProgressTag : ColumnProgressTag };
                plan.Values = new[] { complete ? next - basis : row ? rows : columns, complete ? 0 : paid };
                plan.Open = !complete;
            }
            else if (kind == 2)
            {
                // Kits are materials, bags are whole single empty donors.
                if (Supported(source.identifier) && (source.unitCount != 1 || !Storage.Empty(source))) return null;
                if (!TabsAvailable()) return null;
                plan.Tabs = Math.Max(tabs, current[2] - 1) + 1;
                plan.Needed = 1;
                plan.Keys = new[] { TabsTag }; plan.Values = new[] { plan.Tabs };
            }
            else
            {
                bool complete = BackpackRules.Complete(level, progress);
                int ingredient = Array.IndexOf(BackpackRules.Materials, source.identifier);
                plan.Needed = BackpackRules.Ingredients(level)[ingredient] - progress[ingredient] + 1;
                // A one-step drop stops when this ingredient is full. A whole
                // stack with stack_steps on may then reach the next matching track.
                plan.Open = !complete && BackpackRules.Drop(level, progress, source.identifier) != null;
                plan.Keys = new[] { StackTag, ProgressTags[0], ProgressTags[1], ProgressTags[2] };
                plan.Values = new[] { complete ? level + 1 : level, complete ? 0 : progress[0], complete ? 0 : progress[1], complete ? 0 : progress[2] };
            }
            return plan;
        }

        internal static bool CanApply(GameItem source, GameItem target)
        {
            try { return Prepare(source, target) != null; }
            catch (Exception error) { Storage.Fault("backpack targeting", error); return false; }
        }

        // Read the same plan as Apply: no duplicate upgrade priority or cost table.
        internal static int Needed(GameItem source, GameItem target) => Prepare(source, target)?.Needed ?? 0;

        internal static bool Verdict(GameItem source, GameItem target, bool original) => original || CanApply(source, target);

        private static void EnsureTabs(GameItem item, int extra)
        {
            int[] current = Current(item);
            if (current == null) throw new InvalidOperationException("Missing backpack window");
            int missing = extra + 1 - current[2];
            if (missing <= 0) return;
            if (!TabsAvailable()) throw new InvalidOperationException("Storage tab method differs");
            AddTabs(item, missing);
            if (Current(item)[2] < extra + 1) throw new InvalidOperationException("Backpack tabs did not grow");
            EmptyBagCarry.Prepare(item);
        }

        private static bool Grew(GameItem item, int[] before)
        {
            try
            {
                int[] after = Current(item);
                return after != null && (after[0] > before[0] || after[1] > before[1] || after[2] > before[2]);
            }
            catch (Exception error) { Storage.Fault("checking backpack window after a fault", error); return false; }
        }

        private static void CheckSize(GameItem item, int width, int height)
        {
            int[][] pages = Storage.Sizes(item);
            if (pages == null || pages.Length == 0) throw new InvalidOperationException("Missing backpack grids");
            foreach (int[] page in pages)
                if (page == null || page.Length != 2 || page[0] < width || page[1] < height)
                    throw new InvalidOperationException("Backpack grid did not grow");
        }

        // The chosen step may take more of this ingredient, never another step.
        internal static bool StepOpen;

        internal static bool Apply(GameItem source, GameItem target) => ApplyFor(source, target, 0);

        internal static bool Dropped(GameItem source, GameItem target)
        {
            bool previous = StepOpen;
            try { return Material.Dropped(source, target, Apply, () => StepOpen, true, Switch("stack_steps"), CanApply); }
            catch (Exception error) { Storage.Fault("backpack drop", error); return false; }
            finally { StepOpen = previous; }
        }

        internal static bool ApplyFor(GameItem source, GameItem target, int selected)
        {
            StepOpen = false;
            try
            {
                Plan plan = PrepareFor(source, target, selected);
                if (plan == null) return false;
                if (plan.Slots != null && plan.Slots.Complete) Pockets.TryMove(target);
                int[] windowBefore = Current(target);
                int[] panelsBefore = plan.Slots != null && plan.Slots.Complete ? Pockets.Layout(target) : null;
                bool windowChanged = false;
                int[] before = new int[plan.Keys.Length];
                for (int i = 0; i < before.Length; i++) before[i] = Storage.Read(target, plan.Keys[i]);
                try
                {
                    for (int i = 0; i < before.Length; i++) Storage.Write(target, plan.Keys[i], plan.Values[i]);
                    if (plan.Kind == 2)
                    {
                        EnsureTabs(target, plan.Tabs);
                        windowChanged = true;
                    }
                    if (plan.Slots != null && plan.Slots.Complete)
                    {
                        Pockets.Restore(target);
                        windowChanged = true;
                    }
                    if (plan.Kind != 3 && plan.Slots == null)
                    {
                        Storage.Resize(target, plan.Width, plan.Height);
                        CheckSize(target, plan.Width, plan.Height);
                    }
                }
                catch (Exception error)
                {
                    // Validation may throw after AddTabs/SetShape has already grown
                    // the window. Keep the saved capacity and count that drop.
                    if (windowChanged || (panelsBefore != null ? Pockets.Changed(target, panelsBefore) : plan.Slots == null && plan.Kind != 3 && Grew(target, windowBefore)))
                        Storage.Fault("validating a grown backpack window", error);
                    else
                    {
                        for (int i = 0; i < before.Length; i++)
                            try { Storage.Write(target, plan.Keys[i], before[i]); }
                            catch (Exception rollbackError) { Storage.Fault("putting backpack tags back", rollbackError); }
                        // A page that exists must be in the tag, or it would not be put
                        // back on the next load.
                        try
                        {
                            int[] now = Current(target);
                            if (now != null && now[2] - 1 > Storage.Read(target, TabsTag)) Storage.Write(target, TabsTag, now[2] - 1);
                        }
                        catch (Exception tagError) { Storage.Fault("recording the pages of a backpack", tagError); }
                        throw;
                    }
                }
                // A committed window/progress change counts even if consuming fails.
                if (plan.Kind == 2)
                    try { NamePages(target, plan.Tabs + 1); }
                    catch (Exception error) { Storage.Fault("naming backpack compartments", error); }
                try { Consume(source); }
                catch (Exception error) { Storage.Fault("using up backpack material", error); }
                StepOpen = plan.Open;
                return true;
            }
            catch (Exception error) { Storage.Fault("backpack upgrade", error); return false; }
        }

        internal static void Restore(GameItem item)
        {
            try
            {
                if (item == null || !Supported(item.identifier)) return;
                int[] current = Current(item);
                if (current == null) return;
                // What was saved is put back, whatever the table allows today: the table
                // only limits new pages. Four extra pages is the game's own most.
                int tabs = StorageRules.Clamp(Storage.Read(item, TabsTag), 0, 4);
                // The rows are put back even when the pages cannot be, so that a change
                // in the game's tab code cannot leave the first page without its rows.
                try { EnsureTabs(item, tabs); }
                catch (Exception error) { Storage.Fault("restoring backpack tabs", error); }
                // The save numbers inventories before attaching their contents.
                // Always rebuild pages, pockets, toolbox, then sizes, from tags.
                try { Pockets.Restore(item); }
                catch (Exception error) { Storage.Fault("restoring backpack slots", error); }
                int[] bag = BackpackRules.Bags[Military.Canonical(item.identifier)];
                int rows = StorageRules.Clamp(Storage.Read(item, Storage.RowsTag), 0, StorageRules.Sane - bag[1]);
                int columns = StorageRules.Clamp(Storage.Read(item, Storage.ColumnsTag), 0, StorageRules.Sane - bag[0]);
                int width = Math.Max(current[0], bag[0] + columns), height = Math.Max(current[1], bag[1] + rows);
                foreach (int[] page in Storage.Sizes(item))
                    if (page[0] < width || page[1] < height) { Storage.Resize(item, width, height); break; }
                EmptyBagCarry.Prepare(item);
            }
            catch (Exception error) { Storage.Fault("restoring backpacks", error); }
        }

        internal static void Attach(GameItem item, string identifier)
        {
            try
            {
                UpgradeView.Attach(item, identifier);
                if (item != null && Supported(identifier))
                {
                    EmptyBagCarry.Prepare(item);
                    if (item.onLoaded != null)
                        foreach (Delegate handler in item.onLoaded.GetInvocationList()) if (handler.Equals((Action<GameItem>)Loaded)) return;
                    item.onLoaded = (Action<GameItem>)Delegate.Combine(item.onLoaded, (Action<GameItem>)Loaded);
                }
            }
            catch (Exception error) { Storage.Fault("preparing backpacks", error); }
        }

        private static void Loaded(GameItem item)
        {
            Restore(item);
            // onLoaded is after saved children have been attached. The decode
            // restore must never migrate, even when its as-yet empty slots look safe.
            Storage.Guard("moving loaded backpack slots", () =>
            {
                if (item != null && Supported(item.identifier)) Pockets.TryMove(item);
            });
        }

        private static string Name(string id) => UpgradeViewRules.Name(id);

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (item == null || !Supported(item.identifier)) return;
                int[] current = Current(item);
                if (current == null) return;
                string id = Military.Canonical(item.identifier);
                int[] bag = BackpackRules.Bags[id];
                int rows = Storage.Read(item, Storage.RowsTag), tabs = Math.Max(Storage.Read(item, TabsTag), current[2] - 1);
                int level = StorageRules.Clamp(Storage.Read(item, StackTag), 0, 4);
                int next = BackpackRules.NextRow(id, rows, current[1]);
                int columns = Storage.Read(item, Storage.ColumnsTag);
                int nextColumn = BackpackRules.NextColumn(id, columns, current[0]);
                var columnCost = BackpackRules.ColumnCost(id, StorageRules.Clamp(columns, 0, StorageRules.Sane) + 1);
                int columnProgress = StorageRules.Clamp(Storage.Read(item, ColumnProgressTag), 0, Math.Max(0, columnCost.Units - 1));
                string material = BackpackRules.NextTab(id, tabs);
                string RowState() => next == 0 ? TextTable.Get(TextKeys.TooltipLimit)
                    : TextTable.Get(TextKeys.TooltipNextProgress, UpgradeViewRules.Name("kotton_fabric"), Storage.Read(item, RowProgressTag), next - bag[1]);
                string ColumnState() => nextColumn == 0 ? TextTable.Get(TextKeys.TooltipLimit)
                    : TextTable.Get(TextKeys.TooltipNextProgress, UpgradeViewRules.Name(columnCost.Material), columnProgress, columnCost.Units);
                Storage.Line(builder, Disabled("backpack_rows_enabled", TextTable.Get(TextKeys.TooltipRows, Math.Max(current[1], bag[1] + StorageRules.Clamp(rows, 0, StorageRules.Sane)), Math.Max(bag[2], Math.Max(current[1], bag[1] + StorageRules.Clamp(rows, 0, StorageRules.Sane))), RowState())));
                Storage.Line(builder, Disabled("backpack_width_enabled", TextTable.Get(TextKeys.TooltipColumns, Math.Max(current[0], bag[0] + StorageRules.Clamp(columns, 0, StorageRules.Sane - bag[0])), BackpackRules.Widths[id], ColumnState())));
                Storage.Line(builder, Disabled("backpack_tabs_enabled", TextTable.Get(TextKeys.TooltipCompartments, tabs + 1, BackpackRules.MaxTabs(id) + 1, material == null ? TextTable.Get(TextKeys.TooltipLimit) : TextTable.Get(TextKeys.TooltipNextProgress, Name(material), 0, 1))));
                int[] recipe = BackpackRules.Ingredients(level), progress = Progress(item);
                var needs = new List<string>();
                if (recipe != null)
                    for (int i = 0; i < recipe.Length; i++)
                        if (recipe[i] > 0) needs.Add(TextTable.Get(TextKeys.MaterialProgress, Name(BackpackRules.Materials[i]), progress[i], recipe[i]));
                Storage.Line(builder, Disabled("backpack_stacking_enabled", TextTable.Get(TextKeys.TooltipStack, level + 1, recipe == null ? TextTable.Get(TextKeys.TooltipLimit) : TextTable.Get(TextKeys.TooltipNext, string.Join(", ", needs)))));
            }
            catch (Exception error) { Storage.Fault("backpack tooltip", error); }
        }

        internal static string Disabled(string key, string text) => Switch("backpacks_enabled") && Switch(key) ? text : TextTable.Get(TextKeys.TooltipDisabled, text);
    }
}

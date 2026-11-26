using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

using LocalMods;

namespace StorageUpgrades
{
    internal static class MachineStorage
    {
        internal const string WaterTag = "MACHINE_UPGRADES_WATER_INT";
        private const int WaterBase = 12000000, WaterStep = 6000000, WaterLimit = 6;
        // Each row is the game's final width/height and our largest width/height.
        // Only the first grid chooses the next axis; the fridge shares its extras.
        internal static readonly Dictionary<string, int[][]> Machines = new Dictionary<string, int[][]>
        {
            { "fridge", new[] { new[] {5,9,9,13}, new[] {3,9,7,13} } },
            { "wine_rack", new[] { new[] {6,4,12,8} } },
            { "ras", new[] { new[] {12,8,24,16} } },
            { "animal_cage", new[] { new[] {10,8,24,16} } }
        };

        // Unity-only boundaries, substituted by the offline harness.
        internal static Func<GameItem, int[][]> Sizes = GridSizes;
        internal static Action<GameItem, int[][]> Resize = ResizeGrids;
        internal static Func<GameItem, IEnumerable<GameGridInventory>> Inventories = item => Grids(item);
        internal static Func<GameItem, int> WaterCapacity = item => item.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();
        internal static Action<GameItem, int> SetWaterCapacity = (item, value) =>
        {
            item.ModifyTag("LIQUID_CONTAINER_CAPACITY", state => state.SetInt(value));
            item.ValidateShapeState();
        };

        private static bool Water(string id) => id == "ras_water_tank";
        internal static bool Supported(string id) => id != null && (Water(id) || Machines.ContainsKey(id));
        internal static bool Pair(GameItem source, GameItem target) => source != null && target != null && source != target
            && source.identifier == "metal_ingot" && Supported(target.identifier);

        internal static bool Complete(GameItem item) => item.identifier switch
        {
            "fridge" => Storage.Read(item, "FRIDGE_STORAGE_UPGRADE_COUNT_TAG") >= 7,
            "ras" => Storage.Read(item, "RAS_WIDTH_UPGRADE_COUNT_TAG") >= 4
                && Storage.Read(item, "RAS_HEIGHT_UPGRADE_COUNT_TAG") >= 4,
            "animal_cage" => Storage.Read(item, "CAGE_SIZE_TAG") >= 8,
            "wine_rack" => true,
            "ras_water_tank" => true,
            _ => false
        };

        private static bool On() => Storage.Switch("storage_enabled") && Storage.Switch("machine_storage_enabled");

        private static GameGridInventory[] Grids(GameItem item)
        {
            // Never walk the window: it also contains module, water and power grids.
            return item.identifier switch
            {
                "fridge" => new[] { MachineFridge.GetChillZone(item), MachineFridge.GetFreezeZone(item) },
                "wine_rack" => new[] { MachineHelper.GetBodyStorage(item, 2) as GameGridInventory },
                "ras" => new[] { MachineRAS.GetTankGrid(item) },
                "animal_cage" => new[] { AnimalCage.GetCageInventory(item) },
                _ => Array.Empty<GameGridInventory>()
            };
        }

        private static int[][] GridSizes(GameItem item)
        {
            if (item.contentWindow == null) return null;
            GameGridInventory[] grids = Grids(item);
            var sizes = new int[grids.Length][];
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] == null) return null;
                sizes[i] = new[] { grids[i].inventoryShape.width, grids[i].inventoryShape.height };
            }
            return sizes;
        }

        private static void ResizeGrids(GameItem item, int[][] sizes)
        {
            GameGridInventory[] grids = Grids(item);
            if (item.contentWindow == null || grids.Length != sizes.Length) throw new InvalidOperationException("Missing machine storage");
            // Check every zone before changing any. Each grid keeps any dimensions
            // another mod has already given it, including sizes above our limit.
            foreach (GameGridInventory grid in grids)
                if (grid == null || grid.inventoryShape == null) throw new InvalidOperationException("Missing machine grid");
            for (int i = 0; i < grids.Length; i++)
            {
                int w = Math.Max(grids[i].inventoryShape.width, sizes[i][0]);
                int h = Math.Max(grids[i].inventoryShape.height, sizes[i][1]);
                if (w != grids[i].inventoryShape.width || h != grids[i].inventoryShape.height) grids[i].SetShape(w, h);
            }
            item.contentWindow.Validate();
        }

        private static int[][] Current(GameItem item, int[][] basis)
        {
            int[][] sizes = Sizes(item);
            if (sizes == null || sizes.Length == 0) return null;
            if (sizes.Length != basis.Length) throw new InvalidOperationException("Machine grid count differs");
            foreach (int[] size in sizes)
                if (size == null || size.Length != 2 || size[0] <= 0 || size[1] <= 0) throw new InvalidOperationException("Invalid machine grid shape");
            return sizes;
        }

        private static int[] Extras(GameItem item, int[][] basis, int[][] current)
        {
            // As for bays, a step limit must not discard already saved capacity.
            // Only newly adopted dimensions are clamped to our table's maximum.
            int columns = StorageRules.Clamp(Storage.Read(item, Storage.ColumnsTag), 0, StorageRules.Sane - basis[0][0]);
            int rows = StorageRules.Clamp(Storage.Read(item, Storage.RowsTag), 0, StorageRules.Sane - basis[0][1]);
            for (int i = 0; i < basis.Length; i++)
            {
                columns = Math.Max(columns, StorageRules.Clamp(current[i][0] - basis[i][0], 0, basis[i][2] - basis[i][0]));
                rows = Math.Max(rows, StorageRules.Clamp(current[i][1] - basis[i][1], 0, basis[i][3] - basis[i][1]));
            }
            return new[] { columns, rows };
        }

        private static int[][] Targets(int[][] basis, int[][] current, int[] extra)
        {
            var sizes = new int[basis.Length][];
            for (int i = 0; i < basis.Length; i++)
                sizes[i] = new[] { Math.Max(current[i][0], basis[i][0] + extra[0]), Math.Max(current[i][1], basis[i][1] + extra[1]) };
            return sizes;
        }

        // Adoption is independent of settings and ownership. Only vanilla-complete
        // machines can interpret dimensions as extras beyond the game's end size.
        private static int[] Adopt(GameItem item, int[][] basis, int[][] current)
        {
            int[] extra = Extras(item, basis, current);
            if (extra[0] > Storage.Read(item, Storage.ColumnsTag)) Storage.Write(item, Storage.ColumnsTag, extra[0]);
            if (extra[1] > Storage.Read(item, Storage.RowsTag)) Storage.Write(item, Storage.RowsTag, extra[1]);
            return extra;
        }

        private sealed class Plan
        {
            internal int[] Extra;
            internal int[][] Sizes;
            internal int WaterLevel, Capacity;
        }

        private static int WaterLevel(GameItem item, int current)
        {
            int saved = StorageRules.Clamp(Storage.Read(item, WaterTag), 0, WaterLimit);
            int adopted = (int)Math.Min(WaterLimit, Math.Max(0L, (long)current - WaterBase) / WaterStep);
            return Math.Max(saved, adopted);
        }

        private static int AdoptWater(GameItem item, int current)
        {
            int level = WaterLevel(item, current);
            if (level > Storage.Read(item, WaterTag)) Storage.Write(item, WaterTag, level);
            return level;
        }

        private static int WaterTarget(int current, int level) => Math.Max(current, WaterBase + WaterStep * level);

        private static void ApplyWater(GameItem source, GameItem target, Plan plan)
        {
            int oldLevel = Storage.Read(target, WaterTag), oldCapacity = WaterCapacity(target);
            bool capacityAttempted = false;
            try
            {
                Storage.Write(target, WaterTag, plan.WaterLevel);
                capacityAttempted = true;
                SetWaterCapacity(target, Math.Max(oldCapacity, plan.Capacity));
                if (WaterCapacity(target) < Math.Max(oldCapacity, plan.Capacity))
                    throw new InvalidOperationException("Water tank capacity did not grow");
            }
            catch (Exception error)
            {
                if (WaterCapacity(target) >= plan.Capacity)
                    Storage.Fault("validating grown water capacity", error);
                else
                {
                    if (capacityAttempted)
                        Storage.Guard("putting water capacity back", () => SetWaterCapacity(target, oldCapacity));
                    Storage.Guard("putting water level back", () => Storage.Write(target, WaterTag, oldLevel));
                    throw;
                }
            }
            try { Material.UseOne(source); }
            catch (Exception error) { Storage.Fault("using machine storage material", error); }
        }

        private static Plan Prepare(GameItem source, GameItem target)
        {
            if (!Pair(source, target) || !On() || Storage.InCombat() || !Storage.Owned(source) || !Storage.Owned(target) || !Complete(target)) return null;
            if (Water(target.identifier))
            {
                int capacity = WaterCapacity(target), level = WaterLevel(target, capacity);
                if (level >= WaterLimit) return null;
                return new Plan { WaterLevel = level + 1, Capacity = WaterTarget(capacity, level + 1) };
            }
            int[][] basis = Machines[target.identifier], current = Current(target, basis);
            if (current == null) return null;
            int[] extra = Extras(target, basis, current);
            int[][] achieved = Targets(basis, current, extra);
            int[] next = StorageRules.NextBay(basis[0][0], basis[0][1], basis[0][2], basis[0][3], achieved[0][0], achieved[0][1]);
            if (next == null) return null;
            extra = new[] { Math.Max(extra[0], StorageRules.Clamp(next[0] - basis[0][0], 0, basis[0][2] - basis[0][0])),
                Math.Max(extra[1], StorageRules.Clamp(next[1] - basis[0][1], 0, basis[0][3] - basis[0][1])) };
            return new Plan { Extra = extra, Sizes = Targets(basis, current, extra) };
        }

        internal static bool CanApply(GameItem source, GameItem target)
        {
            try { return Prepare(source, target) != null; }
            catch (Exception error) { Storage.Fault("machine storage targeting", error); return false; }
        }

        internal static bool Verdict(GameItem source, GameItem target, bool original) => original || CanApply(source, target);

        internal static bool Apply(GameItem source, GameItem target)
        {
            try
            {
                Plan plan = Prepare(source, target);
                if (plan == null) return false;
                if (Water(target.identifier)) { ApplyWater(source, target, plan); return true; }
                int oldColumns = Storage.Read(target, Storage.ColumnsTag), oldRows = Storage.Read(target, Storage.RowsTag);
                int[][] gridsBefore = Array.ConvertAll(Sizes(target), page => (int[])page.Clone());
                try
                {
                    Storage.Write(target, Storage.ColumnsTag, plan.Extra[0]);
                    Storage.Write(target, Storage.RowsTag, plan.Extra[1]);
                    Resize(target, plan.Sizes);
                    int[][] actual = Current(target, Machines[target.identifier]);
                    if (actual == null) throw new InvalidOperationException("Machine grids disappeared after resize");
                    for (int i = 0; i < actual.Length; i++)
                        if (actual[i][0] < plan.Sizes[i][0] || actual[i][1] < plan.Sizes[i][1]) throw new InvalidOperationException("Machine grid did not grow");
                }
                catch (Exception error)
                {
                    bool grew = false;
                    try { grew = Material.Grew(gridsBefore, Sizes(target)); }
                    catch (Exception checkError) { Storage.Fault("checking grown machine storage", checkError); }
                    if (grew) Storage.Fault("validating grown machine storage", error);
                    else
                    {
                        // No zone grew, so the saved step can be rolled back.
                        Storage.Guard("putting machine columns back", () => Storage.Write(target, Storage.ColumnsTag, oldColumns));
                        Storage.Guard("putting machine rows back", () => Storage.Write(target, Storage.RowsTag, oldRows));
                        throw;
                    }
                }
                try { Material.UseOne(source); }
                catch (Exception error) { Storage.Fault("using machine storage material", error); }
                return true;
            }
            catch (Exception error) { Storage.Fault("machine storage upgrade", error); return false; }
        }

        internal static void Restore(GameItem item)
        {
            try
            {
                if (item == null || !Supported(item.identifier) || !Complete(item)) return;
                if (Water(item.identifier))
                {
                    int capacity = WaterCapacity(item);
                    int target = WaterTarget(capacity, AdoptWater(item, capacity));
                    if (target > capacity) SetWaterCapacity(item, target);
                    return;
                }
                int[][] basis = Machines[item.identifier], current = Current(item, basis);
                if (current == null) return;
                int[] extra = Adopt(item, basis, current);
                Resize(item, Targets(basis, current, extra));
            }
            catch (Exception error) { Storage.Fault("restoring machine containers", error); }
        }

        internal static void Attach(GameItem item, string identifier)
        {
            try
            {
                if (item != null && Supported(identifier))
                    item.onLoaded = (Action<GameItem>)Delegate.Combine(item.onLoaded, (Action<GameItem>)Restore);
            }
            catch (Exception error) { Storage.Fault("preparing machine containers", error); }
        }

        private static string Describe(int[][] sizes)
        {
            string text = sizes[0][0] + "x" + sizes[0][1];
            for (int i = 1; i < sizes.Length; i++) text += " and " + sizes[i][0] + "x" + sizes[i][1];
            return text;
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (item == null || !Supported(item.identifier)) return;
                if (Water(item.identifier))
                {
                    int capacity = WaterCapacity(item), level = AdoptWater(item, capacity);
                    string waterDetail = level >= WaterLimit ? "limit reached" : "next: Metal Ingot";
                    Storage.Line(builder, "Water tank: " + (capacity / 1000000m).ToString("0.######", CultureInfo.InvariantCulture)
                        + " of 48 | " + waterDetail);
                    return;
                }
                int[][] basis = Machines[item.identifier], current = Current(item, basis);
                if (current == null) return;
                bool complete = Complete(item);
                if (complete) current = Targets(basis, current, Adopt(item, basis, current));
                var most = new int[basis.Length][];
                for (int i = 0; i < basis.Length; i++) most[i] = new[] { basis[i][2], basis[i][3] };
                string detail = !complete ? "after the machine's own upgrades"
                    : StorageRules.NextBay(basis[0][0], basis[0][1], basis[0][2], basis[0][3], current[0][0], current[0][1]) == null
                        ? "limit reached" : "next: Metal Ingot";
                Storage.Line(builder, "Storage: " + Describe(current) + " of " + Describe(most) + " | " + detail);
            }
            catch (Exception error) { Storage.Fault("machine storage tooltip", error); }
        }
    }

    [HarmonyPatch]
    internal static class FridgeStoragePatch
    {
        internal static MethodBase TargetMethod()
        {
            try { return AccessTools.Method(typeof(MachineFridge), "ApplyStorageSize", new[] { typeof(GameItem) }); }
            catch (Exception error) { Storage.Fault("fridge storage patch target", error); return null; }
        }
        internal static bool Prepare()
        {
            try
            {
                return TargetMethod() is MethodInfo method && method.IsStatic && method.ReturnType == typeof(void)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(GameItem);
            }
            catch (Exception error) { Storage.Fault("fridge storage patch target", error); return false; }
        }
        internal static void Postfix(GameItem __0) => MachineStorage.Restore(__0);
    }

    [HarmonyPatch]
    internal static class TankStoragePatch
    {
        internal static MethodBase TargetMethod()
        {
            try { return AccessTools.Method(typeof(MachineRAS), "ApplyTankSize", new[] { typeof(GameItem) }); }
            catch (Exception error) { Storage.Fault("tank storage patch target", error); return null; }
        }
        internal static bool Prepare()
        {
            try
            {
                return TargetMethod() is MethodInfo method && method.IsStatic && method.ReturnType == typeof(void)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(GameItem);
            }
            catch (Exception error) { Storage.Fault("tank storage patch target", error); return false; }
        }
        internal static void Postfix(GameItem __0) => MachineStorage.Restore(__0);
    }
}

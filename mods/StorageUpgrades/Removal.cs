using System;
using LocalMods;

namespace StorageUpgrades
{
    internal static class Removal
    {
        internal static Func<GameItem, int[][], RemovalContainer> CaptureMachine = (item, basis) =>
            RemovalList.Snapshot(item, basis, int.MaxValue, false, MachineStorage.Inventories(item), false);
        internal static RemovalContainer Describe(GameItem item)
        {
            if (item == null) return null;
            if (Storage.Read(item, Storage.RowsTag) <= 0 && Storage.Read(item, Storage.ColumnsTag) <= 0
                && Storage.Read(item, Storage.CratesTag) <= 0 && Storage.Read(item, MachineStorage.WaterTag) <= 0) return null;
            int[][] basis = null;
            // Native expansion pages survive removal; each keeps the game's bay size.
            if (item.identifier != null && StorageRules.Bays.TryGetValue(item.identifier, out int[] bay))
                basis = new[] { new[] { bay[0], bay[1] } };
            else if (item.identifier == "hypertech_bay") basis = new[] { new[] { 16, 30 }, new[] { 16, 30 } };
            else if (Storage.IsCrate(item.identifier)) basis = new[] { new[] { 7, 5 } };
            else if (item.identifier == "beerCase") basis = new[] { new[] { 8, 4 } };
            if (basis != null && (Storage.IsCrate(item.identifier) || item.identifier == "beerCase"))
            {
                int columns = Storage.Read(item, "MACHINE_UPGRADES_CRATE_BASE_COLUMNS_INT");
                int rows = Storage.Read(item, "MACHINE_UPGRADES_CRATE_BASE_ROWS_INT");
                if (columns > 0) basis[0][0] = Math.Min(basis[0][0], columns);
                if (rows > 0) basis[0][1] = Math.Min(basis[0][1], rows);
            }
            if (MachineStorage.Supported(item.identifier))
                return CaptureMachine(item, MachineBasis(item));
            return RemovalList.Capture(item, basis, int.MaxValue, false);
        }

        private static int[][] MachineBasis(GameItem item)
        {
            // MachineStorage writes only our extra-row/column tags, never these
            // native counts. Do not use its table of fully upgraded machine sizes
            // for an item whose own upgrades are still incomplete.
            switch (item.identifier)
            {
                case "fridge":
                    int steps = Storage.Read(item, "FRIDGE_STORAGE_UPGRADE_COUNT_TAG");
                    return steps < 0 || steps > 7 ? null : new[] { new[] { 5, 2 + steps }, new[] { 3, 2 + steps } };
                case "ras":
                    int width = Storage.Read(item, "RAS_WIDTH_UPGRADE_COUNT_TAG"), height = Storage.Read(item, "RAS_HEIGHT_UPGRADE_COUNT_TAG");
                    return width < 0 || width > 4 || height < 0 || height > 4 ? null : new[] { new[] { 8 + width, 4 + height } };
                case "animal_cage":
                    int rows = Storage.Read(item, "CAGE_SIZE_TAG");
                    return rows <= 0 || rows > 8 ? null : new[] { new[] { 10, rows } };
                case "wine_rack": return new[] { new[] { 6, 4 } };
                // A water tank holds liquid, which this list cannot count.
                default: return null;
            }
        }
    }
}

using System;
using LocalMods;

namespace Backpacks
{
    internal static class MilitaryItems
    {
        // EquippableBack is private and non-static in the installed game. Reuse only
        // a released ordinary backpack, including its equipment and audio behaviour.
        internal static Func<string, GameItem> Create = id => DirectoryMaster.Item(id);
        internal static Action<ModItemDirectory, string, Func<GameItem>> Add = (directory, id, factory) => directory.Add(id, factory);
        internal static Action<GameItem, int, int> Resize = (item, width, height) =>
        {
            var grids = Storage.OwnGrids(item.contentWindow);
            if (grids.Count != 1 || grids[0].childItems.Count != 0) throw new InvalidOperationException("Ordinary backpack factory shape differs");
            grids[0].SetShape(width, height);
            item.contentWindow.Validate();
        };

        internal static void Register(ModItemDirectory directory)
        {
            Storage.Guard("Scavenger's Pack registration", () => Add(directory, Military.Medium, () => Make(Military.Medium)));
            Storage.Guard("Hauler's Pack registration", () => Add(directory, Military.Large, () => Make(Military.Large)));
            Storage.Guard("legacy Scavenger's Pack registration", () => Add(directory, Military.LegacyMedium, () => Make(Military.LegacyMedium)));
            Storage.Guard("legacy Hauler's Pack registration", () => Add(directory, Military.LegacyLarge, () => Make(Military.LegacyLarge)));
        }

        internal static GameItem Make(string id)
        {
            id = Military.Canonical(id);
            GameItem item = null;
            try
            {
                if (!Military.IsMilitary(id)) return null;
                bool medium = id == Military.Medium;
                item = Create(medium ? "backpack_medium" : "backpack_large");
                if (item == null) throw new InvalidOperationException("Ordinary backpack factory returned nothing");
                Resize(item, medium ? 6 : 7, medium ? 9 : 11);
                item.name = TextTable.Get(medium ? TextKeys.ScavengersPackName : TextKeys.HaulersPackName);
                item.shortDescription = TextTable.Get(medium ? TextKeys.ScavengersPackDescription : TextKeys.HaulersPackDescription);
                item.unitValue = medium ? 700 : 1000;
                Military.Dress(item, id);
                // Inner creation supplies a unique id; outer creation keeps it.
                return item;
            }
            catch (Exception error)
            {
                if (item != null) Storage.Guard("discarding an unfinished own pack", () => Material.Destroy(item));
                Storage.Fault("own pack item factory", error);
                return null;
            }
        }
    }
}

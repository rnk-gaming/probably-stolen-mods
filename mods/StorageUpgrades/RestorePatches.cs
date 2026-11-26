using System;
using HarmonyLib;
using LocalMods;
namespace StorageUpgrades
{
    [HarmonyPatch(typeof(DirectoryMaster), nameof(DirectoryMaster.Item), new[] { typeof(string), typeof(bool) })]
    [HarmonyPriority(Priority.Last)]
    internal static class StorageCreatedPatch
    {
        private static void Postfix(string identifier, GameItem __result)
        {
            Storage.Attach(__result, identifier);
            MachineStorage.Attach(__result, identifier);
        }
    }

    // The game calls this for every item of a save right after the item and its tags
    // have been created and before the items inside it are put back. A container must
    // have its full size by then, or what lay beyond the game's own size has no place.
    // The same sizes are also put back from the item's onLoaded, which runs later.
    [HarmonyPatch(typeof(StorageExpansionHelper), "RestoreTabs", new[] { typeof(GameItem) })]
    [HarmonyPriority(Priority.Last)]
    internal static class StorageDecodedPatch
    {
        private static void Postfix(GameItem item)
        {
            Storage.Restore(item);
            MachineStorage.Restore(item);
        }
    }

}

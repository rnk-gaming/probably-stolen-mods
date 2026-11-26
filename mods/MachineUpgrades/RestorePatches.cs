using HarmonyLib;
using LocalMods;
namespace MachineUpgrades
{
    [HarmonyPatch(typeof(StorageExpansionHelper), "RestoreTabs", new[] { typeof(GameItem) })]
    [HarmonyPriority(Priority.Last)]
    internal static class StorageDecodedPatch
    {
        private static void Postfix(GameItem item) => Storage.Guard("restoring machine storage before its contents", () => { if (item != null) Upgrades.ApplyStorage(item); });
    }
}

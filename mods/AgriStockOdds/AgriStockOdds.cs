// Agri Stock Odds: replaces Agri Expansion's 100% vendor stock for its machines
// with a chance roll per machine and visit: one chance while the player owns
// none of that machine, a lower one once they own one. Research gates are kept.
// The Integrated Farm and the perfume manual are deliberately left alone.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace AgriStockOdds
{
    internal static class StockRules
    {
        internal static bool Offered(bool owns, int unownedChance, int ownedChance, Func<int, bool> roll)
        {
            int chance = owns ? ownedChance : unownedChance;
            if (chance <= 0) return false;
            return chance >= 100 || roll(chance);
        }
    }

    internal static class Faults
    {
        internal static Action<string> Log = message => UnityEngine.Debug.LogWarning(message);
        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static void Report(string where, Exception error)
        {
            try
            {
                if (!reported.Add(where)) return;
                string text;
                try { text = error.GetType().Name + ": " + error.Message; }
                catch { text = "an error that cannot be shown"; }
                Log(("[Agri Stock Odds] a fault in " + where + ". " + text)
                    .Replace('\r', ' ').Replace('\n', ' '));
            }
            catch { }
        }
    }

    public class StockOddsMod : IMod
    {
        internal static ConfigEntry<int> UnownedChance;
        internal static ConfigEntry<int> OwnedChance;

        // Machine id -> research upgrade that must be unlocked (null = none).
        internal static readonly Dictionary<string, string> JacksonMachines = new Dictionary<string, string>
        {
            { "agri_food_packer", null },
            { "agri_fiber_processor", null },
            { "agri_cooking_machine", "agri_expansion.COOKING" },
            { "agri_takeaway_terminal", "agri_expansion.COOKING" },
            { "agri_scent_dryer", "agri_expansion.PERFUMERY" },
            { "agri_scent_softener", "agri_expansion.PERFUMERY" },
            { "agri_scent_extractor", "agri_expansion.PERFUMERY" },
            { "agri_scent_blender", "agri_expansion.PERFUMERY" },
        };

        // Sold by the Hypertech Smuggler; Agri Expansion adds it on every visit.
        internal const string SmugglerMachine = "agri_super_hydroponic";

        // Set while this mod places an owned-machine offer, so the gate lets it through.
        internal static bool Placing;

        public void Init(ModManifest manifest)
        {
            try
            {
                ModConfig config = ModConfig.For(manifest.ID);
                UnownedChance = config.Define("unowned_chance", 25, "Machine stock",
                    () => "Chance when you own none (%)", 0, 100,
                    () => "Per machine and visit, for Agri Expansion machines at Dr. Jackson and the Hypertech Smuggler while you own none of that machine. Agri Expansion alone uses 100%.");
                OwnedChance = config.Define("owned_chance", 5, "Machine stock",
                    () => "Chance when you already own one (%)", 0, 100,
                    () => "Per machine and visit, once you own that machine. Agri Expansion alone never offers it again at Dr. Jackson, and always at the smuggler.");

                LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Agri Stock Odds", Faults.Report);
            }
            catch (Exception error) { Faults.Report("initialising", error); }
        }

        public void OnEnable()
        {
            try
            {
                ModHook.OnPlaceInventorInventoryItemEarly -= OfferOwnedMachines;
                ModHook.OnPlaceInventorInventoryItemEarly += OfferOwnedMachines;
            }
            catch (Exception error) { Faults.Report("enabling", error); }
        }

        public void OnDisable()
        {
            try { ModHook.OnPlaceInventorInventoryItemEarly -= OfferOwnedMachines; }
            catch (Exception error) { Faults.Report("disabling", error); }
        }

        internal static bool IsManaged(string id) => id == SmugglerMachine || JacksonMachines.ContainsKey(id);

        // Mirrors Agri Expansion's own ownership check (AgriDoctorStock.PlayerOwns).
        internal static bool PlayerOwns(string id)
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || EmporiumEntry.Instance == null) return false;
            foreach (GameItem item in store.FindAllItem(true))
            {
                if (item != null && item.identifier == id && GeneralHelper.IsItemEffectivelyOwned(item)) return true;
            }
            return false;
        }

        internal static bool Roll(bool owns)
        {
            return StockRules.Offered(owns, UnownedChance == null ? 25 : UnownedChance.Value,
                OwnedChance == null ? 5 : OwnedChance.Value, RNG.Roll);
        }

        // Agri Expansion skips machines the player owns, so offer those here.
        // Unowned machines are still offered by Agri Expansion and pass the gate below.
        private static void OfferOwnedMachines(List<GameItem> ownedItems)
        {
            try
            {
                if (!PlayerStore.HasInstance) return;
                foreach (KeyValuePair<string, string> machine in JacksonMachines)
                {
                    if (machine.Value != null && !NetworkUpgrade.IsUnlocked(machine.Value)) continue;
                    if (!PlayerOwns(machine.Key)) continue;
                    if (!Roll(true)) continue;
                    Placing = true;
                    try
                    {
                        PlayerStore.Instance.TryAddDirectSellingItemToTable(machine.Key, false, false, false, 0);
                    }
                    finally
                    {
                        Placing = false;
                    }
                }
            }
            // Includes a failed placement called by this hook: stop this visit's loop.
            catch (Exception error) { Faults.Report("offering owned machines", error); }
        }
    }

    // The single place every counter offer goes through. For a managed machine
    // offered for sale (not handed back to the player as owned), roll the chance;
    // on a miss, skip the original, report "not placed" and destroy the spare item.
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.TryAddDirectSellingItemToTable),
        new Type[] { typeof(GameItem), typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
    internal static class MachineStockGatePatch
    {
        private static bool Prefix(GameItem gameItem, bool isOwend, ref bool __result)
        {
            try
            {
                if (StockOddsMod.Placing || gameItem == null || isOwend) return true;
                string id = gameItem.identifier;
                if (id == null || !StockOddsMod.IsManaged(id)) return true;

                bool offered = StockOddsMod.Roll(StockOddsMod.PlayerOwns(id));
                if (offered) return true;

                // The roll said no. If removing the spare item fails, the offer is still
                // withheld; the fault is reported and the item is left to the game.
                try { GraphUtils.DestroyIfUnplaced(gameItem, "Agri Stock Odds: machine not offered this visit"); }
                catch (Exception error) { Faults.Report("removing a machine that is not offered", error); }
                __result = false;
                return false;
            }
            catch (Exception error)
            {
                Faults.Report("checking a machine offer", error);
                return true;
            }
        }
    }
}

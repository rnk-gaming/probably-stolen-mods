// Farm Story Tweaks: replaces Probably a Farm Story's 100% vendor stock for its machines
// with a chance roll per machine and visit: one chance while the player owns
// none of that machine, a lower one once they own one. Research gates are kept.
// The Integrated Farm and the perfume manual are deliberately left alone.
using System;
using System.Collections.Generic;
using HarmonyLib;
using LocalMods;

namespace FarmStoryTweaks
{
    // Every text a player sees is a line in dist/Localization/en.csv with one of these keys.
    internal static class TextKeys
    {

        internal const string OptionsTitle = "options_title";
        internal const string Section = "section_machine_stock";
        internal const string UnownedChance = "option_unowned_chance";
        internal const string UnownedChanceTip = "option_unowned_chance_desc";
        internal const string OwnedChance = "option_owned_chance";
        internal const string OwnedChanceTip = "option_owned_chance_desc";
    }

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
                Log(("[Farm Story Tweaks] a fault in " + where + ". " + text)
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

        // Sold by the Hypertech Smuggler; Probably a Farm Story adds it on every visit.
        internal const string SmugglerMachine = "agri_super_hydroponic";

        // Set while this mod places an owned-machine offer, so the gate lets it through.
        internal static bool Placing;

        public void Init(ModManifest manifest)
        {
            try
            {
                TextTable.Start(manifest.ID, Faults.Report);
                // Mods it does not go together with: add one list line and two rows in en.csv.
                Incompatible.Register(manifest.ID, "[Farm Story Tweaks]", new IncompatibleMod[]
                {
                    // No confirmed conflicts yet.
                }, NoteFault);
                ModConfig config = ModConfig.For(manifest.ID);
                string section = TextTable.Early(TextKeys.Section);
                UnownedChance = config.Define("unowned_chance", 25, section,
                    TextTable.Later(TextKeys.UnownedChance), 0, 100, TextTable.Later(TextKeys.UnownedChanceTip));
                OwnedChance = config.Define("owned_chance", 5, section,
                    TextTable.Later(TextKeys.OwnedChance), 0, 100, TextTable.Later(TextKeys.OwnedChanceTip));

                OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Faults.Report);
            }
            catch (Exception error) { Faults.Report("initialising", error); }
        }

        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Farm Story Tweaks] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
            try
            {
                ModHook.OnPlaceInventorInventoryItemEarly -= OfferOwnedMachines;
                ModHook.OnPlaceInventorInventoryItemEarly += OfferOwnedMachines;
            }
            catch (Exception error) { Faults.Report("enabling", error); }
        }

        public void OnDisable()
        {
            Incompatible.Disable();
            try { ModHook.OnPlaceInventorInventoryItemEarly -= OfferOwnedMachines; }
            catch (Exception error) { Faults.Report("disabling", error); }
        }

        internal static bool IsManaged(string id) => id == SmugglerMachine || JacksonMachines.ContainsKey(id);

        // Mirrors Probably a Farm Story's own ownership check (AgriDoctorStock.PlayerOwns).
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

        // Probably a Farm Story skips machines the player owns, so offer those here.
        // Unowned machines are still offered by Probably a Farm Story and pass the gate below.
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
                // The reason is written to the log only, so it is not in the text table.
                try { GraphUtils.DestroyIfUnplaced(gameItem, "Farm Story Tweaks: machine not offered this visit"); }
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

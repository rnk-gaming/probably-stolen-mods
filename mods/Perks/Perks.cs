using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LocalMods;
using UnityEngine;

[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]

namespace Perks
{
    public sealed class PerkMod : IMod
    {
        internal const string Id = "rnk.perks", HarmonyId = "mod.rnk.perks";
        internal static ConfigEntry<bool> Enabled;
        internal static readonly Dictionary<string, ConfigEntry<bool>> Offers = new Dictionary<string, ConfigEntry<bool>>();
        public void Init(ModManifest manifest)
        {
            TextTable.Start(manifest.ID, Runtime.Fault);
            // Mods it does not go together with: add one list line and two rows in en.csv.
            Incompatible.Register(manifest.ID, "[Perks]", new IncompatibleMod[]
            {
                new IncompatibleMod("xiaowo_trade_perks", "trade_perks"),
            }, NoteFault);
            Runtime.Guard("settings", () =>
            {
                var config = ModConfig.For(manifest.ID);
                string section = TextTable.Early(TextKeys.Section);
                Enabled = config.Define("enabled", true, section, TextTable.Later(TextKeys.Enabled), TextTable.Later(TextKeys.EnabledTip));
                foreach (Definition perk in Catalogue.All)
                {
                    Definition entry = perk;
                    Offers[entry.Slug] = config.Define("offer_" + entry.Slug, true, section, () => TextTable.Get(TextKeys.Offer, entry.Name()),
                        TextTable.Later(TextKeys.OfferTip));
                }
                OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Runtime.Fault);
            });
        }
        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Perks] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
            Runtime.Guard("enabling", () =>
            {
                Runtime.Reset();
                Runtime.Registered.Clear();
                // A second call must not leave two copies of a patch on a method.
                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                Bindings.Enable();
                SummaryRow.Enable();
                ModHelper.RemoveStartingPerks(Id);
                foreach (Definition entry in Catalogue.All) Runtime.Register(entry);
                ModHook.OnGenerateCustomerLate -= Customers.Generate; ModHook.OnGenerateCustomerLate += Customers.Generate;
                ModHook.OnShutterClosedLate -= Night.Closed; ModHook.OnShutterClosedLate += Night.Closed;
                ModHook.OnHandlingNightlyServicesEarly -= Night.Services; ModHook.OnHandlingNightlyServicesEarly += Night.Services;
                ModHook.OnGameLoadedEarly -= Runtime.Loaded; ModHook.OnGameLoadedEarly += Runtime.Loaded;
                Runtime.Summary();
            });
        }
        public void OnDisable()
        {
            Incompatible.Disable();
            Runtime.Guard("disabling", () =>
            {
                ModHook.OnGenerateCustomerLate -= Customers.Generate;
                ModHook.OnShutterClosedLate -= Night.Closed;
                ModHook.OnHandlingNightlyServicesEarly -= Night.Services;
                ModHook.OnGameLoadedEarly -= Runtime.Loaded;
                ModHelper.RemoveStartingPerks(Id);
                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                Runtime.Reset();
                Runtime.Registered.Clear();
            });
        }
    }
    // Every player text is a line in dist/Localization/en.csv.
    internal static class TextKeys
    {

        internal const string OptionsTitle = "options_title";
        internal const string Section = "section_perks";
        internal const string Enabled = "option_enabled";
        internal const string EnabledTip = "option_enabled_desc";
        internal const string Offer = "option_offer";
        internal const string OfferTip = "option_offer_desc";
        internal const string BrokenDescription = "perk_broken_desc";
        internal const string NightOverheads = "night_overheads";
        internal const string MasterValuerName = "perk_master_valuer_name";
        internal const string MasterValuerDescription = "perk_master_valuer_desc";
        internal const string AppraisalAdvertName = "perk_appraisal_advert_name";
        internal const string AppraisalAdvertDescription = "perk_appraisal_advert_desc";
        internal const string CoolingOffDealName = "perk_cooling_off_deal_name";
        internal const string CoolingOffDealDescription = "perk_cooling_off_deal_desc";
        internal const string LightsOutPremiumName = "perk_lights_out_premium_name";
        internal const string LightsOutPremiumDescription = "perk_lights_out_premium_desc";
        internal const string PrivateViewingName = "perk_private_viewing_name";
        internal const string PrivateViewingDescription = "perk_private_viewing_desc";
        internal const string TrustedWholesalerName = "perk_trusted_wholesaler_name";
        internal const string TrustedWholesalerDescription = "perk_trusted_wholesaler_desc";
        internal const string GoodNameName = "perk_good_name_name";
        internal const string GoodNameDescription = "perk_good_name_desc";
        internal const string HardBargainerName = "perk_hard_bargainer_name";
        internal const string HardBargainerDescription = "perk_hard_bargainer_desc";
        internal const string NightOwlName = "perk_night_owl_name";
        internal const string NightOwlDescription = "perk_night_owl_desc";
        internal const string WindowDresserName = "perk_window_dresser_name";
        internal const string WindowDresserDescription = "perk_window_dresser_desc";
        internal const string ThoroughSearchName = "perk_thorough_search_name";
        internal const string ThoroughSearchDescription = "perk_thorough_search_desc";
        internal const string FriendOnTheForceName = "perk_friend_on_the_force_name";
        internal const string FriendOnTheForceDescription = "perk_friend_on_the_force_desc";
        internal const string WealthyVisitorsName = "perk_wealthy_visitors_name";
        internal const string WealthyVisitorsDescription = "perk_wealthy_visitors_desc";
        internal const string BriskTradeName = "perk_brisk_trade_name";
        internal const string BriskTradeDescription = "perk_brisk_trade_desc";
        internal const string JackOfAllTradesName = "perk_jack_of_all_trades_name";
        internal const string JackOfAllTradesDescription = "perk_jack_of_all_trades_desc";
        internal const string HeadStartName = "perk_head_start_name";
        internal const string HeadStartDescription = "perk_head_start_desc";
        internal const string TechDealerName = "perk_tech_dealer_name";
        internal const string TechDealerDescription = "perk_tech_dealer_desc";
        internal const string BargainBinName = "perk_bargain_bin_name";
        internal const string BargainBinDescription = "perk_bargain_bin_desc";
        internal const string BulkyGoodsName = "perk_bulky_goods_name";
        internal const string BulkyGoodsDescription = "perk_bulky_goods_desc";
        internal const string PremiumRetailerName = "perk_premium_retailer_name";
        internal const string PremiumRetailerDescription = "perk_premium_retailer_desc";
        internal const string MarkedShopName = "perk_marked_shop_name";
        internal const string MarkedShopDescription = "perk_marked_shop_desc";
        internal const string ShabbyFrontName = "perk_shabby_front_name";
        internal const string ShabbyFrontDescription = "perk_shabby_front_desc";
        internal const string TightRegularsName = "perk_tight_regulars_name";
        internal const string TightRegularsDescription = "perk_tight_regulars_desc";
        internal const string CarelessDiggerName = "perk_careless_digger_name";
        internal const string CarelessDiggerDescription = "perk_careless_digger_desc";
        internal const string AugBountyName = "perk_aug_bounty_name";
        internal const string AugBountyDescription = "perk_aug_bounty_desc";
        internal const string SteepOverheadsName = "perk_steep_overheads_name";
        internal const string SteepOverheadsDescription = "perk_steep_overheads_desc";
        internal const string LeanSuppliersName = "perk_lean_suppliers_name";
        internal const string LeanSuppliersDescription = "perk_lean_suppliers_desc";
        internal const string SkimmedTillName = "perk_skimmed_till_name";
        internal const string SkimmedTillDescription = "perk_skimmed_till_desc";
        internal const string GreedyWholesalerName = "perk_greedy_wholesaler_name";
        internal const string GreedyWholesalerDescription = "perk_greedy_wholesaler_desc";
    }
    internal sealed class Definition
    {
        internal readonly string Slug, Id, NameKey, DescriptionKey;
        internal readonly int Cost, Slots;
        internal readonly StartingPerk.StartingPerkType Type;
        internal readonly string[] Incompatible;
        internal Definition(string slug, string nameKey, int cost, StartingPerk.StartingPerkType type, string descriptionKey, string[] incompatible = null, int slots = 0)
        { Slug = slug; Id = "rnk.perks." + slug; NameKey = nameKey; Cost = cost; Type = type; DescriptionKey = descriptionKey; Incompatible = incompatible ?? Array.Empty<string>(); Slots = slots; }
        internal string Name() => TextTable.Get(NameKey);
        internal string Description(bool broken) => broken ? TextTable.Get(TextKeys.BrokenDescription, TextTable.Get(DescriptionKey)) : TextTable.Get(DescriptionKey);
    }
    internal static class Catalogue
    {
        private const StartingPerk.StartingPerkType P = StartingPerk.StartingPerkType.POSITIVE, N = StartingPerk.StartingPerkType.NEGATIVE, Z = StartingPerk.StartingPerkType.NEUTRAL;
        internal static readonly Definition[] All =
        {
            new Definition("master_valuer", TextKeys.MasterValuerName, 2, P, TextKeys.MasterValuerDescription),
            new Definition("appraisal_advert", TextKeys.AppraisalAdvertName, 1, P, TextKeys.AppraisalAdvertDescription),
            new Definition("cooling_off_deal", TextKeys.CoolingOffDealName, 1, P, TextKeys.CoolingOffDealDescription),
            new Definition("lights_out_premium", TextKeys.LightsOutPremiumName, 1, P, TextKeys.LightsOutPremiumDescription),
            new Definition("private_viewing", TextKeys.PrivateViewingName, 2, P, TextKeys.PrivateViewingDescription),
            new Definition("trusted_wholesaler", TextKeys.TrustedWholesalerName, 2, P, TextKeys.TrustedWholesalerDescription, new[] { "rnk.perks.greedy_wholesaler" }),
            new Definition("good_name", TextKeys.GoodNameName, 1, P, TextKeys.GoodNameDescription),
            new Definition("hard_bargainer", TextKeys.HardBargainerName, 2, P, TextKeys.HardBargainerDescription),
            new Definition("night_owl", TextKeys.NightOwlName, 3, P, TextKeys.NightOwlDescription),
            new Definition("window_dresser", TextKeys.WindowDresserName, 2, P, TextKeys.WindowDresserDescription),
            new Definition("thorough_search", TextKeys.ThoroughSearchName, 2, P, TextKeys.ThoroughSearchDescription),
            new Definition("friend_on_the_force", TextKeys.FriendOnTheForceName, 3, P, TextKeys.FriendOnTheForceDescription),
            new Definition("wealthy_visitors", TextKeys.WealthyVisitorsName, 3, P, TextKeys.WealthyVisitorsDescription),
            new Definition("brisk_trade", TextKeys.BriskTradeName, 3, P, TextKeys.BriskTradeDescription),
            new Definition("jack_of_all_trades", TextKeys.JackOfAllTradesName, 3, P, TextKeys.JackOfAllTradesDescription, slots: Rules.ExtraSlots),
            new Definition("head_start", TextKeys.HeadStartName, -12, Z, TextKeys.HeadStartDescription),
            new Definition("tech_dealer", TextKeys.TechDealerName, 1, P, TextKeys.TechDealerDescription, new[] { "rnk.perks.bargain_bin", "rnk.perks.bulky_goods" }),
            new Definition("bargain_bin", TextKeys.BargainBinName, 1, P, TextKeys.BargainBinDescription, new[] { "rnk.perks.tech_dealer", "rnk.perks.bulky_goods" }),
            new Definition("bulky_goods", TextKeys.BulkyGoodsName, 1, P, TextKeys.BulkyGoodsDescription, new[] { "rnk.perks.tech_dealer", "rnk.perks.bargain_bin" }),
            new Definition("premium_retailer", TextKeys.PremiumRetailerName, 0, Z, TextKeys.PremiumRetailerDescription),
            new Definition("marked_shop", TextKeys.MarkedShopName, -2, N, TextKeys.MarkedShopDescription),
            new Definition("shabby_front", TextKeys.ShabbyFrontName, -2, N, TextKeys.ShabbyFrontDescription),
            new Definition("tight_regulars", TextKeys.TightRegularsName, -1, N, TextKeys.TightRegularsDescription),
            new Definition("careless_digger", TextKeys.CarelessDiggerName, -1, N, TextKeys.CarelessDiggerDescription, new[] { "careful_scavenger" }),
            new Definition("aug_bounty", TextKeys.AugBountyName, -2, N, TextKeys.AugBountyDescription),
            new Definition("steep_overheads", TextKeys.SteepOverheadsName, -1, N, TextKeys.SteepOverheadsDescription),
            new Definition("lean_suppliers", TextKeys.LeanSuppliersName, -2, N, TextKeys.LeanSuppliersDescription),
            new Definition("skimmed_till", TextKeys.SkimmedTillName, -1, N, TextKeys.SkimmedTillDescription),
            new Definition("greedy_wholesaler", TextKeys.GreedyWholesalerName, -2, N, TextKeys.GreedyWholesalerDescription, new[] { "rnk.perks.trusted_wholesaler" })
        };
    }
    internal static class Runtime
    {
        internal static Func<string, bool> Selected = id => ModHelper.IsStartingPerkActive(id);
        internal static Func<bool> Master = () => PerkMod.Enabled == null || PerkMod.Enabled.Value;
        internal static Func<int> Today = () => StoreStation.GetDayCounter();
        internal static Func<string, string> Read = key => ModHelper.GetModData(PerkMod.Id, key, "");
        internal static Action<string, string> Write = (key, text) => ModHelper.SetModData(PerkMod.Id, key, text);
        internal static Action<string> Log = text => Debug.Log(text);
        internal static Func<StoreClient> Counter = () => PlayerStore.Instance == null || PlayerStore.Instance.currentClientInstance == null ? null : PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
        internal static Func<bool> InShop = () => MapUIManager.Instance == null || !MapUIManager.Instance.isOutside;
        internal static Func<int> Cash = () => PlayerStore.Instance.playerCash;
        internal static Action<int> SetCash = value => PlayerStore.Instance.playerCash = value;
        internal static Action<int> AppraisalCash = value => PlayerStore.Instance.ModCash(value, false);
        internal static Func<int> StartingDay = () => PlayerStore.Instance.startingDay;
        internal static Func<string> RunId = () => PlayerStore.Instance.runID;
        internal static Func<string, int> VisitCount = id => StoreClientManager.GetCustomerTotalVisitedCount(id);
        internal static Func<int, bool> Roll = percent => RNG.Roll(percent);
        internal static readonly Dictionary<string, string> Unavailable = new Dictionary<string, string>();
        internal static readonly HashSet<string> Registered = new HashSet<string>();
        private static readonly HashSet<string> reported = new HashSet<string>();
        internal static bool Active(string slug) => Master() && !Unavailable.ContainsKey(slug) && Selected("rnk.perks." + slug);
        internal static int Week => Rules.Week(Today(), StartingDay());
        internal static bool Ordinary(StoreClient client) => client != null && Rules.Ordinary(client.CanReturn, client.isSecurity, client.isWanted, client.eventSourceId, client.identifier);
        internal static bool Buyer(StoreClient client) => Ordinary(client) && client.clientIntent == StoreClient.ClientIntent.BUY;
        internal static bool Supplier(StoreClient client) => Ordinary(client) && client.clientIntent == StoreClient.ClientIntent.SELL;
        internal static bool Stored(string key, int value) => int.TryParse(Read(key), out int stored) && stored == value;
        internal static void Remember(string key, int value) => Write(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        internal static void Say(string text) { try { Log("[Perks] " + text); } catch { } }
        internal static void Report(string key, string text) { try { if (reported.Add(key)) Say(text); } catch { } }
        internal static void Fault(string where, Exception error)
        {
            try { Report("fault:" + where, "fault in " + where + "; the game's own behaviour stands. " + error.GetType().Name + ": " + error.Message); } catch { }
        }
        internal static void Guard(string where, Action action) { try { action(); } catch (Exception error) { Fault(where, error); } }
        // What the new-game screen offers: not with the master switch off, not a perk whose
        // offer is switched off, and not a perk that cannot work with this game version (a
        // drawback that does nothing would hand out points for free).
        internal static Func<string, bool> Offered = slug => !PerkMod.Offers.TryGetValue(slug, out var option) || option.Value;
        internal static int Registrations;
        internal static void Register(Definition entry)
        {
            if (!Master() || !Offered(entry.Slug) || Unavailable.ContainsKey(entry.Slug)) return;
            Add(entry);
        }
        // A loaded run shows each of its perks by the name and text of the registered perk.
        // A perk the run has is therefore registered when the run is loaded, also when it is
        // not offered; it then stays on the new-game screen until the game is restarted. A
        // perk that does nothing there (it cannot work, or the mod is switched off) is
        // registered without cost and without slots, so that it gives no points for nothing.
        internal static void Loaded()
        {
            Guard("resetting for a loaded run", Reset);
            Guard("naming a loaded run's perks", () => { foreach (Definition entry in Catalogue.All) if (Selected(entry.Id)) Add(entry); });
        }
        // A white glyph per perk, 48 by 48, files of our own in Textures/Items (drawn by art/icons.py).
        // Without a key the game draws its default perk icon, which covers the neighbours.
        internal static string Icon(string slug) => PerkMod.Id + ":rnk_perks_" + slug;
        private static void Add(Definition entry)
        {
            if (Registered.Contains(entry.Id)) return;
            bool broken = Unavailable.ContainsKey(entry.Slug), idle = broken || !Master();
            ModHelper.AddStartingPerk(PerkMod.Id, entry.Id, idle ? 0 : entry.Cost, idle ? StartingPerk.StartingPerkType.NEUTRAL : entry.Type, () => entry.Name(),
                () => entry.Description(broken), Icon(entry.Slug), entry.Incompatible, idle ? 0 : entry.Slots, false);
            Registrations++;
            Registered.Add(entry.Id);
        }
        internal static void Summary() => Say(Registered.Count + " perks registered; " + Unavailable.Count + " not working.");
        internal static void Reset() { Trade.Reset(); Customers.Reset(); }
    }
}

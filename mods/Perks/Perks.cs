using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyVersion("0.2.2.0")]
[assembly: AssemblyFileVersion("0.2.2.0")]

namespace Perks
{
    public sealed class PerkMod : IMod
    {
        internal const string Id = "rnk.perks", HarmonyId = "mod.rnk.perks";
        internal static ConfigEntry<bool> Enabled;
        internal static readonly Dictionary<string, ConfigEntry<bool>> Offers = new Dictionary<string, ConfigEntry<bool>>();
        public void Init(ModManifest manifest)
        {
            Runtime.Guard("settings", () =>
            {
                var config = ModConfig.For(manifest.ID);
                Enabled = config.Define("enabled", true, "Perks", () => Runtime.Text("option_enabled", "Enable Perks"), () => Runtime.Text("option_enabled_desc", "Enable the effects of these starting perks. Restart the game after changes."));
                foreach (Definition perk in Catalogue.All)
                {
                    Definition entry = perk;
                    Offers[entry.Slug] = config.Define("offer_" + entry.Slug, true, "Perks", () => Runtime.TextWith("option_offer", "Offer {0} in a new game", Runtime.Text("perk_" + entry.Slug + "_name", entry.Name)),
                        () => Runtime.Text("option_offer_desc", "Restart the game after changes. A run that already has this perk keeps its effect."));
                }
                LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Perks", Runtime.Fault);
            });
        }
        public void OnEnable()
        {
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
    internal sealed class Definition
    {
        internal readonly string Slug, Id, Name, Description;
        internal readonly int Cost, Slots;
        internal readonly StartingPerk.StartingPerkType Type;
        internal readonly string[] Incompatible;
        internal Definition(string slug, string name, int cost, StartingPerk.StartingPerkType type, string description, string[] incompatible = null, int slots = 0)
        { Slug = slug; Id = "rnk.perks." + slug; Name = name; Cost = cost; Type = type; Description = description; Incompatible = incompatible ?? Array.Empty<string>(); Slots = slots; }
    }
    internal static class Catalogue
    {
        private const StartingPerk.StartingPerkType P = StartingPerk.StartingPerkType.POSITIVE, N = StartingPerk.StartingPerkType.NEGATIVE, Z = StartingPerk.StartingPerkType.NEUTRAL;
        internal static readonly Definition[] All =
        {
            new Definition("master_valuer", "Master Valuer", 2, P, "Appraisals pay you 25% more."),
            new Definition("appraisal_advert", "Appraisal Advert", 1, P, "With a certificate in the showcase, each day has a 25% chance of an extra appraisal customer."),
            new Definition("cooling_off_deal", "Cooling-Off Deal", 1, P, "Stolen goods you buy arrive 15% cooler."),
            new Definition("lights_out_premium", "Lights-Out Premium", 1, P, "Customers pay 15% more during a blackout."),
            new Definition("private_viewing", "Private Viewing", 2, P, "A luxury buyer visits once a week with twice the usual budget, and pays 10% under your price."),
            new Definition("trusted_wholesaler", "Trusted Wholesaler", 2, P, "Suppliers charge 10% less. Rules out Greedy Wholesaler.", new[] { "rnk.perks.greedy_wholesaler" }),
            new Definition("good_name", "Good Name", 1, P, "Trades earn 15% more reputation."),
            new Definition("hard_bargainer", "Hard Bargainer", 2, P, "Asking for 10% or less always works, unless the customer never haggles."),
            new Definition("night_owl", "Night Owl", 3, P, "Three outings a night, and no lost customer the morning after."),
            new Definition("window_dresser", "Window Dresser", 2, P, "A stocked showcase makes the shop 50 more attractive."),
            new Definition("thorough_search", "Thorough Search", 2, P, "Each find at the Dumping Grounds has a 25% chance of a second item."),
            new Definition("friend_on_the_force", "Friend on the Force", 3, P, "Routine inspections come half as often."),
            new Definition("wealthy_visitors", "Wealthy Visitors", 3, P, "Everyone who comes in has 25% more to spend."),
            new Definition("brisk_trade", "Brisk Trade", 3, P, "Your first three sales each day pay 10% extra, up to 25 each."),
            new Definition("jack_of_all_trades", "Jack of All Trades", 3, P, "Free build: room for 12 more perks.", slots: Rules.ExtraSlots),
            new Definition("head_start", "Head Start", -12, Z, "Free build: 12 extra perk points."),
            new Definition("tech_dealer", "Tech Dealer", 1, P, "Machines, modules and nodes sell for 15% more. Everything else sells for 10% less. Not for filled containers. Rules out Bargain Bin and Bulky Goods.", new[] { "rnk.perks.bargain_bin", "rnk.perks.bulky_goods" }),
            new Definition("bargain_bin", "Bargain Bin", 1, P, "Goods worth 50 or less sell for 15% more. Goods worth 200 or more sell for 10% less. Not for filled containers. Rules out Tech Dealer and Bulky Goods.", new[] { "rnk.perks.tech_dealer", "rnk.perks.bulky_goods" }),
            new Definition("bulky_goods", "Bulky Goods", 1, P, "Goods of 6 squares or more sell for 15% more. Goods of 2 squares or fewer sell for 10% less. Not for filled containers. Rules out Tech Dealer and Bargain Bin.", new[] { "rnk.perks.tech_dealer", "rnk.perks.bargain_bin" }),
            new Definition("premium_retailer", "Premium Retailer", 0, Z, "You sell for 5% more, and suppliers charge you 10% more."),
            new Definition("marked_shop", "Marked Shop", -2, N, "Stolen goods come 25% hotter, and inspections come 25% sooner."),
            new Definition("shabby_front", "Shabby Front", -2, N, "The shop is 150 less attractive, and renovations cost 25% more."),
            new Definition("tight_regulars", "Tight Regulars", -1, N, "Returning customers have 15% less to spend."),
            new Definition("careless_digger", "Careless Digger", -1, N, "Wounds at the Dumping Grounds are 5 points more likely. Rules out Careful Scavenger.", new[] { "careful_scavenger" }),
            new Definition("aug_bounty", "Aug Bounty", -2, N, "One Aug in four brings a second."),
            new Definition("steep_overheads", "Steep Overheads", -1, N, "Power, cleaning, insurance and referrals cost 25% more."),
            new Definition("lean_suppliers", "Lean Suppliers", -2, N, "Suppliers bring a quarter less."),
            new Definition("skimmed_till", "Skimmed Till", -1, N, "5% of every sale goes missing, at most 10 a day."),
            new Definition("greedy_wholesaler", "Greedy Wholesaler", -2, N, "Suppliers charge 10% more. Rules out Trusted Wholesaler.", new[] { "rnk.perks.trusted_wholesaler" })
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
        // Texts come from Localization/<language>.csv when the game has loaded it; the English
        // text in the code stands when a key is missing, so the mod reads the same without the file.
        internal static Func<string, string> Localized = key => ModHelper.GetLocalized(PerkMod.Id, key);
        internal static string Text(string key, string fallback)
        {
            try { string text = Localized(key); return string.IsNullOrEmpty(text) || text == key ? fallback : text; }
            catch { return fallback; }
        }
        internal static string TextWith(string key, string fallback, object value)
        {
            try { return string.Format(Text(key, fallback), value); } catch { return string.Format(fallback, value); }
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
            ModHelper.AddStartingPerk(PerkMod.Id, entry.Id, idle ? 0 : entry.Cost, idle ? StartingPerk.StartingPerkType.NEUTRAL : entry.Type, () => Text("perk_" + entry.Slug + "_name", entry.Name),
                () => Text("perk_" + entry.Slug + "_desc", entry.Description) + (broken ? Text("perk_not_working", " (not working with this game version)") : ""), Icon(entry.Slug), entry.Incompatible, idle ? 0 : entry.Slots, false);
            Registrations++;
            Registered.Add(entry.Id);
        }
        internal static void Summary() => Say(Registered.Count + " perks registered; " + Unavailable.Count + " not working.");
        internal static void Reset() { Trade.Reset(); Customers.Reset(); }
    }
}

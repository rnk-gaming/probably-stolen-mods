using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Perks
{
    internal sealed class Target
    {
        internal Type Type, Result, Patch;
        internal string Name, Prefix, Postfix, Finalizer;
        internal string[] Perks;
        internal Type[] Args;
        internal bool Static;
        internal MethodInfo Method => Bindings.LookupMethod(Type, Name, Args);
    }
    internal static class Bindings
    {
        internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static Func<MemberInfo, bool> Accept = member => true;
        internal static Func<MethodInfo> FindPrice = () => typeof(GameItem).GetMethod("GetNegociatedValue", Any, null, Type.EmptyTypes, null);
        private static readonly string[] Sales = { "lights_out_premium", "private_viewing", "trusted_wholesaler", "greedy_wholesaler", "tech_dealer", "bargain_bin", "bulky_goods", "premium_retailer" };
        private static Target T(Type type, string name, Type result, bool isStatic, Type patch, string pre, string post, string final, string[] perks, params Type[] args)
            => new Target { Type = type, Name = name, Result = result, Static = isStatic, Patch = patch, Prefix = pre, Postfix = post, Finalizer = final, Perks = perks, Args = args };
        internal static readonly Target[] Targets =
        {
            T(typeof(GameItem), "GetNegociatedValue", typeof(long), false, typeof(PricePatch), null, "Postfix", null, Sales),
            T(typeof(PlayerStore), "SellItem", typeof(void), false, typeof(SalePatch), "Prefix", null, "Finalizer", Sales.Concat(new[] { "brisk_trade", "skimmed_till" }).ToArray(), typeof(GameItem)),
            T(typeof(PlayerStore), "BuyItem", typeof(void), false, typeof(BuyPatch), "Prefix", null, "Finalizer", Sales, typeof(GameItem)),
            T(typeof(NegociationUIManager), "SellMultiple", typeof(void), false, typeof(BulkSalePatch), "Prefix", null, "Finalizer", new[] { "brisk_trade", "skimmed_till" }, typeof(List<GameItem>), typeof(bool)),
            T(typeof(StoreClientManager), "TrackClient", typeof(void), false, typeof(BudgetPatch), "Prefix", null, null, new[] { "wealthy_visitors", "tight_regulars" }, typeof(StoreClient)),
            T(typeof(AppraisalServiceHelper), "GiveAppraisalFee", typeof(void), true, typeof(AppraisalPatch), "Prefix", "Postfix", null, new[] { "master_valuer" }, typeof(int), typeof(int), typeof(int)),
            T(typeof(PlayerStore), "OnItemBought", typeof(void), false, typeof(CoolingPatch), null, "Postfix", null, new[] { "cooling_off_deal" }, typeof(GameItem), typeof(int)),
            T(typeof(StoreReputation), "OnItemTraded", typeof(void), true, typeof(ReputationPatch), "Prefix", "Postfix", null, new[] { "good_name" }, typeof(string), typeof(int), typeof(int), typeof(StoreClient), typeof(bool), typeof(GameItem)),
            T(typeof(BargainUIManager), "GetBargainSuccessChance", typeof(int), false, typeof(BargainPatch), null, "Postfix", null, new[] { "hard_bargainer" }, typeof(BargainType), typeof(int)),
            T(typeof(StartingPerk), "IsShortSleeper", typeof(bool), true, typeof(SleeperPatch), null, "Postfix", null, new[] { "night_owl" }),
            T(typeof(PlayerStore), "GetCurrentStoreAttractiveness", typeof(int), false, typeof(AttractivenessPatch), null, "Postfix", null, new[] { "shabby_front", "window_dresser" }),
            T(typeof(NetworkUpgrade), "GetCost", typeof(int), false, typeof(RenovationPatch), null, "Postfix", null, new[] { "shabby_front" }),
            T(typeof(ScavHelper), "GetRandomScavengedItem", typeof(List<GameItem>), true, typeof(SearchPatch), null, "Postfix", null, new[] { "thorough_search" }, typeof(GameItem).MakeByRefType()),
            T(typeof(StolenHelper), "InitStolenItem", typeof(void), true, typeof(StolenPatch), "Prefix", "Postfix", null, new[] { "marked_shop" }, typeof(GameItem), typeof(int)),
            T(typeof(StoreClientManager), "ComputeInspectionCooldownRange", typeof(void), false, typeof(CooldownPatch), null, "Postfix", null, new[] { "friend_on_the_force", "marked_shop" }, typeof(int).MakeByRefType(), typeof(int).MakeByRefType()),
            T(typeof(ScavHelper), "GetMinorWoundChance", typeof(float), true, typeof(WoundPatch), null, "Postfix", null, new[] { "careless_digger" }),
            T(typeof(ScavHelper), "GetMajorWoundChance", typeof(float), true, typeof(WoundPatch), null, "Postfix", null, new[] { "careless_digger" }),
            T(typeof(PlayerStore), "TryAddDirectSellingItemToTable", typeof(bool), false, typeof(LeanPatch), "Prefix", null, null, new[] { "lean_suppliers" }, typeof(GameItem), typeof(bool), typeof(bool), typeof(bool), typeof(int)),
        };
        // Literal type at every reflection call: required by the game scanner.
        internal static MethodInfo LookupMethod(Type type, string name, Type[] args)
        {
            if (type == typeof(AppraisalPatch)) return AccessTools.Method(typeof(AppraisalPatch), name, args);
            if (type == typeof(AppraisalRankHelper)) return AccessTools.Method(typeof(AppraisalRankHelper), name, args);
            if (type == typeof(AppraisalServiceHelper)) return AccessTools.Method(typeof(AppraisalServiceHelper), name, args);
            if (type == typeof(AttractivenessPatch)) return AccessTools.Method(typeof(AttractivenessPatch), name, args);
            if (type == typeof(BargainPatch)) return AccessTools.Method(typeof(BargainPatch), name, args);
            if (type == typeof(BargainUIManager)) return AccessTools.Method(typeof(BargainUIManager), name, args);
            if (type == typeof(BudgetPatch)) return AccessTools.Method(typeof(BudgetPatch), name, args);
            if (type == typeof(BulkSalePatch)) return AccessTools.Method(typeof(BulkSalePatch), name, args);
            if (type == typeof(BuyPatch)) return AccessTools.Method(typeof(BuyPatch), name, args);
            if (type == typeof(CooldownPatch)) return AccessTools.Method(typeof(CooldownPatch), name, args);
            if (type == typeof(CoolingPatch)) return AccessTools.Method(typeof(CoolingPatch), name, args);
            if (type == typeof(GameItem)) return AccessTools.Method(typeof(GameItem), name, args);
            if (type == typeof(GeneralHelper)) return AccessTools.Method(typeof(GeneralHelper), name, args);
            if (type == typeof(GraphUtils)) return AccessTools.Method(typeof(GraphUtils), name, args);
            if (type == typeof(LeanPatch)) return AccessTools.Method(typeof(LeanPatch), name, args);
            if (type == typeof(ModHelper)) return AccessTools.Method(typeof(ModHelper), name, args);
            if (type == typeof(NegociationUIManager)) return AccessTools.Method(typeof(NegociationUIManager), name, args);
            if (type == typeof(NetworkUpgrade)) return AccessTools.Method(typeof(NetworkUpgrade), name, args);
            if (type == typeof(PlayerStore)) return AccessTools.Method(typeof(PlayerStore), name, args);
            if (type == typeof(PricePatch)) return AccessTools.Method(typeof(PricePatch), name, args);
            if (type == typeof(RNG)) return AccessTools.Method(typeof(RNG), name, args);
            if (type == typeof(RenovationPatch)) return AccessTools.Method(typeof(RenovationPatch), name, args);
            if (type == typeof(ReputationPatch)) return AccessTools.Method(typeof(ReputationPatch), name, args);
            if (type == typeof(SalePatch)) return AccessTools.Method(typeof(SalePatch), name, args);
            if (type == typeof(ScavHelper)) return AccessTools.Method(typeof(ScavHelper), name, args);
            if (type == typeof(SearchPatch)) return AccessTools.Method(typeof(SearchPatch), name, args);
            if (type == typeof(ShowcaseHelper)) return AccessTools.Method(typeof(ShowcaseHelper), name, args);
            if (type == typeof(SleeperPatch)) return AccessTools.Method(typeof(SleeperPatch), name, args);
            if (type == typeof(StartingPerk)) return AccessTools.Method(typeof(StartingPerk), name, args);
            if (type == typeof(StolenHelper)) return AccessTools.Method(typeof(StolenHelper), name, args);
            if (type == typeof(StolenPatch)) return AccessTools.Method(typeof(StolenPatch), name, args);
            if (type == typeof(StoreClient)) return AccessTools.Method(typeof(StoreClient), name, args);
            if (type == typeof(StoreClientInstance)) return AccessTools.Method(typeof(StoreClientInstance), name, args);
            if (type == typeof(StoreClientList)) return AccessTools.Method(typeof(StoreClientList), name, args);
            if (type == typeof(StoreClientListAppraisal)) return AccessTools.Method(typeof(StoreClientListAppraisal), name, args);
            if (type == typeof(StoreClientManager)) return AccessTools.Method(typeof(StoreClientManager), name, args);
            if (type == typeof(StoreEventManager)) return AccessTools.Method(typeof(StoreEventManager), name, args);
            if (type == typeof(StoreReputation)) return AccessTools.Method(typeof(StoreReputation), name, args);
            if (type == typeof(StoreStation)) return AccessTools.Method(typeof(StoreStation), name, args);
            if (type == typeof(TagState)) return AccessTools.Method(typeof(TagState), name, args);
            if (type == typeof(WoundPatch)) return AccessTools.Method(typeof(WoundPatch), name, args);
            return null;
        }
        internal static FieldInfo LookupField(Type type, string name)
        {
            if (type == typeof(StoreStation)) return AccessTools.Field(typeof(StoreStation), name);
            if (type == typeof(ModHook)) return AccessTools.Field(typeof(ModHook), name);
            if (type == typeof(NegociationUIManager)) return AccessTools.Field(typeof(NegociationUIManager), name);
            if (type == typeof(NetworkUpgrade)) return AccessTools.Field(typeof(NetworkUpgrade), name);
            if (type == typeof(PlayerStore)) return AccessTools.Field(typeof(PlayerStore), name);
            if (type == typeof(StoreClient)) return AccessTools.Field(typeof(StoreClient), name);
            if (type == typeof(StoreClientManager)) return AccessTools.Field(typeof(StoreClientManager), name);
            if (type == typeof(StoreService)) return AccessTools.Field(typeof(StoreService), name);
            return null;
        }
        internal static PropertyInfo LookupProperty(Type type, string name)
        {
            if (type == typeof(GameItem)) return AccessTools.Property(typeof(GameItem), name);
            if (type == typeof(GraphNodeStorage)) return AccessTools.Property(typeof(GraphNodeStorage), name);
            if (type == typeof(GridShape)) return AccessTools.Property(typeof(GridShape), name);
            if (type == typeof(MapUIManager)) return AccessTools.Property(typeof(MapUIManager), name);
            return null;
        }
        private static void M(Type type, string name, Type result, bool stat, params Type[] args)
        {
            var method = LookupMethod(type, name, args);
            if (method == null || method.ReturnType != result || method.IsStatic != stat || method.ContainsGenericParameters || !Accept(method))
                throw new InvalidOperationException(type.Name + "." + name + " method differs");
        }
        private static void F(Type type, string name, Type result)
        {
            var field = LookupField(type, name);
            if (field == null || field.FieldType != result || !Accept(field)) throw new InvalidOperationException(type.Name + "." + name + " field differs");
        }
        private static void P(Type type, string name, Type result)
        {
            var property = LookupProperty(type, name);
            if (property == null || property.PropertyType != result || !Accept(property)) throw new InvalidOperationException(type.Name + "." + name + " property differs");
        }
        private static void Client()
        {
            foreach (string name in new[] { "CanReturn", "isSecurity", "isWanted" }) F(typeof(StoreClient), name, typeof(bool));
            foreach (string name in new[] { "identifier", "eventSourceId" }) F(typeof(StoreClient), name, typeof(string));
            F(typeof(StoreClient), "clientIntent", typeof(StoreClient.ClientIntent));
        }
        private static void Budget()
        {
            M(typeof(StoreClient), "GetBudget", typeof(int), false); M(typeof(StoreClient), "OverrideBudget", typeof(void), false, typeof(int));
            M(typeof(StoreClient), "IsTag", typeof(bool), false, typeof(string)); M(typeof(StoreClient), "AddTag", typeof(void), false, typeof(string));
        }
        private static void Clock()
        {
            M(typeof(StoreStation), "GetDayCounter", typeof(int), true);
            F(typeof(PlayerStore), "startingDay", typeof(int));
            M(typeof(ModHelper), "GetModData", typeof(string), true, typeof(string), typeof(string), typeof(string));
            M(typeof(ModHelper), "SetModData", typeof(void), true, typeof(string), typeof(string), typeof(string));
        }
        private static void Counter()
        {
            F(typeof(PlayerStore), "currentClientInstance", typeof(StoreClientInstance));
            M(typeof(StoreClientInstance), "GetClientBlueprint", typeof(StoreClient), false);
        }
        private static void Cash() => F(typeof(PlayerStore), "playerCash", typeof(int));
        private static void Queue() => F(typeof(StoreClientManager), "clientStack", typeof(List<StoreClient>));
        private static void Stolen()
        {
            M(typeof(StolenHelper), "IsStolenItem", typeof(bool), true, typeof(GameItem));
            M(typeof(StolenHelper), "SetHeat", typeof(void), true, typeof(GameItem), typeof(int));
            M(typeof(GameItem), "GetTagReadonly", typeof(TagState), false, typeof(string));
            M(typeof(TagState), "GetInt", typeof(int), false);
            M(typeof(GameItem), "IsTag", typeof(bool), false, typeof(string));
            M(typeof(GameItem), "EnableTag", typeof(bool), false, typeof(string), typeof(bool));
        }
        internal static string Check(string slug)
        {
            try
            {
                M(typeof(ModHelper), "AddStartingPerk", typeof(void), true, typeof(string), typeof(string), typeof(int), typeof(StartingPerk.StartingPerkType),
                    typeof(Func<string>), typeof(Func<string>), typeof(string), typeof(string[]), typeof(int), typeof(bool));
                M(typeof(ModHelper), "RemoveStartingPerks", typeof(int), true, typeof(string));
                M(typeof(ModHelper), "IsStartingPerkActive", typeof(bool), true, typeof(string));
                foreach (Target t in Targets.Where(t => t.Perks.Contains(slug))) M(t.Type, t.Name, t.Result, t.Static, t.Args);
                // Dependencies beyond patch targets are checked for the perk that uses them.
                if (new[] { "brisk_trade", "skimmed_till", "lean_suppliers", "appraisal_advert", "private_viewing", "aug_bounty" }.Contains(slug)) Clock();
                if (Sales.Contains(slug) || new[] { "brisk_trade", "skimmed_till", "good_name", "wealthy_visitors", "tight_regulars", "lean_suppliers", "appraisal_advert" }.Contains(slug)) Client();
                if (Sales.Contains(slug) || new[] { "brisk_trade", "skimmed_till", "lean_suppliers" }.Contains(slug)) Counter();
                if (Sales.Contains(slug) || slug == "brisk_trade" || slug == "skimmed_till")
                {
                    P(typeof(MapUIManager), "isOutside", typeof(bool)); F(typeof(NegociationUIManager), "uiObject", typeof(GameObject));
                    M(typeof(GeneralHelper), "IsItemOwned", typeof(bool), true, typeof(GameItem));
                    P(typeof(GameItem), "identifier", typeof(string)); P(typeof(GameItem), "shape", typeof(GridShape));
                    P(typeof(GameItem), "unitValue", typeof(long)); P(typeof(GridShape), "width", typeof(int)); P(typeof(GridShape), "height", typeof(int));
                    M(typeof(GameItem), "IsGameItemType", typeof(bool), false, typeof(string));
                    // The node graph is traversed without crossing engine objects.
                    P(typeof(GraphNodeStorage), "children", typeof(List<GraphNodeStorage>));
                }
                if (slug == "lights_out_premium")
                {
                    F(typeof(StoreStation), "storeEventManager", typeof(StoreEventManager));
                    M(typeof(StoreEventManager), "IsBlackoutEventActive", typeof(bool), false);
                }
                if (slug == "master_valuer" || slug == "brisk_trade" || slug == "skimmed_till" || slug == "steep_overheads") Cash();
                if (slug == "master_valuer") M(typeof(PlayerStore), "ModCash", typeof(void), false, typeof(int), typeof(bool));
                if (slug == "wealthy_visitors" || slug == "tight_regulars" || slug == "private_viewing") Budget();
                if (slug == "cooling_off_deal" || slug == "marked_shop") Stolen();
                if (slug == "tight_regulars") M(typeof(StoreClientManager), "GetCustomerTotalVisitedCount", typeof(int), true, typeof(string));
                if (slug == "good_name")
                {
                    M(typeof(StoreReputation), "GetStoreReputation", typeof(StoreReputation), true, typeof(string));
                    M(typeof(StoreReputation), "GetReputationExact", typeof(double), false); M(typeof(StoreReputation), "ModReputation", typeof(void), false, typeof(double));
                }
                if (slug == "night_owl")
                {
                    F(typeof(PlayerStore), "visitLeftTonight", typeof(int)); F(typeof(ModHook), "OnShutterClosedLate", typeof(Action));
                }
                if (slug == "window_dresser") M(typeof(ShowcaseHelper), "HasMerchandise", typeof(bool), true);
                if (slug == "shabby_front") F(typeof(NetworkUpgrade), "id", typeof(string));
                if (new[] { "appraisal_advert", "thorough_search", "aug_bounty" }.Contains(slug)) M(typeof(RNG), "Roll", typeof(bool), true, typeof(int));
                if (slug == "thorough_search") M(typeof(ScavHelper), "SpawnDumpingGroundLoot", typeof(GameItem), true);
                if (slug == "private_viewing" || slug == "aug_bounty") Queue();
                if (slug == "steep_overheads") M(typeof(PlayerStore), "AddNightLog", typeof(void), false, typeof(string));
                if (slug == "private_viewing" || slug == "appraisal_advert")
                {
                    M(typeof(StoreClientManager), "TryAddClient", typeof(void), false, typeof(StoreClient), typeof(bool));
                    F(typeof(ModHook), "OnGenerateCustomerLate", typeof(Action<StoreClientManager>));
                }
                if (slug == "private_viewing")
                {
                    F(typeof(PlayerStore), "runID", typeof(string)); F(typeof(StoreClient), "CanReturnAsBuyer", typeof(bool));
                    M(typeof(StoreClientList), "CreateUpperLowerVisitorLuxury", typeof(StoreClient), true);
                }
                if (slug == "appraisal_advert")
                {
                    F(typeof(PlayerStore), "isAppraisalIntroduced", typeof(bool));
                    M(typeof(AppraisalRankHelper), "GetDisplayedCertificate", typeof(GameItem), true);
                    M(typeof(StoreClientListAppraisal), "CreateAppraisalRandom", typeof(StoreClient), true);
                }
                if (slug == "steep_overheads")
                {
                    F(typeof(ModHook), "OnHandlingNightlyServicesEarly", typeof(Action));
                    F(typeof(PlayerStore), "storeService", typeof(List<StoreService>));
                    F(typeof(StoreService), "id", typeof(string)); F(typeof(StoreService), "cost", typeof(int));
                    F(typeof(StoreService), "unlocked", typeof(bool)); F(typeof(StoreService), "isUsing", typeof(bool));
                }
                if (slug == "lean_suppliers") M(typeof(GraphUtils), "DestroyIfUnplaced", typeof(bool), true, typeof(GameItem), typeof(string));
                if (slug == "aug_bounty")
                {
                    F(typeof(ModHook), "OnGenerateCustomerLate", typeof(Action<StoreClientManager>));
                    foreach (string name in new[] { "isAugIntroduced", "isAugEnded" }) F(typeof(PlayerStore), name, typeof(bool));
                    F(typeof(StoreClient), "isAug", typeof(bool));
                    M(typeof(StoreClientManager), "AddAugClient", typeof(void), false);
                }
                return null;
            }
            catch (Exception error) { return error.Message; }
        }
        internal static void Validate()
        {
            Runtime.Unavailable.Clear();
            foreach (Definition entry in Catalogue.All)
            {
                string difference = Check(entry.Slug);
                if (difference == null) continue;
                Runtime.Unavailable[entry.Slug] = difference;
                Runtime.Report("shape:" + entry.Slug, entry.Name + " is not working with this game version: " + difference);
            }
        }
        internal static void Enable()
        {
            Validate();
            var harmony = new Harmony(PerkMod.HarmonyId);
            var installed = new HashSet<MethodInfo>();
            foreach (Target t in Targets)
            {
                if (!t.Perks.Any(p => !Runtime.Unavailable.ContainsKey(p))) continue;
                try
                {
                    MethodInfo target = t.Type == typeof(GameItem) && t.Name == "GetNegociatedValue" ? FindPrice() : t.Method;
                    HarmonyMethod Patch(string name) => name == null ? null : new HarmonyMethod(LookupMethod(t.Patch, name, null));
                    harmony.Patch(target, Patch(t.Prefix), Patch(t.Postfix), null, Patch(t.Finalizer));
                    installed.Add(target);
                }
                catch (Exception error)
                {
                    foreach (string slug in t.Perks) Runtime.Unavailable[slug] = t.Type.Name + "." + t.Name + ": " + error.GetType().Name;
                    Runtime.Fault("patching " + t.Type.Name + "." + t.Name, error);
                }
            }
            // A failed installation must not leave half a perk's targets patched.
            foreach (Target t in Targets)
                if (t.Perks.All(p => Runtime.Unavailable.ContainsKey(p)))
                {
                    MethodInfo target = t.Type == typeof(GameItem) && t.Name == "GetNegociatedValue" ? FindPrice() : t.Method;
                    if (target != null && installed.Contains(target))
                    {
                        try { harmony.Unpatch(target, HarmonyPatchType.All, PerkMod.HarmonyId); }
                        catch (Exception error) { Runtime.Fault("removing an incomplete patch on " + t.Type.Name + "." + t.Name, error); }
                    }
                }
        }
    }
}

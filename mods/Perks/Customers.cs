using System;
using System.Collections.Generic;

namespace Perks
{
    internal static class Customers
    {
        internal const string LuxuryId = "rnk.perks.luxury_buyer", WealthTag = "rnk.perks.wealthy", TightTag = "rnk.perks.tight";
        internal static Func<bool> AppraisalReady = () => PlayerStore.Instance.isAppraisalIntroduced && AppraisalRankHelper.GetDisplayedCertificate() != null;
        internal static Func<StoreClient> MakeAppraisal = () => StoreClientListAppraisal.CreateAppraisalRandom();
        internal static Func<StoreClient> MakeLuxury = () => StoreClientList.CreateUpperLowerVisitorLuxury();
        internal static Action<StoreClientManager, StoreClient, bool> Add = (manager, client, noDuplicate) => manager.TryAddClient(client, noDuplicate);
        private static WeakReference<StoreClient> supplier = new WeakReference<StoreClient>(null);
        private static int offered, offeredDay = int.MinValue;
        internal static void Reset() { supplier = new WeakReference<StoreClient>(null); offered = 0; offeredDay = int.MinValue; }
        internal static void Generate(StoreClientManager manager)
        {
            Runtime.Guard("appraisal advert", () => Advert(manager));
            Runtime.Guard("private viewing", () => Luxury(manager));
            AugBounty.Generate(manager);
        }
        internal static void Advert(StoreClientManager manager)
        {
            if (!Runtime.Active("appraisal_advert") || manager == null) return;
            int day = Runtime.Today(); if (Runtime.Stored("advert_day", day)) return;
            Runtime.Remember("advert_day", day);
            if (!AppraisalReady() || !Runtime.Roll(Rules.AdvertChance)) return;
            StoreClient client = MakeAppraisal();
            if (client == null) return;
            client.eventSourceId = "rnk.perks.appraisal_advert"; Add(manager, client, false);
        }
        internal static void Luxury(StoreClientManager manager)
        {
            if (!Runtime.Active("private_viewing") || manager == null) return;
            int day = Runtime.Today(); if (Runtime.Stored("luxury_day", day)) return;
            if (Rules.LuxuryDay(Runtime.RunId(), Runtime.Week) != Rules.WeekDay(day, Runtime.StartingDay())) return;
            foreach (StoreClient queued in manager.clientStack) if (queued != null && queued.identifier == LuxuryId) return;
            StoreClient client = MakeLuxury(); if (client == null) return;
            long total = 0; int count = 0;
            foreach (StoreClient queued in manager.clientStack)
                if (Runtime.Ordinary(queued) && queued.GetBudget() > 0) { total += queued.GetBudget(); count++; }
            client.identifier = client.eventSourceId = LuxuryId; client.CanReturn = client.CanReturnAsBuyer = false;
            client.OverrideBudget(Rules.LuxuryAverage(total, count, client.GetBudget())); client.AddTag(WealthTag); client.AddTag(TightTag);
            Add(manager, client, true);
            // Marked only when the buyer stands in the queue: a fault before this point, or a
            // refusal by the game's own rules, leaves the visit to be tried again the same day,
            // and the test for a queued buyer above stops a second one.
            if (manager.clientStack.Contains(client)) Runtime.Remember("luxury_day", day);
        }
        internal static bool Withhold(StoreClient client)
        {
            // A new customer or a new day starts a new count, also when the game hands us
            // the same customer object again on a later visit.
            int day = Runtime.Today();
            if (!supplier.TryGetTarget(out StoreClient current) || !ReferenceEquals(client, current) || day != offeredDay) { supplier.SetTarget(client); offered = 0; offeredDay = day; }
            offered = offered == int.MaxValue ? 1 : offered + 1;
            return Rules.Withhold(offered);
        }
    }
    internal static class BudgetPatch
    {
        internal static void Prefix(StoreClient __0)
        {
            try
            {
                if (__0 == null) return;
                bool wealthy = Runtime.Active("wealthy_visitors") && !__0.IsTag(Customers.WealthTag);
                bool tight = Runtime.Active("tight_regulars") && Runtime.Ordinary(__0) && !__0.IsTag(Customers.TightTag) && Runtime.VisitCount(__0.identifier) > 0;
                if (!wealthy && !tight) return;
                int budget = __0.GetBudget(); if (budget <= 0) return;
                if (wealthy) budget = Rules.Wealth(budget);
                if (tight) budget = Rules.Tight(budget);
                if (wealthy) __0.AddTag(Customers.WealthTag);
                if (tight) __0.AddTag(Customers.TightTag);
                __0.OverrideBudget(budget);
            }
            catch (Exception error) { Runtime.Fault("visitor budgets", error); }
        }
    }
    internal static class LeanPatch
    {
        // The game's routine answers true when the item lies in an inventory and was left alone.
        internal static Func<GameItem, string, bool> Destroy = (item, reason) => GraphUtils.DestroyIfUnplaced(item, reason);
        internal static bool Prefix(GameItem __0, bool __1, ref bool __result)
        {
            try
            {
                if (!Runtime.Active("lean_suppliers") || __0 == null || __1) return true;
                StoreClient client = Runtime.Counter();
                if (!Runtime.Supplier(client) || !Customers.Withhold(client)) return true;
            }
            catch (Exception error) { Runtime.Fault("lean suppliers", error); return true; }
            // From here the item is being taken away. If that faults half way the item may
            // already be gone, and the game's method must not be handed it.
            try { if (Destroy(__0, "Lean Suppliers")) return true; }
            catch (Exception error) { Runtime.Fault("withholding a supplier's item", error); }
            __result = false; return false;
        }
    }
}

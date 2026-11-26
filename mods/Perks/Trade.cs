using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Perks
{
    internal sealed class SaleFrame { internal bool Price, Counted, Outer; }
    internal static class Trade
    {
        internal static int SaleDepth, PurchaseDepth;
        private static int batchDepth;
        private static int before;
        private static StoreClient buyer;
        private static bool failed;
        internal static Func<bool> Preview = () => NegociationUIManager.Instance != null && NegociationUIManager.Instance.uiObject != null && NegociationUIManager.Instance.uiObject.activeInHierarchy;
        internal static Func<GameItem, bool> Owned = item => GeneralHelper.IsItemOwned(item);
        internal static Func<bool> Blackout = () => StoreStation.Instance.storeEventManager.IsBlackoutEventActive();
        internal static Func<GameItem, bool> Contents = item => HasContents(item);
        internal static void Reset() { SaleDepth = PurchaseDepth = batchDepth = 0; buyer = null; failed = false; }
        internal static bool HasContents(GameItem item)
        {
            var seen = new HashSet<GraphNodeStorage>();
            var todo = new Stack<GraphNodeStorage>();
            if (item.children != null) foreach (var child in item.children) todo.Push(child);
            while (todo.Count > 0)
            {
                var node = todo.Pop();
                if (node == null || !seen.Add(node)) continue;
                if (node is GameItem) return true;
                if (node.children != null) foreach (var child in node.children) todo.Push(child);
            }
            return false;
        }
        internal static long Price(GameItem item, long original)
        {
            if (!Runtime.Master() || item == null || !Runtime.InShop()) return original;
            StoreClient client = Runtime.Counter();
            if (client == null) return original;
            bool sale = SaleDepth > 0, purchase = PurchaseDepth > 0;
            if (!sale && !purchase && Preview())
            {
                sale = client.clientIntent == StoreClient.ClientIntent.BUY && Owned(item);
                purchase = client.clientIntent == StoreClient.ClientIntent.SELL && !Owned(item);
            }
            if (sale)
            {
                bool ordinary = Runtime.Buyer(client), luxury = client.identifier == Customers.LuxuryId && !Runtime.Unavailable.ContainsKey("private_viewing");
                if (!ordinary && !luxury) return original;
                bool tech = Runtime.Active("tech_dealer"), bargain = Runtime.Active("bargain_bin"), bulky = Runtime.Active("bulky_goods");
                int spec = tech ? 1 : bargain ? 2 : bulky ? 3 : 0;
                bool contents = spec != 0 && Contents(item);
                bool technology = tech && !contents && (item.IsGameItemType("MACHINE") || item.IsGameItemType("MODULE") || item.IsGameItemType("NODE"));
                long area = bulky && !contents ? (long)item.shape.width * item.shape.height : 0;
                return Rules.Sale(original, ordinary, luxury, Runtime.Active("premium_retailer"), spec, technology, item.unitValue, area, contents,
                    ordinary && Runtime.Active("lights_out_premium") && Blackout());
            }
            return purchase ? Rules.Purchase(original, Runtime.Supplier(client), Runtime.Active("trusted_wholesaler"), Runtime.Active("greedy_wholesaler"), Runtime.Active("premium_retailer")) : original;
        }
        internal static void Begin(SaleFrame frame, GameItem item, bool single)
        {
            if (single) { SaleDepth++; frame.Price = true; }
            if (!Runtime.Active("brisk_trade") && !Runtime.Active("skimmed_till")) return;
            if (batchDepth == 0)
            {
                // All reads before opening the aggregation scope.
                int cash = Runtime.Cash(); StoreClient client = Runtime.Counter();
                before = cash; buyer = client; failed = false; frame.Outer = true;
            }
            batchDepth++; frame.Counted = true;
        }
        internal static void End(SaleFrame frame, Exception exception)
        {
            if (frame == null) return;
            if (frame.Price) SaleDepth = Math.Max(0, SaleDepth - 1);
            if (!frame.Counted) return;
            failed |= exception != null;
            batchDepth = Math.Max(0, batchDepth - 1);
            if (!frame.Outer || batchDepth != 0) return;
            try
            {
                if (failed || !Runtime.Buyer(buyer) || !Runtime.InShop()) return;
                bool brisk = Runtime.Active("brisk_trade"), skim = Runtime.Active("skimmed_till");
                if (!brisk && !skim) return;
                int cash = Runtime.Cash(); long paid = (long)cash - before;
                if (paid <= 0) return;
                int day = Runtime.Today();
                var result = Rules.Settle(DailyState.Parse(Runtime.Read("trade"), day), paid, brisk, skim);
                int nextCash = checked(cash + result.Delta);
                // The cash first: if it cannot be set, the day's count is not spent.
                Runtime.SetCash(nextCash);
                Runtime.Write("trade", result.State.Text());
                Runtime.Say("sale: opening +" + result.Opening + ", fee -" + result.Fee + ".");
            }
            finally { buyer = null; }
        }
    }
    internal static class PricePatch
    {
        internal static void Postfix(GameItem __instance, ref long __result)
        { try { long result = Trade.Price(__instance, __result); __result = result; } catch (Exception error) { Runtime.Fault("prices", error); } }
    }
    internal static class SalePatch
    {
        internal static void Prefix(GameItem __0, out SaleFrame __state)
        {
            __state = null;
            try { __state = new SaleFrame(); Trade.Begin(__state, __0, true); } catch (Exception error) { Runtime.Fault("starting a sale", error); }
        }
        internal static Exception Finalizer(SaleFrame __state, Exception __exception)
        { try { Trade.End(__state, __exception); } catch (Exception error) { Runtime.Fault("settling a sale", error); } return __exception; }
    }
    internal static class BulkSalePatch
    {
        internal static void Prefix(out SaleFrame __state)
        {
            __state = null;
            try { __state = new SaleFrame(); Trade.Begin(__state, null, false); } catch (Exception error) { Runtime.Fault("starting a bulk sale", error); }
        }
        internal static Exception Finalizer(SaleFrame __state, Exception __exception)
        { try { Trade.End(__state, __exception); } catch (Exception error) { Runtime.Fault("settling a bulk sale", error); } return __exception; }
    }
    internal static class BuyPatch
    {
        internal static void Prefix(out bool __state)
        { __state = false; try { Trade.PurchaseDepth++; __state = true; } catch (Exception error) { Runtime.Fault("starting a purchase", error); } }
        internal static Exception Finalizer(bool __state, Exception __exception)
        { try { if (__state) Trade.PurchaseDepth = Math.Max(0, Trade.PurchaseDepth - 1); } catch (Exception error) { Runtime.Fault("ending a purchase", error); } return __exception; }
    }
    internal static class AppraisalPatch
    {
        internal static void Prefix(out int? __state)
        { __state = null; try { if (Runtime.Active("master_valuer")) __state = Runtime.Cash(); } catch (Exception error) { Runtime.Fault("reading an appraisal fee", error); } }
        internal static void Postfix(int? __state)
        {
            try
            {
                if (!__state.HasValue || !Runtime.Active("master_valuer")) return;
                long gain = (long)Runtime.Cash() - __state.Value;
                if (gain > 0) Runtime.AppraisalCash(Rules.Appraisal((int)Math.Min(int.MaxValue, gain)));
            }
            catch (Exception error) { Runtime.Fault("appraisal fees", error); }
        }
    }
    internal static class CoolingPatch
    {
        internal const string Cooled = "RNK_PERKS_COOLED", HeatTag = "STOLEN_VALUE_INT";
        internal static Func<GameItem, bool> IsStolen = item => StolenHelper.IsStolenItem(item);
        internal static Func<GameItem, bool> HasTag = item => item.IsTag(Cooled);
        internal static Func<GameItem, int> Heat = item => { TagState heat = item.GetTagReadonly(HeatTag); return heat == null ? 0 : heat.GetInt(); };
        internal static Action<GameItem, int> SetHeat = (item, heat) => StolenHelper.SetHeat(item, heat);
        internal static Action<GameItem> Tag = item => item.EnableTag(Cooled);
        internal static void Postfix(GameItem __0)
        {
            try
            {
                if (!Runtime.Active("cooling_off_deal") || __0 == null || !IsStolen(__0) || HasTag(__0)) return;
                int heat = Heat(__0);
                // No heat tag is written for an item without heat; every stolen purchase
                // is marked so that the same item cannot be cooled twice.
                if (heat > 0) SetHeat(__0, Rules.Cool(heat));
                Tag(__0);
            }
            catch (Exception error) { Runtime.Fault("cooling stolen goods", error); }
        }
    }
    internal sealed class ReputationState { internal StoreReputation Faction; internal double Before; }
    internal static class ReputationPatch
    {
        internal static void Prefix(string __0, StoreClient __3, out ReputationState __state)
        {
            __state = null;
            try
            {
                if (!Runtime.Active("good_name") || !Runtime.Ordinary(__3)) return;
                var faction = StoreReputation.GetStoreReputation(__0);
                if (faction != null) __state = new ReputationState { Faction = faction, Before = faction.GetReputationExact() };
            }
            catch (Exception error) { Runtime.Fault("reading trade reputation", error); }
        }
        internal static void Postfix(ReputationState __state)
        {
            try
            {
                if (__state == null || !Runtime.Active("good_name")) return;
                double extra = Rules.Reputation(__state.Faction.GetReputationExact() - __state.Before);
                if (extra > 0) __state.Faction.ModReputation(extra);
            }
            catch (Exception error) { Runtime.Fault("trade reputation", error); }
        }
    }
    internal static class BargainPatch
    {
        internal static void Postfix(BargainType __0, int __1, ref int __result)
        { try { if (Runtime.Active("hard_bargainer")) __result = Rules.Bargain(__result, __1); } catch (Exception error) { Runtime.Fault("bargaining", error); } }
    }
}


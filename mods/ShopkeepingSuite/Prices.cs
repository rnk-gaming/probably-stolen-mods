using System;
using System.Collections.Generic;
using System.Linq;

namespace ShopkeepingSuite
{
    internal static class Prices
    {
        internal static Func<List<PriceEntry>> ReadEvents = ReadGame;
        internal static Func<int> Day = () => StoreStation.GetDayCounter();
        // Prices apply wherever the game draws the item.
        internal static Func<GameItem, string, bool> Tag = (item, tag) => item.IsTag(tag);
        internal static Func<GameItem, string> Id = item => item.identifier;
        // GameItem is not a Unity object; IsDestroyed tests its render handler with Unity's == null.
        internal static Func<GameItem, bool> SampleItem = item => item != null && (!(item is GameItemElement element) || !element.IsDestroyed());
        internal static Action<GameItem, int, int> Draw = MarkArt.DrawPrice;
        internal static Action Clear = MarkArt.ClearPrice;
        internal static PriceMarket Market = new PriceMarket();
        internal static bool failed;
        private static readonly PriceCache cache = new PriceCache();
        private static float nextRefresh = float.NegativeInfinity, previous = float.NegativeInfinity;
        private static List<PriceEntry> ReadGame()
        {
            var result = new List<PriceEntry>();
            StoreStation station = StoreStation.Instance;
            if (station == null || station.storeEventManager == null) return result;
            return Snapshot(station.storeEventManager.GetActiveEvents());
        }
        // Copies data only. The harness supplies inert event objects, never calls the manager.
        internal static List<PriceEntry> Snapshot(List<StoreEvent> events)
        {
            var result = new List<PriceEntry>();
            for (int i = 0; i < events.Count; i++)
            {
                StoreEvent active = events[i];
                if (active?.negociationDatas == null) continue;
                string name = active.isHidden ? Texts.Get("price_event") : active.displayName;
                foreach (NegociationData data in active.negociationDatas)
                    if (data != null) result.Add(new PriceEntry { Tag = data.itemType, Value = data.value, Name = name, Group = i });
            }
            return result;
        }
        private static bool Available => !failed && Settings.On("enabled") && Settings.On("price_data") && Settings.On("location_data");
        internal static void Fault(Exception error)
        {
            if (failed) return;
            failed = true;
            SuiteMod.Fault("price aid", error);
            try { Clear(); } catch (Exception) { }
        }
        internal static void Poll()
        {
            try
            {
                if (!Available || (!Settings.On("mark_price") && !Settings.On("tooltip_price"))) return;
                float now = SuiteRuntime.Clock();
                bool changed = cache.Poll(now, Day(), ReadEvents);
                // The optional market may change without a game event changing. Redraw
                // through the existing bounded queue, never in a loop over UI objects here.
                if (changed || Settings.On("mark_price") && Market.Ready() && (now < previous || now >= nextRefresh))
                { nextRefresh = now + 2f; Marks.RequestAll(); }
                previous = now;
            }
            catch (Exception error) { Fault(error); }
        }
        internal static PriceAnswer Get(GameItem item, bool tooltip)
        {
            try
            {
                if (item == null || !Available || !Settings.On(tooltip ? "tooltip_price" : "mark_price")) return null;
                Poll(); if (failed) return null;
                float now = SuiteRuntime.Clock();
                string id = Id(item), type = SignPrices.GoodsType(id);
                if (SignPrices.IsSign(id))
                    return type == null ? null : cache.GetSign(type, now, () => ReadSign(type, now));
                return cache.Get(item, now, () =>
                {
                    var tags = new HashSet<string>(cache.ByTag.Where(g => Tag(item, g.Key)).Select(g => g.Key));
                    List<PriceEntry> matches = cache.Events.Where(e => tags.Contains(e.Tag)).ToList();
                    double factor = Market.Read(item, now);
                    return new PriceAnswer { Events = matches, Factor = factor, MarketUntil = factor == 1 ? float.PositiveInfinity : Market.ValidUntil(item),
                        Total = PriceRules.Total(PriceRules.EventShift(matches, _ => true), factor) };
                });
            }
            catch (Exception error) { Fault(error); return null; }
        }
        private static PriceAnswer ReadSign(string type, float now)
        {
            List<PriceEntry> matches = cache.ByTag.FirstOrDefault(group => group.Key == type)?.ToList() ?? new List<PriceEntry>();
            bool ready = Settings.On("shop_data") && Market.Ready();
            float until = float.PositiveInfinity;
            SignMarket sample = new SignMarket();
            try
            {
                List<GameItem> shop = ready ? cache.GetShop(now, Marks.ShopItems) : null;
                ready &= shop != null;
                if (ready) sample = SignPrices.Sample(type, shop.Where(SampleItem), true, Id, Tag, item =>
                {
                    double value = Market.ReadSample(item, now);
                    if (PriceRules.ValidFactor(value)) until = Math.Min(until, Market.ValidUntil(item));
                    return value;
                });
            }
            catch (Exception error)
            {
                SuiteMod.Fault("price sign sample", error);
                ready = false; sample = new SignMarket(); until = float.PositiveInfinity;
            }
            // An unavailable adapter contributes no market reading.
            if (ready && !Market.Ready()) { ready = false; sample = new SignMarket(); until = float.PositiveInfinity; }
            return new PriceAnswer { SignType = type, Events = matches, Factor = sample.Factor, MarketUntil = until,
                MarketRead = ready && sample.HasSign, MarketCount = sample.Count, MarketSeen = sample.Seen,
                Total = PriceRules.Total(SignPrices.EventShift(matches, type), sample.Factor) };
        }
        internal static int Mark(GameItem item)
        {
            try
            {
                PriceAnswer answer = Get(item, false);
                return answer == null ? 0 : PriceRules.Mark(answer.Total, Settings.PriceMargin(), Settings.PriceMarginStrong());
            }
            catch (Exception error) { Fault(error); return 0; }
        }
        internal static void Paint(GameItem item)
        {
            if (failed) return;
            try { Draw(item, Mark(item), MarkArt.Size(Settings.Size())); }
            catch (Exception error) { Fault(error); }
        }
        internal static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                PriceAnswer answer = Get(item, true); if (answer == null || answer.Total == 0 && answer.SignType == null) return;
                string[] keys = answer.SignType == null
                    ? new[] { "price_now", "price_more", "price_market", "price_event" }
                    : new[] { "price_sign_now", "price_more", "price_event", "price_sign_usual", "price_sign_market",
                        "price_sign_empty", "price_sign_unknown", SignPrices.NameKey(answer.SignType) };
                var labels = keys.ToDictionary(key => key, key => Texts.Get(key, "{0}", "{1}"));
                SuiteRuntime.AddLine(builder, PriceRules.Tooltip(answer, labels));
            }
            catch (Exception error) { Fault(error); }
        }
        internal static void Reset()
        {
            try { cache.Reset(); Market.ClearCache(); nextRefresh = previous = float.NegativeInfinity; Clear(); }
            catch (Exception error) { Fault(error); }
        }
    }
}

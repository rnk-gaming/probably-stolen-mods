using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ShopkeepingSuite
{
    internal sealed class PriceCache
    {
        internal int Revision;
        internal List<PriceEntry> Events = new List<PriceEntry>();
        internal List<IGrouping<string, PriceEntry>> ByTag = new List<IGrouping<string, PriceEntry>>();
        private int knownDay;
        private bool known;
        private float last = float.NegativeInfinity;
        private sealed class Cached { internal float At; internal PriceAnswer Answer; }
        private ConditionalWeakTable<object, Cached> items = new ConditionalWeakTable<object, Cached>();
        private readonly Dictionary<string, Cached> signs = new Dictionary<string, Cached>();
        private List<GameItem> shop;
        private float shopAt = float.NegativeInfinity;
        internal bool Poll(float now, int day, Func<List<PriceEntry>> read)
        {
            bool changed = !known || knownDay != day;
            known = true; knownDay = day;
            if (now < last || now - last >= 1f)
            {
                last = now;
                List<PriceEntry> next = read();
                changed |= !Events.Select(e => (e.Tag, e.Value, e.Name, e.Group)).SequenceEqual(next.Select(e => (e.Tag, e.Value, e.Name, e.Group)));
                Events = next.Select(e => new PriceEntry { Tag = e.Tag, Value = e.Value, Name = e.Name, Group = e.Group }).ToList();
                ByTag = Events.GroupBy(e => e.Tag).ToList();
            }
            if (changed) { Revision++; items = new ConditionalWeakTable<object, Cached>(); signs.Clear(); shop = null; shopAt = float.NegativeInfinity; }
            return changed;
        }
        internal PriceAnswer Get(object item, float now, Func<PriceAnswer> read)
        {
            if (items.TryGetValue(item, out Cached cached) && now >= cached.At && now - cached.At < 2f && now < cached.Answer.MarketUntil) return cached.Answer;
            PriceAnswer answer = read();
            items.Remove(item); items.Add(item, new Cached { At = now, Answer = answer });
            return answer;
        }
        internal PriceAnswer GetSign(string type, float now, Func<PriceAnswer> read)
        {
            if (signs.TryGetValue(type, out Cached cached) && now >= cached.At && now - cached.At < 2f && now < cached.Answer.MarketUntil) return cached.Answer;
            PriceAnswer answer = read();
            signs[type] = new Cached { At = now, Answer = answer };
            return answer;
        }
        internal List<GameItem> GetShop(float now, Func<List<GameItem>> read)
        {
            if (now >= shopAt && now - shopAt < 2f) return shop;
            // Copy once for all types. A failed walk also waits two seconds before retrying.
            shopAt = now; shop = null;
            shop = read()?.ToList();
            return shop;
        }
        internal void Reset() { known = false; last = float.NegativeInfinity; Events.Clear(); ByTag.Clear(); items = new ConditionalWeakTable<object, Cached>(); signs.Clear(); shop = null; shopAt = float.NegativeInfinity; }
    }
}

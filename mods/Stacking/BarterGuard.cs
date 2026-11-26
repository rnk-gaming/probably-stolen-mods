using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Stacking
{
    internal static class BarterGuard
    {
        internal static bool ExchangeInstalled, OfferInstalled;
        internal static Func<MemberInfo[]> FindExchange = () => new MemberInfo[]
        {
            AccessTools.Method(typeof(ExchangeBarter), "IsRequiredItemPresent", Type.EmptyTypes), AccessTools.Method(typeof(ExchangeBarter), "Accept", Type.EmptyTypes),
            AccessTools.Field(typeof(ExchangeBarter), "conditions"), AccessTools.Field(typeof(ExchangeBarter), "currentBarterCounter"),
            AccessTools.Field(typeof(ExchangeBarter), "onAccept"), ValidMethod(), AccessTools.Property(typeof(EmporiumEntry), "trashcan"), ChildrenMethod(), SplitMethod()
        };
        internal static Func<MemberInfo[]> FindOffer = () => new MemberInfo[]
        {
            AccessTools.Method(typeof(BarterOffer), "IsConditionAcceptabled", Type.EmptyTypes), AccessTools.Method(typeof(BarterOffer), "OnBarterAccepted", Type.EmptyTypes),
            AccessTools.Field(typeof(BarterOffer), "conditions"), AccessTools.Field(typeof(BarterOffer), "isFreeReward"), ValidMethod(),
            AccessTools.Method(typeof(EmporiumEntry), "GetOwnedItemsBack", Type.EmptyTypes), AccessTools.Method(typeof(PlayerStore), "OnItemsSold", new[] { typeof(List<GameItem>) }), SplitMethod()
        };
        internal static Func<object, bool, List<BarterCondition>> Conditions = (b, a) => a ? ((ExchangeBarter)b).conditions : ((BarterOffer)b).conditions;
        internal static Func<bool, List<GameItem>> Owned = a => a ? GraphUtils.FindAllChildrenType<GameItem>(EmporiumEntry.Instance.trashcan) : EmporiumEntry.Instance.GetOwnedItemsBack();
        internal static Func<BarterCondition, GameItem, bool> Accepts = (c, i) => c.IsItemValid(i);
        internal static Func<GameItem, int> Count = i => i.unitCount;
        internal static Func<GameItem, bool> OwnRule = i => RequestGuard.OwnRule(i);
        internal static Func<GameItem, bool> Gone = i => i is GameItemElement element && element.IsDestroyed();
        internal static Func<GameItem, GameItem, string> Difference = EngineRules.Difference;
        internal static Action<GameItem, int> SetCount = (i, n) => i.unitCount = n;
        internal static Action<GameItem> Redraw = i => Runtime.Redraw(i);
        internal static Func<object, int> Counter = b => ((ExchangeBarter)b).currentBarterCounter;
        internal static Action<object, int> SetCounter = (b, n) => ((ExchangeBarter)b).currentBarterCounter = n;
        internal static Func<object, Action<List<GameItem>>> Callback = b => ((ExchangeBarter)b).onAccept;
        internal static Func<object, bool> Free = b => ((BarterOffer)b).isFreeReward;
        internal static Action<List<GameItem>> Sold = items => PlayerStore.Instance.OnItemsSold(items);
        internal static Action<Harmony, MethodInfo, HarmonyMethod, HarmonyMethod, HarmonyMethod> Patch =
            (h, m, pre, post, final) => h.Patch(m, prefix: pre, postfix: post, finalizer: final);
        private static bool exchangeFailed, offerFailed;
        private static readonly HashSet<string> reported = new HashSet<string>();
        private static ConditionalWeakTable<object, object> stopped = new ConditionalWeakTable<object, object>();
        [ThreadStatic] private static int checking;
        [ThreadStatic] private static bool taking;
        private static bool Enabled => Bridge.Active && (Runtime.Own || (Runtime.Mode == StackMode.Economy && Bridge.On()))
            && Bridge.Switch("barters_take_units");
        internal static bool ExchangeOn => ExchangeInstalled && Enabled;
        internal static bool OfferOn => OfferInstalled && Enabled;
        // Only our unit planner may inspect stacks without the original's refusal.
        internal static bool Bypass => checking > 0;
        private static MethodInfo ValidMethod() => AccessTools.Method(typeof(BarterCondition), "IsItemValid", new[] { typeof(GameItem) });
        private static MethodInfo SplitMethod() => AccessTools.Method(typeof(GameItem), "GetSplitItem", new[] { typeof(int) });
        private static MethodInfo ChildrenMethod() => AccessTools.Method(typeof(GraphUtils), "FindAllChildrenType");
        private static bool Method(MemberInfo member, Type result, bool isPublic, params Type[] types)
        {
            if (!(member is MethodInfo m) || m.IsStatic || m.ContainsGenericParameters || m.ReturnType != result || (isPublic && !m.IsPublic)) return false;
            ParameterInfo[] args = m.GetParameters();
            if (args.Length != types.Length) return false;
            for (int i = 0; i < args.Length; i++) if (args[i].ParameterType != types[i]) return false;
            return true;
        }
        private static bool Field(MemberInfo member, Type type) => member is FieldInfo f && f.IsPublic && !f.IsStatic
            && !f.IsInitOnly && !f.IsLiteral && f.FieldType == type;
        private static bool Trash(MemberInfo member) => member is PropertyInfo p && p.PropertyType == typeof(GameItem)
            && p.GetIndexParameters().Length == 0 && Method(p.GetGetMethod(), typeof(GameItem), true);
        private static bool Children(MemberInfo member)
        {
            if (!(member is MethodInfo m) || !m.IsPublic || !m.IsStatic || !m.IsGenericMethodDefinition || m.GetGenericArguments().Length != 1) return false;
            try
            {
                m = m.MakeGenericMethod(typeof(GameItem));
                ParameterInfo[] p = m.GetParameters();
                return m.ReturnType == typeof(List<GameItem>) && p.Length == 3 && p[0].ParameterType == typeof(GraphNodeStorage)
                    && p[1].ParameterType == typeof(Func<GameItem, bool>) && p[2].ParameterType == typeof(Func<GameItem, bool>)
                    && p[1].IsOptional && p[2].IsOptional && p[1].DefaultValue == null && p[2].DefaultValue == null;
            }
            catch (Exception) { return false; }
        }
        internal static bool ExchangeFits(MemberInfo[] m) => m != null && m.Length == 9
            && Method(m[0], typeof(bool), true) && Method(m[1], typeof(void), true) && Field(m[2], typeof(List<BarterCondition>))
            && Field(m[3], typeof(int)) && Field(m[4], typeof(Action<List<GameItem>>)) && Method(m[5], typeof(bool), true, typeof(GameItem))
            && Trash(m[6]) && Children(m[7]) && Method(m[8], typeof(GameItem), true, typeof(int));
        internal static bool OfferFits(MemberInfo[] m) => m != null && m.Length == 8
            && Method(m[0], typeof(bool), true) && Method(m[1], typeof(void), true) && Field(m[2], typeof(List<BarterCondition>))
            && Field(m[3], typeof(bool)) && Method(m[4], typeof(bool), true, typeof(GameItem)) && Method(m[5], typeof(List<GameItem>), false)
            && Method(m[6], typeof(void), false, typeof(List<GameItem>)) && Method(m[7], typeof(GameItem), true, typeof(int));
        internal static void TryInstall()
        {
            try
            {
                if (!Bridge.Active || (Runtime.Mode != StackMode.Own && Runtime.Mode != StackMode.Economy)) return;
                Install(true); Install(false);
            }
            catch (Exception error) { Report(true, "start", "installation failed. " + error.GetType().Name); }
        }
        private static void Install(bool exchange)
        {
            var patches = new List<KeyValuePair<MethodInfo, MethodInfo>>();
            Harmony harmony = null;
            try
            {
                if (exchange ? ExchangeInstalled || exchangeFailed : OfferInstalled || offerFailed) return;
                MemberInfo[] members = exchange ? FindExchange() : FindOffer();
                if (!(exchange ? ExchangeFits(members) : OfferFits(members))) throw new InvalidOperationException("Unexpected game shape.");
                harmony = new Harmony(Bridge.HarmonyId);
                MethodInfo can = exchange ? AccessTools.Method(typeof(BarterGuard), nameof(ExchangeCan)) : AccessTools.Method(typeof(BarterGuard), nameof(OfferCan));
                MethodInfo take = exchange ? AccessTools.Method(typeof(BarterGuard), nameof(ExchangeTake)) : AccessTools.Method(typeof(BarterGuard), nameof(OfferTake));
                MethodInfo canTarget = (MethodInfo)members[0], takeTarget = (MethodInfo)members[1];
                // Record before Patch: an adapter may apply and then throw.
                patches.Add(new KeyValuePair<MethodInfo, MethodInfo>(canTarget, can));
                Patch(harmony, canTarget, null, new HarmonyMethod(can), null);
                patches.Add(new KeyValuePair<MethodInfo, MethodInfo>(takeTarget, take));
                Patch(harmony, takeTarget, new HarmonyMethod(take) { priority = Priority.Last }, null, null);
                if (exchange) ExchangeInstalled = true; else OfferInstalled = true;
            }
            catch (Exception error)
            {
                if (exchange) { ExchangeInstalled = false; exchangeFailed = true; }
                else { OfferInstalled = false; offerFailed = true; }
                foreach (var patch in patches)
                    try { harmony?.Unpatch(patch.Key, patch.Value); } catch (Exception) { }
                Report(exchange, "install", "unit patches unavailable; this kind uses the stack-refusal setting. " + error.GetType().Name);
            }
        }
        private static void ExchangeCan(ExchangeBarter __instance, ref bool __result) => Can(__instance, true, ref __result);
        private static void OfferCan(BarterOffer __instance, ref bool __result) => Can(__instance, false, ref __result);
        private static bool ExchangeTake(ExchangeBarter __instance) => Take(__instance, true);
        private static bool OfferTake(BarterOffer __instance) => Take(__instance, false);
        internal static void Reset()
        {
            ExchangeInstalled = OfferInstalled = exchangeFailed = offerFailed = taking = false;
            checking = 0; stopped = new ConditionalWeakTable<object, object>(); reported.Clear();
        }
        private static void Report(bool exchange, string key, string message)
        {
            try { if (reported.Add((exchange ? "exchange:" : "offer:") + key))
                Bridge.Log?.Invoke("[Stacking] " + (exchange ? "exchange barter: " : "counter barter: ") + message); }
            catch (Exception) { }
        }
        private sealed class Snapshot
        {
            internal GameItem[] Items;
            internal int[] Counts;
            internal string[] Ids;
            internal GameInventory[] Parents;
            internal BarterCondition[] Conditions;
            internal BarterRules.Plan Plan;
        }
        private static Snapshot Plan(object barter, bool exchange)
        {
            var s = new Snapshot
            {
                Items = (Owned(exchange) ?? throw new InvalidOperationException("No item list.")).ToArray(),
                Conditions = (Conditions(barter, exchange) ?? throw new InvalidOperationException("No conditions.")).ToArray()
            };
            s.Counts = new int[s.Items.Length]; s.Ids = new string[s.Items.Length]; s.Parents = new GameInventory[s.Items.Length];
            var values = new List<RequestRules.Item>(); var seen = new HashSet<GameItem>();
            for (int i = 0; i < s.Items.Length; i++)
            {
                GameItem item = s.Items[i];
                if (ReferenceEquals(item, null) || !seen.Add(item)) throw new InvalidOperationException("Null or repeated item.");
                s.Counts[i] = Math.Max(1, Count(item)); s.Ids[i] = item.identifier; s.Parents[i] = item.parentInventory;
                bool own = s.Counts[i] > 1 && OwnRule(item);
                values.Add(new RequestRules.Item(s.Counts[i], s.Counts[i] > 1 && !own, own));
            }
            var accepts = new bool[s.Conditions.Length][];
            int depth = checking;
            try
            {
                checking++;
                for (int c = 0; c < s.Conditions.Length; c++)
                {
                    if (s.Conditions[c] == null) throw new InvalidOperationException("Null condition.");
                    accepts[c] = new bool[s.Items.Length];
                    for (int i = 0; i < s.Items.Length; i++) accepts[c][i] = Accepts(s.Conditions[c], s.Items[i]);
                }
            }
            finally { checking = depth; }
            s.Plan = BarterRules.Build(values, accepts);
            return s;
        }
        internal static void Can(object barter, bool exchange, ref bool result)
        {
            if (result) return;
            try
            {
                if (barter == null || !(exchange ? ExchangeOn : OfferOn)) return;
                BarterRules.Plan plan = Plan(barter, exchange).Plan;
                if (!plan.LeaveOriginal && plan.CanFill) result = true;
            }
            catch (Exception error) { Report(exchange, "count", "counting failed; original answer retained; 0 units taken. " + error.GetType().Name); }
        }
        private static void Validate(Snapshot s, object barter, bool exchange)
        {
            List<BarterCondition> conditions = Conditions(barter, exchange);
            if (conditions == null || conditions.Count != s.Conditions.Length) throw new InvalidOperationException("Conditions changed.");
            for (int c = 0; c < conditions.Count; c++)
                if (!ReferenceEquals(conditions[c], s.Conditions[c])) throw new InvalidOperationException("Condition changed.");
            for (int i = 0; i < s.Items.Length; i++)
                if (Math.Max(1, Count(s.Items[i])) != s.Counts[i] || Gone(s.Items[i]) || s.Items[i].identifier != s.Ids[i]
                    || !ReferenceEquals(s.Items[i].parentInventory, s.Parents[i])) throw new InvalidOperationException("Item changed during planning.");
        }
        private static bool SetSafely(GameItem item, int count)
        {
            for (int retry = 0; retry < 2; retry++)
                try { if (Count(item) != count) SetCount(item, count); if (Count(item) == count) return true; } catch (Exception) { }
            return false;
        }
        private static bool DestroySafely(GameItem item)
        {
            for (int retry = 0; retry < 2; retry++)
                try { if (!Gone(item)) OneUnit.Destroy(item); if (Gone(item)) return true; } catch (Exception) { }
            return false;
        }
        private sealed class Detached
        {
            internal GameItem Item;
            internal int Source;
        }
        private static void Rescue(Snapshot s, Detached split)
        {
            try
            {
                GameItem unit = split.Item;
                GameInventory home = s.Parents[split.Source];
                if (home == null || unit.parentInventory != null || Count(unit) <= 0 || Gone(unit)) return;
                for (int retry = 0; retry < 2; retry++)
                {
                    try { OneUnit.Accept(home, unit); } catch (Exception) { }
                    if (unit.parentInventory != null || Count(unit) <= 0 || Gone(unit)) return;
                }
            }
            catch (Exception) { }
        }
        private static bool RollBack(Snapshot s, List<Detached> splits)
        {
            int[] targets = (int[])s.Counts.Clone();
            bool[] restored = new bool[s.Items.Length];
            for (int i = 0; i < s.Items.Length; i++)
            {
                restored[i] = SetSafely(s.Items[i], targets[i]);
                if (restored[i]) continue;
                // Never destroy detached units when their source could not be restored.
                try { targets[i] = Count(s.Items[i]); } catch (Exception) { }
            }
            foreach (Detached split in splits)
            {
                GameItem unit = split.Item;
                if (Array.IndexOf(s.Items, unit) >= 0) continue;
                if (!restored[split.Source])
                {
                    Rescue(s, split);
                    try { targets[split.Source] = Count(s.Items[split.Source]); } catch (Exception) { }
                    continue;
                }
                if (SetSafely(unit, 0)) { DestroySafely(unit); continue; }
                // Destruction also removes a duplicate credit when emptying failed.
                if (DestroySafely(unit)) continue;
                // Both failed: remove the source's credit before rescuing this unit.
                try
                {
                    targets[split.Source] = Math.Max(1, targets[split.Source] - Math.Max(0, Count(unit)));
                    if (SetSafely(s.Items[split.Source], targets[split.Source]))
                    {
                        Rescue(s, split);
                        targets[split.Source] = Count(s.Items[split.Source]);
                    }
                }
                catch (Exception) { }
            }
            foreach (GameItem item in s.Items)
                try { Redraw(item); } catch (Exception) { }
            // Redraw must not undo a successful restore or compensation.
            for (int i = 0; i < s.Items.Length; i++) SetSafely(s.Items[i], targets[i]);
            try
            {
                for (int i = 0; i < s.Items.Length; i++) if (Count(s.Items[i]) != s.Counts[i]) return false;
                foreach (Detached split in splits)
                    if (Array.IndexOf(s.Items, split.Item) < 0 && !Gone(split.Item)) return false;
                return true;
            }
            catch (Exception) { return false; }
        }
        private static void VerifyGoods(Snapshot s, List<GameItem> goods, object barter, bool exchange)
        {
            List<BarterCondition> conditions = Conditions(barter, exchange);
            if (conditions == null || conditions.Count != s.Conditions.Length) throw new InvalidOperationException("Conditions changed while splitting.");
            for (int c = 0; c < conditions.Count; c++)
                if (!ReferenceEquals(conditions[c], s.Conditions[c])) throw new InvalidOperationException("Condition changed while splitting.");
            int[] left = (int[])s.Counts.Clone();
            foreach (int index in s.Plan.Takes) left[index]--;
            for (int i = 0; i < s.Items.Length; i++)
                if (Count(s.Items[i]) != Math.Max(1, left[i]) || Gone(s.Items[i]) || s.Items[i].identifier != s.Ids[i]
                    || !ReferenceEquals(s.Items[i].parentInventory, s.Parents[i])) throw new InvalidOperationException("Source changed during splitting.");
            for (int c = 0; c < goods.Count; c++)
                if (Count(goods[c]) != 1 || Gone(goods[c]) || goods[c].identifier != s.Ids[s.Plan.Takes[c]]
                    || (!ReferenceEquals(s.Items[s.Plan.Takes[c]], goods[c])
                        && (goods[c].parentInventory != null || Difference(s.Items[s.Plan.Takes[c]], goods[c]) != null)))
                    throw new InvalidOperationException("Staged unit changed.");
        }
        private static string Taken(Snapshot s, List<Detached> splits, bool handed)
        {
            if (s == null) return "0";
            long taken = 0;
            try
            {
                for (int i = 0; i < s.Items.Length; i++)
                {
                    GameItem source = s.Items[i];
                    bool moved = !ReferenceEquals(source.parentInventory, s.Parents[i]);
                    taken += Gone(source) || moved ? s.Counts[i] : Math.Max(0L, (long)s.Counts[i] - Count(source));
                }
                if (!handed)
                    foreach (Detached split in splits)
                        if (Array.IndexOf(s.Items, split.Item) < 0 && s.Parents[split.Source] != null
                            && ReferenceEquals(split.Item.parentInventory, s.Parents[split.Source]) && !Gone(split.Item))
                            taken -= Math.Max(0, Count(split.Item));
                return Math.Max(0L, taken).ToString();
            }
            catch (Exception) { return "an unknown number of"; }
        }
        internal static bool Take(object barter, bool exchange)
        {
            if (taking) return false;
            Snapshot s = null;
            var goods = new List<GameItem>(); var splits = new List<Detached>();
            bool changed = false, handed = false, completed = false, counterChanged = false, failed = false;
            int depth = checking, counter = 0; Exception fault = null;
            try
            {
                if (barter == null) return true;
                if (stopped.TryGetValue(barter, out _)) return false;
                if (!(exchange ? ExchangeOn : OfferOn)) return true;
                if (!exchange && (Free(barter) || Conditions(barter, false).Count == 0)) return true;
                taking = true; checking++;
                s = Plan(barter, exchange);
                if (s.Plan.LeaveOriginal) return true;
                if (!s.Plan.CanFill) return false;
                Action<List<GameItem>> callback = exchange ? Callback(barter) : Sold;
                if (exchange) counter = Counter(barter);
                Validate(s, barter, exchange);
                foreach (int index in s.Plan.Takes)
                {
                    GameItem source = s.Items[index], unit = source;
                    int before = Count(source);
                    if (before > 1)
                    {
                        changed = true;
                        unit = OneUnit.Split(source, 1);
                        if (!ReferenceEquals(unit, null) && !splits.Exists(split => ReferenceEquals(split.Item, unit)))
                            splits.Add(new Detached { Item = unit, Source = index });
                        if (ReferenceEquals(unit, null) || Array.IndexOf(s.Items, unit) >= 0 || goods.Contains(unit)
                            || unit.parentInventory != null || Count(source) != before - 1 || Count(unit) != 1
                            || unit.identifier != s.Ids[index] || Difference(source, unit) != null || Gone(unit))
                            throw new InvalidOperationException("Split did not preserve units, identity or state.");
                    }
                    if (Count(unit) != 1 || Gone(unit)) throw new InvalidOperationException("Not a single unit.");
                    goods.Add(unit);
                }
                VerifyGoods(s, goods, barter, exchange);
                foreach (GameItem item in s.Items) Redraw(item);
                VerifyGoods(s, goods, barter, exchange);
                if (exchange)
                {
                    counterChanged = true; SetCounter(barter, counter - 1);
                    if (Counter(barter) != counter - 1) throw new InvalidOperationException("Counter was not lowered.");
                }
                VerifyGoods(s, goods, barter, exchange);
                // Callbacks may change their list. Keep our own destruction list.
                var handoverGoods = new List<GameItem>(goods);
                handed = true;
                callback?.Invoke(handoverGoods);
                completed = true;
            }
            catch (Exception error)
            {
                failed = true; fault = error;
                // A callback belongs to the game or another mod. Its own fault must
                // reach the caller, and an attempted handover is not a completed one.
                if (handed) throw;
            }
            finally
            {
                if (completed && exchange)
                    for (int i = goods.Count - 1; i >= 0; i--) if (!DestroySafely(goods[i])) failed = true;
                if (failed)
                {
                    bool restored = true;
                    if (handed && !completed && s != null)
                        foreach (Detached split in splits) Rescue(s, split);
                    if (!handed)
                    {
                        if (counterChanged)
                        {
                            restored = false;
                            for (int retry = 0; retry < 2; retry++)
                                try { SetCounter(barter, counter); if (Counter(barter) == counter) { restored = true; break; } } catch (Exception) { }
                        }
                        if (changed && s != null) restored &= RollBack(s, splits);
                    }
                    if (handed || !restored)
                        try { stopped.GetValue(barter, _ => new object()); } catch (Exception) { }
                    Report(exchange, handed ? "handover" : "take", "taking failed; " + (changed || handed ? Taken(s, splits, completed) : "0")
                        + " units taken; original skipped; " + (handed ? "will not retry this barter." : restored ? "units restored." : "rollback incomplete; will not retry.")
                        + (fault == null ? "" : " " + fault.GetType().Name));
                }
                checking = depth; taking = false;
            }
            return false;
        }
    }
}

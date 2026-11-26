using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Stacking
{
    internal static class RequestGuard
    {
        internal static Func<MethodInfo> FindCan = () => AccessTools.Method(typeof(StoreRequest), "CanFillRequest", Type.EmptyTypes);
        internal static Func<MethodInfo> FindFill = () => AccessTools.Method(typeof(StoreRequest), "FillRequest", Type.EmptyTypes);
        internal static Func<MethodInfo> FindDelivered = () => AccessTools.Method(typeof(StoreRequest), "CheckDeliveredItem", new[] { typeof(GameItem) });
        internal static Func<MethodInfo> FindReward = () => AccessTools.Method(typeof(StoreRequest), "DispenseReward", Type.EmptyTypes);
        internal static Func<FieldInfo> FindRequired = () => AccessTools.Field(typeof(StoreRequest), "requiredItemCount");
        internal static Func<MethodInfo> FindOwned = () => AccessTools.Method(typeof(EmporiumEntry), "GetAllOwnedItems", new[] { typeof(bool), typeof(bool) });
        internal static Func<FieldInfo> FindRequests = () => AccessTools.Field(typeof(PlayerStore), "storeRequests");
        internal static Func<List<GameItem>> Owned = () => EmporiumEntry.Instance.GetAllOwnedItems();
        internal static Func<StoreRequest, GameItem, bool> Accepts = (request, item) => request.CanThisItemFillRequest(item);
        internal static Func<GameItem, int> Count = item => item.unitCount;
        internal static Func<GameItem, bool> OwnRule = OtherStackingRule;
        internal static Func<StoreRequest, int> Required = request => request.requiredItemCount;
        internal static Func<List<StoreRequest>> Requests = () => PlayerStore.Instance.storeRequests;
        internal static Action<StoreRequest, GameItem> Delivered = (request, item) => deliveredMethod.Invoke(request, new object[] { item });
        internal static Action<GameItem> Destroy = item => item.Destroy();
        internal static Func<GameItem, bool> Gone = item => item is GameItemElement element && element.IsDestroyed();
        internal static Action<GameItem, int> SetCount = (item, count) => item.unitCount = count;
        internal static Action<GameItem> Redraw = item => Runtime.Redraw(item);
        internal static Action<StoreRequest> Reward = request => request.DispenseReward();
        internal static Action<List<StoreRequest>, StoreRequest> Remove = (requests, request) => requests.Remove(request);
        internal static Action<Harmony, MethodInfo, HarmonyMethod, HarmonyMethod> Patch =
            (harmony, target, prefix, postfix) => harmony.Patch(target, prefix: prefix, postfix: postfix);
        internal static bool Installed;
        private static bool gaveUp;
        private static MethodInfo deliveredMethod;
        private static readonly HashSet<string> reported = new HashSet<string>();
        private static ConditionalWeakTable<StoreRequest, object> stopped = new ConditionalWeakTable<StoreRequest, object>();
        [ThreadStatic] private static int checking;
        [ThreadStatic] private static bool taking;
        internal static bool On => Installed && Bridge.Active
            && (Runtime.Own || (Runtime.Mode == StackMode.Economy && Bridge.On())) && Bridge.Switch("requests_take_units");
        internal static bool Bypass => checking > 0;

        private static bool OtherStackingRule(GameItem item)
        {
            if (Runtime.Mode != StackMode.Own) return Bridge.HasOwnRule(item);
            // Our engine attaches both delegates too. Inspect their origins without
            // executing another mod's rule; one foreign callback hands the item back.
            return OtherCallback(item.mayStackItemFunc) || OtherCallback(item.maxSlotAmountItemFunc);
        }

        private static bool OtherCallback(Delegate callbacks)
        {
            if (callbacks == null) return false;
            foreach (Delegate callback in callbacks.GetInvocationList())
                if (callback.Method.Module.Assembly != typeof(RequestGuard).Assembly) return true;
            return false;
        }
        internal static bool Fits(MethodInfo can, MethodInfo fill, MethodInfo delivered, MethodInfo reward,
            FieldInfo required, MethodInfo owned, FieldInfo requests) =>
            MethodFits(can, typeof(bool), true) && MethodFits(fill, typeof(void), true)
            && MethodFits(delivered, typeof(void), false, typeof(GameItem)) && MethodFits(reward, typeof(void), true)
            && FieldFits(required, typeof(int)) && OwnedFits(owned)
            && FieldFits(requests, typeof(List<StoreRequest>));

        // C# omits both optional arguments at the call site; they still exist in metadata.
        private static bool OwnedFits(MethodInfo method) => MethodFits(method, typeof(List<GameItem>), false, typeof(bool), typeof(bool))
            && method.GetParameters()[0].IsOptional && method.GetParameters()[1].IsOptional;

        private static bool MethodFits(MethodInfo method, Type result, bool isPublic, params Type[] types)
        {
            if (method == null || method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result || (isPublic && !method.IsPublic)) return false;
            ParameterInfo[] args = method.GetParameters();
            if (args.Length != types.Length) return false;
            for (int i = 0; i < types.Length; i++) if (args[i].ParameterType != types[i]) return false;
            return true;
        }

        private static bool FieldFits(FieldInfo field, Type type) => field != null && field.IsPublic && !field.IsStatic && field.FieldType == type;

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo can = null, fill = null, postfix = null, prefix = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp || (Runtime.Mode != StackMode.Own && Runtime.Mode != StackMode.Economy)) return;
                postfix = AccessTools.Method(typeof(RequestGuard), nameof(CanPostfix));
                prefix = AccessTools.Method(typeof(RequestGuard), nameof(FillPrefix));
                can = FindCan(); fill = FindFill();
                MethodInfo delivered = FindDelivered();
                if (!Fits(can, fill, delivered, FindReward(), FindRequired(), FindOwned(), FindRequests()))
                {
                    gaveUp = true;
                    Report("shape", "the game is laid out differently; requests do not take stack units and use the stack-refusal setting.");
                    return;
                }
                harmony = new Harmony(Bridge.HarmonyId);
                deliveredMethod = delivered;
                Patch(harmony, can, null, new HarmonyMethod(postfix));
                Patch(harmony, fill, new HarmonyMethod(prefix) { priority = Priority.Last }, null);
                Installed = true;
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false; deliveredMethod = null;
                Unpatch(harmony, can, postfix); Unpatch(harmony, fill, prefix);
                Report("install", "request unit patches failed to install; requests use the stack-refusal setting. " + error.GetType().Name);
            }
        }

        private static void Unpatch(Harmony harmony, MethodInfo target, MethodInfo patch)
        {
            try { if (harmony != null && target != null && patch != null) harmony.Unpatch(target, patch); }
            catch (Exception error) { Report("unpatch", "could not remove an inactive request unit patch. " + error.GetType().Name); }
        }

        private static void CanPostfix(StoreRequest __instance, ref bool __result) => CanFill(__instance, ref __result);
        private static bool FillPrefix(StoreRequest __instance) => Fill(__instance);
        internal static void Reset()
        {
            Installed = gaveUp = false; deliveredMethod = null; checking = 0; taking = false;
            reported.Clear(); stopped = new ConditionalWeakTable<StoreRequest, object>();
        }

        private static void Report(string key, string message)
        {
            try { if (reported.Add(key)) Bridge.Log?.Invoke("[Stacking] " + message); }
            catch (Exception) { }
        }

        private sealed class Snapshot
        {
            internal readonly List<GameItem> Items = new List<GameItem>();
            internal readonly List<RequestRules.Item> Values = new List<RequestRules.Item>();
            internal int Required;
            internal RequestRules.Plan Plan;
        }

        private static Snapshot Plan(StoreRequest request, ref bool stackRisk)
        {
            // Inspect every count before calling predicates. If a predicate fails before
            // an accepted stack is reached, the original must still not destroy stacks.
            GameItem[] owned = (Owned() ?? throw new InvalidOperationException("No owned-item list.")).ToArray();
            int[] counts = new int[owned.Length];
            bool stacks = false;
            var seen = new HashSet<GameItem>();
            for (int i = 0; i < owned.Length; i++)
            {
                if (ReferenceEquals(owned[i], null)) continue;
                if (!seen.Add(owned[i])) throw new InvalidOperationException("Repeated owned item.");
                counts[i] = Units(owned[i]); // A count below one is read as one: one object is one item.
                stacks |= counts[i] > 1;
            }
            stackRisk = stacks;
            var snapshot = new Snapshot { Required = Required(request) };
            int depth = checking;
            try
            {
                checking++;
                for (int i = owned.Length - 1; i >= 0; i--)
                {
                    GameItem item = owned[i];
                    if (ReferenceEquals(item, null) || !Accepts(request, item)) continue;
                    bool own = counts[i] > 1 && OwnRule(item);
                    snapshot.Items.Add(item);
                    snapshot.Values.Add(new RequestRules.Item(counts[i], counts[i] > 1 && !own, own));
                }
            }
            finally { checking = depth; }
            snapshot.Plan = RequestRules.Build(snapshot.Values, snapshot.Required);
            return snapshot;
        }

        internal static void CanFill(StoreRequest request, ref bool result)
        {
            if (result) return;
            try
            {
                if (!On || request == null) return;
                bool risk = true;
                result = Plan(request, ref risk).Plan.CanFill;
            }
            catch (Exception error) { Report("count", "request unit counting failed; keeping the original answer. " + error.GetType().Name); }
        }

        private static int Units(GameItem item) => Math.Max(1, Count(item));

        internal static bool Fill(StoreRequest request)
        {
            if (taking) return false; // A delivery/reward callback must not re-enter a take.
            bool stackRisk = true, started = false;
            long taken = 0;
            try
            {
                if (!On || request == null) return true;
                if (stopped.TryGetValue(request, out _)) return false;
                Snapshot snapshot = Plan(request, ref stackRisk);
                if (snapshot.Plan.LeaveOriginal) return true;
                if (!snapshot.Plan.CanFill) return false;
                List<StoreRequest> requests = Requests() ?? throw new InvalidOperationException("No request list.");
                if (!requests.Contains(request)) throw new InvalidOperationException("Request is no longer pending.");
                // Finish all validation before recording deliveries or removing goods.
                for (int i = 0; i < snapshot.Items.Count; i++)
                    if (Units(snapshot.Items[i]) != snapshot.Values[i].Units || Gone(snapshot.Items[i]))
                        throw new InvalidOperationException("An accepted item changed during planning.");
                started = taking = true;
                foreach (RequestRules.Take step in snapshot.Plan.Takes)
                {
                    GameItem item = snapshot.Items[step.Index];
                    int before = snapshot.Values[step.Index].Units;
                    for (int unit = 0; unit < step.Units; unit++) Delivered(request, item);
                    if (Units(item) != before || Gone(item)) throw new InvalidOperationException("A delivery callback changed the item.");
                    if (step.Destroy)
                    {
                        bool gone = false;
                        try { Destroy(item); }
                        finally
                        {
                            gone = Gone(item);
                            taken += gone ? step.Units : Math.Max(0L, (long)before - Units(item));
                        }
                        if (!gone) throw new InvalidOperationException("Item was not destroyed.");
                    }
                    else
                    {
                        // A setter may change the count before throwing. Count that loss
                        // in the fault report too; never retry an uncertain transaction.
                        try
                        {
                            SetCount(item, before - step.Units);
                            if (Units(item) != before - step.Units) throw new InvalidOperationException("Unit count did not match the plan.");
                            Redraw(item);
                            if (Units(item) != before - step.Units) throw new InvalidOperationException("Redraw changed the unit count.");
                        }
                        finally { taken += Math.Max(0L, (long)before - Units(item)); }
                    }
                }
                if (taken != snapshot.Required) throw new InvalidOperationException("Delivery was incomplete.");
                Reward(request);
                Remove(requests, request);
                return false;
            }
            catch (Exception error)
            {
                try { if (started && request != null) stopped.GetValue(request, _ => new object()); }
                catch (Exception) { }
                Report(started ? "take" : "plan", "request " + (started ? "taking" : "planning") + " failed; "
                    + taken + " units taken; " + (stackRisk ? "original skipped" : "original retained") + ". " + error.GetType().Name);
                return !started && !stackRisk;
            }
            finally { if (started) taking = false; }
        }
    }
}

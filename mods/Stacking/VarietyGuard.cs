using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Stacking
{
    // The counter changes the sale synchronously. Only the shop's storage may
    // receive the rest, through the same transaction in both stacking modes.
    internal static class VarietyGuard
    {
        internal static Func<MethodInfo> FindPlaced = () => AccessTools.Method(typeof(PlayerStore), "PlacedItemForSelling", new[] { typeof(GameItem) });
        internal static Func<FieldInfo> FindVariety = () => AccessTools.Field(typeof(StoreClient), "isRequireVariety");
        internal static Func<MethodInfo> FindBought = () => AccessTools.Method(typeof(StoreClient), "IsItemInVarietyList", new[] { typeof(GameItem) });
        internal static Func<object> CurrentCustomer = () =>
        {
            PlayerStore store = PlayerStore.Instance;
            return store == null || store.currentClientInstance == null ? null : store.currentClientInstance.GetClientBlueprint();
        };
        internal static Func<object, bool> WantsVariety = client => ((StoreClient)client).isRequireVariety;
        internal static Func<object, GameItem, bool> AlreadyBought = (client, item) => ((StoreClient)client).IsItemInVarietyList(item);
        internal static Func<GameInventory> Counter = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.backInvinvElement;
        internal static Func<GameInventory> Storage = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.invElement;
        internal static Func<GameInventory, GameItem, int, int> Place = (inventory, item, count) => GraphUtils.TryAcceptAll(inventory, item, count);
        internal static Func<GameItem, GameItem, string> Difference = EngineRules.Difference;
        internal static Action<GameItem, int> SetCount = (item, count) => item.unitCount = count;
        internal static Action Refresh = () => { if (NegociationUIManager.Instance != null) NegociationUIManager.Instance.RefreshUI(); };
        internal static Action<Harmony, MethodInfo, HarmonyMethod> Patch = (harmony, target, postfix) => harmony.Patch(target, postfix: postfix);
        internal static bool Installed;
        private static bool gaveUp;
        private sealed class FailedCount { internal int Count; internal object Customer; }
        private static ConditionalWeakTable<GameItem, FailedCount> failures = new ConditionalWeakTable<GameItem, FailedCount>();
        [ThreadStatic] private static bool splitting;

        // Only values; safe without the game or either mod running.
        internal static bool ShouldAct(StackMode mode, bool enabled, bool oneUnit, bool variety, bool owned, int count, bool bought) =>
            (mode == StackMode.Economy || mode == StackMode.Own) && enabled && oneUnit && variety && owned && count > 1 && !bought;

        internal static bool GameFits(MethodInfo placed, FieldInfo variety, MethodInfo bought) =>
            InstanceMethod(placed, typeof(void)) && InstanceMethod(bought, typeof(bool))
            && variety != null && variety.IsPublic && !variety.IsStatic && variety.FieldType == typeof(bool);

        private static bool InstanceMethod(MethodInfo method, Type result)
        {
            if (method == null || !method.IsPublic || method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] args = method.GetParameters();
            return args.Length == 1 && args[0].ParameterType == typeof(GameItem);
        }

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo target = null, postfix = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp || (Runtime.Mode != StackMode.Own && Runtime.Mode != StackMode.Economy)) return;
                target = FindPlaced();
                if (!GameShape.Ready || !GameFits(target, FindVariety(), FindBought()))
                {
                    gaveUp = true;
                    Bridge.Report("variety shape", "the game is laid out differently; the variety guard was not patched and customers buy stacks whole.");
                    return;
                }
                harmony = new Harmony(Bridge.HarmonyId);
                postfix = AccessTools.Method(typeof(VarietyGuard), nameof(Postfix));
                Patch(harmony, target, new HarmonyMethod(postfix));
                Installed = true;
                Bridge.Report("variety installed", "a customer who wants variety takes one unit of a stack (the rest goes to the shop's storage).");
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false;
                try { if (harmony != null && target != null && postfix != null) harmony.Unpatch(target, postfix); }
                catch (Exception removal) { Bridge.Fault("removing an incomplete variety patch", removal); }
                Bridge.Fault("installing the variety guard", error);
            }
        }

        internal static void Reset()
        {
            try
            {
                Installed = gaveUp = splitting = false;
                failures = new ConditionalWeakTable<GameItem, FailedCount>();
            }
            catch (Exception error) { Bridge.Fault("resetting the variety guard", error); }
        }

        // No finalizer: an exception from the sale itself must reach its caller.
        private static void Postfix(GameItem __0)
        {
            if (splitting) return;
            bool failed = false;
            object customer = null;
            splitting = true;
            try
            {
                if (!Installed || !Bridge.Active || ReferenceEquals(__0, null)
                    || (Runtime.Mode != StackMode.Own && Runtime.Mode != StackMode.Economy)
                    || !Bridge.Switch("enabled") || !Bridge.Switch("variety_one_unit")) return;
                customer = CurrentCustomer();
                // Stop at the first no: an ordinary customer needs only the
                // variety field read, without ownership or kind lookups.
                if (customer == null || !WantsVariety(customer)) return;
                if (!Bridge.Owned(__0)) return;
                int count = __0.unitCount;
                if (count <= 1) return;
                if (failures.TryGetValue(__0, out FailedCount previous) && previous.Count == count
                    && ReferenceEquals(previous.Customer, customer)) return;
                if (AlreadyBought(customer, __0) || !ShouldAct(Runtime.Mode, true, true, true, true, count, false)) return;
                GameInventory counter = Counter();
                if (counter == null || !ReferenceEquals(__0.parentInventory, counter)) return;
                GameInventory storage = Storage();
                if (storage == null || ReferenceEquals(storage, counter)) return;
                SplitOne(__0, count, storage, ref failed);
            }
            catch (Exception error)
            {
                failed = true;
                Bridge.Fault("placing one unit for a variety customer", error);
            }
            finally
            {
                if (failed && !ReferenceEquals(__0, null))
                {
                    try
                    {
                        FailedCount failure = failures.GetOrCreateValue(__0);
                        failure.Count = __0.unitCount; failure.Customer = customer;
                    }
                    catch (Exception error) { Bridge.Fault("remembering a failed variety split", error); }
                }
                splitting = false;
            }
        }

        private static void SplitOne(GameItem item, int count, GameInventory storage, ref bool failed)
        {
            int amount = count - 1;
            GameItem rest;
            try
            {
                rest = OneUnit.Split(item, amount);
                if (ReferenceEquals(rest, null)) throw new InvalidOperationException("The split returned no item.");
            }
            catch (Exception error)
            {
                // No returned rest: all count units belong back on the counter.
                // If both restore attempts fail after a null or throwing split,
                // there is no returned object to rescue; this cannot be repaired
                // here and the failed restore is reported.
                SetSafely(item, count, "restoring the count after a failed variety split", ref failed);
                failed = true;
                SplitFault(error);
                UpdateDisplay(item, ref failed, false);
                return;
            }

            try
            {
                if (ReferenceEquals(rest, item) || rest.parentInventory != null || item.unitCount != 1
                    || rest.unitCount != amount || item.identifier != rest.identifier || Difference(item, rest) != null)
                    throw new InvalidOperationException("The split did not preserve the count, identity or state.");
            }
            catch (Exception error)
            {
                // Before our placement: restore count units to the counter, then
                // empty and destroy the split-off object, even if the split wrongly
                // attached it. The source itself must never be emptied or destroyed.
                failed = true;
                bool rescued = false;
                if (ReferenceEquals(rest, item)) SetSafely(item, count, "restoring the source after an invalid variety split", ref failed);
                else rescued = ReturnRest(item, rest, count, storage, ref failed);
                SplitFault(error);
                UpdateDisplay(item, ref failed, rescued);
                return;
            }

            int left;
            try
            {
                left = Place(storage, rest, amount); // Returns units NOT placed.
                if (left < 0 || left > amount) throw new InvalidOperationException("Unexpected unplaced unit count.");
                if ((left == 0 && rest.parentInventory == null && rest.unitCount > 0)
                    || (left > 0 && rest.parentInventory != null && (long)rest.unitCount + left > amount))
                    throw new InvalidOperationException("The placement answer conflicts with the remainder's units.");
            }
            catch (Exception error)
            {
                failed = true;
                bool detached = rest.parentInventory == null;
                int remaining = rest.unitCount;
                // Nothing placed only when all amount units remain detached:
                // count returns to the counter and the rest becomes empty.
                // Otherwise an attached rest stays in storage; for a detached
                // rest, only its remaining units return. Units already merged
                // into storage are left there, so none are credited twice.
                bool moved = !detached || remaining < amount;
                if (detached) moved |= ReturnRest(item, rest, item.unitCount + remaining, storage, ref failed);
                Bridge.Report("variety placement failed", "the variety split's storage placement failed (" + error.GetType().Name
                    + "); units already placed stay in storage and only the detached remainder returns to the counter.");
                UpdateDisplay(item, ref failed, moved);
                return;
            }

            if (left == 0)
            {
                // One unit on the counter; all amount units are in storage,
                // either as the rest itself or merged into existing stacks.
                UpdateDisplay(item, ref failed);
                return;
            }

            // An attached rest owns its units in storage. The checked left can
            // return to the counter without counting any of those units twice.
            // A detached rest, however, owns exactly the units we can return;
            // the placing call's answer alone is never proof of that count.
            failed = true;
            bool outside = rest.parentInventory == null;
            int returned = outside ? rest.unitCount : left;
            if (outside && returned != left)
                Bridge.Report("variety placement failed", "the shop's storage reported " + left
                    + " unplaced units, but the detached remainder held " + returned + "; only its held units return to the counter.");
            bool rescuedRest = false;
            if (outside) rescuedRest = ReturnRest(item, rest, item.unitCount + returned, storage, ref failed);
            else SetSafely(item, item.unitCount + returned, "returning unplaced variety units", ref failed);
            Bridge.Report("variety storage full", "the shop's storage was full; the customer is offered "
                + item.unitCount + " units from the counter stack, including what did not fit. Units placed stay in storage.");
            UpdateDisplay(item, ref failed, returned < amount || rescuedRest);
        }

        private static void SplitFault(Exception error) =>
            Bridge.Report("variety split failed", "the variety split failed (" + error.GetType().Name
                + "); the counter count was restored where possible and the stack is sold whole.");

        // A count setter may throw before OR after its write. Inspect the value
        // and retry an unfinished write once, in its own guard. Never let it
        // prevent a later fault report or leave a transient failure uncorrected.
        private static bool SetSafely(GameItem item, int count, string where, ref bool failed)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (item.unitCount != count) SetCount(item, count);
                    if (item.unitCount == count) return true;
                    throw new InvalidOperationException("The count setter did not retain its value.");
                }
                catch (Exception error) { failed = true; Bridge.Fault(where, error); }
            }
            return item.unitCount == count;
        }

        private static bool ReturnRest(GameItem item, GameItem rest, int target, GameInventory storage, ref bool failed)
        {
            int before = item.unitCount;
            if (!SetSafely(item, target, "restoring the variety counter count", ref failed))
                return RescueRest(rest, storage, ref failed);
            if (!SetSafely(rest, 0, "emptying the variety remainder", ref failed))
            {
                // An unemptyable rest still owns its units. Undo the credit to
                // the counter instead of destroying those units or duplicating them.
                SetSafely(item, before, "undoing an unemptied variety remainder", ref failed);
                return RescueRest(rest, storage, ref failed);
            }
            // If destruction fails, the emptied object may remain in storage as
            // an empty slot. It owns no units and the failure is reported.
            try { OneUnit.Destroy(rest); }
            catch (Exception error) { failed = true; Bridge.Fault("destroying the empty variety remainder", error); }
            return false;
        }

        private static bool RescueRest(GameItem rest, GameInventory storage, ref bool failed)
        {
            try
            {
                // The count setter failed twice and this detached object still
                // owns its units. Give storage one final chance; credit nothing.
                if (rest.parentInventory != null || rest.unitCount <= 0) return false;
                int held = rest.unitCount;
                try { Place(storage, rest, held); }
                catch (Exception error) { failed = true; Bridge.Fault("the last placement of the variety remainder", error); }
                bool moved = rest.parentInventory != null || rest.unitCount < held;
                Bridge.Report("variety last placement", "after a failed count change, the last placement of the variety remainder was attempted; "
                    + (rest.parentInventory != null ? "it is in storage." : rest.unitCount + " units remain outside storage."));
                return moved;
            }
            catch (Exception error) { failed = true; Bridge.Fault("the last placement of the variety remainder", error); return false; }
        }

        private static void UpdateDisplay(GameItem item, ref bool failed, bool refresh = true)
        {
            try { Runtime.Redraw(item); }
            catch (Exception error) { failed = true; Bridge.Fault("redrawing the variety counter stack", error); }
            if (!refresh) return;
            try { Refresh(); }
            catch (Exception error) { failed = true; Bridge.Fault("refreshing the variety sale display", error); }
        }
    }
}

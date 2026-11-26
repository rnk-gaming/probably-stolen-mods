using System;
using System.Reflection;
using HarmonyLib;

namespace Stacking
{
    // Economy returns the unused part even when it found no home for it. Only
    // that detached, live remainder belongs to this guard; placed goods stay put.
    internal static class RestGuard
    {
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.StackUse");
        internal static Func<MethodInfo> FindSettle = () => AccessTools.Method("StationEconomy.StackUse:Settle");
        internal static Func<FieldInfo> FindUsedItem = () => AccessTools.Field("StationEconomy.StackUseSnap:It");
        internal static Func<FieldInfo> FindInventory = () => AccessTools.Field("StationEconomy.StackUseSnap:Inv");
        internal static Func<GameItem, bool> Gone = item => item is GameItemElement element && element.IsDestroyed();
        internal static Func<GameInventory> Main = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.invElement;
        internal static Func<GameInventory> Back = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.backInvinvElement;
        internal static Func<GameInventory> BackCounter = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.backInvinvElementCounter;
        internal static Func<GameInventory, GameItem, int, int> Place = (inventory, item, amount) => GraphUtils.TryAcceptAll(inventory, item, amount);
        internal static Action<GameItem, int> SetCount = (item, count) => item.unitCount = count;
        // The game's own routine after a count has changed, so that what is drawn follows.
        internal static Action<GameItem> Redraw = item => item.Validate();
        internal static Action<Harmony, MethodInfo, HarmonyMethod> Patch = (harmony, target, postfix) => harmony.Patch(target, postfix: postfix);
        internal static bool Installed;
        private static bool gaveUp;
        private static FieldInfo usedItem, inventory;
        private static int events;

        internal static bool Fits(Type type, MethodInfo method, FieldInfo item, FieldInfo inv)
        {
            if (type == null || !type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters
                || method == null || method.DeclaringType != type || method.Name != "Settle" || !method.IsStatic
                || method.ContainsGenericParameters || method.ReturnType != typeof(GameItem)) return false;
            ParameterInfo[] args = method.GetParameters();
            if (args.Length != 1 || args[0].ParameterType.FullName != "StationEconomy.StackUseSnap") return false;
            Type snap = args[0].ParameterType;
            return item != null && item.Name == "It" && !item.IsStatic && item.FieldType == typeof(GameItem)
                && item.DeclaringType.IsAssignableFrom(snap)
                && inv != null && inv.Name == "Inv" && !inv.IsStatic && typeof(GameInventory).IsAssignableFrom(inv.FieldType)
                && inv.DeclaringType.IsAssignableFrom(snap);
        }

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo target = null, postfix = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp || !Bridge.Running()) return;
                Type type = FindType();
                if (type == null) return; // Assemblies can arrive after this mod.
                target = FindSettle();
                FieldInfo item = FindUsedItem(), inv = FindInventory();
                if (!Fits(type, target, item, inv))
                {
                    gaveUp = true;
                    Bridge.Report("rest shape", "Station Economy's used stack settlement is laid out differently; the rest guard was not patched.");
                    return;
                }
                usedItem = item; inventory = inv;
                harmony = new Harmony(Bridge.HarmonyId);
                postfix = AccessTools.Method(typeof(RestGuard), nameof(Postfix));
                Patch(harmony, target, new HarmonyMethod(postfix));
                Installed = true;
                Bridge.Report("rest installed", "the rest of a used stack is never lost (with Station Economy's stacking).");
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false; usedItem = inventory = null;
                try { if (harmony != null && target != null && postfix != null) harmony.Unpatch(target, postfix); }
                catch (Exception removal) { Bridge.Fault("removing an incomplete rest patch", removal); }
                Bridge.Fault("connecting the rest guard to Station Economy", error);
            }
        }

        internal static void Reset()
        {
            Installed = gaveUp = false;
            usedItem = inventory = null;
            events = 0;
        }

        // A postfix only: an exception thrown by Economy itself is never swallowed.
        private static void Postfix(object __0, GameItem __result)
        {
            try
            {
                if (!Installed || !Bridge.On() || !Bridge.Switch("save_stack_rest") || ReferenceEquals(__result, null)
                    || Gone(__result) || __result.unitCount <= 0 || __result.parentInventory != null) return;
                int count = __result.unitCount, left = count;
                string identifier = __result.identifier;
                int[] unplaced = new int[4];
                for (int step = 0; step < unplaced.Length; step++)
                {
                    if (left > 0)
                    {
                        try
                        {
                            GameInventory destination = step == 0 ? (__0 == null ? null : (GameInventory)inventory.GetValue(__0))
                                : step == 1 ? Main() : step == 2 ? Back() : BackCounter();
                            if (destination != null)
                            {
                                int answer = Place(destination, __result, left);
                                if (answer < 0 || answer > left) throw new InvalidOperationException("Unexpected unplaced unit count.");
                                if (answer > 0 && (Gone(__result) || __result.parentInventory != null || __result.unitCount != answer))
                                    throw new InvalidOperationException("The detached remainder does not match the unplaced unit count.");
                                left = Rules.RestLeft(left, answer);
                            }
                        }
                        catch (Exception error)
                        {
                            Bridge.Fault("saving the rest of a used stack", error);
                            // A failed placement may already have moved some or all
                            // units. Continue only with the live, detached remainder.
                            if (Gone(__result) || __result.parentInventory != null) left = 0;
                            else
                            {
                                int remaining = __result.unitCount;
                                if (remaining > left) throw new InvalidOperationException("The remainder grew during placement.");
                                left = Math.Max(0, remaining);
                            }
                        }
                    }
                    unplaced[step] = left;
                }
                bool returned = false;
                if (left > 0 && __result.parentInventory == null && !Gone(__result))
                {
                    GameItem used = __0 == null ? null : (GameItem)usedItem.GetValue(__0);
                    if (!ReferenceEquals(used, null) && !ReferenceEquals(used, __result) && !Gone(used) && used.parentInventory != null)
                    {
                        ReturnToStack(used, __result, left);
                        returned = true;
                        try { Redraw(used); }
                        catch (Exception error) { Bridge.Fault("redrawing a stack that took its rest back", error); }
                    }
                }
                if (events < 20)
                {
                    events++;
                    Bridge.Log?.Invoke("[Stacking] the rest of a used stack found no room: "
                        + Rules.RestMessage(count, unplaced, returned, identifier));
                }
            }
            catch (Exception error) { Bridge.Fault("saving the rest of a used stack", error); }
        }

        private static void ReturnToStack(GameItem used, GameItem rest, int left)
        {
            int before = used.unitCount, total = checked(before + left);
            try
            {
                // Empty before crediting. Keep the empty object rather than calling
                // Destroy and involving another mod's destruction patches.
                SetCount(rest, 0);
                if (rest.unitCount != 0) throw new InvalidOperationException("The remainder count was not cleared.");
                SetCount(used, total);
                if (used.unitCount != total) throw new InvalidOperationException("The stack did not retain the rescued units.");
            }
            catch (Exception error)
            {
                Bridge.Fault("saving the rest of a used stack", error);
                // A setter may throw after its write. Restore only the units that
                // were not credited, so an already completed transfer is not doubled.
                int credited = used.unitCount - before;
                if (credited == 0) RestoreCount(rest, left);
                else if (credited == left) RestoreCount(rest, 0);
                throw;
            }
        }

        private static void RestoreCount(GameItem item, int count)
        {
            // Retry a transient setter failure once, inspecting after each attempt.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { if (item.unitCount != count) SetCount(item, count); if (item.unitCount == count) return; }
                catch (Exception error) { Bridge.Fault("saving the rest of a used stack", error); }
            }
        }
    }
}

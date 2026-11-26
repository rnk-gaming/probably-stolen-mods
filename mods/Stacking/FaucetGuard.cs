using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Stacking
{
    // Faucet Filter counts one upgrade unit before queuing the dropped item for
    // destruction. With Economy's stacks, keep the remainder out of that queue.
    internal static class FaucetGuard
    {
        internal static Func<Type> FindType = () => AccessTools.TypeByName("FaucetFilter.FaucetController");
        internal static Func<MethodInfo> FindDrop = () => AccessTools.Method("FaucetFilter.FaucetController:OnUpgradeMaterialDropped", new[] { typeof(GameItem) });
        internal static Func<FieldInfo> FindPending = () => AccessTools.Field(AccessTools.TypeByName("FaucetFilter.FaucetController"), "_pendingDestroy");
        internal static Action<GameItem, int> LowerCount = (item, count) => item.unitCount = count;
        internal static Action<Harmony, MethodInfo, HarmonyMethod, HarmonyMethod> Patch =
            (harmony, target, prefix, postfix) => harmony.Patch(target, prefix: prefix, postfix: postfix);
        internal static bool Installed;
        private static bool gaveUp;
        private static FieldInfo pendingDestroy;

        // Only values: no game, settings or Workshop calls.
        internal static bool ShouldAct(StackMode mode, bool on, bool pending, int count) =>
            mode == StackMode.Economy && on && pending && count > 1;

        internal static bool Fits(MethodInfo method, FieldInfo field)
        {
            try
            {
                if (method == null || method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(void)
                    || field == null || field.IsStatic || field.FieldType != typeof(List<GameItem>)) return false;
                ParameterInfo[] args = method.GetParameters();
                return args.Length == 1 && args[0].ParameterType == typeof(GameItem);
            }
            catch (Exception error) { Bridge.Fault("checking Faucet Filter's shape", error); return false; }
        }

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo target = null, prefix = null, postfix = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp) return;
                Type type = FindType();
                if (type == null) return; // Workshop assemblies may arrive after ours.
                target = FindDrop();
                FieldInfo field = FindPending();
                if (!Fits(target, field))
                {
                    gaveUp = true;
                    Bridge.Report("faucet shape", "Faucet Filter is laid out differently; its faucet was not patched.");
                    return;
                }
                harmony = new Harmony(Bridge.HarmonyId);
                prefix = AccessTools.Method(typeof(FaucetGuard), nameof(Prefix));
                postfix = AccessTools.Method(typeof(FaucetGuard), nameof(Postfix));
                pendingDestroy = field;
                Patch(harmony, target, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
                Installed = true;
                Bridge.Report("faucet installed", "connected to Faucet Filter: a stack dropped on its faucet gives one unit (with Station Economy's stacking).");
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false; pendingDestroy = null;
                Remove(harmony, target, prefix);
                Remove(harmony, target, postfix);
                Bridge.Fault("connecting to Faucet Filter", error);
            }
        }

        private static void Remove(Harmony harmony, MethodInfo target, MethodInfo patch)
        {
            try { if (harmony != null && target != null && patch != null) harmony.Unpatch(target, patch); }
            catch (Exception error) { Bridge.Fault("removing an incomplete faucet patch", error); }
        }

        internal static void Reset()
        {
            try { Installed = gaveUp = false; pendingDestroy = null; }
            catch (Exception error) { Bridge.Fault("resetting the faucet guard", error); }
        }

        private static bool On(GameItem item) => Installed && Bridge.Active && Runtime.Mode == StackMode.Economy
            && !ReferenceEquals(item, null) && Bridge.Switch("enabled") && Bridge.Switch("faucet_one_unit");

        private static List<GameItem> PendingList(object instance)
        {
            if (pendingDestroy == null || instance == null || !pendingDestroy.DeclaringType.IsInstanceOfType(instance))
                throw new InvalidOperationException("Unexpected faucet instance.");
            return (List<GameItem>)pendingDestroy.GetValue(instance)
                ?? throw new InvalidOperationException("Missing faucet destroy list.");
        }

        private static int References(List<GameItem> pending, GameItem item, out int last)
        {
            int count = 0; last = -1;
            for (int i = 0; i < pending.Count; i++)
                if (ReferenceEquals(pending[i], item)) { count++; last = i; }
            return count;
        }

        private static void Prefix(object __instance, GameItem __0, out int __state)
        {
            __state = 0;
            try
            {
                if (On(__0)) __state = References(PendingList(__instance), __0, out _);
            }
            catch (Exception error) { Bridge.Fault("taking one unit for Faucet Filter", error); }
        }

        // No finalizer: an exception from Faucet Filter itself must reach its caller.
        private static void Postfix(object __instance, GameItem __0, int __state)
        {
            try
            {
                if (!On(__0)) return;
                var pending = PendingList(__instance);
                bool added = References(pending, __0, out int last) == (long)__state + 1;
                int count = __0.unitCount;
                if (!ShouldAct(Runtime.Mode, true, added, count)) return;
                // Remove only the last reference added by this call, by identity.
                // If removal fails, never lower the count.
                pending.RemoveAt(last);
                try { LowerCount(__0, count - 1); }
                catch
                {
                    // A setter can mutate before it throws. Restore the count before
                    // restoring the queue; use the real setter, not the test boundary.
                    try { __0.unitCount = count; }
                    catch (Exception error) { Bridge.Fault("restoring a stack after a faucet fault", error); }
                    // Removal did not shrink capacity; this needs no new storage.
                    pending.Insert(last, __0);
                    throw;
                }
                // A display fault must not undo a unit already taken.
                try { Runtime.Redraw(__0); }
                catch (Exception error) { Bridge.Fault("redrawing a stack after a faucet drop", error); }
            }
            catch (Exception error) { Bridge.Fault("taking one unit for Faucet Filter", error); }
        }
    }
}

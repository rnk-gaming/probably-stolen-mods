using System;
using System.Reflection;
using HarmonyLib;

namespace Stacking
{
    // Station Economy uses a dropped stack of metal or electronic parts one unit after
    // another on the item it was dropped on, and asks the game before each unit whether
    // it may still be used there. When the first use replaces that item (the game
    // destroys a corrupted module and makes a new module), the game still answers yes
    // for the destroyed item, and every further unit makes another new item. The answer
    // is turned to no for a destroyed item, only while that loop runs and only for the
    // item the loop was given.
    internal static class TargetGuard
    {
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.StackUse");
        internal static Func<MethodInfo> FindUseAll = () => AccessTools.Method("StationEconomy.StackUse:UseAll");
        internal static Func<MethodInfo> FindCanTarget = () => AccessTools.DeclaredMethod(typeof(GameItem), "CanTarget", new[] { typeof(GameItem) });
        internal static Func<GameItem, bool> Gone = item => item is GameItemElement element && element.IsDestroyed();
        internal static Action<Harmony, MethodInfo, HarmonyMethod, HarmonyMethod> PatchLoop =
            (harmony, target, prefix, finalizer) => harmony.Patch(target, prefix: prefix, finalizer: finalizer);
        internal static Action<Harmony, MethodInfo, HarmonyMethod> PatchQuestion =
            (harmony, target, postfix) => harmony.Patch(target, postfix: postfix);
        internal static bool Installed;
        private static bool gaveUp;
        private static int events;
        // The item Station Economy's loop works on, on this thread; null outside the loop.
        [ThreadStatic] private static GameItem looped;

        // Entered is false when another mod's prefix kept ours from running; nothing is restored then.
        internal struct Previous { internal GameItem Item; internal bool Entered; }

        internal static bool Fits(Type type, MethodInfo useAll, MethodInfo canTarget)
        {
            if (type == null || !type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters
                || useAll == null || useAll.DeclaringType != type || useAll.Name != "UseAll" || !useAll.IsStatic
                || useAll.ContainsGenericParameters || useAll.ReturnType != typeof(void)) return false;
            ParameterInfo[] loop = useAll.GetParameters();
            if (loop.Length != 3 || loop[0].ParameterType.FullName != "StationEconomy.StackUseSnap"
                || loop[1].ParameterType != typeof(GameItem) || loop[2].ParameterType != typeof(GameItem)) return false;
            if (canTarget == null || canTarget.Name != "CanTarget" || canTarget.IsStatic || canTarget.ContainsGenericParameters
                || canTarget.ReturnType != typeof(bool)) return false;
            ParameterInfo[] question = canTarget.GetParameters();
            return question.Length == 1 && question[0].ParameterType == typeof(GameItem);
        }

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo useAll = null, canTarget = null, enter = null, leave = null, answer = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp || !Bridge.Running()) return;
                Type type = FindType();
                if (type == null) return; // Assemblies can arrive after this mod.
                useAll = FindUseAll(); canTarget = FindCanTarget();
                if (!Fits(type, useAll, canTarget))
                {
                    gaveUp = true;
                    Bridge.Report("target shape", "Station Economy's use of a whole stack is laid out differently; the target guard was not patched.");
                    return;
                }
                harmony = new Harmony(Bridge.HarmonyId);
                enter = AccessTools.Method(typeof(TargetGuard), nameof(Enter));
                leave = AccessTools.Method(typeof(TargetGuard), nameof(Leave));
                answer = AccessTools.Method(typeof(TargetGuard), nameof(Answer));
                // The question first: with the loop patched alone nothing changes. Last, so that
                // another mod's postfix that says yes cannot undo the stop.
                PatchQuestion(harmony, canTarget, new HarmonyMethod(answer) { priority = Priority.Last });
                PatchLoop(harmony, useAll, new HarmonyMethod(enter), new HarmonyMethod(leave));
                Installed = true;
                Bridge.Report("target installed", "a stack of parts is not used on an item that is gone (with Station Economy's stacking).");
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false; looped = null;
                Remove(harmony, useAll, enter); Remove(harmony, useAll, leave); Remove(harmony, canTarget, answer);
                Bridge.Fault("connecting the target guard to Station Economy", error);
            }
        }

        private static void Remove(Harmony harmony, MethodInfo target, MethodInfo patch)
        {
            try { if (harmony != null && target != null && patch != null) harmony.Unpatch(target, patch); }
            catch (Exception error) { Bridge.Fault("removing an incomplete target patch", error); }
        }

        internal static void Reset()
        {
            Installed = gaveUp = false;
            looped = null;
            events = 0;
        }

        // Prefix and finalizer only: the loop itself always runs, and an exception it
        // throws is passed on unchanged (a finalizer without a result does not catch).
        private static void Enter(GameItem __2, out Previous __state)
        {
            __state = new Previous { Item = looped, Entered = true };
            try { looped = __2; }
            catch (Exception error) { Bridge.Fault("following Station Economy's use of a stack", error); }
        }

        private static void Leave(Previous __state)
        {
            try { if (__state.Entered) looped = __state.Item; }
            catch (Exception error) { Bridge.Fault("following Station Economy's use of a stack", error); }
        }

        // Asked for every item the player points at, so the cheap tests come first.
        private static void Answer(GameItem __0, ref bool __result)
        {
            try
            {
                if (!__result || ReferenceEquals(looped, null) || !ReferenceEquals(__0, looped)) return;
                if (!Installed || !Bridge.On() || !Bridge.Switch("stack_use_live_target") || !Gone(__0)) return;
                __result = false;
                if (events < 20)
                {
                    events++;
                    Bridge.Log?.Invoke("[Stacking] stopped using a stack on " + (__0.identifier ?? "an item") + ": the item is gone.");
                }
            }
            catch (Exception error) { Bridge.Fault("checking the target of a stack", error); }
        }
    }
}

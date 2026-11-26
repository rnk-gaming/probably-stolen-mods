using System;
using HarmonyLib;
using LocalMods;
namespace StorageUpgrades
{
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayTarget))]
    internal static class MayTargetPatch
    {
        internal static bool Suppress;
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (Suppress) return;
            if (MachineStorage.Pair(__instance, targetItem)) __result = MachineStorage.Verdict(__instance, targetItem, __result);
            else if (Storage.Pair(__instance, targetItem)) __result = Storage.Verdict(__instance, targetItem, __result);
        }
    }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.CanTarget))]
    internal static class CanTargetPatch
    {
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (MayTargetPatch.Suppress) return;
            if (MachineStorage.Pair(__instance, targetItem)) __result = MachineStorage.Verdict(__instance, targetItem, __result);
            else if (Storage.Pair(__instance, targetItem)) __result = Storage.Verdict(__instance, targetItem, __result);
        }
    }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.Target))]
    internal static class TargetPatch
    {
        internal static Func<GameItem, GameItem, bool> GameAllows = (source, target) =>
        {
            bool before = MayTargetPatch.Suppress;
            MayTargetPatch.Suppress = true;
            // An error thrown by the game or another mod here is theirs and is not hidden.
            try { return source.MayTarget(target) && source.CanTarget(target); }
            finally { MayTargetPatch.Suppress = before; }
        };

        private static bool Prefix(GameItem __instance, GameItem targetItem)
        {
            if (MachineStorage.Pair(__instance, targetItem))
                return !Apply(__instance, targetItem, MachineStorage.Apply, MachineStorage.CanApply) && GameAllows(__instance, targetItem);
            if (Storage.Pair(__instance, targetItem))
                return !Apply(__instance, targetItem, Storage.Apply, Storage.CanApply) && GameAllows(__instance, targetItem);
            return true;
        }

        private static bool Apply(GameItem source, GameItem target, Func<GameItem, GameItem, bool> apply, Func<GameItem, GameItem, bool> ready)
        {
            try { return Material.Held(source, () => Material.Dropped(source, target, apply, () => false, false, Storage.Switch("stack_steps"), ready, source.identifier == "metal_ingot")); }
            catch (Exception error) { Storage.Fault("storage drop", error); return false; }
        }

        // The frame is opened before any other prefix and closed after every postfix.
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Enter(out Material.Frame __state) => __state = Material.Enter();

        private static void Finalizer(Material.Frame __state) => Material.Leave(__state);
    }
}

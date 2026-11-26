using System;
using HarmonyLib;
using LocalMods;
namespace Backpacks
{
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayTarget))]
    internal static class MayTargetPatch
    {
        internal static bool Suppress;
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (Suppress) return;
            if (Backpacks.Pair(__instance, targetItem)) __result = Backpacks.Verdict(__instance, targetItem, __result);
        }
    }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.CanTarget))]
    internal static class CanTargetPatch
    {
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (MayTargetPatch.Suppress) return;
            if (Backpacks.Pair(__instance, targetItem)) __result = Backpacks.Verdict(__instance, targetItem, __result);
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
            if (!Backpacks.Pair(__instance, targetItem)) return true;
            if (Drops.Handles(targetItem)) return GameAllows(__instance, targetItem);
            return !Material.Held(__instance, () => Backpacks.Dropped(__instance, targetItem)) && GameAllows(__instance, targetItem);
        }

        // The frame is opened before any other prefix and closed after every postfix.
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Enter(out Material.Frame __state) => __state = Material.Enter();

        private static void Finalizer(Material.Frame __state) => Material.Leave(__state);
    }
}

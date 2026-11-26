using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

using LocalMods;
using Material = LocalMods.Material;

namespace Backpacks
{
    // Rules accept only captured values and replaceable boundaries.
    internal static class DropRules
    {
        internal static IEnumerable<T> PageItems<T>(T root, Func<T,IEnumerable<T>> children, Func<T,bool> item, Func<T,bool> grid) where T : class
        {
            foreach (T page in PocketRules.Pages(root, children, item, grid))
            {
                IEnumerable<T> direct = children(page);
                if (direct != null) foreach (T child in direct) if (child != null && item(child)) yield return child;
            }
        }

        internal static bool Run(GameItem[] items, string[] ids,
            Func<string, IEnumerable<GameItem>> contents, Func<GameItem, bool> usable,
            Func<GameItem, int> needed, Func<GameItem, bool> apply, Func<bool> more,
            Func<string, bool> donor)
        {
            bool any = false;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (id == null || !seenIds.Add(id)) continue;
                var payers = new List<GameItem>();
                var seen = new HashSet<GameItem>();
                for (int j = 0; j < items.Length; j++)
                    if (ids[j] == id && items[j] != null && seen.Add(items[j])) payers.Add(items[j]);
                // A Compartment donor must be the bag actually dropped.
                if (!donor(id))
                    foreach (GameItem item in contents(id))
                        if (item != null && seen.Add(item)) payers.Add(item);
                GameItem First()
                {
                    foreach (GameItem payer in payers) if (usable(payer)) return payer;
                    return null;
                }
                GameItem first = First();
                if (first == null) continue;
                // Kept Tinkerer units pay too. Bound payments by the pending
                // ingredient, not by changes in the payer's count.
                int cap = needed(first);
                for (int n = 0; n < cap; n++)
                {
                    GameItem payer = First();
                    if (payer == null || !apply(payer)) break;
                    any = true;
                    if (!more()) break;
                }
            }
            return any;
        }
    }

    internal static class Drops
    {
        // Unity/game boundaries, replaced by the offline harness.
        internal static Func<GameItem, bool> Alive = item => item is GameItemElement element && !element.IsDestroyed();
        internal static Func<GameItem, IEnumerable<GameItem>> Contents = PageContents;
        internal static Func<GameItem, GameItem, bool> Inside = (item, target) => Pockets.Inside(item, target);
        internal static Func<GameItem> GroupTarget = RaycastTarget;

        private static GameItem RaycastTarget()
        {
            return RenderHandler.RaycastElement<GameItem>(Input.mousePosition);
        }

        private static IEnumerable<GameItem> PageContents(GameItem target)
        {
            if (target.contentWindow == null) yield break;
            foreach (GraphNodeStorage node in DropRules.PageItems<GraphNodeStorage>(target.contentWindow,
                child => child.children, child => child is GameItem, child => child is GameGridInventory))
                yield return (GameItem)node;
        }

        internal sealed class Drop
        {
            internal Drop Previous;
            internal GameItem Target;
            internal GameItem[] Items = Array.Empty<GameItem>();
            internal string[] Ids = Array.Empty<string>();
            internal int StackUnits;
            internal bool Finished;
        }

        // Each invocation owns its frame, including cancelled/no-target calls.
        // Finalizers restore it even when another patch or the game throws.
        private static Drop current;

        // `whole` is false when only part of a stack is being dragged: the dragged object
        // then still holds units that were not dropped, so it must not pay. The units
        // that were dropped are found in the backpack's contents.
        internal static Drop Begin(GameItem target, IEnumerable<GameItem> items, bool cancelled, bool whole = true)
        {
            var drop = new Drop { Previous = current };
            current = drop;
            try
            {
                if (cancelled || target == null || !Backpacks.Supported(target.identifier)) return drop;
                var captured = new List<GameItem>(); var ids = new List<string>();
                if (items != null)
                    foreach (GameItem item in items)
                    {
                        // EndGroupDrag treats a selected target as no target.
                        if (item == target) return drop;
                        if (item == null) continue;
                        captured.Add(item); ids.Add(item.identifier);
                    }
                drop.Items = whole ? captured.ToArray() : new GameItem[0]; drop.Ids = ids.ToArray(); drop.Target = target;
                if (whole && captured.Count == 1 && !Backpacks.Supported(ids[0]))
                    drop.StackUnits = Math.Max(0, Material.Units(captured[0]));
            }
            catch (Exception error) { Storage.Fault("capturing backpack drop", error); }
            return drop;
        }

        internal static bool Handles(GameItem target) => current != null && current.Target == target;

        internal static Exception Finish(Drop drop, Exception error)
        {
            if (drop == null || drop.Finished) return error;
            drop.Finished = true;
            try
            {
                // A failed original remains failed, unchanged, and pays nothing.
                if (error == null && drop.Target != null && Alive(drop.Target))
                {
                    bool Usable(GameItem item) => Alive(item) && !Material.Spent(item) && Material.Units(item) > 0 && Storage.Owned(item);
                    if (drop.StackUnits > 1 && Backpacks.Switch("stack_steps"))
                    {
                        PayStack(drop, Usable);
                        return error;
                    }
                    bool any = DropRules.Run(drop.Items, drop.Ids,
                        id => MatchingContents(drop.Target, id), Usable,
                        item => Backpacks.Needed(item, drop.Target),
                        item => Backpacks.Apply(item, drop.Target), () => Backpacks.StepOpen, Backpacks.Supported);
                    // Nothing is moved afterwards: the game has already put what it accepts
                    // into the backpack, and what it refused stays where the game left it.
                    _ = any;
                }
            }
            catch (Exception fault) { Storage.Fault("handling backpack drop", fault); }
            finally { current = drop.Previous; }
            return error;
        }

        private static void PayStack(Drop drop, Func<GameItem, bool> usable)
        {
            // The game may have merged the dropped stack into an existing one.
            // Only the captured number of units may pay, regardless of that merge.
            var payers = new List<GameItem>(drop.Items);
            foreach (GameItem item in MatchingContents(drop.Target, drop.Ids[0]))
                if (!payers.Contains(item)) payers.Add(item);
            bool previousGone = Material.Gone, previousOpen = Backpacks.StepOpen;
            bool any = false;
            try
            {
                for (int n = 0; n < drop.StackUnits; n++)
                {
                    GameItem source = null;
                    foreach (GameItem item in payers) if (usable(item)) { source = item; break; }
                    if (source == null) break;
                    Material.Gone = false;
                    long faults = Storage.Faults;
                    if (!Material.Step(source, drop.Target, Backpacks.CanApply, Backpacks.Apply)) break;
                    any = true;
                    if (Storage.Faults != faults) break;
                }
            }
            finally { Material.Gone = previousGone; Backpacks.StepOpen = previousOpen; }
            // An upgrade may have made room after the game tried to store the drop.
            // A merged source is already gone; never move its recipient as well.
            if (any && usable(drop.Items[0]) && !Inside(drop.Items[0], drop.Target))
                Storage.Guard("putting the rest into the backpack", () => Material.Stow(drop.Items[0], drop.Target));
        }

        private static IEnumerable<GameItem> MatchingContents(GameItem target, string id)
        {
            foreach (GameItem item in Contents(target))
                if (item != null && item.identifier == id) yield return item;
        }

        internal static bool Shape(MethodInfo method, params Type[] parameters)
        {
            if (method == null || !method.IsPrivate || method.IsStatic || method.ReturnType != typeof(void) || method.ContainsGenericParameters) return false;
            ParameterInfo[] actual = method.GetParameters();
            if (actual.Length != parameters.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i].ParameterType != parameters[i]) return false;
            return true;
        }
    }

    [HarmonyPatch]
    internal static class SingleDropPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new[] { typeof(bool) });

        private static bool Prepare()
        {
            try
            {
                if (Drops.Shape((MethodInfo)TargetMethod(), typeof(bool))
                    && AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem")?.FieldType == typeof(GameItemElement)
                    && AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem")?.FieldType == typeof(GameItem)
                    && AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove")?.FieldType == typeof(int)) return true;
                throw new InvalidOperationException("Single drag method or fields differ");
            }
            catch (Exception error) { Storage.Fault("single backpack drop shape", error); return false; }
        }

        internal static Func<GameItem, int, bool> Whole = (item, dragged) => item == null || dragged >= item.unitCount;

        private static void Prefix(bool cancelled, GameItemElement ___currentItem, GameItem ___lastItem, int ___currentRemove, out Drops.Drop __state)
        {
            bool whole = true;
            try { whole = Whole(___currentItem, ___currentRemove); }
            catch (Exception error) { Storage.Fault("reading the dragged amount", error); }
            __state = Drops.Begin(___lastItem, new GameItem[] { ___currentItem }, cancelled, whole);
        }

        private static Exception Finalizer(Drops.Drop __state, Exception __exception) => Drops.Finish(__state, __exception);
    }

    [HarmonyPatch]
    internal static class GroupDropPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(ItemMultiSelectHandler), "EndGroupDrag", Type.EmptyTypes);

        private static bool Prepare()
        {
            try
            {
                if (Drops.Shape((MethodInfo)TargetMethod())
                    && AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems")?.FieldType == typeof(List<GameItemElement>)) return true;
                throw new InvalidOperationException("Group drag method or fields differ");
            }
            catch (Exception error) { Storage.Fault("group backpack drop shape", error); return false; }
        }

        private static void Prefix(List<GameItemElement> ___selectedItems, out Drops.Drop __state)
        {
            // Install the frame before a raycast can fail, so a nested drag cannot
            // accidentally inherit its parent's target suppression.
            __state = Drops.Begin(null, null, false);
            try
            {
                GameItem target = Drops.GroupTarget();
                Drops.Drop captured = Drops.Begin(target, ___selectedItems, false);
                captured.Previous = __state.Previous;
                __state = captured;
            }
            catch (Exception error) { Storage.Fault("capturing group backpack drop", error); }
        }

        private static Exception Finalizer(Drops.Drop __state, Exception __exception) => Drops.Finish(__state, __exception);
    }
}

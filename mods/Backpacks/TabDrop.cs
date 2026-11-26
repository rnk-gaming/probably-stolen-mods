using System;
using System.Reflection;
using HarmonyLib;
using LocalMods;
using UnityEngine;

namespace Backpacks
{
    [HarmonyPatch]
    internal static class TabDrop
    {
        internal static bool Failed;
        internal static bool SingleReady;
        internal static Func<bool> SingleMetadata = SingleShape;
        private static bool singleReported;
        internal static Func<bool> Enabled = () => Backpacks.Switch(TabHover.SettingKey);
        internal static Func<bool> Group = () => ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging;
        internal static Func<bool> Single = () => ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem;
        internal static Func<Vector2, GameInventory> Find = position => (GameInventory)TabHover.FindTab(position).Grid;
        internal static Func<bool> Shape = () => TabHover.GameSharedShape()
            && Fits(TargetMethod() as MethodInfo, typeof(GameInventory), typeof(Vector2), typeof(TreeNodeRender), typeof(PixelElement));
        internal static Action<Exception> Report = error => Storage.Fault("tab drop", error);

        // Pure rule; adapters are evaluated only when the game's answer needs help.
        internal static object Resolve(object original, Func<bool> enabled, Func<bool> group,
            Func<object> find, Action<Exception> fault)
        {
            if (original != null) return original;
            try { return enabled() && group() ? find() : original; }
            catch (Exception error)
            {
                try { fault(error); }
                catch { /* Logging must never change the game's drop result. */ }
                return original;
            }
        }

        internal static bool Fits(MethodInfo method, Type inventory, Type position, Type node, Type element)
        {
            if (method == null || !method.IsPublic || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != inventory) return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 3 && parameters[0].ParameterType == position
                && parameters[1].ParameterType == node && parameters[2].ParameterType == element;
        }

        internal static MethodBase TargetMethod() => typeof(MachineryTabs).GetMethod("FindDropInventoryAt",
            new[] { typeof(Vector2), typeof(TreeNodeRender), typeof(PixelElement) });

        private static bool SingleShape()
        {
            FieldInfo current = typeof(ItemMouseDragHandler).GetField("current", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo dragging = typeof(ItemMouseDragHandler).GetProperty("IsDraggingItem", BindingFlags.Public | BindingFlags.Instance);
            return current?.FieldType == typeof(ItemMouseDragHandler) && current.IsPublic && current.IsStatic
                && dragging?.PropertyType == typeof(bool) && dragging.GetIndexParameters().Length == 0
                && dragging.GetGetMethod()?.IsStatic == false;
        }

        internal static bool Prepare()
        {
            if (Failed) return false;
            try
            {
                if (!Shape()) throw new InvalidOperationException("game API shape differs; compartment tab drop search is disabled");
                PrepareSingle();
                return true;
            }
            catch (Exception error) { Fail(error); return false; }
        }

        private static void PrepareSingle()
        {
            try
            {
                SingleReady = SingleMetadata();
                if (!SingleReady) throw new InvalidOperationException("game API shape differs; single-item tab drop search is disabled");
            }
            catch (Exception error) { FailSingle(error); }
        }

        private static void FailSingle(Exception error)
        {
            SingleReady = false;
            if (singleReported) return;
            singleReported = true;
            TabDragRules.Report(Report, error);
        }

        private static void Fail(Exception error)
        {
            if (Failed) return;
            Failed = true;
            try { Report(error); }
            catch { /* A failed logger must not escape into the game. */ }
        }

        // The loader owns this patch under mod.rnk.backpacks and removes it on disable.
        // No finaliser: exceptions from the original game method remain its own.
        // The shared search passes over the dragged pictures for either kind of drag.
        internal static void Postfix(Vector2 __0, ref GameInventory __result)
        {
            if (!TabHover.Active || Failed || (object)__result != null) return;
            try { __result = (GameInventory)Resolve(__result, Enabled, () => Group() || (SingleReady && Single()), () => Find(__0), Fail); }
            catch (Exception error) { Fail(error); }
        }
    }
}

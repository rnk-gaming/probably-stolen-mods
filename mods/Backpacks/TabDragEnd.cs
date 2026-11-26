using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LocalMods;
using UnityEngine;

namespace Backpacks
{
    internal static class TabDragRules
    {
        internal static bool ClearSingle(bool cancelled, bool enabled, bool hasItem, bool tab) => !cancelled && enabled && !hasItem && tab;
        internal static bool MoveItem(Func<bool> alive, Func<bool> parent, Func<bool> removable,
            Func<bool> units, Func<bool> insert, Func<bool> same) =>
            alive() && parent() && removable() && units() && insert() && !same();
        internal static bool RunGroup(Func<object[]> snapshot, Func<object, bool> eligible,
            Action<object> move, Action close, Action<Exception> report)
        {
            bool handled = false;
            try
            {
                foreach (object item in snapshot())
                {
                    if (!eligible(item)) continue;
                    // The call can mutate an item and then throw. Never replay it.
                    handled = true;
                    move(item);
                }
            }
            catch (Exception error) { Report(report, error); }
            finally
            {
                if (handled)
                    try { close(); }
                    catch (Exception error) { Report(report, error); }
            }
            return !handled;
        }
        internal static void Report(Action<Exception> report, Exception error)
        {
            try { report(error); }
            catch { /* Logging cannot escape into a game drag. */ }
        }
    }

    internal sealed class TabDragApi
    {
        internal Type Owner;
        internal MethodInfo End, Restore, ClearTarget, ClearSelection;
        internal FieldInfo CurrentItem, Amount, LastItem, Inventory, Items, State;
        internal object Idle;
    }

    internal static class TabDragShape
    {
        internal const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static bool Field(FieldInfo field, Type owner, Type value, bool readOnly = false) => field != null
            && field.DeclaringType == owner && field.IsPrivate && !field.IsStatic && field.FieldType == value
            && field.IsInitOnly == readOnly;
        internal static bool Method(MethodInfo method, Type result, bool isPublic, bool isStatic, params Type[] args)
        {
            if (method == null || method.IsPublic != isPublic || (!isPublic && !method.IsPrivate)
                || method.IsStatic != isStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < args.Length; i++) if (parameters[i].ParameterType != args[i]) return false;
            return true;
        }
        private static bool End(TabDragApi api, params Type[] args) => api.End?.DeclaringType == api.Owner
            && Method(api.End, typeof(void), false, false, args);
        internal static bool SingleFits(TabDragApi api) => End(api, typeof(bool))
            && Field(api.CurrentItem, api.Owner, typeof(GameItemElement))
            && Field(api.Amount, api.Owner, typeof(int)) && Field(api.LastItem, api.Owner, typeof(GameItem))
            && Field(api.Inventory, api.Owner, typeof(GameInventory));
        internal static bool GroupFits(TabDragApi api) => End(api)
            && Field(api.Items, api.Owner, typeof(List<GameItemElement>), true)
            && api.State != null && api.State.FieldType.IsEnum && api.State.FieldType.IsNestedPrivate
            && api.State.FieldType.DeclaringType == api.Owner && Field(api.State, api.Owner, api.State.FieldType)
            && api.Idle != null && api.State.FieldType.IsInstanceOfType(api.Idle) && Convert.ToInt64(api.Idle) == 0
            && Enum.GetName(api.State.FieldType, api.Idle) == "Idle"
            && api.Restore?.DeclaringType == api.Owner && Method(api.Restore, typeof(void), false, false)
            && api.ClearTarget?.DeclaringType == api.Owner && Method(api.ClearTarget, typeof(void), false, false)
            && api.ClearSelection?.DeclaringType == api.Owner && Method(api.ClearSelection, typeof(void), false, false);
        internal static TabDragApi SingleApi() => new TabDragApi
        {
            Owner = typeof(ItemMouseDragHandler),
            End = typeof(ItemMouseDragHandler).GetMethod("EndDrag", Private, null, new[] { typeof(bool) }, null),
            CurrentItem = typeof(ItemMouseDragHandler).GetField("currentItem", Private),
            Amount = typeof(ItemMouseDragHandler).GetField("currentRemove", Private),
            LastItem = typeof(ItemMouseDragHandler).GetField("lastItem", Private),
            Inventory = typeof(ItemMouseDragHandler).GetField("lastInventory", Private)
        };
        internal static TabDragApi GroupApi()
        {
            FieldInfo state = typeof(ItemMultiSelectHandler).GetField("state", Private);
            Type stateType = state?.FieldType;
            return new TabDragApi
            {
                Owner = typeof(ItemMultiSelectHandler),
                End = typeof(ItemMultiSelectHandler).GetMethod("EndGroupDrag", Private, null, Type.EmptyTypes, null),
                Items = typeof(ItemMultiSelectHandler).GetField("selectedItems", Private), State = state,
                Idle = stateType != null && stateType.IsEnum && Enum.IsDefined(stateType, "Idle") ? Enum.Parse(stateType, "Idle") : null,
                Restore = typeof(ItemMultiSelectHandler).GetMethod("RestoreGhosts", Private, null, Type.EmptyTypes, null),
                ClearTarget = typeof(ItemMultiSelectHandler).GetMethod("ClearDropTarget", Private, null, Type.EmptyTypes, null),
                ClearSelection = typeof(ItemMultiSelectHandler).GetMethod("ClearSelection", Private, null, Type.EmptyTypes, null)
            };
        }
        internal static bool TransferFits() =>
            Method(typeof(GameItemElement).GetMethod("MayRemove", Type.EmptyTypes), typeof(bool), true, false)
            && Method(typeof(GameItemElement).GetMethod("MaxNumRemove", Type.EmptyTypes), typeof(int), true, false)
            && Method(typeof(GameItemElement).GetMethod("IsDestroyed", Type.EmptyTypes), typeof(bool), true, false)
            && Method(typeof(GeneralHelper).GetMethod("MayPlayerInsertInto", new[] { typeof(GameInventory) }), typeof(bool), true, true, typeof(GameInventory))
            && Method(typeof(GraphUtils).GetMethod("TryAcceptAll", new[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) }),
                typeof(int), true, true, typeof(GraphNodeStorage), typeof(GameItem), typeof(int))
            && Readable((MemberInfo)typeof(GameItemElement).GetField("parentInventory", BindingFlags.Public | BindingFlags.Instance)
                ?? typeof(GameItemElement).GetProperty("parentInventory", BindingFlags.Public | BindingFlags.Instance), typeof(GameInventory));
        private static bool Readable(MemberInfo member, Type value)
        {
            if (member is FieldInfo field) return field.IsPublic && !field.IsStatic && field.FieldType == value;
            PropertyInfo property = member as PropertyInfo;
            return property?.PropertyType == value && property.GetIndexParameters().Length == 0
                && property.GetGetMethod()?.IsStatic == false;
        }
    }

    [HarmonyPatch]
    internal static class TabSingleDrop
    {
        internal static bool Failed;
        internal static TabDragApi Api;
        internal static Func<bool> Enabled = () => Backpacks.Switch(TabHover.SettingKey);
        internal static Func<object> Find = () => TabHover.FindTab(Input.mousePosition).Grid;
        internal static Action<Exception> Report = error => Storage.Fault("single tab drop", error);
        internal static MethodBase TargetMethod() => (Api ??= TabDragShape.SingleApi()).End;
        internal static bool Prepare()
        {
            try
            {
                if (Failed) return false;
                Api ??= TabDragShape.SingleApi();
                if (TabDragShape.SingleFits(Api) && TabHover.GameSharedShape()) return true;
                throw new InvalidOperationException("game API shape differs; single tab drops are disabled");
            }
            catch (Exception error) { Fail(error); return false; }
        }
        private static void Fail(Exception error)
        {
            if (Failed) return;
            Failed = true; TabDragRules.Report(Report, error);
        }
        internal static void Prefix(object __instance, bool __0)
        {
            if (!TabHover.Active || Failed || TabDrop.Failed || !TabDrop.SingleReady || __0) return;
            try
            {
                if (!Enabled()) return;
                if (Api == null || !Api.Owner.IsInstanceOfType(__instance)) throw new InvalidOperationException("unexpected single drag handler");
                bool hasItem = Api.LastItem.GetValue(__instance) != null;
                if (hasItem) return;
                // IsDraggingItem stays true throughout EndDrag's tab search, so the
                // prepared postfix can supply this grid after the outer slot is cleared.
                if (TabDragRules.ClearSingle(__0, true, hasItem, Find() is GameGridInventory)) Api.Inventory.SetValue(__instance, null);
            }
            catch (Exception error) { Fail(error); }
        }
    }

    [HarmonyPatch]
    internal static class TabGroupDrop
    {
        internal static bool Failed;
        internal static TabDragApi Api;
        internal static Func<bool> Enabled = () => Backpacks.Switch(TabHover.SettingKey);
        internal static Func<object> Find = () => TabHover.FindTab(Input.mousePosition).Grid;
        internal static Func<object, object[]> Snapshot = handler => ((List<GameItemElement>)Api.Items.GetValue(handler)).ToArray();
        internal static Func<object, object, bool> Eligible = (value, target) =>
        {
            GameItemElement item = (GameItemElement)value;
            return TabDragRules.MoveItem(() => item != null && !item.IsDestroyed(),
                () => item.parentInventory != null, () => item.MayRemove(), () => item.MaxNumRemove() >= 1,
                () => GeneralHelper.MayPlayerInsertInto((GameInventory)target),
                () => ReferenceEquals(item.parentInventory, target));
        };
        internal static Action<object, object> Move = (item, target) => GraphUtils.TryAcceptAll((GameInventory)target, (GameItem)item);
        internal static Action<Exception> Report = error => Storage.Fault("group tab drop", error);
        internal static MethodBase TargetMethod() => (Api ??= TabDragShape.GroupApi()).End;
        internal static bool Prepare()
        {
            try
            {
                if (Failed) return false;
                Api ??= TabDragShape.GroupApi();
                if (TabDragShape.GroupFits(Api) && TabDragShape.TransferFits() && TabHover.GameSharedShape()) return true;
                throw new InvalidOperationException("game API shape differs; group tab drops are disabled");
            }
            catch (Exception error) { Fail(error); return false; }
        }
        private static void Fail(Exception error)
        {
            if (Failed) return;
            Failed = true; TabDragRules.Report(Report, error);
        }
        internal static bool Prefix(object __instance)
        {
            if (!TabHover.Active || Failed) return true;
            try
            {
                if (!Enabled()) return true;
                if (Api == null || !Api.Owner.IsInstanceOfType(__instance)) throw new InvalidOperationException("unexpected group drag handler");
                object target = Find();
                if (!(target is GameGridInventory)) return true;
                return TabDragRules.RunGroup(() => Snapshot(__instance), item => Eligible(item, target),
                    item => Move(item, target), () => Close(__instance), Fail);
            }
            catch (Exception error) { Fail(error); return true; }
        }
        private static void Close(object handler)
        {
            // A failed closing step must not prevent the remaining steps, especially Idle.
            try { Api.Restore.Invoke(handler, null); }
            catch (Exception error) { Fail(error); }
            try { Api.ClearTarget.Invoke(handler, null); }
            catch (Exception error) { Fail(error); }
            try { Api.ClearSelection.Invoke(handler, null); }
            catch (Exception error) { Fail(error); }
            try { Api.State.SetValue(handler, Api.Idle); }
            catch (Exception error) { Fail(error); }
        }
    }
}

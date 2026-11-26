using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Perks
{
    internal static class SummaryRow
    {
        internal static Func<Type> FindType = () => AccessTools.TypeByName("RunSummaryUI");
        internal static Func<MethodInfo> FindOverview = FindRealOverview;
        internal static MethodInfo FindRealOverview() => AccessTools.Method("RunSummaryUI:BuildOverview",
            new[] { typeof(CustomUIBuilder), typeof(PlayerStore), typeof(RunStats) });
        internal static Action<MethodInfo> Install = method => new Harmony(PerkMod.HarmonyId).Patch(method,
            postfix: new HarmonyMethod(typeof(SummaryRow), nameof(Postfix)));
        internal static bool Fits(Type type, MethodInfo method) => type != null && method != null
            && method.DeclaringType == type && method.Name == "BuildOverview" && method.IsStatic
            && method.ReturnType == typeof(void) && !method.ContainsGenericParameters
            && method.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(new[] { typeof(CustomUIBuilder), typeof(PlayerStore), typeof(RunStats) });

        internal static void Enable()
        {
            try
            {
                Type type = FindType();
                MethodInfo target = type == null ? null : FindOverview();
                if (!Fits(type, target))
                {
                    Runtime.Report("shape:summary", "the run summary has changed shape; starting perk rows are left as the game builds them.");
                    return;
                }
                Install(target);
                Runtime.Say("starting perks on the summary wrap after eight icons.");
            }
            catch (Exception error) { Runtime.Fault("patching the summary", error); }
        }

        // Keep the UI assembly optional at compile time: this project references the
        // engine's core module only. Literal property lookups also suit the game scanner.
        internal static PropertyInfo[] LayoutProperties() => new[]
        {
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:spacing"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:padding"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:childAlignment"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:childControlWidth"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:childControlHeight"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:childForceExpandWidth"),
            AccessTools.Property("UnityEngine.UI.HorizontalLayoutGroup:childForceExpandHeight")
        };
        internal static void CopySettings(Func<string, object> read, Action<string, object> write)
        {
            foreach (string name in new[] { "spacing", "padding", "childAlignment", "childControlWidth", "childControlHeight", "childForceExpandWidth", "childForceExpandHeight" })
                write(name, read(name));
        }

        internal static void Postfix(CustomUIBuilder __0)
        {
            try
            {
                if (__0 == null) return;
                RectTransform page = __0.CurrentContainer;
                if (page == null) return;
                Type layoutType = AccessTools.TypeByName("UnityEngine.UI.HorizontalLayoutGroup");
                if (layoutType == null) throw new InvalidOperationException("HorizontalLayoutGroup is missing");
                Transform row = FindRow(page, layoutType);
                if (row == null || row.childCount <= Rules.SummaryRowSize) return;
                Component layout = row.GetComponent(layoutType);
                var properties = LayoutProperties().ToDictionary(property => property.Name);
                var children = new object[row.childCount];
                for (int i = 0; i < children.Length; i++) children[i] = row.GetChild(i);
                Transform parent = row.parent;
                int sibling = row.GetSiblingIndex();
                object Create(int index)
                {
                    var made = new GameObject("row", typeof(RectTransform));
                    try
                    {
                        Component copy = made.AddComponent(layoutType);
                        CopySettings(name =>
                        {
                            object value = properties[name].GetValue(layout);
                            // A padding object is mutable, so each row gets its own copy.
                            if (value is RectOffset padding) return new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
                            return value;
                        }, (name, value) => properties[name].SetValue(copy, value));
                        made.transform.SetParent(parent, false);
                        made.transform.SetSiblingIndex(sibling + index);
                        return made.transform;
                    }
                    catch { Remove(made.transform); throw; }
                }
                Wrap(row, children, Create, (child, destination, index) =>
                {
                    var transform = (Transform)child;
                    transform.SetParent((Transform)destination, false);
                    transform.SetSiblingIndex(index);
                }, made => Remove((Transform)made), () => CustomUIBuilder.RebuildLayout(page));
            }
            catch (Exception error) { Runtime.Fault("wrapping the summary", error); }
        }

        private static Transform FindRow(Transform root, Type layoutType)
        {
            if (root.GetComponent(layoutType) != null && root.childCount > 0)
            {
                bool perks = true;
                for (int i = 0; i < root.childCount; i++)
                    if (root.GetChild(i).Find("perk_slot") == null) { perks = false; break; }
                if (perks) return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform row = FindRow(root.GetChild(i), layoutType);
                if (row != null) return row;
            }
            return null;
        }
        private static void Remove(Transform row)
        {
            // Destroy is deferred in Unity. Detach immediately so the page is restored
            // before the postfix returns, without waiting for the end of the frame.
            row.gameObject.SetActive(false);
            row.SetParent(null, false);
            UnityEngine.Object.Destroy(row.gameObject);
        }

        // The real postfix and the offline containers share this transaction. Each
        // factory cleans up a row itself if it fails before returning that row.
        internal static void Wrap(object first, object[] children, Func<int, object> create,
            Action<object, object, int> move, Action<object> remove, Action rebuild)
        {
            var made = new List<object>();
            try
            {
                if (children.Length <= Rules.SummaryRowSize) return;
                int[][] plan = Rules.SummaryRows(children.Length);
                made.Capacity = plan.Length - 1;
                for (int row = 1; row < plan.Length; row++)
                {
                    object destination = create(row);
                    made.Add(destination);
                    for (int column = 0; column < plan[row].Length; column++) move(children[plan[row][column]], destination, column);
                }
                rebuild();
                Runtime.Report("summary:wrapped", "the summary shows " + children.Length + " starting perks in " + plan.Length + " rows.");
            }
            catch (Exception error)
            {
                bool restored = true;
                // Try every child even if one rollback operation fails. In that case
                // keep the extra rows rather than destroy any stranded perk objects.
                for (int i = 0; i < children.Length; i++)
                    try { move(children[i], first, i); }
                    catch (Exception rollback) { restored = false; error = new AggregateException(error, rollback); }
                if (restored)
                    for (int i = made.Count - 1; i >= 0; i--)
                        try { remove(made[i]); }
                        catch (Exception rollback) { error = new AggregateException(error, rollback); }
                // Restore geometry too if the failed rebuild had already laid out rows.
                if (made.Count > 0)
                    try { rebuild(); }
                    catch (Exception rollback) { error = new AggregateException(error, rollback); }
                Runtime.Fault("wrapping the summary", error);
            }
        }
    }
}

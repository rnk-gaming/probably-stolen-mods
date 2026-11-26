using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LocalMods;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Backpacks
{
    // Independent of Unity: one selection per hover, tolerating one missed frame.
    internal sealed class TabHoverRule
    {
        private object strip;
        private int index = -1;
        private double since;
        private bool chosen;
        private int missingFrames;
        internal bool Tick(double now, bool dragging, object target, int page, int selected)
        {
            if (!dragging) { Reset(); return false; }
            if (target == null || page < 0)
            {
                if (++missingFrames >= 2) Reset();
                return false;
            }
            missingFrames = 0;
            if (!ReferenceEquals(strip, target) || index != page || now < since)
            {
                strip = target; index = page; since = now; chosen = false;
            }
            if (chosen || page == selected || now < since + 0.35) return false;
            chosen = true;
            return true;
        }
        internal void Reset() { strip = null; index = -1; chosen = false; missingFrames = 0; }
    }

    internal readonly struct TabHoverTarget
    {
        internal readonly object Strip;
        internal readonly int Index;
        internal readonly object Grid;
        internal TabHoverTarget(object strip, int index) : this(strip, index, null) { }
        internal TabHoverTarget(object strip, int index, object grid) { Strip = strip; Index = index; Grid = grid; }
    }

    // Only metadata crosses this boundary. Actual lookups name fixed game types so
    // the game's scanner can verify which API we inspect; tests supply stand-ins.
    internal sealed class TabHoverApi
    {
        internal Type Single, Multi, Tabs, Node, Inventory;
        internal FieldInfo SingleCurrent, MultiCurrent;
        internal PropertyInfo SingleDragging, GroupDragging;
        internal MemberInfo Count, Selected;
        internal MethodInfo Select, NodeInventory, IndexInventory;
        internal MethodInfo[] ParentMethods;
    }

    internal static class TabHover
    {
        internal const string SettingKey = "tab_hover";
        internal const bool Default = true;
        internal static bool Active, Running, Failed;
        internal static Func<bool> Dragging = ReadDragging, Enabled = () => Backpacks.Switch(SettingKey), Shape = GameShape;
        internal static Func<double> Clock = () => Time.unscaledTimeAsDouble;
        internal static Func<int> FrameNumber = () => Time.frameCount;
        internal static Func<TabHoverTarget> UnderPointer = () => FindTab(Input.mousePosition);
        internal static Func<object, bool> Alive = strip => (MachineryTabs)strip != null;
        internal static Func<object, int> Selected = strip => ((MachineryTabs)strip).selected;
        internal static Action<object, int> Select = (strip, index) => ((MachineryTabs)strip).Select(index, true);
        internal static Action<Exception> Report = error => Storage.Fault("tab hover", error);
        internal static Func<IEnumerator, Coroutine> Start = routine => ModHelper.StartCoroutine(Texts.ModId, routine);
        internal static Action Stop = () => ModHelper.StopAllCoroutines(Texts.ModId);
        private static readonly Func<object, int, object> Inventory = (strip, index) => ((MachineryTabs)strip).GetTabInventory(index);
        private static readonly TabHoverRule Rule = new TabHoverRule();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>(64);
        private static PointerEventData pointer;
        private static EventSystem pointerSystem;
        private static bool checkedShape, wasDragging;
        private static int faults;
        private static int lastFrame;
        private static Coroutine handle;

        private static bool ReadDragging() =>
            (ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem)
            || (ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging);

        internal static int FindIndex(object strip, object grid, int count, Func<object, int, object> inventory)
        {
            if (grid == null) return -1;
            for (int i = 0; i < count; i++) if (ReferenceEquals(grid, inventory(strip, i))) return i;
            return -1;
        }

        internal static TabHoverTarget FindTab(Vector2 screenPosition)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return default;
            if (!ReferenceEquals(pointerSystem, system))
            {
                pointerSystem = system;
                pointer = new PointerEventData(system);
            }
            pointer.position = screenPosition;
            Hits.Clear();
            try
            {
                system.RaycastAll(pointer, Hits);
                // Pass over dragged pictures. There is no cheap game API to tell them
                // from another window, so a window over the strip does not block hover.
                for (int i = 0; i < Hits.Count; i++)
                {
                    GameObject hit = Hits[i].gameObject;
                    if (hit == null) continue;
                    TreeNodeRender node = hit.GetComponent<TreeNodeRender>();
                    if (node == null) continue;
                    MachineryTabs strip = node.FindParentElement<MachineryTabs>();
                    if (strip == null) continue;
                    GameInventory grid = strip.GetTabInventory(node);
                    if (grid == null) continue;
                    int index = FindIndex(strip, grid, strip.count, Inventory);
                    // Stop at the first actual tab, even if its grid is inconsistent.
                    return index < 0 ? default : new TabHoverTarget(strip, index, grid);
                }
                return default;
            }
            finally { Hits.Clear(); }
        }

        private static bool Readable(MemberInfo member, Type value)
        {
            if (member is FieldInfo field) return field.IsPublic && !field.IsStatic && field.FieldType == value;
            PropertyInfo property = member as PropertyInfo;
            return property?.PropertyType == value && property.GetIndexParameters().Length == 0
                && property.GetGetMethod()?.IsStatic == false;
        }
        private static bool DragShape(Type owner, FieldInfo current, PropertyInfo dragging)
        {
            return current != null && current.IsPublic && current.IsStatic && current.FieldType == owner && dragging?.PropertyType == typeof(bool)
                && dragging.GetIndexParameters().Length == 0 && dragging.GetGetMethod()?.IsStatic == false;
        }
        private static bool Method(MethodInfo method, Type result, params Type[] args)
        {
            if (method == null || !method.IsPublic || method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < args.Length; i++) if (parameters[i].ParameterType != args[i]) return false;
            return true;
        }
        internal static bool Fits(TabHoverApi api) => SharedFits(api)
            && DragShape(api.Single, api.SingleCurrent, api.SingleDragging)
            && Readable(api.Selected, typeof(int)) && Method(api.Select, api.Tabs, typeof(int), typeof(bool));

        internal static bool SharedFits(TabHoverApi api)
        {
            if (!DragShape(api.Multi, api.MultiCurrent, api.GroupDragging) || !Readable(api.Count, typeof(int))
                || !Method(api.NodeInventory, api.Inventory, api.Node) || !Method(api.IndexInventory, api.Inventory, typeof(int))) return false;
            foreach (MethodInfo method in api.ParentMethods)
                if (method.Name == "FindParentElement" && method.IsPublic && !method.IsStatic && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0
                    && method.ReturnType == method.GetGenericArguments()[0])
                {
                    try { return method.MakeGenericMethod(api.Tabs).ReturnType == api.Tabs; }
                    catch (ArgumentException) { return false; }
                }
            return false;
        }
        private static TabHoverApi SharedApi() => new TabHoverApi
        {
            Multi = typeof(ItemMultiSelectHandler),
            Tabs = typeof(MachineryTabs), Node = typeof(TreeNodeRender), Inventory = typeof(GameInventory),
            MultiCurrent = typeof(ItemMultiSelectHandler).GetField("current", BindingFlags.Public | BindingFlags.Static),
            GroupDragging = typeof(ItemMultiSelectHandler).GetProperty("IsGroupDragging", BindingFlags.Public | BindingFlags.Instance),
            Count = (MemberInfo)typeof(MachineryTabs).GetField("count", BindingFlags.Public | BindingFlags.Instance)
                ?? typeof(MachineryTabs).GetProperty("count", BindingFlags.Public | BindingFlags.Instance),
            NodeInventory = typeof(MachineryTabs).GetMethod("GetTabInventory", new[] { typeof(TreeNodeRender) }),
            IndexInventory = typeof(MachineryTabs).GetMethod("GetTabInventory", new[] { typeof(int) }),
            ParentMethods = typeof(TreeNodeRender).GetMethods(BindingFlags.Public | BindingFlags.Instance)
        };
        internal static bool GameShape()
        {
            TabHoverApi api = SharedApi();
            api.Single = typeof(ItemMouseDragHandler);
            api.SingleCurrent = typeof(ItemMouseDragHandler).GetField("current", BindingFlags.Public | BindingFlags.Static);
            api.SingleDragging = typeof(ItemMouseDragHandler).GetProperty("IsDraggingItem", BindingFlags.Public | BindingFlags.Instance);
            api.Selected = (MemberInfo)typeof(MachineryTabs).GetField("selected", BindingFlags.Public | BindingFlags.Instance)
                ?? typeof(MachineryTabs).GetProperty("selected", BindingFlags.Public | BindingFlags.Instance);
            api.Select = typeof(MachineryTabs).GetMethod("Select", new[] { typeof(int), typeof(bool) });
            return Fits(api) && RaycastShape();
        }
        internal static bool GameSharedShape() => SharedFits(SharedApi()) && RaycastShape();
        private static bool RaycastShape() =>
            typeof(EventSystem).GetMethod("RaycastAll", new[] { typeof(PointerEventData), typeof(List<RaycastResult>) }) is MethodInfo raycast
            && !raycast.IsStatic && raycast.ReturnType == typeof(void)
            && StaticProperty(typeof(EventSystem).GetProperty("current"), typeof(EventSystem))
            && typeof(PointerEventData).GetConstructor(new[] { typeof(EventSystem) }) != null
            && Readable(typeof(PointerEventData).GetProperty("position"), typeof(Vector2))
            && typeof(PointerEventData).GetProperty("position")?.GetSetMethod()?.IsStatic == false
            && Readable(typeof(RaycastResult).GetProperty("gameObject"), typeof(GameObject));
        private static bool StaticProperty(PropertyInfo property, Type value) => property?.PropertyType == value
            && property.GetIndexParameters().Length == 0 && property.GetGetMethod()?.IsStatic == true;

        private static void Fail(Exception error)
        {
            if (Failed) return;
            Failed = ++faults >= 3;
            Rule.Reset();
            if (faults != 1) return;
            try { Report(error); }
            catch { /* A failed logger must not escape into the game. */ }
        }
        internal static void Frame()
        {
            if (!Active || Failed) return;
            try
            {
                lastFrame = FrameNumber();
                if (!Dragging())
                {
                    if (wasDragging) { Rule.Reset(); wasDragging = false; }
                    return;
                }
                wasDragging = true;
                if (!Enabled()) { Rule.Reset(); return; }
                TabHoverTarget target = UnderPointer();
                if (target.Strip != null && !Alive(target.Strip)) { Rule.Reset(); return; }
                if (Rule.Tick(Clock(), true, target.Strip, target.Index, target.Strip == null ? -1 : Selected(target.Strip)))
                {
                    if (Alive(target.Strip)) Select(target.Strip, target.Index);
                    else Rule.Reset();
                }
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void Enable()
        {
            Active = true;
            ModHook.OnModAssetsLoaded -= AssetsLoaded;
            ModHook.OnModAssetsLoaded += AssetsLoaded;
            ModHook.OnGameLoadedEarly -= Loaded;
            ModHook.OnGameLoadedEarly += Loaded;
        }
        internal static void Disable()
        {
            Active = false;
            ModHook.OnModAssetsLoaded -= AssetsLoaded;
            ModHook.OnGameLoadedEarly -= Loaded;
            Rule.Reset(); wasDragging = false;
            pointer = null; pointerSystem = null; Hits.Clear();
            try { if ((object)handle != null) Stop(); }
            catch (Exception error) { Fail(error); }
            finally { Running = false; handle = null; }
        }
        internal static void AssetsLoaded(LoadedMod mod)
        {
            try { if (mod?.Manifest?.ID == Texts.ModId) EnsureLoop(); }
            catch (Exception error) { Fail(error); }
        }
        internal static void Loaded() { Rule.Reset(); wasDragging = false; EnsureLoop(); }
        internal static void EnsureLoop()
        {
            if (!Active || Failed) return;
            try
            {
                int frame = FrameNumber();
                if (Running && (lastFrame == frame || lastFrame == unchecked(frame - 1))) return;
                if ((object)handle != null)
                {
                    Stop();
                    handle = null;
                    Running = false;
                }
                if (!checkedShape)
                {
                    if (!Shape()) throw new InvalidOperationException("game API shape differs; compartment tab hover is disabled");
                    checkedShape = true;
                }
                handle = Start(Loop());
                Running = (object)handle != null;
                if (!Running) throw new InvalidOperationException("compartment tab hover coroutine did not start");
                // Covers registration before Unity has advanced the iterator once.
                lastFrame = frame;
            }
            catch (Exception error) { Fail(error); }
        }
        private static IEnumerator Loop()
        {
            try { while (Active && !Failed) { Frame(); yield return null; } }
            finally { Running = false; handle = null; }
        }
    }
}

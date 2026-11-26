using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ShopkeepingSuite
{
    internal static class Marks
    {
        internal static Func<bool> Outside = () => MapUIManager.Instance != null && MapUIManager.Instance.isOutside;
        internal static Func<bool> StoreClosed = () => PlayerStore.Instance.storeState == PlayerStore.StoreState.CLOSED;
        internal static Func<StoreClient> Current = () => PlayerStore.HasInstance && PlayerStore.Instance.currentClientInstance != null ? PlayerStore.Instance.currentClientInstance.GetClientBlueprint() : null;
        internal static Func<List<GameItem>> ShopItems = ReadShop;
        internal static Func<List<GameItem>> SaleItems = ReadSaleItems;
        internal static List<GameItem> ItemsBelow(IEnumerable<GraphNodeStorage> roots)
        {
            var result = new List<GameItem>();
            var seen = new HashSet<GraphNodeStorage>(); var todo = new Stack<GraphNodeStorage>(roots);
            while (todo.Count > 0)
            {
                GraphNodeStorage node = todo.Pop(); if (node == null || !seen.Add(node)) continue;
                if (node is GameItem item) result.Add(item);
                foreach (GraphNodeStorage child in Children(node)) if (child != null) todo.Push(child);
            }
            return result;
        }
        private static List<GameItem> ReadSaleItems()
        {
            if (!Settings.On("sale_data") || EmporiumEntry.Instance == null) return new List<GameItem>();
            EmporiumEntry shop = EmporiumEntry.Instance;
            return ItemsBelow(new GraphNodeStorage[] { shop.invElement, shop.backInvinvElement, shop.backInvinvElementCounter, shop.showcaseElement });
        }
        internal static Func<GameItem, bool> Owned = item => GeneralHelper.IsItemOwned(item);
        internal static Func<GameItem, string, bool> Tag = (item, tag) => item.IsTag(tag);
        internal static Func<GameItem, bool> Flaggable = item => ContrabandHelper.IsFlaggableItem(item);
        internal static Func<GameItem, string, int> IntTag = (item, tag) => item.GetTagReadonly(tag).GetInt();
        internal static Func<GameItem, string, string> StringTag = (item, tag) => item.GetTagReadonly(tag).GetString();
        internal static Func<GameItem, int> Appraise = item => AppraisalHelper.GetCorrectBaseValue(item);
        internal static Func<GraphNodeStorage, IEnumerable<GraphNodeStorage>> Children = node => node.children;
        internal static Func<GameItem, IEnumerable<GameItem>> Contents = OwnContents;
        internal static Func<GameItem, GameInventory> ParentInventory = item => item.parentInventory;
        internal static Func<GameItem> Dragged = () => ItemMouseDragHandler.current == null ? null : (GameItem)typeof(ItemMouseDragHandler).GetField("currentItem", Shapes.DragFlags).GetValue(ItemMouseDragHandler.current);
        internal static Action<GameItem> Redraw = item => { if (item is GameItemElement element && !element.IsDestroyed()) element.ValidateSelect(false); /* updateBackground: the corner mark is drawn either way. */ };
        internal static readonly RenderHandler.ColorPalette[] Palette = { RenderHandler.ColorPalette.White, RenderHandler.ColorPalette.White, RenderHandler.ColorPalette.Red, RenderHandler.ColorPalette.Purple, RenderHandler.ColorPalette.LightRed, RenderHandler.ColorPalette.LightGreen, RenderHandler.ColorPalette.LightOrange };
        internal static Func<int, Color> Colour = kind => RenderHandler.GetColorFromPalette(Palette[kind]);
        internal static Func<GameItem, bool, ItemFacts> ReadFacts = Facts;
        private static readonly Func<string, bool> RuleEnabled = Settings.On;
        private sealed class Drawn { internal MarkKind Kind; internal bool Engine; internal long Revision; internal int Size, Price; }
        private static ConditionalWeakTable<GameItem, Drawn> drawn = new ConditionalWeakTable<GameItem, Drawn>();
        private static HashSet<GameItem> sorting = new HashSet<GameItem>();
        private static readonly Queue<GameItem> queue = new Queue<GameItem>();
        private static readonly HashSet<GameItem> queued = new HashSet<GameItem>();
        private static bool all;
        internal static StoreClient Client;
        internal static bool OutsideShop, Security, Buyer, ShutterClosed;
        internal static bool ObserveCustomer(StoreClient client, bool outside)
        {
            bool security = client != null && client.isSecurity, buyer = IsBuyer(client);
            bool changed = !ReferenceEquals(Client, client) || OutsideShop != outside || Security != security || Buyer != buyer;
            Client = client; OutsideShop = outside; Security = security; Buyer = buyer;
            if (changed) { InspectionSearch.Reset(); WantedInside.Reset(); }
            return changed;
        }
        internal static bool IsBuyer(StoreClient client) => client != null && (client.clientIntent == StoreClient.ClientIntent.BUY || client.clientIntent == StoreClient.ClientIntent.SELLNBUY);
        internal static ItemFacts Facts(GameItem item, bool tooltip)
        {
            bool outside = tooltip ? Outside() : OutsideShop;
            if (item == null || outside || (!tooltip && ShutterClosed) || !Settings.On("enabled") || !Settings.On("ownership_data")) return tooltip ? new ItemFacts { Outside = outside } : null;
            bool same = sorting.Contains(item), owned = Owned(item);
            StoreClient client = Settings.On("customer_data") ? (tooltip ? Current() : Client) : null;
            bool security = client != null && (tooltip ? client.isSecurity : Security), buyer = client != null && (tooltip ? IsBuyer(client) : Buyer);
            bool stolen = false, contraband = false, flaggable = false, concealed = false;
            int demand = 0, inspectionCount = 0, wantedCount = 0, insideDemand = 0;
            if ((!owned && Settings.On(tooltip ? "tooltip_risk" : "mark_risk")) || (owned && security && Settings.On("mark_inspection")))
            {
                stolen = Tag(item, "STOLEN_TAG"); contraband = Tag(item, "CONTRABAND_ITEM_TAG"); flaggable = Flaggable(item);
                if (owned && security && Settings.On("mark_inspection"))
                {
                    InspectionAnswer inspection = InspectionSearch.Get(item, client, security);
                    concealed = !inspection.Found; inspectionCount = inspection.Holds;
                }
            }
            if (owned && buyer && Settings.On("wanted_data") && Settings.On(tooltip ? "tooltip_wanted" : "mark_wanted")
                && (tooltip || !(same && Settings.On("mark_same_kind")) && !(security && (flaggable && !concealed || inspectionCount > 0) && Settings.On("mark_inspection"))))
            {
                demand = Wanted.Get(client, item);
                if ((tooltip || demand == 0) && Settings.On("mark_wanted") && Settings.On("mark_wanted_inside"))
                {
                    WantedContents inside = WantedInside.Get(item, client, buyer);
                    wantedCount = inside.Count; insideDemand = inside.Demand;
                }
            }
            if (!tooltip && !(same && Settings.On("mark_same_kind")) && !(!owned && Settings.On("mark_risk") && (stolen || contraband))
                && !(owned && security && Settings.On("mark_inspection") && (flaggable && !concealed || inspectionCount > 0)) && demand == 0 && insideDemand == 0) return null;
            var f = new ItemFacts { Same = same, Owned = owned, Security = security, Buyer = buyer, Stolen = stolen, Contraband = contraband, Flaggable = flaggable, Concealed = concealed, Demand = demand, InspectionCount = inspectionCount, WantedCount = wantedCount, InsideDemand = insideDemand };
            if (tooltip && !owned)
            {
                if (stolen) f.Heat = IntTag(item, "STOLEN_VALUE_INT");
                if (contraband) f.Level = StringTag(item, "CONTRABAND_LEVEL");
            }
            if (tooltip && Settings.On("tooltip_appraisal") && client != null && client.clientIntent == StoreClient.ClientIntent.APPRAISAL_SERVICE)
            {
                f.Appraiser = true; f.AppraisalItem = Tag(item, "APPRAISAL_ITEM");
                if (f.AppraisalItem) f.Appraisal = Appraise(item);
            }
            return f;
        }
        internal static void Postfix(GameItem __0, ref Color? __result)
        {
            try
            {
                if (__0 == null || !Settings.On("enabled") || !Settings.On("marks_data") || (!Settings.On("mark_same_kind") && !Settings.On("mark_risk") && !Settings.On("mark_inspection") && !Settings.On("mark_wanted"))) { RememberOriginal(__0, __result.HasValue); return; }
                if (OutsideShop || ShutterClosed || !Settings.On("ownership_data")) { RememberOriginal(__0, __result.HasValue); return; }
                if (sorting.Count == 0 && (Client == null || !Settings.On("customer_data")))
                {
                    if (!Settings.On("mark_risk") || Owned(__0)) { RememberOriginal(__0, __result.HasValue); return; }
                }
                if (__result.HasValue)
                {
                    Drawn native = drawn.GetOrCreateValue(__0); native.Engine = true; native.Kind = MarkKind.None; native.Revision++;
                    return;
                }
                MarkKind kind = Rules.Mark(ReadFacts(__0, false), RuleEnabled);
                Color? next = kind == MarkKind.None ? (Color?)null : Colour((int)kind);
                Drawn state;
                if (kind == MarkKind.None) { if (!drawn.TryGetValue(__0, out state)) return; }
                else state = drawn.GetOrCreateValue(__0);
                state.Engine = false; state.Kind = kind; state.Revision++;
                __result = next;
            }
            catch (Exception error)
            {
                // The game will draw its original answer. Do not remember our failed
                // prediction as though it reached the screen.
                try
                {
                    if (__0 != null)
                    {
                        Drawn state = drawn.GetOrCreateValue(__0); state.Engine = __result.HasValue; state.Kind = MarkKind.None; state.Revision++;
                    }
                }
                catch (Exception) { }
                SuiteMod.Fault("item mark", error);
            }
        }
        // Keep the game's own animal mark, but draw our marks separately with shapes.
        internal static void ColourPostfix(GameItem __0, ref Color? __result)
        {
            bool native = __result.HasValue;
            Postfix(__0, ref __result);
            if (!native) __result = null;
        }
        internal static Action<GameItem, int, int> DrawVisual = MarkArt.Draw;
        internal static void Paint(GameItemElement __instance)
        {
            Prices.Paint(__instance);
            try
            {
                int kind = __instance != null && drawn.TryGetValue(__instance, out Drawn state) && !state.Engine
                    && !ShutterClosed && Settings.On("enabled") && Settings.On("marks_data") ? (int)state.Kind : 0;
                DrawVisual(__instance, kind, MarkArt.Size(Settings.Size()));
            }
            catch (Exception error)
            {
                try { MarkArt.Hide(__instance); } catch (Exception) { }
                SuiteMod.Fault("drawing mark shape", error);
            }
        }
        private static void RememberOriginal(GameItem item, bool engine)
        {
            // Keep an existing drawing truthful on the allocation-free early path.
            if (item != null && drawn.TryGetValue(item, out Drawn state)) { state.Engine = engine; state.Kind = MarkKind.None; state.Revision++; }
        }
        internal static IEnumerable<GameItem> OwnContents(GameItem item)
        {
            var todo = new Stack<(GraphNodeStorage Node, bool InInventory)>(); var seen = new HashSet<GraphNodeStorage>();
            foreach (var child in Children(item)) if (child != null) todo.Push((child, false));
            while (todo.Count > 0)
            {
                var entry = todo.Pop(); var node = entry.Node; if (!seen.Add(node)) continue;
                if (node is GameItem held) { if (entry.InInventory) yield return held; continue; }
                bool inInventory = entry.InInventory || node is GameInventory;
                foreach (var child in Children(node)) if (child != null) todo.Push((child, inInventory));
            }
        }
        private static List<GameItem> ReadShop()
        {
            return Settings.On("shop_data") && PlayerStore.HasInstance ? PlayerStore.Instance.FindAllItem(false) : new List<GameItem>();
        }
        internal static void BeginDrag()
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("mark_same_kind") || Outside()) return;
                GameItem dragged = Dragged();
                if (dragged == null || ParentInventory(dragged) == null) return;
                HashSet<GameItem> next = Rules.Containers(ShopItems(), dragged, Owned, Contents);
                foreach (var item in sorting) if (!next.Contains(item)) Request(item);
                foreach (var item in next) if (!sorting.Contains(item)) Request(item);
                sorting = next;
            }
            catch (Exception error) { SuiteMod.Fault("drag start", error); }
        }
        internal static void EndDrag()
        {
            try
            {
                Wanted.Reset(); InspectionSearch.Reset(); WantedInside.Reset();
                // A move can change every ancestor's count, including when the
                // same-kind aid is off. Reuse this hook and the bounded redraw queue.
                if (Settings.On("enabled") && (Security && Settings.On("mark_inspection") || Buyer && Settings.On("mark_wanted") && Settings.On("mark_wanted_inside"))) RequestAll();
                foreach (var item in sorting) Request(item); sorting.Clear();
            }
            catch (Exception error) { SuiteMod.Fault("drag end", error); }
        }
        internal static void CheckDrag() { if (Settings.On("mark_same_kind") && sorting.Count > 0 && Dragged() == null) EndDrag(); }
        internal static void Request(GameItem item) { Enqueue(item); }
        private static void Enqueue(GameItem item) { if (item != null && queued.Add(item)) queue.Enqueue(item); }
        internal static void RequestAll() { InspectionSearch.Reset(); WantedInside.Reset(); InspectionSearch.Batch = WantedInside.Batch = true; all = true; }
        internal static void RefreshStep()
        {
            try
            {
                if (all) { all = false; foreach (GameItem item in ShopItems()) Enqueue(item); }
                for (int count = 0; count < 200 && queue.Count > 0; count++)
                {
                    GameItem item = queue.Dequeue(); queued.Remove(item);
                    try
                    {
                        if (!Settings.Disabled.Contains("mark_same_kind") && ReferenceEquals(item, Dragged())) continue;
                        Drawn previous = drawn.GetOrCreateValue(item);
                        MarkKind next = ShutterClosed || !Settings.On("marks_data") || !Settings.On("enabled") || previous.Engine ? MarkKind.None : Rules.Mark(ReadFacts(item, false), RuleEnabled);
                        int price = Prices.Mark(item);
                        int size = MarkArt.Size(Settings.Size());
                        if (previous.Kind == next && previous.Price == price && ((next == MarkKind.None && price == 0) || previous.Size == size)) continue;
                        long revision = previous.Revision;
                        Redraw(item);
                        previous.Size = size;
                        previous.Price = price;
                        // The real redraw records its actual answer in the postfix.
                        // A stand-in redraw used by a test has no such callback.
                        if (previous.Revision == revision) previous.Kind = previous.Engine ? MarkKind.None : next;
                    }
                    catch (Exception error) { SuiteMod.Fault("redrawing item", error); }
                }
            }
            catch (Exception error) { SuiteMod.Fault("finding shop items", error); }
            finally { if (!all && queue.Count == 0) InspectionSearch.Batch = WantedInside.Batch = false; }
        }
        internal static void Reset()
        {
            Prices.Reset();
            InspectionSearch.Reset();
            WantedInside.Reset();
            InspectionSearch.Batch = WantedInside.Batch = false;
            try { MarkArt.Clear(); } catch (Exception error) { SuiteMod.Fault("clearing mark shapes", error); }
            sorting.Clear(); drawn = new ConditionalWeakTable<GameItem, Drawn>(); queue.Clear(); queued.Clear(); all = false; Client = null; OutsideShop = Security = Buyer = ShutterClosed = false; Wanted.Reset();
        }
    }
}

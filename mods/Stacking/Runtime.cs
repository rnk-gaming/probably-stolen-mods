using System;
using HarmonyLib;

namespace Stacking
{
    internal enum StackMode { Pending, Own, Economy, Blocked }

    internal static class Runtime
    {
        internal const string Id = "rnk.stacking";
        internal static StackMode Mode;
        internal static bool Own => Bridge.Active && Mode == StackMode.Own && GameShape.Ready;
        internal static Func<string, string> Items = key => StackingMod.ItemLists.TryGetValue(key, out ConfigEntry<string> entry) ? entry.Value : "";
        internal static Action<GameItem> Redraw = item => item.Validate();
        internal static Func<GameInventory, bool> ShopGrid = inventory =>
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            return shop != null && (ReferenceEquals(inventory, shop.invElement)
                || ReferenceEquals(inventory, shop.backInvinvElement)
                || ReferenceEquals(inventory, shop.backInvinvElementCounter)
                || ReferenceEquals(inventory, shop.frontInvinvElement)
                || ReferenceEquals(inventory, shop.afterhourInventory));
        };

        // Assets are loaded only after every mod has started. Never select the own
        // engine during local-mod discovery, before Workshop mods can be running.
        internal static void Start()
        {
            if (!Bridge.Active || Mode != StackMode.Pending) return;
            Mode = StackMode.Blocked;
            try
            {
                Bridge.TryInstall();
                Mode = Decide(Bridge.Running(), Bridge.Installed, GameShape.Ready);
                VarietyGuard.TryInstall();
                RequestGuard.TryInstall();
                BarterGuard.TryInstall();
                Bridge.Report("mode", Mode == StackMode.Own
                    ? "using Stacking's own engine. ItemStacks must be switched off."
                    : Mode == StackMode.Economy
                        ? "using Station Economy; Stacking's own engine is inactive. ItemStacks must be switched off."
                        : "stacking is inactive: Station Economy or the game has an unexpected shape. ItemStacks must be switched off.");
            }
            catch (Exception error) { Bridge.Fault("choosing the stacking mode", error); }
        }

        internal static StackMode Decide(bool economyRunning, bool economyFits, bool gameFits) =>
            economyRunning ? (economyFits ? StackMode.Economy : StackMode.Blocked)
                : (gameFits ? StackMode.Own : StackMode.Blocked);

        internal static Func<string, string> Localized = key => ModHelper.GetLocalized(Id, key);
        internal static string Text(string key) => Text(key, Texts.English[key]);
        internal static string TextWith(string key, object value) => TextWith(key, Texts.English[key], value);
        internal static string Text(string key, string fallback)
        {
            try { string text = Localized(key); return string.IsNullOrEmpty(text) || text == key ? fallback : text; }
            catch { return fallback; }
        }
        internal static string TextWith(string key, string fallback, object value)
        {
            try { return string.Format(Text(key, fallback), value); }
            catch { return string.Format(fallback, value); }
        }
    }

    // A common gate for the engine's patches: no partial engine on a changed game.
    internal static class GameShape
    {
        private static readonly Lazy<bool> ready = new Lazy<bool>(Check);
        internal static bool Ready => ready.Value;
        private static bool Check()
        {
            try
            {
                return typeof(GameItem).GetProperty("unitCount")?.PropertyType == typeof(int)
                    && typeof(GameItem).GetProperty("unitCount").CanRead && typeof(GameItem).GetProperty("unitCount").CanWrite
                    && typeof(GameItem).GetProperty("mayStackItemFunc")?.PropertyType == typeof(Func<GameItem, GameItem, bool>)
                    && typeof(GameItem).GetProperty("maxSlotAmountItemFunc")?.PropertyType == typeof(Func<GameItem, GameInventory, SlotMarker, int>)
                    && typeof(GameItem).GetProperty("state")?.PropertyType == typeof(TagSystem)
                    && typeof(GameItem).GetProperty("modifiedState")?.PropertyType == typeof(TagSystem)
                    && typeof(GameItem).GetField("itemFeatures")?.FieldType == typeof(System.Collections.Generic.List<ItemFeature>)
                    && Fits(AccessTools.Method(typeof(DirectoryMaster), "Item", new[] { typeof(string), typeof(bool) }), typeof(GameItem), true)
                    && Fits(AccessTools.Method(typeof(GameItem), "GetSplitItem", new[] { typeof(int) }), typeof(GameItem))
                    && Fits(AccessTools.Method(typeof(GameItem), "GetCurrentValue", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(long) }), typeof(long))
                    && Fits(AccessTools.Method(typeof(GameItem), "GetNegociatedValue", Type.EmptyTypes), typeof(long))
                    && Fits(AccessTools.Method(typeof(GameItem), "Target", new[] { typeof(GameItem) }), typeof(void))
                    && Fits(AccessTools.Method(typeof(ItemBehaviourManager), "Target", new[] { typeof(GameItem), typeof(GameItem) }), typeof(void))
                    && Fits(AccessTools.Method(typeof(GameItemElement), "Destroy", Type.EmptyTypes), typeof(void))
                    && Fits(AccessTools.Method(typeof(GameItem), "MayActivateSlotItem", Type.EmptyTypes), typeof(bool))
                    && Fits(AccessTools.Method(typeof(GameItem), "CanActivateSlotItem", Type.EmptyTypes), typeof(bool))
                    && Fits(AccessTools.Method(typeof(GameItem), "ActivateSlotItem", Type.EmptyTypes), typeof(void));
            }
            catch (Exception error) { Bridge.Fault("checking the game shape", error); return false; }
        }
        private static bool Fits(System.Reflection.MethodInfo method, Type result, bool isStatic = false) =>
            method != null && method.IsStatic == isStatic && method.ReturnType == result && !method.ContainsGenericParameters;
    }
}

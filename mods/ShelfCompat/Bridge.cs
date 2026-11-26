using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ShelfCompat
{
    internal static class Bridge
    {
        internal const string HarmonyId = "mod.rnk.shelf-compat";
        internal static bool Active, Installed;
        private static bool started, faulted, firstCheckSaid;
        // These boundaries let the harness use only stand-ins and metadata, never Unity.
        internal static Func<string, bool> Running = id => ModHelper.IsModRunning(id);
        internal static Func<Type> FindWaterType = () => InAssembly(AccessTools.TypeByName("WaterExpansion.WaterRules"), "WaterExpansion");
        internal static Func<Type> FindShelfType = () => InAssembly(AccessTools.TypeByName("ShopExpansion.ShelfManager"), "ShopExpansion");
        internal static Func<MethodInfo> FindTarget = () => AccessTools.Method("WaterExpansion.WaterRules:IsInStoreGrid");
        internal static Func<PropertyInfo> FindGrids = () => AccessTools.Property(AccessTools.TypeByName("ShopExpansion.ShelfManager"), "Grids");
        internal static Func<PropertyInfo> FindReady = () => AccessTools.Property(AccessTools.TypeByName("ShopExpansion.ShelfManager"), "IsReady");
        internal static Action<string> Log = text => Debug.Log(text);
        internal static Func<bool> Enabled = () => ShelfCompatMod.Enabled != null && ShelfCompatMod.Enabled.Value;
        internal static Func<bool> WaterEnabled = () => ShelfCompatMod.WaterExpanded != null && ShelfCompatMod.WaterExpanded.Value;
        internal static Func<bool> BackEnabled = () => ShelfCompatMod.BackRoom != null && ShelfCompatMod.BackRoom.Value;
        internal static Func<bool> Ready = () => false;
        internal static Func<GameInventory[]> Grids = () => null;
        private static readonly Func<GraphNodeStorage, bool> passThrough = node => true;
        internal static Func<GameItem, IList<GraphNodeStorage>> Above = item => GraphUtils.FindParentsType<GraphNodeStorage>(item, null, passThrough);
        internal static Func<GraphNodeStorage> BackRoom = () => EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.backInvWindow;
        private static bool backAvailable;
        private static readonly Func<bool> canUseBackRoom = () => backAvailable && BackEnabled();
        private static readonly Diagnostics diagnostics = new Diagnostics();
        private static readonly Func<GameItem, int> itemId = item => item.uniqueId;
        private static readonly Func<GameItem, string> itemLabel = item => item.identifier ?? "GameItem";
        private static readonly Func<GraphNodeStorage, string> nodeWord = node => node is GameItem item
            ? item.identifier ?? "GameItem" : node?.GetType().Name ?? "null";
        private static readonly Action<Exception> diagnosticFault = error => Fault("describing a location", error);
        private static readonly Action<string> diagnosticLog = Say;
        private static readonly Action<GameItem, IList<GraphNodeStorage>, bool, int, string> denied =
            (item, above, ready, grids, back) => diagnostics.Report(item, above, ready, grids, back, itemId, itemLabel, nodeWord, diagnosticLog, diagnosticFault);
        private static readonly Action<Exception> postfixFault = error => Fault("checking a location", error);

        private static Type InAssembly(Type type, string name) => type?.Assembly.GetName().Name == name ? type : null;

        internal static void Begin()
        {
            started = false;
            Active = true;
            // Faults are reported once for the lifetime of the loaded mod.
        }

        internal static void Start()
        {
            if (!Active || started) return;
            started = true;
            bool water = false, shop = false;
            string enabled = "unavailable", waterEnabled = "unavailable", backEnabled = "unavailable", reason;
            try
            {
                water = Running("water_expansion");
                shop = Running("ShopExpansion");
                enabled = Enabled() ? "on" : "off";
                waterEnabled = WaterEnabled() ? "on" : "off";
                backEnabled = BackEnabled() ? "on" : "off";
                reason = !water ? "required mod is not running" : Prepare(shop);
            }
            catch (Exception error)
            {
                Remove();
                reason = "startup fault (" + error.GetType().Name + ")";
            }
            Say("[Shelf Compat] loaded " + ShelfCompatMod.Version + "; enabled=" + enabled + "; water_expanded=" + waterEnabled + "; back_room=" + backEnabled
                + "; Water Expanded=" + (water ? "running" : "not running")
                + "; Shop Expansion=" + (shop ? "running" : "not running")
                + "; " + (Installed ? "patched." : "not patched: " + reason + "."));
        }

        private static string Prepare(bool shop)
        {
            Type water = FindWaterType();
            // The literal lookups above are also understood by the game's scanner.
            MethodInfo target = FindTarget();
            string difference = Shape.Water(water, target, typeof(GameItem))
                ?? Shape.Game(AccessTools.Method(typeof(GraphUtils), "FindParentsType"), typeof(GraphNodeStorage));
            if (difference != null) return difference;
            difference = Shape.BackRoom(AccessTools.Property(typeof(EmporiumEntry), "Instance"),
                AccessTools.Property(typeof(EmporiumEntry), "backInvWindow"), typeof(EmporiumEntry), typeof(PixelWindow));
            backAvailable = difference == null;
            if (!backAvailable) Say("[Shelf Compat] back room part off: " + difference + ".");
            PrepareShelves(shop);
            new Harmony(HarmonyId).Patch(target, prefix: new HarmonyMethod(typeof(Bridge), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(Bridge), nameof(Postfix)));
            Installed = true;
            return null;
        }

        private static void PrepareShelves(bool shop)
        {
            Ready = () => false;
            Grids = () => null;
            string difference;
            try
            {
                if (!shop) difference = "Shop Expansion is not running";
                else
                {
                    Type shelf = FindShelfType();
                    PropertyInfo gridProperty = FindGrids(), readyProperty = FindReady();
                    difference = Shape.Shelves(shelf, gridProperty, readyProperty, typeof(GameGridInventory));
                    if (difference == null)
                    {
                        // Read the current array, which is replaced when a shop loads.
                        var grids = (Func<GameGridInventory[]>)gridProperty.GetGetMethod().CreateDelegate(typeof(Func<GameGridInventory[]>));
                        var ready = (Func<bool>)readyProperty.GetGetMethod().CreateDelegate(typeof(Func<bool>));
                        Grids = () => grids();
                        Ready = ready;
                        return;
                    }
                }
            }
            catch (Exception error) { difference = "shelf startup fault (" + error.GetType().Name + ")"; }
            Say("[Shelf Compat] shelf part off: " + difference + ".");
        }

        // Water Expanded's routine walks upwards in its own parameter: when its answer is
        // no, the parameter is null by the time it returns, and that is what a postfix
        // is given as the argument. The item asked about is therefore kept here, before
        // the routine runs.
        internal static void Prefix(GameItem __0, out GameItem __state)
        {
            __state = __0;
        }

        internal static void Postfix(GameItem __state, ref bool __result)
        {
            GameItem __0 = __state;
            try
            {
                if (!Active || !Installed) return;
                // Said once, so the log shows that Water Expanded's test really reaches us.
                if (!firstCheckSaid) { firstCheckSaid = true; Say("[Shelf Compat] first location check for Water Expanded."); }
                // Assign only the completed answer. Any fault keeps the original bool.
                __result = Rules.After<GameItem, GraphNodeStorage>(__result, __0, Enabled, WaterEnabled, canUseBackRoom,
                    Ready, Grids, BackRoom, Above, denied, postfixFault);
            }
            catch (Exception error) { Fault("checking a location", error); }
        }

        internal static void Stop()
        {
            Active = false;
            Remove();
        }

        private static void Remove()
        {
            Installed = false;
            backAvailable = false;
            Ready = () => false;
            Grids = () => null;
            try { new Harmony(HarmonyId).UnpatchAll(HarmonyId); }
            catch (Exception error) { Fault("removing the patch", error); }
        }

        internal static void Fault(string where, Exception error)
        {
            if (faulted) return;
            faulted = true;
            Say("[Shelf Compat] a fault in " + where + "; leaving Water Expanded's answer (" + error.GetType().Name + ").");
        }

        private static void Say(string text)
        {
            try { Log(text); } catch (Exception) { }
        }
    }
}

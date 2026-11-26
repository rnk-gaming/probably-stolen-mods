using System;
using System.Collections.Generic;

namespace Perks
{
    internal static class Night
    {
        internal static Func<bool> HasGoods = () => ShowcaseHelper.HasMerchandise();
        internal static Action<string> Log = text => PlayerStore.Instance.AddNightLog(text);
        internal static void Closed()
        {
            Runtime.Guard("night outings", () => { if (Runtime.Active("night_owl")) PlayerStore.Instance.visitLeftTonight = Rules.Visits(PlayerStore.Instance.visitLeftTonight); });
        }
        internal static void Services()
        {
            Runtime.Guard("nightly services", () =>
            {
                if (!Runtime.Active("steep_overheads")) return;
                int extra = 0;
                foreach (StoreService service in PlayerStore.Instance.storeService)
                    if (service != null && service.unlocked && service.isUsing && IsService(service.id)) extra = checked(extra + Rules.Service(service.cost));
                if (extra <= 0) return;
                int cash = checked(Runtime.Cash() - extra);
                Runtime.SetCash(cash);
                Log(Runtime.TextWith("night_overheads", "Steep Overheads: {0} extra for services.", extra));
            });
        }
        internal static bool IsService(string id) => Rules.IsService(id);
    }
    internal static class SleeperPatch
    {
        internal static void Postfix(ref bool __result)
        { try { if (Runtime.Active("night_owl")) __result = true; } catch (Exception error) { Runtime.Fault("short sleep", error); } }
    }
    internal static class AttractivenessPatch
    {
        internal static void Postfix(ref int __result)
        {
            try
            {
                bool shabby = Runtime.Active("shabby_front"), window = Runtime.Active("window_dresser");
                if (!shabby && !window) return;
                int value = Rules.Attractiveness(__result, shabby, false, false);
                if (window) value = Rules.Attractiveness(value, false, true, Night.HasGoods());
                __result = value;
            }
            catch (Exception error) { Runtime.Fault("store attractiveness", error); }
        }
    }
    internal static class RenovationPatch
    {
        internal static void Postfix(NetworkUpgrade __instance, ref int __result)
        {
            try
            {
                if (Runtime.Active("shabby_front") && __instance != null && (__instance.id ?? "").StartsWith("RENOVATION", StringComparison.OrdinalIgnoreCase)) __result = Rules.Renovation(__result);
            }
            catch (Exception error) { Runtime.Fault("renovation costs", error); }
        }
    }
    internal static class SearchPatch
    {
        internal static Func<GameItem> Spawn = () => ScavHelper.SpawnDumpingGroundLoot();
        internal static void Postfix(ref List<GameItem> __result)
        {
            try
            {
                if (!Runtime.Active("thorough_search") || __result == null || __result.Count == 0) return;
                if (!Runtime.Roll(Rules.SearchChance)) return;
                GameItem extra = Spawn(); if (extra == null) return;
                // Publish the replacement only after all fallible work has completed.
                __result = new List<GameItem>(__result) { extra };
            }
            catch (Exception error) { Runtime.Fault("thorough search", error); }
        }
    }
    internal static class WoundPatch
    {
        internal static void Postfix(ref float __result)
        { try { if (Runtime.Active("careless_digger")) __result = Rules.Wound(__result); } catch (Exception error) { Runtime.Fault("scavenging wounds", error); } }
    }
    internal static class StolenPatch
    {
        internal static void Prefix(GameItem __0, out bool? __state)
        { __state = null; try { if (Runtime.Active("marked_shop") && __0 != null) __state = CoolingPatch.IsStolen(__0); } catch (Exception error) { Runtime.Fault("reading stolen status", error); } }
        internal static void Postfix(GameItem __0, bool? __state)
        {
            try
            {
                if (__state != false || !Runtime.Active("marked_shop") || __0 == null || !CoolingPatch.IsStolen(__0)) return;
                int heat = CoolingPatch.Heat(__0);
                if (heat > 0) CoolingPatch.SetHeat(__0, Rules.Heat(heat));
            }
            catch (Exception error) { Runtime.Fault("initial stolen heat", error); }
        }
    }
    internal static class CooldownPatch
    {
        internal static void Postfix(ref int __0, ref int __1)
        {
            try
            {
                bool friend = Runtime.Active("friend_on_the_force"), marked = Runtime.Active("marked_shop");
                if (!friend && !marked) return;
                int min = Rules.InspectionMin(__0, friend, marked), max = Rules.InspectionMax(__1, min, friend, marked);
                __0 = min; __1 = max;
            }
            catch (Exception error) { Runtime.Fault("inspection cooldown", error); }
        }
    }
}


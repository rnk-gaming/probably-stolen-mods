using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    // Correct the sign before Station Economy creates a seller's offer.
    internal static class WholesaleSign
    {
        internal static bool Active, Installed, shapeSaid;
        internal static Func<bool> On = () => StationEconomyCompatMod.Wholesale == null || StationEconomyCompatMod.Wholesale.Value;
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.StorySell");
        internal static Func<MethodInfo> FindStart = () => AccessTools.DeclaredMethod(AccessTools.TypeByName("StationEconomy.StorySell"), "Start");

        // Negative percentages already mean a discount, including int.MinValue.
        internal static int Corrected(int wholesale) => wholesale > 0 ? -wholesale : wholesale;

        internal static bool Fits(Type type, MethodInfo start)
        {
            if (type == null || !type.IsClass || !type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters
                || start == null || start.DeclaringType != type || start.Name != "Start" || !start.IsPublic || !start.IsStatic
                || start.IsGenericMethod || start.ContainsGenericParameters || start.ReturnType != typeof(void)) return false;
            Type[] expected = { typeof(StoreClient), typeof(Dialogue), typeof(string), typeof(List<string>), typeof(int), typeof(string), typeof(string), typeof(Action<int>) };
            ParameterInfo[] actual = start.GetParameters();
            if (actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i].ParameterType != expected[i]) return false;
            return true;
        }

        internal static void TryInstall()
        {
            MethodInfo target = null;
            try
            {
                if (!Active || Installed) return;
                Type type = FindType();
                if (type == null) return; // Not loaded yet: another asset/game hook retries.
                MethodInfo start = FindStart();
                if (!Fits(type, start))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's wholesale seller has changed shape; this part is left off."); }
                    return;
                }
                target = start;
                new Harmony(Bridge.HarmonyId).Patch(target, prefix: new HarmonyMethod(typeof(WholesaleSign), nameof(Prefix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] wholesale offers of Station Economy's sellers are a discount.");
            }
            catch (Exception error)
            {
                Installed = false;
                try { if (target != null) new Harmony(Bridge.HarmonyId).Unpatch(target, AccessTools.Method(typeof(WholesaleSign), nameof(Prefix))); }
                catch (Exception rollback) { Bridge.Fault("removing the wholesale sign patch", rollback); }
                Bridge.Fault("installing the wholesale sign", error);
            }
        }

        internal static void Disable() { Active = false; Installed = false; }

        internal static bool Prefix(ref int __4)
        {
            int original = __4;
            try
            {
                if (Active && Installed && On()) __4 = Corrected(__4);
            }
            catch (Exception error)
            {
                __4 = original;
                Bridge.Fault("correcting a wholesale offer", error);
            }
            return true;
        }
    }
}

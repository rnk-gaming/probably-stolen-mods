using System;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    internal static class FurnaceHopper
    {
        internal static bool Active, Installed, shapeSaid;
        internal static Func<bool> On = () => StationEconomyCompatMod.Hoppers == null || StationEconomyCompatMod.Hoppers.Value;
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.Patch_HopperRunToFurnace");
        internal static Func<MethodInfo> FindPrefix = () => AccessTools.Method("StationEconomy.Patch_HopperRunToFurnace:Prefix");
        internal static Func<MethodInfo> FindRun = () => AccessTools.DeclaredMethod(typeof(HopperHelper), "Run");

        private static bool MethodFits(MethodInfo method, string name, Type returns, params Type[] parameters)
        {
            if (method == null || method.Name != name || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != returns) return false;
            ParameterInfo[] actual = method.GetParameters();
            if (actual.Length != parameters.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i].ParameterType != parameters[i]) return false;
            return true;
        }

        internal static bool Fits(Type type, MethodInfo prefix, MethodInfo run) =>
            type != null && type.IsClass && type.IsAbstract && type.IsSealed && !type.ContainsGenericParameters
            && MethodFits(prefix, "Prefix", typeof(bool), typeof(GameItem), typeof(ValueTuple<int, int>).MakeByRefType())
            && prefix.DeclaringType == type
            && MethodFits(run, "Run", typeof(ValueTuple<int, int>), typeof(GameItem));

        internal static void TryInstall()
        {
            MethodInfo target = null;
            try
            {
                if (!Active || Installed) return;
                Type type = FindType();
                if (type == null) return; // Not loaded yet: another asset/game hook retries.
                MethodInfo prefix = FindPrefix();
                if (!Fits(type, prefix, FindRun()))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's furnace hopper stop has changed shape; this part is left off."); }
                    return;
                }
                target = prefix;
                new Harmony(Bridge.HarmonyId).Patch(target, prefix: new HarmonyMethod(typeof(FurnaceHopper), nameof(Prefix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] hoppers fill the Furnace as in the base game.");
            }
            catch (Exception error)
            {
                Installed = false;
                try { if (target != null) new Harmony(Bridge.HarmonyId).Unpatch(target, AccessTools.Method(typeof(FurnaceHopper), nameof(Prefix))); }
                catch (Exception rollback) { Bridge.Fault("removing the furnace hopper patch", rollback); }
                Bridge.Fault("installing the furnace hopper", error);
            }
        }

        internal static void Disable() { Active = false; Installed = false; }

        // This result belongs to Station Economy's bool prefix, not HopperHelper.Run's
        // tuple. Its ref tuple argument is deliberately not bound or touched here.
        internal static bool Prefix(ref bool __result)
        {
            try
            {
                if (!Active || !Installed || !On()) return true;
                __result = true;
                return false;
            }
            catch (Exception error)
            {
                Bridge.Fault("letting hoppers fill the Furnace", error);
                return true;
            }
        }
    }
}

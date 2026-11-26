using System;
using System.Reflection;
using HarmonyLib;

namespace ScavengingPool
{
    internal static class TransitQuotaReader
    {
        internal static Func<bool> Running = () => ModHelper.IsModRunning("transit_quota");
        internal static Func<Type> FindType = () => AccessTools.TypeByName("TransitQuota.Quota");
        internal static Func<PropertyInfo> FindTotal = () => AccessTools.Property(AccessTools.TypeByName("TransitQuota.Quota"), "Total");
        private static Func<int> readTotal;
        private static bool gaveUp;

        internal static bool Fits(Type type, PropertyInfo total)
        {
            if (type == null || type.FullName != "TransitQuota.Quota" || !type.IsPublic
                || !type.IsClass || !type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters
                || total == null || total.DeclaringType != type || total.Name != "Total"
                || total.PropertyType != typeof(int) || total.GetIndexParameters().Length != 0) return false;
            MethodInfo getter = total.GetGetMethod();
            return getter != null && getter.IsPublic && getter.IsStatic && !getter.IsGenericMethod
                && !getter.ContainsGenericParameters && getter.ReturnType == typeof(int)
                && getter.GetParameters().Length == 0;
        }

        internal static bool TryRead(out int total)
        {
            total = 0;
            try
            {
                if (!Running() || gaveUp) return false;
                if (readTotal == null)
                {
                    Type type = FindType();
                    PropertyInfo property = type == null ? null : FindTotal();
                    if (!Fits(type, property))
                    {
                        gaveUp = true;
                        Pool.Report("transit quota shape", "Transit Quota is laid out differently; counting the game's own outings.");
                        return false;
                    }
                    readTotal = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), property.GetGetMethod());
                }
                total = readTotal();
                if (total >= 1) return true;
                Pool.Fault("reading Transit Quota", new InvalidOperationException("the quota answered fewer than one trip"));
            }
            catch (Exception error) { Pool.Fault("reading Transit Quota", error); }
            return false;
        }

        // Tests reset the connection; a game load only forgets the save's counters.
        internal static void Reset() { readTotal = null; gaveUp = false; }
    }
}

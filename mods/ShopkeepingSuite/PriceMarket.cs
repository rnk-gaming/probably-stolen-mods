using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ShopkeepingSuite
{
    internal sealed class PriceMarket
    {
        internal static Func<bool> Running = () => ModHelper.IsModRunning("suoer.stationeconomy");
        internal static Func<Type> Resolve = () => AccessTools.TypeByName("StationEconomy.Market");
        internal static Func<MethodInfo> Method = () => AccessTools.Method(AccessTools.TypeByName("StationEconomy.Market"), "ValueFactor", new[] { typeof(GameItem) });
        internal static Func<GameItem, double> Bind(Type type, MethodInfo method)
        {
            if (type == null || !type.IsClass || !type.IsAbstract || !type.IsSealed || !(type.IsPublic || type.IsNestedPublic) || type.ContainsGenericParameters)
                throw new InvalidOperationException("Station Economy's Market type differs");
            if (method == null || method.Name != "ValueFactor" || method.DeclaringType != type || !method.IsPublic || !method.IsStatic || method.ReturnType != typeof(double) || method.ContainsGenericParameters || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(GameItem) }))
                throw new InvalidOperationException("Station Economy's ValueFactor shape differs");
            return (Func<GameItem, double>)Delegate.CreateDelegate(typeof(Func<GameItem, double>), method);
        }
        private bool checkedRunning, bound, failed;
        private Func<GameItem, double> reader;
        private sealed class Cached { internal float At, ValidAt = float.NegativeInfinity; internal double Value = 1; internal bool SampleFault; }
        private ConditionalWeakTable<GameItem, Cached> cache = new ConditionalWeakTable<GameItem, Cached>();
        internal bool Ready()
        {
            if (failed) return false;
            try
            {
                if (!checkedRunning)
                {
                    checkedRunning = true;
                    if (!Running()) return false;
                    Type type = Resolve();
                    reader = Bind(type, Method());
                    bound = true;
                }
                return bound;
            }
            catch (Exception error) { Fault(error); return false; }
        }
        private void Fault(Exception error)
        {
            failed = true; reader = null; ClearCache(); SuiteMod.Fault("price market", error);
        }
        private static double Retained(Cached value, float now) => now >= value.ValidAt && now - value.ValidAt < 60f ? value.Value : 1;
        internal float ValidUntil(GameItem item) => cache.TryGetValue(item, out Cached value) ? value.ValidAt + 60f : float.PositiveInfinity;
        // The item display uses 1 for unknown. An average must exclude unknown readings.
        internal double ReadSample(GameItem item, float now)
        {
            if (failed || item == null) return double.NaN;
            Cached value = null;
            try
            {
                if (!Ready()) return double.NaN;
                bool recent = cache.TryGetValue(item, out value) && now >= value.At && now - value.At < 1f;
                if (!recent)
                {
                    if (value == null || now < value.At) value = new Cached();
                    // Remember failed attempts too, without latching the ordinary reader off.
                    value.At = now; value.SampleFault = true;
                    cache.Remove(item); cache.Add(item, value);
                    double factor = reader(item);
                    if (PriceRules.ValidFactor(factor)) { value.ValidAt = now; value.Value = factor; }
                    value.SampleFault = false;
                }
                return !value.SampleFault && now >= value.ValidAt && now - value.ValidAt < 60f ? value.Value : double.NaN;
            }
            catch (Exception error) { SuiteMod.Fault("price sign market", error); return double.NaN; }
        }
        internal double Read(GameItem item, float now)
        {
            if (failed || item == null) return 1;
            try
            {
                if (!Ready()) return 1;
                if (cache.TryGetValue(item, out Cached value) && now >= value.At && now - value.At < 1f) return Retained(value, now);
                if (value == null || now < value.At) value = new Cached();
                double factor = reader(item);
                if (PriceRules.ValidFactor(factor)) { value.ValidAt = now; value.Value = factor; }
                value.At = now; value.SampleFault = false;
                cache.Remove(item); cache.Add(item, value);
                return Retained(value, now);
            }
            catch (Exception error) { Fault(error); return 1; }
        }
        internal void ClearCache()
        {
            cache = new ConditionalWeakTable<GameItem, Cached>();
            // A load may be the first point at which the loader knows the optional mod.
            // A shape/read fault, however, keeps this adapter off for the session.
            if (!bound && !failed) checkedRunning = false;
        }
    }
}

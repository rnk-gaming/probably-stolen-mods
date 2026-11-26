using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    internal sealed class FurnaceShape
    {
        internal Type Batch, Inventory, Item, Node;
        internal MethodInfo Take, Accept, Hopper;
        internal MemberInfo Children, Identifier, Parent;
        internal PropertyInfo Units;
    }

    internal static class FurnaceFeed
    {
        internal static bool Active, Installed;
        internal static bool shapeSaid;
        internal static Func<bool> On = () => StationEconomyCompatMod.Furnace == null || StationEconomyCompatMod.Furnace.Value;
        internal static Func<Type> FindBatch = () => AccessTools.TypeByName("StationEconomy.FurnaceBatch");
        internal static Func<MethodInfo> FindTake = () => AccessTools.Method("StationEconomy.FurnaceBatch:Take");
        internal static Func<Type, FurnaceShape> FindShape = ReadShape;
        internal static FurnaceTransfer Port = new FurnaceTransfer
        {
            Items = inventory => ((GameInventory)inventory).childItems.ToArray(),
            Identifier = item => ((GameItem)item).identifier,
            Units = item => ((GameItem)item).unitCount,
            IsHopper = item => HopperHelper.IsHopper((GameItem)item),
            InSource = (item, source) => ReferenceEquals(((GameItem)item).parentInventory, source),
            Place = (input, item) => GraphUtils.TryAcceptAll((GameInventory)input, (GameItem)item, 1),
            Prepare = (input, ledger) =>
            {
                var mine = (List<KeyValuePair<GameItem, GameInventory>>)ledger;
                int required = checked(mine.Count + ((GameInventory)input).childItems.Count + 1);
                if (mine.Capacity < required) mine.Capacity = required;
            },
            RecordNew = RecordNew
        };

        internal static FurnaceShape ReadShape(Type batch) => new FurnaceShape
        {
            Batch = batch, Inventory = typeof(GameInventory), Item = typeof(GameItem), Node = typeof(GraphNodeStorage),
            Take = FindTake(),
            Accept = AccessTools.Method(typeof(GraphUtils), "TryAcceptAll", new[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) }),
            Hopper = AccessTools.Method(typeof(HopperHelper), "IsHopper", new[] { typeof(GameItem) }),
            Children = AccessTools.Property(typeof(GameInventory), "childItems"),
            Identifier = AccessTools.Property(typeof(GameItem), "identifier"),
            Units = AccessTools.Property(typeof(GameItem), "unitCount"),
            Parent = AccessTools.Property(typeof(GameItem), "parentInventory")
        };

        private static bool MethodFits(MethodInfo method, bool publicly, Type returns, params Type[] parameters)
        {
            if (method == null || !method.IsStatic || (publicly && !method.IsPublic) || method.ContainsGenericParameters || method.ReturnType != returns) return false;
            ParameterInfo[] actual = method.GetParameters();
            if (actual.Length != parameters.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i].ParameterType != parameters[i]) return false;
            return true;
        }

        private static bool MemberFits(MemberInfo member, Type owner, Type type)
        {
            if (member == null || !member.DeclaringType.IsAssignableFrom(owner)) return false;
            // These are property calls in the compiled adapter: a field of the same
            // name and type is still a different shape and must not be patched.
            if (!(member is PropertyInfo property)) return false;
            MethodInfo getter = property.GetGetMethod();
            return getter != null && !getter.IsStatic && !getter.ContainsGenericParameters && getter.ReturnType == type
                && getter.GetParameters().Length == 0 && property.GetIndexParameters().Length == 0;
        }

        internal static bool Fits(FurnaceShape shape)
        {
            if (shape == null || shape.Batch == null || !shape.Batch.IsAbstract || !shape.Batch.IsSealed || shape.Batch.ContainsGenericParameters
                || shape.Inventory == null || shape.Item == null || shape.Node == null || !shape.Node.IsAssignableFrom(shape.Inventory)) return false;
            Type ledger = typeof(List<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(shape.Item, shape.Inventory));
            MethodInfo getter = shape.Units?.GetGetMethod();
            return MethodFits(shape.Take, false, typeof(int), shape.Inventory, shape.Inventory, typeof(string[]), typeof(int), typeof(int), typeof(List<string>), typeof(int), ledger)
                && shape.Take.DeclaringType == shape.Batch
                && MethodFits(shape.Accept, true, typeof(int), shape.Node, shape.Item, typeof(int))
                && MethodFits(shape.Hopper, true, typeof(bool), shape.Item)
                && MemberFits(shape.Children, shape.Inventory, typeof(List<>).MakeGenericType(shape.Item))
                && MemberFits(shape.Identifier, shape.Item, typeof(string)) && MemberFits(shape.Parent, shape.Item, shape.Inventory)
                && getter != null && !getter.IsStatic && !getter.ContainsGenericParameters && getter.ReturnType == typeof(int)
                && getter.GetParameters().Length == 0 && shape.Units.GetIndexParameters().Length == 0 && getter.DeclaringType.IsAssignableFrom(shape.Item);
        }

        internal static void TryInstall()
        {
            MethodInfo take = null;
            try
            {
                if (!Active || Installed) return;
                Type batch = FindBatch();
                if (batch == null) return; // Not loaded yet: another asset/game hook retries.
                FurnaceShape shape = FindShape(batch);
                if (!Fits(shape))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's furnace feed has changed shape; this part is left off."); }
                    return;
                }
                take = shape.Take;
                new Harmony(Bridge.HarmonyId).Patch(take, prefix: new HarmonyMethod(typeof(FurnaceFeed), nameof(Prefix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] feeding the Furnace in full through a hopper and recording what can go back.");
            }
            catch (Exception error)
            {
                Installed = false;
                try { if (take != null) new Harmony(Bridge.HarmonyId).Unpatch(take, AccessTools.Method(typeof(FurnaceFeed), nameof(Prefix))); }
                catch (Exception rollback) { Bridge.Fault("removing the furnace feed patch", rollback); }
                Bridge.Fault("installing the furnace feed", error);
            }
        }

        // Reserve capacity before placement, then only compare references and append.
        // This is idempotent so fault recovery can finish a partially written ledger.
        internal static int RecordNew(object input, object source, object[] before, object ledger)
        {
            var mine = (List<KeyValuePair<GameItem, GameInventory>>)ledger;
            int added = 0;
            foreach (GameItem item in ((GameInventory)input).childItems)
            {
                if (item == null) continue;
                bool existed = false;
                foreach (object old in before) if (ReferenceEquals(old, item)) { existed = true; break; }
                if (existed) continue;
                added++;
                bool recorded = false;
                foreach (var pair in mine) if (ReferenceEquals(pair.Key, item) && ReferenceEquals(pair.Value, source)) { recorded = true; break; }
                if (!recorded) mine.Add(new KeyValuePair<GameItem, GameInventory>(item, (GameInventory)source));
            }
            return added;
        }

        internal static void Disable() { Active = false; Installed = false; }
        internal static bool Prefix(GameInventory __0, GameInventory __1, string[] __2, int __3, int __4,
            List<string> __5, int __6, List<KeyValuePair<GameItem, GameInventory>> __7, ref int __result)
        {
            // Before contains the entire gate and operation in its exception boundary.
            return FurnaceRules.Before(Active, Installed, On, __0, __1, __2, __3, __4, __5, __6, __7, Port,
                error => Bridge.Fault("feeding the Furnace", error), ref __result);
        }
    }
}

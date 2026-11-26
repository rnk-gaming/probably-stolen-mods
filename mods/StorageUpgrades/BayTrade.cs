using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LocalMods;

namespace StorageUpgrades
{
    internal static class BayTrade
    {
        internal static bool Active;
        // In the installed game the trashcan member is a GameItem. Its own pages
        // are the inventories the barter searches; do not search the whole shop.
        internal static Func<List<GameInventory>> Trash = () => Storage.OwnGrids(EmporiumEntry.Instance.trashcan.contentWindow).Cast<GameInventory>().ToList();
        internal static Func<GameInventory, IEnumerable<GameItem>> Items = inventory => GraphUtils.FindAllChildrenType<GameItem>(inventory);
        internal static Func<List<GameItem>> Buffer = () => PlayerStore.Instance.exchangeManager.itemsBuffer;
        internal static Func<GameItem, string, bool> HasTag = (item, key) => item.IsTag(key);
        internal static Action<GameItem, string> ClearTag = (item, key) => item.ModifyTag(key, tag => tag.Disable());
        internal static Action<GameItem, int[][]> PutSizesBack = RestoreSizes;
        private const string Owner = "mod.rnk.storage-upgrades.bay-trade";
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        internal sealed class State
        {
            internal int Columns, Rows, Count;
            internal List<GameItem> Buffer;
            internal HashSet<GameItem> Existing;
        }
        private static bool Method(MethodInfo method, Type result, params Type[] args)
        {
            return method != null && method.IsPublic && !method.IsStatic && method.ReturnType == result && !method.ContainsGenericParameters
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(args);
        }
        private static bool Member(FieldInfo field, PropertyInfo property, Type expected, bool isStatic = false)
        {
            if (field != null) return field.IsPublic && field.IsStatic == isStatic && field.FieldType == expected;
            MethodInfo getter = property?.GetGetMethod();
            return getter != null && getter.IsStatic == isStatic && property.PropertyType == expected;
        }
        internal static bool Fits()
        {
            try { return BarterFits(); }
            catch (TypeLoadException) { return false; }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BarterFits()
        {
            FieldInfo exchange = typeof(PlayerStore).GetField("exchangeManager", PublicInstance);
            return Method(AccessTools.Method(typeof(ExchangeBarter), "Accept", Type.EmptyTypes), typeof(void))
                && Method(AccessTools.Method(typeof(ExchangeBarter), "CanAccept", Type.EmptyTypes), typeof(bool))
                && Member(typeof(ExchangeBarter).GetField("identifier", PublicInstance), null, typeof(string))
                && Member(typeof(ExchangeBarter).GetField("onAccept", PublicInstance), null, typeof(Action<List<GameItem>>))
                && Member(AccessTools.Field(typeof(EmporiumEntry), "Instance"), AccessTools.Property(typeof(EmporiumEntry), "Instance"), typeof(EmporiumEntry), true)
                && Member(AccessTools.Field(typeof(EmporiumEntry), "trashcan"), AccessTools.Property(typeof(EmporiumEntry), "trashcan"), typeof(GameItem))
                && Member(AccessTools.Field(typeof(PlayerStore), "Instance"), AccessTools.Property(typeof(PlayerStore), "Instance"), typeof(PlayerStore), true)
                && Member(exchange, null, typeof(ExchangeManager))
                && Member(typeof(ExchangeManager).GetField("itemsBuffer", PublicInstance), null, typeof(List<GameItem>));
        }
        internal static void Install()
        {
            Storage.Guard("bay trade patches", () =>
            {
                if (!Fits()) throw new InvalidOperationException("the bay trade has a different shape; no trade patch applied");
                PatchChecked();
            });
        }
        // Keep all typed barter references behind the successful shape check,
        // including JIT compilation of the install step and hook initialisation.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchChecked()
        {
            var harmony = new Harmony(Owner);
            MethodInfo accept = typeof(ExchangeBarter).GetMethod("Accept", PublicInstance, null, Type.EmptyTypes, null);
            if (Harmony.GetPatchInfo(accept)?.Owners.Contains(Owner) == true) return;
            harmony.Patch(accept, prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.Prefix)), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.Postfix)));
        }
        internal static void Disable()
        {
            Active = false;
            Storage.Guard("removing bay trade patches", () => new Harmony(Owner).UnpatchAll(Owner));
        }
        internal static class Hooks
        {
            internal static Func<ExchangeBarter, bool> CanAccept = barter => barter.CanAccept();
            internal static void Prefix(ExchangeBarter __instance, out State __state)
            {
                __state = null;
                try
                {
                    if (!Active || !Storage.Switch("storage_enabled") || __instance == null
                        || __instance.identifier != "smuggler_barter" || !CanAccept(__instance)) return;
                    List<GameItem> buffer = Buffer();
                    if (buffer == null) return;
                    var state = new State { Buffer = buffer, Count = buffer.Count, Existing = new HashSet<GameItem>(buffer) };
                    foreach (GameInventory inventory in Trash())
                        foreach (GameItem item in Items(inventory))
                        {
                            if (item == null || item.identifier != "smuggler_bay" || !Storage.Owned(item)) continue;
                            state.Columns = Math.Max(state.Columns, Storage.Read(item, Storage.ColumnsTag));
                            state.Rows = Math.Max(state.Rows, Storage.Read(item, Storage.RowsTag));
                        }
                    if (state.Columns > 0 || state.Rows > 0) __state = state;
                }
                catch (Exception error) { Storage.Fault("bay trade", error); }
            }
            internal static void Postfix(State __state)
            {
                try
                {
                    if (__state == null || !Active || !Storage.Switch("storage_enabled") || !ReferenceEquals(Buffer(), __state.Buffer)) return;
                    // Copy first: sizing must not change which rewards belong to this call.
                    GameItem[] added = __state.Buffer.Skip(__state.Count).Where(item => item != null
                        && item.identifier == "smuggler_bay_mod" && !__state.Existing.Contains(item)).Distinct().ToArray();
                    foreach (GameItem item in added) Transfer(item, __state);
                }
                catch (Exception error) { Storage.Fault("bay trade", error); }
            }
        }
        private static void Transfer(GameItem item, State state)
        {
            string[] keys = { Storage.ColumnsTag, Storage.RowsTag };
            int[] basis = StorageRules.Bays["smuggler_bay_mod"];
            int[] gains = {
                Math.Min(state.Columns, StorageRules.Clamp(Storage.Setting("smuggler_bay_mod_columns", basis[2]), basis[0], basis[2]) - basis[0]),
                Math.Min(state.Rows, StorageRules.Clamp(Storage.Setting("smuggler_bay_mod_rows", basis[3]), basis[1], basis[3]) - basis[1])
            };
            if (gains[0] <= 0 && gains[1] <= 0) return;
            int[][] sizes = Storage.Sizes(item);
            if (sizes == null || sizes.Length == 0 || sizes.Any(size => size == null || size.Length != 2 || size[0] <= 0 || size[1] <= 0))
                throw new InvalidOperationException("Missing reward grid");
            sizes = sizes.Select(size => (int[])size.Clone()).ToArray();
            bool[] had = keys.Select(key => HasTag(item, key)).ToArray();
            int[] before = keys.Select(key => Storage.Read(item, key)).ToArray();
            try
            {
                for (int i = 0; i < keys.Length; i++) Storage.Write(item, keys[i], gains[i]);
                Storage.RestoreChecked(item);
            }
            catch (Exception error)
            {
                // This is a new empty reward, so putting back its original grid is safe.
                // Restore independently: one failed rollback must not prevent the rest.
                for (int i = 0; i < keys.Length; i++)
                {
                    // Disabled tags retain their int, which save restoration reads.
                    try { Storage.Write(item, keys[i], before[i]); }
                    catch (Exception rollback) { Storage.Fault("bay trade", rollback); }
                    if (!had[i])
                    {
                        try { ClearTag(item, keys[i]); }
                        catch (Exception rollback) { Storage.Fault("bay trade", rollback); }
                    }
                }
                try { PutSizesBack(item, sizes); }
                catch (Exception rollback) { Storage.Fault("bay trade", rollback); }
                Storage.Fault("bay trade", error);
            }
        }
        private static void RestoreSizes(GameItem item, int[][] sizes)
        {
            var grids = Storage.OwnGrids(item.contentWindow);
            if (grids.Count != sizes.Length) throw new InvalidOperationException("Reward pages changed during sizing");
            for (int i = 0; i < grids.Count; i++)
                if (grids[i].inventoryShape.width != sizes[i][0] || grids[i].inventoryShape.height != sizes[i][1]) grids[i].SetShape(sizes[i][0], sizes[i][1]);
            item.contentWindow.Validate();
        }
    }
}

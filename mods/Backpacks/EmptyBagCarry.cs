using System;
using System.Collections.Generic;
using System.Reflection;
using LocalMods;
namespace Backpacks
{
    internal static class EmptyBagCarry
    {
        internal static Func<GameItem, bool> Tag = item => item.IsTag("BACKPACK_TAG");
        internal static Func<GameInventory, GameItem> Parent = inventory => inventory.GetParentItem();
        internal static Func<GameItem, List<GameGridInventory>> Grids = item => Storage.OwnGrids(item.contentWindow);
        internal static Func<GameGridInventory, IEnumerable<GameItem>> Items = grid => grid.childItems;
        internal static Func<GameItem, bool> SlotsEmpty = EmptySlots;
        private static bool EmptySlots(GameItem item)
        {
            foreach (GraphNodeStorage node in GridWalk.Pages<GraphNodeStorage>(item.contentWindow, node => node.children,
                node => node is GameItem, node => node is GameSlotInventory))
                if (((GameSlotInventory)node).childItem != null) return false;
            return true;
        }
        internal static bool Fits()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = HarmonyLib.AccessTools.Field(typeof(GameGridInventory), "mayInventoryAddItemFunc");
            var property = typeof(GameGridInventory).GetProperty("mayInventoryAddItemFunc", flags);
            return field != null ? !field.IsStatic && field.FieldType == typeof(Func<GameItem, GameInventory, bool>)
                : property?.PropertyType == typeof(Func<GameItem, GameInventory, bool>) && property.GetGetMethod(true)?.IsStatic == false && property.GetSetMethod(true)?.IsStatic == false;
        }
        internal static bool Accept(GameItem item, GameInventory inventory)
        {
            try
            {
                if (!BackpacksMod.Running || item == null || inventory == null || !Backpacks.Switch("backpacks_enabled") || !Backpacks.Switch("carry_empty_bags")
                    || !Tag(item) || !Storage.Owned(item)) return false;
                GameItem parent = Parent(inventory);
                if (parent == null || !Backpacks.Supported(parent.identifier) || ReferenceEquals(item, parent) || !SlotsEmpty(item)) return false;
                // Includes every own compartment, side bag and toolbox grid. An occupied
                // attachment slot is nonempty even when the attached bag itself is empty.
                foreach (GameGridInventory grid in Grids(item))
                    foreach (GameItem held in Items(grid)) if (held != null) return false;
                return true;
            }
            catch (Exception error) { Storage.Fault("carrying empty bags", error); return false; }
        }
        internal static void Prepare(GameItem item)
        {
            Storage.Guard("preparing empty bag allowance", () =>
            {
                if (item == null || !Backpacks.Supported(item.identifier)) return;
                if (!Fits()) throw new InvalidOperationException("Backpack inventory predicate shape differs");
                Func<GameItem, GameInventory, bool> allow = Accept;
                foreach (GameGridInventory grid in Grids(item))
                {
                    if (grid.mayInventoryAddItemFunc == null)
                    {
                        Storage.Fault("empty bag allowance", new InvalidOperationException("the empty-bag allowance could not be added to a backpack without a rule"));
                        continue;
                    }
                    bool added = false;
                    foreach (Delegate handler in grid.mayInventoryAddItemFunc.GetInvocationList()) if (handler.Equals(allow)) { added = true; break; }
                    if (!added) grid.mayInventoryAddItemFunc += allow;
                }
            });
        }
    }
}

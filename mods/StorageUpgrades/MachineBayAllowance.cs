using System;
using System.Collections.Generic;
using System.Reflection;
using LocalMods;

namespace StorageUpgrades
{
    internal static class MachineBayAllowance
    {
        internal static Func<GameInventory, GameItem> Parent = inventory => inventory.GetParentItem();
        internal static Func<GameItem, List<GameGridInventory>> Grids = item => Storage.OwnGrids(item.contentWindow);
        // The item that holds an item: the owner of the inventory it lies in, or nothing.
        internal static Func<GameItem, GameItem> Holder = item => item.parentInventory == null ? null : item.parentInventory.GetParentItem();

        internal static string SettingFor(string id)
            => id == "smuggler_bay_mini" || id == "smuggler_bay" || id == "smuggler_bay_mod" ? "smuggler_takes_machine_bays"
                : id == "makeshift_storage_bay" || id == "storage_bay" || id == "storage_bay_large" ? "storage_bays_take_machine_bays" : null;

        internal static bool Fits()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = HarmonyLib.AccessTools.Field(typeof(GameGridInventory), "mayInventoryAddItemFunc");
            var property = typeof(GameGridInventory).GetProperty("mayInventoryAddItemFunc", flags);
            return field != null ? !field.IsStatic && field.FieldType == typeof(Func<GameItem, GameInventory, bool>)
                : property?.PropertyType == typeof(Func<GameItem, GameInventory, bool>) && property.GetGetMethod(true)?.IsStatic == false
                    && property.GetSetMethod(true)?.IsStatic == false;
        }

        internal static bool Accept(GameItem item, GameInventory inventory)
        {
            try
            {
                if (!StorageUpgradesMod.Running || item == null || inventory == null || !Storage.Switch("storage_enabled")
                    || (item.identifier != "machine_bay" && item.identifier != "machine_bay_ext") || !Storage.Owned(item)) return false;
                GameItem parent = Parent(inventory);
                if (parent == null || !Storage.Owned(parent)) return false;
                string setting = SettingFor(parent.identifier);
                if (setting == null || !Storage.Switch(setting)) return false;
                // Never into itself, and never into a bay that lies somewhere inside
                // the Machine Bay being added: that would close a ring of containers.
                GameItem holder = parent;
                for (int depth = 0; holder != null && depth < 64; depth++)
                {
                    if (ReferenceEquals(holder, item)) return false;
                    holder = Holder(holder);
                }
                return holder == null;
            }
            catch (Exception error) { Storage.Fault("machine bay allowance", error); return false; }
        }

        internal static void Prepare(GameItem item, string identifier)
        {
            Storage.Guard("preparing bay allowances", () =>
            {
                if (item == null || SettingFor(identifier) == null) return;
                if (!Fits()) throw new InvalidOperationException("Bay inventory predicate shape differs");
                Func<GameItem, GameInventory, bool> allow = Accept;
                foreach (GameGridInventory grid in Grids(item))
                {
                    // The game ORs every answer. A null rule may mean unrestricted
                    // storage, so adding our limited answer there would change it.
                    if (grid.mayInventoryAddItemFunc == null)
                    {
                        Storage.Fault("bay allowance", new InvalidOperationException("the machine-bay allowance could not be added to a bay without a rule"));
                        continue;
                    }
                    bool added = false;
                    foreach (Delegate handler in grid.mayInventoryAddItemFunc.GetInvocationList())
                        if (handler.Equals(allow)) { added = true; break; }
                    if (!added) grid.mayInventoryAddItemFunc += allow;
                }
            });
        }
    }
}

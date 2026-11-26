// Machine Upgrades: upgrade machines by dragging vanilla materials onto them, in the
// same style as the vanilla Fridge, Vending Machine and Scav Scanner.
//
// Same material, same effect where possible: Metal Ingot = Storage, Electronic Parts
// (Common) = Efficiency, Wire = Performance. Quality uses a material that fits the
// machine. The Furnace and Cryptographic Desequencer also take an empty copy of
// themselves (Batches / Output).
//
// Upgrade levels are stored as tags on the machine, so they travel with the save.
using System;
using System.Collections.Generic;
using HarmonyLib;

using LocalMods;

namespace MachineUpgrades
{
    internal enum Effect { Storage, Efficiency, Quality, Performance, Batches, Output }

    internal sealed class Track
    {
        internal readonly Effect Effect;
        internal readonly string Material;      // identifier of the item used
        internal readonly string MaterialName;  // localisation key of that item
        internal readonly int Max;              // number of steps
        internal readonly int PerStep;          // percent per step (stat tracks)

        internal Track(Effect effect, string material, string materialName, int max, int perStep = 0)
        {
            Effect = effect;
            Material = material;
            MaterialName = materialName;
            Max = max;
            PerStep = perStep;
        }

        internal string Tag => "MACHINE_UPGRADES_" + Effect.ToString().ToUpperInvariant() + "_INT";

        // Batches and Output consume an empty copy of the machine itself.
        internal bool UsesDonorMachine => Effect == Effect.Batches || Effect == Effect.Output;
    }

    internal static class Upgrades
    {
        internal const string Furnace = "furnace";
        internal const string Printer = "printer";
        internal const string Hydroponic = "hydroponic";
        internal const string MoistureFarm = "moisture_farm";
        internal const string WaterPurifier = "water_purifier";
        internal const string Evaporator = "evaporator";
        internal const string Desequencer = "desequencer";
        internal const string ChemFinisher = "chem_finisher";
        internal const string IntegratedFarm = "agri_integrated_farm"; // Agri Expansion, matched by name only
        internal const string BasePowerTag = "MACHINE_UPGRADES_BASE_POWER_INT";
        internal const int MaxRows = 12;
        private static ConfigEntry<bool> stackSteps;
        internal static Func<bool> StackSteps = () => stackSteps == null || stackSteps.Value;
        internal static Func<GameItem, GameItem, bool> Owns = (source, target) => GeneralHelper.IsItemOwned(source, target);
        internal static Action<GameItem> Update = RecomputeStats;
        internal static Action Sound = PlaySound;

        internal static void Configure(string id)
        {
            stackSteps = ModConfig.For(id).Define("stack_steps", true, "Machine Upgrades",
                () => "A dropped stack upgrades as far as it reaches",
                () => "Use a dropped stack for every upgrade it reaches. Off: one drop takes one step. Changes take effect now.");
        }

        private const string Ingot = "metal_ingot", IngotName = "item_metal_ingot_name";
        private const string Electronic = "common_electronic", ElectronicName = "item_common_electronic_name";
        private const string Wire = "wire", WireName = "item_wire_name";
        private const string WaterFilter = "water_filter", WaterFilterName = "item_water_filter_name";

        private static Track Efficiency40() => new Track(Effect.Efficiency, Electronic, ElectronicName, 40, 5);

        // Storage: each Metal Ingot adds one row. Step counts are chosen so every
        // grid ends on an even height (12 rows).
        internal static readonly Dictionary<string, Track[]> Machines = new Dictionary<string, Track[]>
        {
            {
                Furnace, new[]
                {
                    new Track(Effect.Storage, Ingot, IngotName, 6),
                    Efficiency40(),
                    new Track(Effect.Quality, "advanced_flux_agent", "item_advanced_flux_agent_name", 20, 10),
                    new Track(Effect.Batches, Furnace, "item_furnace_name", 5),
                }
            },
            {
                Printer, new[]
                {
                    new Track(Effect.Storage, Ingot, IngotName, 7),
                    Efficiency40(),
                    new Track(Effect.Performance, Wire, WireName, 20, 5),
                    new Track(Effect.Quality, "gun_part", "item_gun_part_name", 25, 4),
                }
            },
            {
                // Performance and Quality have ratio 2 on this machine: half effect.
                Hydroponic, new[]
                {
                    new Track(Effect.Storage, Ingot, IngotName, 6),
                    Efficiency40(),
                    new Track(Effect.Performance, Wire, WireName, 20, 10),
                    new Track(Effect.Quality, "common_chemical", "item_common_chemical_name", 40, 5),
                }
            },
            {
                // Quality: Pure Water at 150%.
                MoistureFarm, new[]
                {
                    Efficiency40(),
                    new Track(Effect.Performance, Wire, WireName, 20, 5),
                    new Track(Effect.Quality, WaterFilter, WaterFilterName, 150, 1),
                }
            },
            {
                // Quality: 100% pure water at 200%.
                WaterPurifier, new[]
                {
                    Efficiency40(),
                    new Track(Effect.Performance, Wire, WireName, 20, 5),
                    new Track(Effect.Quality, WaterFilter, WaterFilterName, 200, 1),
                }
            },
            {
                Evaporator, new[]
                {
                    Efficiency40(),
                    new Track(Effect.Performance, Wire, WireName, 20, 5),
                }
            },
            {
                // Quality is not used by this machine in the game, so it has no track.
                Desequencer, new[]
                {
                    new Track(Effect.Efficiency, "powerblock", "item_powerblock_name", 8, 25),
                    new Track(Effect.Performance, "module_extractor", "item_module_extractor_name", 8, 25),
                    new Track(Effect.Output, Desequencer, "item_desequencer_name", 3),
                }
            },
            {
                // No power and no stats: only the Output grid can grow.
                ChemFinisher, new[]
                {
                    new Track(Effect.Storage, Ingot, IngotName, 8),
                }
            },
        };

        // Body storage indexes and base grid sizes from the game's machine factories.
        // Each entry: inventory index, width, base height.
        private static readonly Dictionary<string, int[][]> Grids = new Dictionary<string, int[][]>
        {
            { Furnace, new[] { new[] { 2, 9, 6 }, new[] { 3, 9, 6 } } },
            { Printer, new[] { new[] { 2, 9, 6 }, new[] { 4, 9, 5 } } },
            { Hydroponic, new[] { new[] { 4, 9, 6 } } },
            { ChemFinisher, new[] { new[] { 2, 6, 4 } } },
        };

        internal static bool IsUpgradeable(GameItem item) => item?.identifier != null && Machines.ContainsKey(item.identifier);

        // Machines with an energy draw. The Chem Finisher has none.
        internal static bool HasPower(GameItem machine) => machine != null && machine.IsTag("machinery");

        internal static Track FindTrack(GameItem machine, Effect effect)
        {
            if (!IsUpgradeable(machine)) return null;
            foreach (Track t in Machines[machine.identifier]) if (t.Effect == effect) return t;
            return null;
        }

        internal static Track FindTrack(GameItem material, GameItem machine)
        {
            if (material?.identifier == null || !IsUpgradeable(machine)) return null;
            Track first = null;
            foreach (Track t in Machines[machine.identifier])
                if (t.Material == material.identifier)
                {
                    if (first == null) first = t;
                    if (Level(machine, t) < t.Max) return t;
                }
            return first;
        }

        internal static int Level(GameItem machine, Track track)
        {
            if (machine == null || track == null) return 0;
            return Math.Max(0, Storage.Read(machine, track.Tag));
        }

        internal static int Level(GameItem machine, Effect effect) => Level(machine, FindTrack(machine, effect));

        // Agri Expansion's Integrated Farm sets its plots' stats itself, so upgrades
        // would do nothing there. Matched by identifier: no dependency on Agri.
        internal static bool InIntegratedFarm(GameItem machine)
        {
            GameItem current = machine;
            for (int depth = 0; depth < 8 && current != null; depth++)
            {
                GameItem parent = current.parentInventory?.GetParentItem();
                if (parent == null) return false;
                if (parent.identifier == IntegratedFarm) return true;
                current = parent;
            }
            return false;
        }

        // A machine used as material must be empty (an instruction note is allowed)
        // and have no vanilla Module Bay Expansion Kit, which would be lost with it.
        // Its own upgrades from this mod may be lost; its value moves to the target.
        private static bool IsSpareMachine(GameItem machine)
        {
            if (machine.GetTagReadonly("MODULE_UPGRADE_STAGE_INT").GetInt() > 0) return false;
            foreach (GameItem child in GraphUtils.FindAllChildrenType<GameItem>(machine))
            {
                if (child != null && child != machine && child.identifier != "postit") return false;
            }
            return true;
        }

        internal static bool Pair(GameItem source, GameItem target) => source != null && target != null && source != target && FindTrack(source, target) != null;

        internal static bool CanApply(GameItem source, GameItem target)
        {
            if (source == null || target == null || source == target) return false;
            Track track = FindTrack(source, target);
            if (track == null || Level(target, track) >= track.Max) return false;
            if (GameMaster.current != null && GameMaster.current.InCombat()) return false;
            if (!Owns(source, target)) return false;
            if (InIntegratedFarm(target)) return false;
            if (track.UsesDonorMachine && !IsSpareMachine(source)) return false;
            return true;
        }

        internal static bool CanApplySafely(GameItem source, GameItem target)
        {
            try { return CanApply(source, target); }
            catch (Exception error) { Storage.Fault("machine targeting", error); return false; }
        }

        internal static void Apply(GameItem source, GameItem target)
        {
            Track track = FindTrack(source, target);
            if (track == null) return;
            int level = Level(target, track) + 1;
            long value = target.unitValue;
            bool stored = false;
            try
            {
                Storage.Write(target, track.Tag, level);
                stored = true;
                target.unitValue += source.unitValue;
                if (track.Effect == Effect.Storage)
                {
                    ApplyStorage(target);
                    if (HasPower(target)) ApplyBasePower(target);
                }
                Update(target);
            }
            catch (Exception error)
            {
                if (track.Effect == Effect.Storage && stored)
                    // A grid may already have grown. Keep its saved level and pay
                    // for it; never shrink a live inventory after validation fails.
                    Storage.Fault("machine storage refresh", error);
                else
                {
                    Storage.Write(target, track.Tag, level - 1);
                    target.unitValue = value;
                    throw;
                }
            }

            Material.UseOne(source);
            Storage.Guard("machine upgrade sound", Sound);
        }

        internal static bool ApplyStep(GameItem source, GameItem target)
            => Material.Paying(source) ? ApplyPaidStep(source, target) : Material.Step(source, target, CanApply, ApplyPaidStep);

        private static bool ApplyPaidStep(GameItem source, GameItem target)
        {
            try { if (!CanApply(source, target)) return false; Apply(source, target); return true; }
            catch (Exception error) { Storage.Fault("machine upgrade", error); return false; }
        }

        private static void PlaySound()
        {
            // Unity objects: use != null, not ?. (destroyed objects compare equal to null).
            EmporiumAudioPlayer player = EmporiumAudioPlayer.Instance;
            if (player != null && player.ratchet != null) player.ratchet.Play();
        }

        internal static void ApplyStorage(GameItem machine)
        {
            if (!IsUpgradeable(machine) || !Grids.ContainsKey(machine.identifier)) return;
            int level = Level(machine, Effect.Storage);
            if (level <= 0) return;
            foreach (int[] grid in Grids[machine.identifier])
            {
                if (MachineHelper.GetBodyStorage(machine, grid[0]) is GameGridInventory inventory)
                {
                    inventory.SetShape(grid[1], Math.Min(MaxRows, grid[2] + level));
                    inventory.Validate();
                }
            }
            machine.contentWindow?.Validate();
            machine.Validate();
        }

        // Like the Vending Machine: each Storage step raises the nightly energy use by 1.
        private static void ApplyBasePower(GameItem machine)
        {
            if (!machine.IsTag(BasePowerTag))
            {
                int original = machine.GetTagReadonly("MACHINERY_DRAWN_POWER_BASE").GetInt();
                machine.ModifyTag(BasePowerTag, t => t.Enable().SetInt(original));
            }
            int basePower = machine.GetTagReadonly(BasePowerTag).GetInt() + Level(machine, Effect.Storage);
            machine.ModifyTag("MACHINERY_DRAWN_POWER_BASE", t => t.Enable().SetInt(basePower));
        }

        // Same sequence the machines run when a module is added or removed. Only for
        // machines with power; the module bay is at body storage index 1 on all of them.
        internal static void RecomputeStats(GameItem machine)
        {
            if (!HasPower(machine)) return;
            if (MachineHelper.GetBodyStorage(machine, 1) is GameGridInventory modules)
            {
                ModuleHelper.ComputeModuleEffect(modules.childItems, machine);
                ModuleHelper.ApplyBasicModuleEffect(modules, null, machine);
            }
            MachineryHelper.UpdatePowerUsageProcessTypeMachine(machine);
        }

        // Called right after the game has summed the module bonuses.
        internal static void AddBonus(GameItem machine)
        {
            if (!IsUpgradeable(machine) || InIntegratedFarm(machine)) return;
            foreach (Track t in Machines[machine.identifier])
            {
                int bonus = Level(machine, t) * t.PerStep;
                if (bonus <= 0) continue;
                string tag = t.Effect switch
                {
                    Effect.Efficiency => "TOTAL_PERCENTAGE_POWER_BONUS_INT",
                    Effect.Quality => "TOTAL_PERCENTAGE_QUALITY_BONUS_INT",
                    Effect.Performance => "TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT",
                    _ => null,
                };
                if (tag != null) machine.ModifyTag(tag, s => s.Enable().SetInt(s.GetInt() + bonus));
            }
        }

        // The game lets efficiency push the energy draw to 0 or below, and a negative
        // draw charges the battery. Keep at least 1 on the machines this mod upgrades.
        // Low Power Mode sets 0 through another path and is left alone.
        internal static void ClampPower(GameItem machine)
        {
            if (!IsUpgradeable(machine) || !HasPower(machine)) return;
            if (machine.GetTagReadonly("machinery_drawn_power").GetInt() < 1)
                machine.ModifyTag("machinery_drawn_power", t => t.Enable().SetInt(1));
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (!IsUpgradeable(item)) return;
            builder.AddLine(" ");
            builder.AddLine(LocHelper.GetLocalizedMechanic("mech_fridge_tooltip_upgrade_header"), true, RenderHandler.ColorPalette.LightCyan);
            foreach (Track t in Machines[item.identifier])
            {
                int level = Level(item, t);
                string detail = t.Effect switch
                {
                    Effect.Storage => " | +" + level + " rows",
                    Effect.Batches => " | " + (1 + level) + " per night",
                    Effect.Output => " | x" + (1 + level),
                    _ => " | +" + level * t.PerStep + "%",
                };
                builder.AddLine(Label(t.Effect) + " (" + LocHelper.GetLocalizedItem(t.MaterialName) + "): " + level + "/" + t.Max + detail, true, RenderHandler.ColorPalette.LightCyan);
            }
            if (FindTrack(item, Effect.Storage) != null)
                builder.AddLine(HasPower(item)
                    ? "Each Metal Ingot adds 1 row (max 12) and raises the nightly energy use by 1."
                    : "Each Metal Ingot adds 1 row (max 12).", true, RenderHandler.ColorPalette.LightCyan);
            if (FindTrack(item, Effect.Batches) != null || FindTrack(item, Effect.Output) != null)
                builder.AddLine("The machine used as upgrade must be empty and have no Module Bay Expansion Kit.", true, RenderHandler.ColorPalette.LightCyan);
            if (item.identifier == Hydroponic)
                builder.AddLine("Performance and Quality count half on this machine. Upgrades have no effect inside an Integrated Farm.", true, RenderHandler.ColorPalette.LightCyan);
        }

        private static string Label(Effect effect) => effect switch
        {
            Effect.Storage => "Storage",
            Effect.Efficiency => "Efficiency",
            Effect.Quality => "Quality",
            Effect.Performance => "Performance",
            Effect.Batches => "Batches",
            Effect.Output => "Output",
            _ => effect.ToString(),
        };

        // Furnace runs one batch per cycle (night or Turbo Booster). Run the vanilla
        // cycle once per batch and stop as soon as a batch changes nothing (out of
        // input, power or output space). Overflow is handled by the game itself.
        internal static void RunBatches(GameItem furnace, Action<GameItem, GameInventory, SlotMarker> original,
            GameItem item, GameInventory parentInventory, SlotMarker slot)
        {
            if (original == null) return;
            if (!MachineUpgradesMod.Running) { original(item, parentInventory, slot); return; }
            int batches;
            try { batches = 1 + Level(furnace, Effect.Batches); }
            catch (Exception error)
            {
                Storage.Fault("reading furnace level", error);
                original(item, parentInventory, slot);
                return;
            }
            for (int i = 0; i < batches; i++)
            {
                (int, long) before;
                try { before = InputState(furnace); }
                catch (Exception error)
                {
                    Storage.Fault("reading furnace input", error);
                    original(item, parentInventory, slot);
                    return;
                }
                // The original's exceptions must propagate unchanged.
                original(item, parentInventory, slot);
                try { if (InputState(furnace) == before) break; }
                catch (Exception error) { Storage.Fault("reading furnace input", error); return; }
            }
        }

        // Item count plus summed unit counts: a batch can remove whole items
        // (vanilla) or only lower a stack's unitCount (Agri's Ironfruit smelting).
        private static (int, long) InputState(GameItem furnace)
        {
            if (!(MachineHelper.GetBodyStorage(furnace, 2) is GameInventory input)) return (0, 0);
            long units = 0;
            foreach (GameItem child in GraphUtils.FindAllChildrenType<GameItem>(input)) units += Math.Max(0, child.unitCount);
            return (input.childItems.Count, units);
        }

        // Desequencer: one extra copy of the finished card per Output step. The first
        // card fills the output slot as usual; extras go through the game's overflow
        // (connected hopper, back room, main storage, showcase, counter).
        internal static void AddExtraOutput(GameItem desequencer, GameSlotInventory outputSlot)
        {
            int extra = Level(desequencer, Effect.Output);
            if (extra <= 0 || outputSlot == null) return;
            string id = desequencer.GetTagReadonly("DESEQUENCER_TARGET_ID_TAG").GetString();
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < extra; i++) GraphUtils.EmporiumTryAdd(outputSlot, DirectoryMaster.Item(id));
        }

        // Called from each machine's factory postfix. The game assigns the item's
        // identifier only after the factory returns, so nothing here may test it.
        internal static void Attach(GameItem machine)
        {
            if (machine == null) return;
            machine.onLoaded = (Action<GameItem>)Delegate.Combine(machine.onLoaded,
                (Action<GameItem>)(item =>
                {
                    if (MachineUpgradesMod.Running) Storage.Guard("restoring machine storage", () => ApplyStorage(item));
                }));
        }

        internal static void AttachBatches(GameItem furnace)
        {
            if (furnace == null) return;
            Action<GameItem, GameInventory, SlotMarker> original = furnace.onCycleEndSlotItemFunc;
            furnace.onCycleEndSlotItemFunc = (item, inv, slot) => RunBatches(item ?? furnace, original, item, inv, slot);
        }
    }

    public class MachineUpgradesMod : IMod
    {
        internal const string Version = "3.1.2";
        internal static bool Running;
        public void Init(ModManifest manifest)
        {
            Storage.Prefix = "[Machine Upgrades]";
            Storage.Guard("machine version", () => Storage.Log?.Invoke(Storage.Prefix + " v" + Version));
            Storage.Guard("machine settings", () => Upgrades.Configure(manifest.ID));
            LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Machine Upgrades", Storage.Fault);
            ModHook.OnCreateTooltipLate += AddTooltip;
        }

        public void OnEnable() { Running = true; }

        public void OnDisable()
        {
            Running = false;
            ModHook.OnCreateTooltipLate -= AddTooltip;
        }

        private static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            Storage.Guard("machine tooltip", () => Upgrades.AddTooltip(builder, item));
        }
    }

    // New and loaded Furnaces: hook load-time storage restore and the batch loop.
    // Priority.Last makes our wrapper the outermost one, so cycle wrappers from
    // other mods (Agri Expansion's Ironfruit smelting) run once per batch too.
    [HarmonyPatch(typeof(MachineFurnace), nameof(MachineFurnace.Furnace))]
    [HarmonyPriority(Priority.Last)]
    internal static class FurnaceCreatePatch
    {
        private static void Postfix(GameItem __result)
        {
            Storage.Guard("preparing furnace", () =>
            {
                Upgrades.Attach(__result);
                Upgrades.AttachBatches(__result);
            });
        }
    }

    [HarmonyPatch(typeof(MachinePrinter), nameof(MachinePrinter.Printer))]
    internal static class PrinterCreatePatch
    {
        private static void Postfix(GameItem __result) => Storage.Guard("preparing printer", () => Upgrades.Attach(__result));
    }

    // Also covers the Old Hydroponic System, which is built from the same machine.
    [HarmonyPatch(typeof(MachineHydroponic), nameof(MachineHydroponic.Hydroponic))]
    internal static class HydroponicCreatePatch
    {
        private static void Postfix(GameItem __result) => Storage.Guard("preparing hydroponic system", () => Upgrades.Attach(__result));
    }

    [HarmonyPatch(typeof(MachineChemFinisher), nameof(MachineChemFinisher.ChemFinisher))]
    internal static class ChemFinisherCreatePatch
    {
        private static void Postfix(GameItem __result) => Storage.Guard("preparing chem finisher", () => Upgrades.Attach(__result));
    }

    [HarmonyPatch(typeof(MachineDesequencer), nameof(MachineDesequencer.FinishDesequencer))]
    internal static class DesequencerOutputPatch
    {
        private static void Postfix(GameItem item, GameSlotInventory outputSlot) => Storage.Guard("desequencer output", () => Upgrades.AddExtraOutput(item, outputSlot));
    }

    // Upgrade bonuses ride on top of whatever the modules give.
    [HarmonyPatch(typeof(ModuleHelper), nameof(ModuleHelper.ComputeModuleEffect), new[] { typeof(List<GameItem>), typeof(GameItem) })]
    internal static class ModuleBonusPatch
    {
        private static void Postfix(GameItem system) => Storage.Guard("machine bonus", () => Upgrades.AddBonus(system));
    }

    // ApplyModifiedPower has no lower bound (unlike ApplyBasicModuleEffect).
    [HarmonyPatch(typeof(ModuleHelper), nameof(ModuleHelper.ApplyModifiedPower))]
    internal static class PowerClampPatch
    {
        private static void Postfix(GameItem item) => Storage.Guard("machine power floor", () => Upgrades.ClampPower(item));
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayTarget))]
    internal static class MayTargetPatch
    {
        internal static bool Suppress;
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (Suppress) return;
            if (!__result && Upgrades.CanApplySafely(__instance, targetItem)) __result = true;
        }
    }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.CanTarget))]
    internal static class CanTargetPatch
    {
        private static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (MayTargetPatch.Suppress) return;
            if (!__result && Upgrades.CanApplySafely(__instance, targetItem)) __result = true;
        }
    }
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.Target))]
    internal static class TargetPatch
    {
        internal static Func<GameItem, GameItem, bool> OtherAllows = (source, target) =>
        {
            bool before = MayTargetPatch.Suppress;
            MayTargetPatch.Suppress = true;
            try { return source.CanTarget(target); }
            finally { MayTargetPatch.Suppress = before; }
        };

        private static bool Prefix(GameItem __instance, GameItem targetItem)
        {
            if (!Upgrades.CanApplySafely(__instance, targetItem)) return true;

            // Vanilla (or another mod) handles this pair already: leave it alone.
            bool handledElsewhere = OtherAllows(__instance, targetItem);
            if (handledElsewhere) return true;

            try { return !Material.Held(__instance, () => Material.Dropped(__instance, targetItem, Upgrades.ApplyStep, () => false, false, Upgrades.StackSteps(), Upgrades.CanApplySafely)); }
            catch (Exception error) { Storage.Fault("machine upgrade", error); return true; }
        }

        // The frame is opened before any other prefix and closed after every postfix.
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Enter(out Material.Frame __state) => __state = Material.Enter();

        private static void Finalizer(Material.Frame __state) => Material.Leave(__state);
    }
}

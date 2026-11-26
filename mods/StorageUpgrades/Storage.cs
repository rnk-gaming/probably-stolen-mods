using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LocalMods
{
    // Values only: no game objects, settings or Unity access.
    internal static partial class StorageRules
    {
        internal static readonly Dictionary<string, int[]> Bays = new Dictionary<string, int[]>
        {
            {"smuggler_bay_mini", new[]{3,3,6,6}}, {"smuggler_bay", new[]{5,5,10,10}},
            {"smuggler_bay_mod", new[]{8,8,16,16}}, {"machine_bay", new[]{6,8,12,14}},
            {"machine_bay_ext", new[]{11,14,20,20}}, {"storage_bay", new[]{10,10,16,16}},
            {"storage_bay_large", new[]{17,10,20,20}}, {"storage_bay_gun", new[]{17,10,20,16}},
            {"storage_bay_chem", new[]{17,10,20,16}}
        };
        internal static int[] BaySize(int bw, int bh, int columns, int rows, int currentWidth, int currentHeight)
            => new[] { Math.Max(currentWidth, Add(bw, columns)), Math.Max(currentHeight, Add(bh, rows)) };

        private static int Add(int basis, int extra) => (int)Math.Min(int.MaxValue, (long)basis + Math.Max(0, extra));

        internal static int[] NextBay(int bw, int bh, int mw, int mh, int w, int h)
        {
            w = Math.Max(bw, w); h = Math.Max(bh, h);
            if (w >= mw && h >= mh) return null;
            bool column = ((long)w - bw + h - bh) % 2 == 0;
            if (w >= mw) column = false;
            else if (h >= mh) column = true;
            return new[] { w + (column ? 1 : 0), h + (column ? 0 : 1) };
        }

        // The agreed vanilla sequence is explicit: x3 and x6 round down by one
        // square. Other bases use the smallest sufficient area, squarest on a tie.
        private static readonly int[][] Crates = {new[]{7,5},new[]{10,7},new[]{13,8},new[]{14,10},new[]{16,11},new[]{19,11}};
        // A Beer Case grows one case to the right up to three wide. The second row needs
        // three more cases and a grid is a rectangle, so the fourth and fifth case are
        // kept as progress and the row appears with the sixth.
        private static readonly int[][] Beer = {new[]{8,4},new[]{16,4},new[]{24,4},new[]{24,4},new[]{24,4},new[]{24,8}};
        internal static int[] CrateSize(int bw, int bh, int multiplier)
        {
            if (bw <= 0 || bh <= 0 || multiplier < 1 || multiplier > 6) return null;
            if (bw == 7 && bh == 5) return (int[])Crates[multiplier - 1].Clone();
            int[] fit = Fit(bw, bh, multiplier);
            if (fit == null) return null;
            // The last multiplier a base can reach (the next one has no grid: an 8x8
            // base at x4 would need 256 squares of 210) gives the largest grid, the
            // one the game's own crates end on. This applies only when
            // the base and every size on the way lie within that grid in both
            // directions: a joined crate keeps the width and height it has, so a
            // crate that was ever wider (a 10x10 base is 20x10 at x2, a 7x7 base
            // 21x7 at x3) would end larger than any grid may be.
            int[] top = Crates[Crates.Length - 1];
            if (multiplier < 6 && Fit(bw, bh, multiplier + 1) != null) return fit;
            if (bw > top[0] || bh > top[1]) return fit;
            for (int m = 1; m <= multiplier; m++)
            {
                int[] earlier = m == multiplier ? fit : Fit(bw, bh, m);
                if (earlier == null || earlier[0] > top[0] || earlier[1] > top[1]) return fit;
            }
            return (int[])top.Clone();
        }

        // The smallest grid of at least base area times the multiplier, or null.
        private static int[] Fit(int bw, int bh, int multiplier)
        {
            if (bw > 210 || bh > 210) return null;
            long area = (long)bw * bh * multiplier;
            int[] best = null;
            for (int w = 3; w <= 21; w++) for (int h = 1; h <= 11 && h <= w - 2; h++)
            {
                int squares = w * h;
                if (squares < area || squares > 210) continue;
                if (best == null || squares < best[0] * best[1]
                    || (squares == best[0] * best[1] && w - h < best[0] - best[1])) best = new[] { w, h };
            }
            return best;
        }

        // The largest multiplier a crate of this base size can reach: the setting,
        // or less when a grid of that many squares does not exist (210 at most).
        internal static int MostCrates(int bw, int bh, int limit)
        {
            int most = 0;
            for (int m = 1; m <= Clamp(limit, 1, 6); m++)
                if (CrateSize(bw, bh, m) != null) most = m;
            return most;
        }

        internal static int[] BeerSize(int multiplier) => multiplier < 1 || multiplier > 6 ? null : (int[])Beer[multiplier - 1].Clone();
        internal static int Sum(int target, int donor, int limit)
        {
            long sum = (long)target + donor;
            return target < 1 || donor < 1 || sum > Clamp(limit, 1, 6) ? 0 : (int)sum;
        }
    }
    internal static partial class Storage
    {
        internal const string CratesTag = "MACHINE_UPGRADES_CRATES_INT";
        private const string BaseColumnsTag = "MACHINE_UPGRADES_CRATE_BASE_COLUMNS_INT", BaseRowsTag = "MACHINE_UPGRADES_CRATE_BASE_ROWS_INT";
        private static readonly Dictionary<string, ConfigEntry<bool>> switches = new Dictionary<string, ConfigEntry<bool>>();
        private static readonly Dictionary<string, ConfigEntry<int>> settings = new Dictionary<string, ConfigEntry<int>>();

        internal static Func<string, bool> Switch = key => switches.TryGetValue(key, out ConfigEntry<bool> entry) ? entry.Value : key != "smuggler_takes_machine_bays" && key != "storage_bays_take_machine_bays";
        internal static Func<string, string> ItemName = key => LocHelper.GetLocalizedItem(key);
        internal static string BayLabel(string id, bool columns)
        {
            string name = null, key = "item_" + id + "_name";
            try { name = ItemName(key); }
            catch (Exception error) { Fault("bay option name", error); }
            if (string.IsNullOrWhiteSpace(name) || name == key)
            {
                name = (id ?? "Storage").Replace('_', ' ');
                if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            }
            return name + ": maximum " + (columns ? "columns" : "rows");
        }
        internal static Func<string, int, int> Setting = (key, fallback) => settings.TryGetValue(key, out ConfigEntry<int> entry) ? entry.Value : fallback;
        // Ingots retain vanilla upgrade perks. A joined container is always used up.
        internal static Action<GameItem> Consume = item =>
        {
            if (item.identifier == "metal_ingot") Material.UseOne(item);
            else Material.UseUnit(item);
        };

        internal static void Configure(string id)
        {
            ModConfig config = ModConfig.For(id);
            Bool(config, "stack_steps", true, "A dropped stack upgrades as far as it reaches", "Use a dropped stack for every upgrade it reaches. Off: one drop takes one step. Changes take effect now.");
            Bool(config, "storage_enabled", true, "Storage upgrades", "Allow new storage upgrades. Saved sizes are restored even when this is off.");
            Bool(config, "machine_storage_enabled", true, "Machine storage upgrades", "Allow Metal Ingots to enlarge machine storage after the machine's own upgrades. Saved sizes are always restored.");
            Bool(config, "crates_enabled", true, "Drop an empty crate on another of the same kind to enlarge it", "Join unlocked crates of the same kind using an empty owned donor.");
            Bool(config, "beer_case_enabled", true, "Beer Case upgrades", "Join Beer Cases using an empty owned donor.");
            Bool(config, "smuggler_takes_machine_bays", false, "Smuggler's Bays take Machine Bays", "A Machine Bay, with what is in it, can be put into a Smuggler's Bay. The game does not allow this. Changes take effect now.");
            Bool(config, "storage_bays_take_machine_bays", false, "Storage Bays take Machine Bays", "A Machine Bay, with what is in it, can be put into a Makeshift Storage Bay, a Storage Bay or a Storage Bay (Extended). The game does not allow this. Changes take effect now.");
            settings["crate_limit"] = config.Define("crate_limit", 6, "Storage", () => "How many crates one crate can grow to", 1, 6,
                () => "Maximum combined multiplier. Lowering this does not shrink existing containers.");
            foreach (KeyValuePair<string, int[]> bay in StorageRules.Bays)
            {
                string key = bay.Key; int[] s = bay.Value;
                settings[key + "_columns"] = config.Define(key + "_columns", s[2], "Storage", () => BayLabel(key, true), s[0], s[2],
                    () => "Largest width for new steps; existing capacity is kept.");
                settings[key + "_rows"] = config.Define(key + "_rows", s[3], "Storage", () => BayLabel(key, false), s[1], s[3],
                    () => "Largest height for new steps; existing capacity is kept.");
            }
        }

        private static void Bool(ModConfig config, string key, bool value, string title, string description)
            => switches[key] = config.Define(key, value, "Storage", () => title, () => description);

        internal static bool IsCrate(string id) => id == "eng_box" || id == "med_box" || id == "sec_box" || id == "service_box" || id == "evidence_box";
        internal static bool Supported(string id) => id != null && (StorageRules.Bays.ContainsKey(id) || IsCrate(id) || id == "beerCase");
        internal static bool Pair(GameItem source, GameItem target) => source != null && target != null && source != target && Supported(target.identifier)
            && (StorageRules.Bays.ContainsKey(target.identifier) ? source.identifier == "metal_ingot" : source.identifier == target.identifier);

        private static bool On(string id) => Switch("storage_enabled")
            && (StorageRules.Bays.ContainsKey(id) || Switch(id == "beerCase" ? "beer_case_enabled" : "crates_enabled"));

        private static int[] Current(GameItem item)
        {
            int[][] pages = Sizes(item);
            if (pages == null || pages.Length == 0) return null;
            int w = 0, h = 0;
            foreach (int[] page in pages)
            {
                if (page == null || page.Length != 2 || page[0] <= 0 || page[1] <= 0) throw new InvalidOperationException("Invalid storage shape");
                w = Math.Max(w, page[0]); h = Math.Max(h, page[1]);
            }
            return new[] { w, h };
        }

        private static int Multiplier(GameItem item)
        {
            int extra = Read(item, CratesTag);
            return extra < 0 || extra > 5 ? 0 : extra + 1;
        }

        private static int[] Basis(GameItem item, int[] current)
        {
            if (item.identifier == "beerCase") return new[] { 8, 4 };
            int w = Read(item, BaseColumnsTag), h = Read(item, BaseRowsTag);
            if (w > 0 && h > 0) return new[] { w, h };
            // Remember the grid supplied by the factory/Workshop on the first
            // upgrade. Future loads must not treat an expanded grid as the base.
            return Multiplier(item) == 1 ? (int[])current.Clone() : new[] { 7, 5 };
        }

        private static int[] BaySize(GameItem item, int[] basis, int[] current)
            => StorageRules.BaySize(basis[0], basis[1], Read(item, ColumnsTag), Read(item, RowsTag), current[0], current[1]);

        private static int[] NextBay(GameItem item, int[] basis, int[] current) => StorageRules.NextBay(basis[0], basis[1],
            StorageRules.Clamp(Setting(item.identifier + "_columns", basis[2]), basis[0], basis[2]),
            StorageRules.Clamp(Setting(item.identifier + "_rows", basis[3]), basis[1], basis[3]), current[0], current[1]);

        private sealed class Plan
        {
            internal int[] Size, Base;
            internal int Multiplier;
            internal bool Bay;
        }

        private static Plan Prepare(GameItem source, GameItem target)
        {
            if (!Pair(source, target) || !On(target.identifier) || InCombat() || !Owned(source) || !Owned(target)) return null;
            int[] current = Current(target);
            if (current == null) return null;
            if (StorageRules.Bays.TryGetValue(target.identifier, out int[] bay))
            {
                int[] next = NextBay(target, bay, BaySize(target, bay, current));
                return next == null ? null : new Plan { Size = next, Base = bay, Bay = true };
            }
            if ((source.unitCount > 1 && !Switch("stack_steps")) || Locked(source) || Locked(target) || !Empty(source)) return null;
            int sum = StorageRules.Sum(Multiplier(target), Multiplier(source), Setting("crate_limit", 6));
            if (sum == 0) return null;
            int[] basis = Basis(target, current);
            int[] size = target.identifier == "beerCase" ? StorageRules.BeerSize(sum) : StorageRules.CrateSize(basis[0], basis[1], sum);
            if (size == null) return null;
            size[0] = Math.Max(size[0], current[0]); size[1] = Math.Max(size[1], current[1]);
            return new Plan { Size = size, Base = basis, Multiplier = sum };
        }

        internal static bool CanApply(GameItem source, GameItem target)
        {
            try { return Prepare(source, target) != null; }
            catch (Exception error) { Fault("storage targeting", error); return false; }
        }

        internal static bool Verdict(GameItem source, GameItem target, bool original)
        {
            // Only ever adds a yes. What the game or another mod answered stays, so a
            // drop that means something else (storing a crate in a crate) is not blocked.
            try { return original || (On(target.identifier) && Prepare(source, target) != null); }
            catch (Exception error) { Fault("storage targeting", error); return original; }
        }

        internal static bool Apply(GameItem source, GameItem target)
        {
            try
            {
                Plan plan = Prepare(source, target);
                if (plan == null) return false;
                // Tags first, then the grid, then the donor. If the grid cannot be
                // resized the tags are put back and nothing was used. Once the grid
                // has grown the upgrade has happened: a donor that cannot be removed
                // is reported and the drop still counts, so that it cannot be tried
                // again with the same donor.
                string[] keys = plan.Bay ? new[] { ColumnsTag, RowsTag } : new[] { BaseColumnsTag, BaseRowsTag, CratesTag, ColumnsTag, RowsTag };
                int[] values = plan.Bay
                    ? new[] { plan.Size[0] - plan.Base[0], plan.Size[1] - plan.Base[1] }
                    : new[] { plan.Base[0], plan.Base[1], plan.Multiplier - 1, plan.Size[0] - plan.Base[0], plan.Size[1] - plan.Base[1] };
                int[] before = new int[keys.Length];
                for (int i = 0; i < keys.Length; i++) before[i] = Read(target, keys[i]);
                int[][] gridsBefore = Array.ConvertAll(Sizes(target), page => (int[])page.Clone());
                try
                {
                    for (int i = 0; i < keys.Length; i++) Write(target, keys[i], values[i]);
                    Resize(target, plan.Size[0], plan.Size[1]);
                }
                catch (Exception error)
                {
                    bool grew = false;
                    try { grew = Material.Grew(gridsBefore, Sizes(target)); }
                    catch (Exception checkError) { Fault("checking grown storage", checkError); }
                    if (grew) Fault("validating grown storage", error);
                    else
                    {
                        for (int i = 0; i < keys.Length; i++)
                        {
                            try { Write(target, keys[i], before[i]); }
                            catch (Exception rollbackError) { Fault("putting storage tags back", rollbackError); }
                        }
                        throw;
                    }
                }
                try { Consume(source); }
                catch (Exception error) { Fault("using up the upgrade material", error); }
                return true;
            }
            catch (Exception error) { Fault("storage upgrade", error); return false; }
        }

        internal static void Restore(GameItem item)
        {
            try { RestoreChecked(item); }
            catch (Exception error) { Fault("restoring storage", error); }
        }

        // The trade needs the throwing form so it can restore its new, empty reward
        // and its tags if sizing fails. Save loading keeps the guarded entry above.
        internal static void RestoreChecked(GameItem item)
        {
            if (item == null) return;
            // Makeshift Storage Bays take the allowance without becoming upgradeable.
            StorageUpgrades.MachineBayAllowance.Prepare(item, item.identifier);
            if (!Supported(item.identifier)) return;
            int[] current = Current(item);
            if (current == null) return;
            int[] size;
            if (StorageRules.Bays.TryGetValue(item.identifier, out int[] bay))
            {
                // A bay that was never enlarged is left exactly as the game built it.
                if (Read(item, ColumnsTag) <= 0 && Read(item, RowsTag) <= 0) return;
                size = BaySize(item, bay, current);
            }
            else
            {
                int multiplier = Multiplier(item);
                if (multiplier <= 1) return;
                int[] basis = Basis(item, current);
                size = item.identifier == "beerCase" ? StorageRules.BeerSize(multiplier) : StorageRules.CrateSize(basis[0], basis[1], multiplier);
                // A multiplier outside the current table keeps its saved dimensions.
                if (size == null) size = new[] { basis[0], basis[1] };
                int[] achieved = StorageRules.BaySize(basis[0], basis[1], Read(item, ColumnsTag), Read(item, RowsTag), 0, 0);
                size[0] = Math.Max(current[0], Math.Max(achieved[0], size[0]));
                size[1] = Math.Max(current[1], Math.Max(achieved[1], size[1]));
            }
            // A tag that says something absurd must not be able to hang the game.
            size[0] = Math.Max(current[0], Math.Min(size[0], StorageRules.Sane));
            size[1] = Math.Max(current[1], Math.Min(size[1], StorageRules.Sane));
            Resize(item, size[0], size[1]);
        }

        internal static void Attach(GameItem item, string identifier)
        {
            try
            {
                if (item != null && (Supported(identifier) || StorageUpgrades.MachineBayAllowance.SettingFor(identifier) != null))
                {
                    StorageUpgrades.MachineBayAllowance.Prepare(item, identifier);
                    item.onLoaded = (Action<GameItem>)Delegate.Combine(item.onLoaded, (Action<GameItem>)Restore);
                }
            }
            catch (Exception error) { Fault("preparing storage", error); }
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (item == null || !Supported(item.identifier)) return;
                int[] current = Current(item);
                if (current == null) return;
                string detail;
                if (StorageRules.Bays.TryGetValue(item.identifier, out int[] bay))
                {
                    current = BaySize(item, bay, current);
                    int[] next = NextBay(item, bay, current);
                    detail = next == null ? "limit reached" : "next: Metal Ingot -> " + next[0] + "x" + next[1];
                }
                else
                {
                    int multiplier = Multiplier(item);
                    int sum = StorageRules.Sum(multiplier, 1, Setting("crate_limit", 6));
                    int[] basis = Basis(item, current);
                    int[] next = item.identifier == "beerCase" ? StorageRules.BeerSize(sum) : StorageRules.CrateSize(basis[0], basis[1], sum);
                    int limit = StorageRules.Clamp(Setting("crate_limit", 6), 1, 6);
                    // A crate with a large grid of its own reaches the largest grid before the setting.
                    int most = item.identifier == "beerCase" ? limit : Math.Max(multiplier, StorageRules.MostCrates(basis[0], basis[1], limit));
                    detail = "x" + multiplier + "/" + most + " | "
                        + (next == null ? (most < limit ? "largest grid reached" : "limit reached") : "next: empty, owned, unlocked " + (item.identifier == "beerCase" ? "Beer Case" : "crate of the same kind"));
                    if (item.identifier == "beerCase" && multiplier >= 3 && multiplier < 6) detail += " | second row at x6";
                }
                if (!On(item.identifier)) detail += " | new upgrades disabled";
                Line(builder, "Storage: " + current[0] + "x" + current[1] + " | " + detail);
            }
            catch (Exception error) { Fault("storage tooltip", error); }
        }
    }

}

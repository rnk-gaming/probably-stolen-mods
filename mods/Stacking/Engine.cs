// The stand-alone engine, without pile drawing or the Workshop shelf lookup.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LocalMods;
using UnityEngine;

namespace Stacking
{
    internal static class EngineRules
    {
        // ItemStacks' tested materials and medicine. Access cards are recognised by type.
        internal static readonly HashSet<string> Ids = new HashSet<string>
        {
            "metal_ingot", "scrap_metal", "common_ore", "rare_ore",
            "common_electronic", "rare_electronic", "hypertech_electronic",
            "wire", "printer_plastic", "gun_part", "nuts_metal", "junk",
            // A filter with durability. Durability is a tag and a price feature, so
            // only pieces worn alike merge. It wears only inside a filter holder,
            // where a slot holds one.
            "kotton_fabric",
            "common_chemical", "restricted_chemical", "common_medical", "restricted_medical",
            // Medicine and medical consumables. Pills, dusts and injectors carry their
            // composition and purity as tags, so only equal ones merge (see Difference).
            "bandage_item", "hemostatic_bandage_item", "topical_bandage_item", "tilapia_skin_graft",
            "birth_control_pill", "fanta_pill", "phagimycin_pill", "unlicensed_phagimycin_pill",
            "zyanide_pill", "caffeine_pill", "zerochew", "smelling_salt",
            "dream_dust", "nightmare_dust", "salve", "rubbing_alcohol",
            "blood_bag", "blue_blood_bag",
            "black_injector", "pink_injector", "pink_alt_injector", "large_purple_injector", "pure_white_injector",
            "surgery_tool",
        };

        // Items whose activation uses the item up at once, inside the activation itself
        // (read from the game's code). A stack of these can be activated and loses one
        // unit. Everything else keeps the rule that a stack cannot be activated.
        internal static readonly HashSet<string> UsedAtOnce = new HashSet<string>
        {
            "bandage_item", "hemostatic_bandage_item", "topical_bandage_item", "tilapia_skin_graft",
            "fanta_pill", "dream_dust", "pink_injector", "pink_alt_injector",
            "black_injector", "large_purple_injector",
        };

        // Tags that may differ between two items that merge. The stack keeps the value
        // of the item that was dropped on. Different purchase prices do not keep
        // otherwise equal goods apart.
        // AGRI_PERFUME_RECORDED is Agri Expansion's note of the vendor visit in which
        // it counted the item; it says nothing about the item itself.
        internal static readonly HashSet<string> IgnoredTags = new HashSet<string> { "BOUGHT_PRICE_TAG", "AGRI_PERFUME_RECORDED" };

        // Plain storage. Machines are deliberately absent: they count and destroy their
        // inputs one object at a time, so a stack inside one would be used up as one item.
        internal static readonly HashSet<string> StorageIds = new HashSet<string>
        {
            "makeshift_storage_bay", "storage_bay", "storage_bay_large", "large_storage_bay",
            // Smuggler's bays are plain grids too. The player can carry one while
            // scavenging, so finds are sorted into it.
            "smuggler_bay", "smuggler_bay_mod", "smuggler_bay_mini",
            // The specialised bays are the same plain grids with a rule for what may be
            // put in (gunsmith, chemist) or with more room (hypertech, machine bays).
            // A machine bay stores machines; it is not one.
            "storage_bay_gun", "storage_bay_chem", "hypertech_bay", "machine_bay", "machine_bay_ext",
        };

        // Replaced by the tests; in the game it writes to the log.
        internal static Action<string> Log = message => Debug.LogWarning(message);
        internal static Action<RichTextBuilder, string> Line = (builder, text) =>
            builder.AddLine(text, true, RenderHandler.ColorPalette.LightYellow);

        private static readonly HashSet<string> reported = new HashSet<string>();
        private static readonly HashSet<string> reportedUnsafe = new HashSet<string>();
        // The harness starts each log-count check independently of earlier checks.
        internal static void ResetReports()
        {
            reported.Clear();
            reportedUnsafe.Clear();
        }
        private static readonly ConditionalWeakTable<GameItem, object> attached = new ConditionalWeakTable<GameItem, object>();

        // One log line per distinct problem, so a fault in a hot path cannot flood the log.
        internal static void Report(string key, string message)
        {
            try
            {
                if (reported.Add(key)) Log("[Stacking] " + message);
            }
            catch
            {
            }
        }

        internal static void Fault(string where, Exception error)
        {
            string text;
            try { text = error.ToString(); } catch { text = "(an error that cannot be shown)"; }
            Report("fault:" + where, where + " failed; the game continues without this part of the mod. " + text);
        }

        internal static bool On => Runtime.Own && Bridge.Switch("enabled");

        // Refused even in extra_items. Kotton Fabric is an exception:
        // its wear is compared and its filter holder only takes a single unit.
        internal static readonly Dictionary<string, string> UnsafeIds = new Dictionary<string, string>
        {
            { "glass_shard", "changes into a shiv when used as a target" },
        };
        internal static string Refusal(GameItem item)
        {
            if (item == null || item.identifier == null) return "missing item";
            if (UnsafeIds.TryGetValue(item.identifier, out string reason)) return reason;
            if (item.contentWindow != null || item.IsTag("CONTAINER_TAG")) return "contents or container";
            if (item.identifier != "kotton_fabric" && (item.IsTag("DURABILITY_TAG") || item.state?.IsTag("DURABILITY_TAG") == true)) return "durability";
            return null;
        }
        internal static bool Listed(string list, string id)
        {
            if (string.IsNullOrEmpty(list) || id == null) return false;
            foreach (string part in list.Split(',')) if (part.Trim() == id) return true;
            return false;
        }
        internal static bool IsStackable(GameItem item)
        {
            if (item == null || item.identifier == null || Listed(Runtime.Items("excluded_items"), item.identifier)) return false;
            if (!Ids.Contains(item.identifier) && !Listed(Runtime.Items("extra_items"), item.identifier) && !IsCard(item)) return false;
            string reason = Refusal(item);
            if (reason == null) return true;
            if (reportedUnsafe.Add(item.identifier))
                Report("unsafe:" + item.identifier, item.identifier + " did not stack because of " + reason + ".");
            return false;
        }
        internal static bool IsCard(GameItem item) => item?.itemTypes != null && item.itemTypes.Contains("ACCESS_CARD");
        // Saved counts keep protection regardless of today's lists or stacking switch.
        internal static bool IsStack(GameItem item) => item != null && item.unitCount > 1
            && HasState(item);
        // A split can have reduced the source to one unit before our postfix runs.
        internal static bool HasState(GameItem item) => Refusal(item) == null;

        // The game asks both items; both must say yes.
        internal static bool MayStack(GameItem a, GameItem b)
        {
            try
            {
                if (!On || !IsStackable(a) || !IsStackable(b) || ReferenceEquals(a, b)) return false;
                if (a.identifier != b.identifier) return false;
                if (!GeneralHelper.IsItemOwned(a) || !GeneralHelper.IsItemOwned(b)) return false;
                // Its unpacking may still be waiting for an answer in a dialog.
                if (JustActivated(a) || JustActivated(b)) return false;
                // A handler may still be about to take units from one of them.
                if (Later.Pending(a) || Later.Pending(b)) return false;
                string difference = Difference(a, b);
                if (difference == null) return true;
                Report("differs:" + a.identifier + ":" + difference, a.identifier + " did not stack because the two items differ in " + difference + ".");
                return false;
            }
            catch (Exception error)
            {
                Fault("the stacking check", error);
                return false;
            }
        }

        // An activated item may be destroyed later, from a confirmation dialog. Until
        // then it must stay a single item, or the dialog would take a whole stack.
        private sealed class Stamp { internal long At; }
        private static readonly ConditionalWeakTable<GameItem, Stamp> activated = new ConditionalWeakTable<GameItem, Stamp>();
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        internal const long ActivationHoldMs = 120000;

        internal static void NoteActivation(GameItem item)
        {
            if (!HasState(item)) return;
            activated.GetOrCreateValue(item).At = clock.ElapsedMilliseconds;
        }

        internal static bool JustActivated(GameItem item)
        {
            return item != null && activated.TryGetValue(item, out Stamp stamp) && clock.ElapsedMilliseconds - stamp.At < ActivationHoldMs;
        }

        // A stack, or a stack that shows a count of 1 because one unit is being used.
        internal static bool BlocksActivation(GameItem item) => (IsStack(item) && !ActivatesAsOne(item)) || OneUnit.InUse(item);

        // A stack whose activation uses one unit on the spot.
        internal static bool ActivatesAsOne(GameItem item) => IsStack(item) && UsedAtOnce.Contains(item.identifier);

        // How many fit in one slot of this inventory. 1 is the game's own answer for
        // everything, so every doubt ends there.
        internal static int MaxFor(GameItem item, GameInventory inventory)
        {
            try
            {
                if (!On || !IsStackable(item) || !HoldsStacks(inventory)) return 1;
                int limit = Bridge.Setting(Rules.Category(item.itemTypes));
                if (!Bridge.IsBackpack(inventory)) return Rules.Limit(limit, false, 5);
                bool needs = Bridge.Switch("backpack_needs_upgrade");
                return Rules.BackpackLimit(limit, Bridge.Setting("backpack_max"),
                    needs ? Bridge.Upgrade(Bridge.Parent(inventory)) : 0, needs);
            }
            catch (Exception error)
            {
                Fault("the stack size check", error);
                return 1;
            }
        }

        // Only the game's own shop grids and owned storage. No Workshop shelf lookup.
        internal static bool HoldsStacks(GameInventory inventory)
        {
            if (!(inventory is GameGridInventory)) return false;
            if (Runtime.ShopGrid(inventory)) return true;
            GameItem owner = Bridge.Parent(inventory);
            if (owner == null || !Bridge.Owned(owner)) return false;
            if (Bridge.Backpack(owner) || Rules.ExtraPlace(owner.identifier) != null) return Bridge.Extra(inventory);
            return StorageIds.Contains(owner.identifier);
        }

        // Null when the two items are interchangeable, otherwise what sets them apart.
        // Anything that would be lost by keeping only one of them counts as a difference.
        // `modifiedState` is not compared: the game derives it from `state` and the
        // item's surroundings, and does not save it.
        internal static string Difference(GameItem a, GameItem b)
        {
            if (a.contentWindow != null || b.contentWindow != null) return "contents";
            if (!new HashSet<string>(a.itemTypes ?? new List<string>()).SetEquals(b.itemTypes ?? new List<string>())) return "item types";
            if (!Features.Same(a.itemFeatures, b.itemFeatures)) return "price features";
            if (a.name != b.name) return "name";
            if (a.customText != b.customText) return "custom text";
            if (a.shortDescription != b.shortDescription) return "description";
            if (a.spritePath != b.spritePath) return "appearance";
            if (a.unitValue != b.unitValue || a.unitBaseValue != b.unitBaseValue
                || a.lateUnitValue != b.lateUnitValue || a.backupUnitValue != b.backupUnitValue)
            {
                return "value";
            }
            return TagDifference(a.state, b.state) ?? TagDifference(b.state, a.state);
        }

        // Every tag that is switched on in `left` must be on in `right` with the same
        // values. A tag that is present but switched off is the same as no tag.
        private static string TagDifference(TagSystem left, TagSystem right)
        {
            if (left == null || left.dict == null) return null;
            foreach (KeyValuePair<string, TagState> pair in left.dict)
            {
                TagState mine = pair.Value;
                if (mine == null || !mine.IsEnabled() || IgnoredTags.Contains(pair.Key)) continue;
                if (pair.Key == Rules.StatedWear && Bridge.Switch("ignore_stated_wear") && Bridge.SameWear(left, right, true)) continue;
                TagState theirs = null;
                if (right != null && right.dict != null) right.dict.TryGetValue(pair.Key, out theirs);
                if (theirs == null || !theirs.IsEnabled()) return "the tag " + pair.Key;
                if (mine.GetString() != theirs.GetString() || mine.GetInt() != theirs.GetInt() || mine.GetLong() != theirs.GetLong()
                    || mine.GetBool() != theirs.GetBool() || !mine.GetFloat().Equals(theirs.GetFloat()) || !mine.GetDouble().Equals(theirs.GetDouble()))
                {
                    return "the tag " + pair.Key;
                }
            }
            return null;
        }

        // Called for every item the game creates. Adds our answers next to whatever the
        // item already has: the game takes "yes" from any answer for stacking and the
        // largest number for the slot size.
        internal static void Attach(GameItem item, string identifier)
        {
            if (!Runtime.Own || item == null || identifier == null || !IsStackable(item)) return;
            if (attached.TryGetValue(item, out _)) return;
            attached.Add(item, null);
            item.mayStackItemFunc += MayStack;
            item.maxSlotAmountItemFunc += (unit, inventory, slot) => MaxFor(unit, inventory);
        }

        // The game builds the split-off item fresh from the item directory, which drops
        // purity, ownership, bought price and any changed value. Carry them over.
        internal static void CopyUnit(GameItem from, GameItem to)
        {
            // Prepare the tags and types before assigning them, so a copy failure
            // leaves the unit with the state the factory gave it.
            TagSystem state = from.state == null ? null : OwnTags(from.state);
            TagSystem modified = from.modifiedState == null ? null : OwnTags(from.modifiedState);
            List<string> types = from.itemTypes == null ? null : new List<string>(from.itemTypes);
            if (state != null) to.state = state;
            if (modified != null) to.modifiedState = modified;
            to.itemTypes = types;
            to.name = from.name;
            to.customText = from.customText;
            to.shortDescription = from.shortDescription;
            if (to.spritePath != from.spritePath || to.spriteAtlasPath != from.spriteAtlasPath)
            {
                to.SetSprite(from.spriteAtlasPath, from.spritePath);
            }
            to.unitValue = from.unitValue;
            to.unitBaseValue = from.unitBaseValue;
            to.lateUnitValue = from.lateUnitValue;
            to.backupUnitValue = from.backupUnitValue;
            // Always replaced: the factory may have given the new unit a label that
            // the stack does not have.
            to.itemFeatures = Features.CopiesFor(from.itemFeatures, to);
        }

        // A copy in which every tag is its own object. The game's `TagSystem.Clone`
        // copies the table but not the tags in it, and the game changes a tag in
        // place. With shared tags, selling a unit taken from a stack also switched off
        // "owned" on the stack it came from.
        internal static TagSystem OwnTags(TagSystem source)
        {
            TagSystem copy = new TagSystem();
            foreach (KeyValuePair<string, TagState> pair in source.dict)
            {
                copy.dict[pair.Key] = pair.Value == null ? null : pair.Value.Clone();
            }
            return copy;
        }

        // Unit value times count. A product that does not fit is left as the unit value.
        internal static long Times(long value, int count)
        {
            if (count <= 1 || value <= 0) return value;
            if (value > long.MaxValue / count)
            {
                Report("overflow", "a stack's value was too large to multiply; the value of one unit is shown.");
                return value;
            }
            return value * count;
        }

        internal static string UseHint(GameItem item)
        {
            string hint = TextTable.Get(IsCard(item) ? TextKeys.TooltipCard : TextKeys.TooltipUse);
            bool requests = Bridge.Switch("requests_take_units"), barters = Bridge.Switch("barters_take_units");
            if (!Bridge.Switch("requests_refuse_stacks") || (requests && barters)) return hint;
            return hint + " " + TextTable.Get(requests ? TextKeys.TooltipBarters : barters ? TextKeys.TooltipRequestsOnly : TextKeys.TooltipRequests);
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (!Runtime.Own || builder == null || !IsStack(item)) return;
            Line(builder, TextTable.Get(TextKeys.TooltipCount, item.unitCount));
            Line(builder, UseHint(item));
        }
    }

    // Price features: labels such as "Low Purity" or "Street Standard", and markups and
    // discounts. Each is an object that belongs to one item (it carries that item's
    // unique id and is saved under it), so two items never share one.
    internal static class Features
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo[] fields, conditionFields;
        private static MethodInfo clone;

        // Everything a feature holds except whose it is, as text. A label carries a
        // condition (the game's ItemCondition, plain values and text; several items
        // share one object, and a loaded save gives each item its own), which is
        // written out by value. Null for a feature that holds anything else: such an
        // item stays single, because that object cannot be compared.
        internal static string Signature(ItemFeature feature)
        {
            if (feature == null) return null;
            if (fields == null) fields = typeof(ItemFeature).GetFields(Own);
            if (conditionFields == null) conditionFields = typeof(ItemCondition).GetFields(Own);
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (FieldInfo field in fields)
            {
                if (field.Name == nameof(ItemFeature.parentItemUniqueId)) continue;
                object value = field.GetValue(feature);
                if (Plain(field.FieldType))
                {
                    Write(text, field.Name, value);
                }
                else if (value == null)
                {
                    text.Append(field.Name).Append("=none;");
                }
                else if (value is ItemCondition)
                {
                    text.Append(field.Name).Append("={");
                    foreach (FieldInfo inner in conditionFields)
                    {
                        if (!Plain(inner.FieldType)) return null;
                        Write(text, inner.Name, inner.GetValue(value));
                    }
                    text.Append("};");
                }
                else
                {
                    return null;
                }
            }
            return text.ToString();
        }

        private static bool Plain(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string);

        // The length keeps two different sets of values from reading the same.
        private static void Write(System.Text.StringBuilder text, string name, object value)
        {
            string part = value == null ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            text.Append(name).Append('=').Append(part == null ? "-" : part.Length + ":" + part).Append(';');
        }

        // True when both items have the same features, in any order.
        internal static bool Same(List<ItemFeature> left, List<ItemFeature> right)
        {
            int count = left == null ? 0 : left.Count;
            if (count != (right == null ? 0 : right.Count)) return false;
            if (count == 0) return true;
            List<string> mine = new List<string>(count), theirs = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string a = Signature(left[i]), b = Signature(right[i]);
                if (a == null || b == null) return false;
                mine.Add(a);
                theirs.Add(b);
            }
            mine.Sort(string.CompareOrdinal);
            theirs.Sort(string.CompareOrdinal);
            for (int i = 0; i < count; i++)
            {
                if (mine[i] != theirs[i]) return false;
            }
            return true;
        }

        // Copies of the features for a unit taken off a stack, each belonging to `owner`.
        internal static List<ItemFeature> CopiesFor(List<ItemFeature> source, GameItem owner)
        {
            List<ItemFeature> copies = new List<ItemFeature>();
            if (source == null || source.Count == 0) return copies;
            if (clone == null) clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            int id = owner.GetUniqueID();
            foreach (ItemFeature feature in source)
            {
                if (feature == null) continue;
                ItemFeature copy = (ItemFeature)clone.Invoke(feature, null);
                copy.parentItemUniqueId = id;
                copies.Add(copy);
            }
            return copies;
        }
    }

    internal sealed class Use
    {
        internal GameItem Stack;
        internal int Count;
        internal bool Consumed;
        internal GameInventory Home;
        internal Use Outer;
        // Opened by a drop on a target (GameItem.Target), not by an activation.
        internal bool OnTarget;
    }

    internal static class OneUnit
    {
        internal static Use Current;
        internal static Func<GameItem, int, GameItem> Split = (item, count) => item.GetSplitItem(count);
        internal static Action<GameInventory, GameItem> Accept = (inventory, item) => GraphUtils.TryAcceptAll(inventory, item);
        internal static Action<GameItem> Destroy = item => item.Destroy();

        internal static Use Begin(GameItem source)
        {
            if (!EngineRules.IsStack(source)) return null;
            Use use = new Use { Stack = source, Count = source.unitCount, Home = source.parentInventory, Outer = Current };
            source.unitCount = 1;
            Current = use;
            return use;
        }

        internal static bool InUse(GameItem item)
        {
            for (Use use = Current; use != null; use = use.Outer)
            {
                if (ReferenceEquals(use.Stack, item)) return true;
            }
            return false;
        }

        // True when `item` is a stack in use: the caller then skips the destruction and
        // the stack loses one unit when the use ends. Several destructions during one
        // use count as one, as destroying one item twice does in the game.
        internal static bool Intercept(GameItem item)
        {
            for (Use use = Current; use != null; use = use.Outer)
            {
                if (ReferenceEquals(use.Stack, item))
                {
                    use.Consumed = true;
                    return true;
                }
            }
            return false;
        }

        internal static void End(Use use)
        {
            if (use == null) return;
            bool open = false;
            for (Use other = Current; other != null; other = other.Outer)
            {
                if (ReferenceEquals(other, use)) open = true;
            }
            // Closed already, by the end of a use around it.
            if (!open) return;
            // Uses opened inside this one that were never closed are closed first.
            while (!ReferenceEquals(Current, use))
            {
                Use inner = Current;
                Current = inner.Outer;
                Close(inner);
            }
            Current = use.Outer;
            Close(use);
        }

        private static void Close(Use use)
        {
            GameItem stack = use.Stack;
            // The handlers saw a count of 1. Zero or less means one was used up by
            // lowering the count; anything above 1 was added by a handler and is kept.
            int left = stack.unitCount < 1
                ? use.Count - 1
                : use.Count + (stack.unitCount - 1) - (use.Consumed ? 1 : 0);
            bool consumed = left < use.Count;
            stack.unitCount = left;
            bool moved = use.Home != null && !ReferenceEquals(stack.parentInventory, use.Home);
            if (use.OnTarget && !consumed && !moved)
            {
                // Nothing was taken while the drop ran. The handler may still have
                // kept the item to destroy it afterwards.
                try
                {
                    Later.Note(stack);
                }
                catch (Exception error)
                {
                    EngineRules.Fault("noting a drop that used nothing", error);
                }
            }
            if (!consumed && left > 1 && moved)
            {
                ReturnRest(use, left - 1);
            }
            try
            {
                Runtime.Redraw(stack);
            }
            catch (Exception error)
            {
                EngineRules.Fault("redrawing a stack after use", error);
            }
        }

        // A handler moved the item instead of using it up, for example into the target.
        // Only the one unit it saw goes along; the rest returns to where the stack was,
        // one unit at a time, so that every step either moves a whole unit or changes
        // nothing.
        private static void ReturnRest(Use use, int rest)
        {
            GameItem stack = use.Stack;
            for (int i = 0; i < rest; i++)
            {
                int before = stack.unitCount;
                GameItem one;
                try
                {
                    one = Split(stack, 1);
                }
                catch (Exception error)
                {
                    // The split lowers the count before it can fail.
                    stack.unitCount = before;
                    EngineRules.Fault("returning the rest of a moved stack", error);
                    return;
                }
                if (one == null)
                {
                    stack.unitCount = before;
                    return;
                }
                try
                {
                    Accept(use.Home, one);
                }
                catch (Exception error)
                {
                    EngineRules.Fault("returning the rest of a moved stack", error);
                }
                if (one.parentInventory != null) continue;

                // No room where the stack was: this unit and the ones after it stay
                // with the moved item.
                stack.unitCount = before;
                one.unitCount = 0;
                EngineRules.Report("no room", "the rest of a moved stack did not fit where it was and stayed with the moved item.");
                try
                {
                    Destroy(one);
                }
                catch (Exception error)
                {
                    EngineRules.Fault("removing an emptied item", error);
                }
                return;
            }
        }
    }

    // A handler that takes the dropped item later. Faucet Filter (Workshop 3833421581)
    // counts one unit when a material is dropped on its faucet and destroys the item it
    // was handed in its next update, after the drop has ended, once for every unit it
    // counted. Outside a use such a destruction would take the whole stack. So a stack
    // that was dropped on a target and lost nothing there is remembered for a few
    // frames, with the number of drops that used nothing. A destruction of that stack
    // in that time takes one unit for each of those drops. A further destruction in
    // the frame in which a unit was taken is skipped while more than one unit is
    // left, as destroying one item twice changes nothing in the game; a single unit
    // that is left, and any other frame, are the game's business. While units are
    // owed, and for the rest of a frame in which one was taken, the stack does not
    // merge, so that the item the other handler holds stays the item the units are
    // taken from and a merge never meets a skipped destruction.
    internal static class Later
    {
        private sealed class Debt { internal int Units; internal int Frame; internal int TakenIn = -1; }

        private static readonly ConditionalWeakTable<GameItem, Debt> owed = new ConditionalWeakTable<GameItem, Debt>();

        // Replaced by the tests; in the game it is the engine's frame counter.
        internal static Func<int> Frame = () => Time.frameCount;

        // The frame of the last such drop and the two after it. Faucet Filter destroys
        // in its next update, which is the same frame or the one after.
        internal const int Frames = 2;

        private static bool Fresh(Debt debt, int frame)
        {
            int age = frame - debt.Frame;
            return age >= 0 && age <= Frames;
        }

        internal static void Note(GameItem stack)
        {
            int frame = Frame();
            Debt debt = owed.GetOrCreateValue(stack);
            // Units from a drop a frame or two ago may still be waiting; they are kept.
            if (!Fresh(debt, frame))
            {
                debt.Units = 0;
                debt.TakenIn = -1;
            }
            debt.Frame = frame;
            debt.Units++;
        }

        // True while a destruction of this item would be read as a later use.
        internal static bool Pending(GameItem item)
        {
            if (item == null || !owed.TryGetValue(item, out Debt debt)) return false;
            int frame = Frame();
            if ((debt.Units > 0 && Fresh(debt, frame)) || debt.TakenIn == frame) return true;
            owed.Remove(item);
            return false;
        }

        // True when the destruction is to be skipped: one unit was taken instead, or
        // this is a further destruction of a stack in the frame in which one was
        // taken. False for everything this class does not know and for a single
        // unit, which the game destroys itself.
        internal static bool Take(GameItem item)
        {
            if (item == null || !owed.TryGetValue(item, out Debt debt)) return false;
            int frame = Frame();
            if (debt.Units < 1 || !Fresh(debt, frame))
            {
                bool again = debt.TakenIn == frame && item.unitCount > 1;
                if (!again) owed.Remove(item);
                return again;
            }
            debt.Units--;
            if (item.unitCount <= 1)
            {
                owed.Remove(item);
                return false;
            }
            debt.TakenIn = frame;
            item.unitCount--;
            try
            {
                Runtime.Redraw(item);
            }
            catch (Exception error)
            {
                EngineRules.Fault("redrawing a stack after a later use", error);
            }
            return true;
        }
    }

    // The rest of a stack after its first unit was dropped on a target. The game gives
    // every item of a multiple selection one attempt on the target; a stack gets the
    // same, one attempt per unit, each of them an ordinary one-unit use. It stops when
    // the target or the stack is gone, when the stack was moved, or when the game says
    // the drop is no longer allowed (an upgrade that is complete, for example).
    internal static class WholeStack
    {
        // Replaced by the tests; in the game they are the game's own calls.
        internal static Func<GameItem, GameItem, bool> Allowed = (source, target) => source.MayTarget(target) && source.CanTarget(target);
        internal static Action<GameItem, GameItem> Behaviours = (source, target) => ItemBehaviourManager.current?.Target(source, target);
        internal static Action<GameItem, GameItem> Drop = (source, target) => source.Target(target);
        internal static Func<GameItem, bool> Gone = item => item is GameItemElement element && element.IsDestroyed();
        internal static Func<GameItem, int> Held = HeldByDrag;

        private static bool running;
        private static FieldInfo dragItem, dragAmount;
        private static bool dragLooked;

        internal const int NotDragged = 0, Unknown = -1;

        // How many units of this stack the player is dragging. NotDragged when it is
        // not the dragged item (it was selected instead). Unknown when something is
        // dragged and the game's fields cannot be read: the amount held may be less
        // than the stack, so nothing follows the first unit then.
        private static int HeldByDrag(GameItem stack)
        {
            ItemMouseDragHandler drag = ItemMouseDragHandler.current;
            if (drag == null || !drag.IsDraggingItem) return NotDragged;
            if (!dragLooked)
            {
                dragLooked = true;
                dragItem = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");
                dragAmount = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");
                if (dragItem == null || dragAmount == null || dragAmount.FieldType != typeof(int)
                    || !typeof(GameItem).IsAssignableFrom(dragItem.FieldType))
                {
                    dragItem = null;
                    EngineRules.Report("drag fields", "the game's drag handler is laid out differently; a dragged stack is used one unit per drop.");
                }
            }
            if (dragItem == null) return Unknown;
            if (!ReferenceEquals(dragItem.GetValue(drag), stack)) return NotDragged;
            int amount = (int)dragAmount.GetValue(drag);
            return amount > 0 ? amount : Unknown;
        }

        // `count` is what the stack held before its first unit was tried.
        internal static void Run(GameItem stack, GameItem target, int count, GameInventory home)
        {
            if (!Runtime.Own || EngineRules.IsCard(stack) || running || !EngineRules.On || stack == null || target == null || count < 2) return;
            // Inside another use the outer drop decides.
            if (OneUnit.Current != null) return;
            running = true;
            try
            {
                int held = Held(stack);
                if (held < 0) return;
                int attempts = (held > 0 ? Math.Min(held, count) : count) - 1;
                for (int i = 0; i < attempts; i++)
                {
                    if (Stop(stack, target, home) || !Allowed(stack, target)) break;
                    Behaviours(stack, target);
                    if (Stop(stack, target, home)) break;
                    if (Allowed(stack, target)) Drop(stack, target);
                }
            }
            finally
            {
                running = false;
            }
        }

        private static bool Stop(GameItem stack, GameItem target, GameInventory home)
        {
            return Gone(stack) || Gone(target) || stack.unitCount < 1 || !ReferenceEquals(stack.parentInventory, home);
        }
    }

    [HarmonyPatch(typeof(DirectoryMaster), nameof(DirectoryMaster.Item), new[] { typeof(string), typeof(bool) })]
    internal static class ItemCreatedPatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(string identifier, GameItem __result)
        {
            if (!Runtime.Own) return;
            try
            {
                EngineRules.Attach(__result, identifier);
            }
            catch (Exception error)
            {
                EngineRules.Fault("preparing a new item", error);
            }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetSplitItem))]
    internal static class SplitPatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(GameItem __instance, GameItem __result)
        {
            if (!Runtime.Own) return;
            try
            {
                if (__result == null || ReferenceEquals(__instance, __result) || !EngineRules.HasState(__instance)) return;
                EngineRules.CopyUnit(__instance, __result);
            }
            catch (Exception error)
            {
                EngineRules.Fault("copying a stack's state to a split-off item", error);
            }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetCurrentValue))]
    internal static class CurrentValuePatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(GameItem __instance, ref long __result)
        {
            if (!Runtime.Own) return;
            try { if (EngineRules.IsStack(__instance)) __result = EngineRules.Times(__result, __instance.unitCount); }
            catch (Exception error) { EngineRules.Fault("valuing a stack", error); }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetNegociatedValue), new Type[0])]
    internal static class NegotiatedValuePatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(GameItem __instance, ref long __result)
        {
            if (!Runtime.Own) return;
            try { if (EngineRules.IsStack(__instance)) __result = EngineRules.Times(__result, __instance.unitCount); }
            catch (Exception error) { EngineRules.Fault("valuing a stack", error); }
        }
    }

    // Dropping an item on a target runs the item's own handlers. Priority.First so the
    // stack already counts as one item when other mods' prefixes on this method run.
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.Target))]
    [HarmonyPriority(Priority.First)]
    internal static class TargetPatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Prefix(GameItem __instance, out Use __state)
        {
            __state = null;
            if (!Runtime.Own) return;
            try
            {
                __state = OneUnit.Begin(__instance);
                if (__state != null) __state.OnTarget = true;
            }
            catch (Exception error)
            {
                EngineRules.Fault("starting the use of a stack", error);
            }
        }

        // A finalizer runs however the method ended, so the count is always put back.
        // When the drop went through without an error, the rest of the stack follows.
        private static void Finalizer(GameItem __instance, GameItem targetItem, Use __state, Exception __exception)
        {
            if (!Runtime.Own) return;
            try
            {
                OneUnit.End(__state);
            }
            catch (Exception error)
            {
                EngineRules.Fault("ending the use of a stack", error);
                return;
            }
            if (__state == null || __exception != null) return;
            try
            {
                WholeStack.Run(__instance, targetItem, __state.Count, __state.Home);
            }
            catch (Exception error)
            {
                EngineRules.Fault("using the rest of a stack", error);
            }
        }
    }

    // The same drop also runs target behaviours registered by mods.
    [HarmonyPatch(typeof(ItemBehaviourManager), nameof(ItemBehaviourManager.Target))]
    [HarmonyPriority(Priority.First)]
    internal static class BehaviourTargetPatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Prefix(GameItem source, out Use __state)
        {
            __state = null;
            if (!Runtime.Own) return;
            try
            {
                __state = OneUnit.Begin(source);
            }
            catch (Exception error)
            {
                EngineRules.Fault("starting the use of a stack", error);
            }
        }

        private static void Finalizer(Use __state)
        {
            if (!Runtime.Own) return;
            try
            {
                OneUnit.End(__state);
            }
            catch (Exception error)
            {
                EngineRules.Fault("ending the use of a stack", error);
            }
        }
    }

    [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.Destroy))]
    [HarmonyPriority(Priority.First)]
    internal static class DestroyPatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static bool Prefix(GameItemElement __instance)
        {
            if (!Runtime.Own) return true;
            try
            {
                // In a use: the use loses one unit when it ends. Shortly after a drop
                // that used nothing: one unit now (see Later).
                return !OneUnit.Intercept(__instance) && !Later.Take(__instance);
            }
            catch (Exception error)
            {
                EngineRules.Fault("the check before an item is destroyed", error);
                return true;
            }
        }
    }

    // Activating an item (unpacking Chemical Supplies) can end in its destruction long
    // after the activation returned, from a confirmation dialog, where a one-unit use
    // cannot be kept open. So a stack is not activated at all: the player takes one off.
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayActivateSlotItem))]
    internal static class MayActivatePatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(GameItem __instance, ref bool __result)
        {
            if (!Runtime.Own) return;
            try { if (__result && EngineRules.BlocksActivation(__instance)) __result = false; }
            catch (Exception error) { EngineRules.Fault("checking activation", error); }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.CanActivateSlotItem))]
    internal static class CanActivatePatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static void Postfix(GameItem __instance, ref bool __result)
        {
            if (!Runtime.Own) return;
            try { if (__result && EngineRules.BlocksActivation(__instance)) __result = false; }
            catch (Exception error) { EngineRules.Fault("checking activation", error); }
        }
    }

    // For code that activates without asking first. A single item that is activated is
    // noted, so that it is not merged into a stack while its dialog is open. Returning
    // false also skips other mods' later prefixes on this method; that only happens for
    // a stack, which no other mod expects to see here.
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ActivateSlotItem))]
    [HarmonyPriority(Priority.First)]
    internal static class ActivatePatch
    {
        private static bool Prepare() => GameShape.Ready;
        private static bool Prefix(GameItem __instance, out Use __state)
        {
            __state = null;
            if (!Runtime.Own) return true;
            try
            {
                if (EngineRules.BlocksActivation(__instance)) return false;
                // The handler destroys the item it was made for; as a one-unit use that
                // takes one off the stack.
                if (EngineRules.ActivatesAsOne(__instance)) __state = OneUnit.Begin(__instance);
                else EngineRules.NoteActivation(__instance);
            }
            catch (Exception error)
            {
                EngineRules.Fault("the check before an item is activated", error);
            }
            return true;
        }

        private static void Finalizer(Use __state)
        {
            if (!Runtime.Own) return;
            try
            {
                OneUnit.End(__state);
            }
            catch (Exception error)
            {
                EngineRules.Fault("ending the activation of a stack", error);
            }
        }
    }

}

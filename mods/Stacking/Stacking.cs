using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Stacking
{
    public class StackingMod : IMod
    {
        internal const string Version = "1.9.2";
        internal static readonly Dictionary<string, ConfigEntry<bool>> Switches = new Dictionary<string, ConfigEntry<bool>>();
        internal static readonly Dictionary<string, ConfigEntry<string>> ItemLists = new Dictionary<string, ConfigEntry<string>>();
        internal static readonly Dictionary<string, ConfigEntry<int>> Limits = new Dictionary<string, ConfigEntry<int>>();

        public void Init(ModManifest manifest)
        {
            Bridge.HarmonyId = "mod.rnk.stacking";
            ModConfig config = ModConfig.For(manifest.ID);
            Bool(config, "enabled");
            Bool(config, "smuggler_bays");
            Bool(config, "machine_bays");
            Bool(config, "loot_crates");
            Bool(config, "backpacks");
            Bool(config, "backpack_needs_upgrade");
            Bool(config, "requests_take_units");
            Bool(config, "barters_take_units");
            Bool(config, "requests_refuse_stacks");
            Bool(config, "ignore_visit_stamp");
            Bool(config, "ignore_empty_stolen_mark");
            Bool(config, "ignore_lock_mark");
            Bool(config, "ignore_empty_bottle_state");
            Bool(config, "faucet_one_unit");
            Bool(config, "variety_one_unit");
            Bool(config, "ignore_stated_wear");
            Bool(config, "types_follow_marks");
            Bool(config, "save_stack_rest");
            Bool(config, "ignore_phone_order_mark");
            foreach (string category in Rules.Categories)
            {
                string key = category;
                Limits[key] = config.Define("max_" + key.ToLowerInvariant(), Rules.DefaultLimit(key), "Stack limits",
                    () => Runtime.Text("category_" + key), 1, 99, () => Runtime.Text("option_category_desc"));
            }
            Limits["backpack_max"] = config.Define("backpack_max", 5, "Stack limits", () => Runtime.Text("option_backpack_max"), 1, 5,
                () => Runtime.Text("option_backpack_max_desc"));
            foreach (string key in new[] { "extra_items", "excluded_items" })
                ItemLists[key] = config.Define(key, "", "Stacking", () => Runtime.Text("option_" + key), null, () => Runtime.Text("option_" + key + "_desc"));
            LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Stacking", Bridge.Fault);
        }

        private static void Bool(ModConfig config, string key)
        {
            Switches[key] = config.Define(key, true, "Stacking", () => Runtime.Text("option_" + key), () => Runtime.Text("option_" + key + "_desc"));
        }

        public void OnEnable()
        {
            Bridge.Reset();
            Bridge.Active = true;
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly -= Runtime.Start;
            ModHook.OnCreateTooltipLate -= Tooltip;
            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly += Runtime.Start;
            ModHook.OnCreateTooltipLate += Tooltip;
            Bridge.TryInstall();
        }

        public void OnDisable()
        {
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly -= Runtime.Start;
            ModHook.OnCreateTooltipLate -= Tooltip;
            Runtime.Mode = StackMode.Pending;
            Bridge.Reset(); // The loader removes all patches under HarmonyId afterwards.
        }

        private static void OnModAssetsLoaded(LoadedMod mod) => Runtime.Start();
        private static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            if (!Runtime.Own) return;
            try { EngineRules.AddTooltip(builder, item); }
            catch (Exception error) { Bridge.Fault("describing a stack", error); }
        }
    }

    // Only values: no game or mod calls, including settings.
    internal static class Rules
    {
        internal static int RestLeft(int offered, int unplaced) => Math.Max(0, Math.Min(Math.Max(0, offered), unplaced));

        // Only counts and words. Each clause names only the units actually moved
        // there; split rescues still produce one log line. The noted inventory is
        // the stack's original home, "where the stack lay"; "the stack itself" is
        // only said when the units were put back on the used item.
        internal static string RestMessage(int count, int[] unplaced, bool returned, string identifier)
        {
            if (count <= 0) return null;
            string[] places = { "where the stack lay", "the main inventory", "the back inventory", "the back counter" };
            var clauses = new List<string>();
            int left = count;
            for (int i = 0; i < places.Length; i++)
            {
                int next = RestLeft(left, unplaced != null && i < unplaced.Length ? unplaced[i] : left);
                if (next < left) clauses.Add((left - next) + " of " + identifier + " moved to " + places[i] + ".");
                left = next;
            }
            if (left > 0) clauses.Add(left + " of " + identifier + (returned ? " moved to the stack itself." : " could not be saved."));
            return string.Join(" ", clauses);
        }

        internal static void FollowMarks<T>(IList<string> types, IList<string> allTypes, T marks, Func<T, string, bool> enabled) where T : class
        {
            if (types == null || allTypes == null || marks == null || enabled == null) return;
            for (int i = 0; i < allTypes.Count; i++)
            {
                string name = allTypes[i];
                if (name != null && !types.Contains(name) && enabled(marks, name)) types.Add(name);
            }
        }

        internal static readonly string[] Categories =
            { "ACCESS_CARD", "AMMUNITION", "MEDICAL", "FOOD", "HOUSEHOLD_GOOD", "SUBSTANCE", "MATERIAL", "other" };

        internal static string Category(IList<string> types)
        {
            if (types != null)
                for (int i = 0; i < Categories.Length - 1; i++)
                    if (types.Contains(Categories[i])) return Categories[i];
            return "other";
        }

        internal static int DefaultLimit(string category)
        {
            switch (category)
            {
                case "MATERIAL": return 20;
                case "AMMUNITION": return 50;
                case "ACCESS_CARD": case "MEDICAL": case "FOOD": case "HOUSEHOLD_GOOD": case "SUBSTANCE": return 10;
                default: return 5;
            }
        }

        internal static int Limit(int categoryLimit, bool backpack, int backpackMax)
        {
            int limit = Math.Max(1, Math.Min(99, categoryLimit));
            return backpack ? Math.Min(limit, Math.Max(1, Math.Min(5, backpackMax))) : limit;
        }

        internal static int BackpackLimit(int categoryLimit, int backpackMax, int level, bool needsUpgrade)
        {
            int limit = Limit(categoryLimit, true, backpackMax);
            return needsUpgrade ? Math.Min(limit, 1 + Math.Max(0, Math.Min(4, level))) : limit;
        }

        internal static int Clamp(int result, int limit, int already, bool empty)
        {
            // Only ever lowers: an answer of nothing or less is the caller's own.
            if (result <= 0) return result;
            long room = empty ? limit : (long)limit - Math.Max(0, already);
            return (int)Math.Max(0L, Math.Min(result, room));
        }

        // Returns the setting for an extra place; a backpack is identified by tag separately.
        internal static string ExtraPlace(string id)
        {
            switch (id)
            {
                case "smuggler_bay": case "smuggler_bay_mod": case "smuggler_bay_mini": return "smuggler_bays";
                case "machine_bay": case "machine_bay_ext": return "machine_bays";
                case "eng_box": case "med_box": case "sec_box": case "service_box": case "evidence_box": return "loot_crates";
                default: return null;
            }
        }

        // Agri Expansion stamps goods a seller lays on the counter with the visit they came
        // in, only to count each once during that visit. The stamp stays after the purchase
        // and differs from visit to visit, so it kept equal goods from stacking.
        internal const string VisitStamp = "AGRI_PERFUME_RECORDED";
        // Agri Expansion also notes which stock list an item was made from, for the same
        // count of a seller's goods; nothing reads it once the item is the player's.
        internal const string SourceTable = "AGRI_PERFUME_SOURCE_TABLE";
        internal static bool Cosmetic(bool already, bool ignoreStamp, string key) => already || (ignoreStamp && key == VisitStamp);
        // Only between two items the player owns: Agri Expansion still reads the note on a
        // seller's goods.
        internal static bool SourceNote(bool ignore, string key, bool known, bool ownedA, bool ownedB) =>
            ignore && known && key == SourceTable && ownedA && ownedB;

        // The game marks goods as stolen with two tags. For goods too cheap to count as
        // stolen it switches both off again and leaves them on the item, empty. An item
        // with the empty pair and one without it are the same goods.
        internal const string StolenMark = "STOLEN_TAG", StolenValue = "STOLEN_VALUE_INT";
        internal static bool StolenKey(string key) => key == StolenMark || key == StolenValue;
        internal static bool EmptyStolenMark(bool ignore, string key, bool known, bool stolenA, bool stolenB) =>
            ignore && known && StolenKey(key) && !stolenA && !stolenB;

        // Marks the game switches off and leaves on the item: the mark of a challenged
        // offer when the goods are bought, and the goods type of a bottle, which is water
        // while it holds something and a household good while it is empty. Switched off on
        // one item and never there on the other is the same thing; switched on is not.
        // A lock mark also stays after unlocking; while on, it hides goods from searches
        // and sales, so it must still count. It has its own switch.
        internal const string ConfrontedMark = "ALREADY_CONFRONTED_TAG";
        internal const string LockMark = "ITEM_BEHIND_LOCK_TAG";
        // Station Economy marks what was ordered by phone as coming from a source of its
        // own. The mark is looked past only when neither item names another source.
        internal const string SourceId = "MOD_ID_TAG", SourceName = "MOD_NAME_TAG", PhoneOrder = "suoer.stationeconomy.bought";
        internal static bool SourceKey(string key) => key == SourceId || key == SourceName;
        internal static string NamedSource(bool enabled, string stored) =>
            enabled ? stored ?? "" : string.IsNullOrEmpty(stored) || stored == PhoneOrder ? null : stored;
        // `idA`, `idB`: the source an item names, null when it names none.
        internal static bool PhoneOrderMark(bool ignore, bool known, string idA, string idB) =>
            ignore && known && (idA != null || idB != null) && (idA == null || idA == PhoneOrder) && (idB == null || idB == PhoneOrder);
        internal static bool SwitchedOffKey(string key) => key == ConfrontedMark || key == "WATER" || key == "HOUSEHOLD_GOOD" || key == LockMark;
        internal static bool SwitchedOffMark(bool ignore, string key, bool known, bool onA, bool onB) =>
            ignore && known && SwitchedOffKey(key) && !onA && !onB;

        internal const string RealWear = "DURABILITY_TAG", StatedWear = "DURABILITY_FAKE_TAG";
        internal static bool HonestWear(string key, int? realA, int? realB, int? statedA, int? statedB) =>
            key == StatedWear && realA.HasValue && realB.HasValue && realA == realB
            && (!statedA.HasValue || statedA == realA) && (!statedB.HasValue || statedB == realB);

        // An empty bottle keeps what the game noted about its last contents: how warm they
        // were, the last scan and strip test, and a line per kind of liquid. Between two
        // empty containers none of that says anything.
        internal const string LiquidContainer = "LIQUID_CONTAINER_TAG", PartPrefix = "CURRENT_PART_";
        internal static bool BottleStateKey(string key) => key != null && (key == "CONTAINER_TEMP_TAG" || key == "LAST_SCAN_RESULT_TAG"
            || key == "STRIP_TESTED_TAG" || key.StartsWith(PartPrefix, StringComparison.Ordinal));
        internal static bool EmptyBottleState(bool ignore, string key, bool known, bool emptyA, bool emptyB) =>
            ignore && known && BottleStateKey(key) && emptyA && emptyB;
        // Empty as the game counts it: a liquid container with no part above zero.
        internal static bool EmptyContainer(bool container, IEnumerable<KeyValuePair<string, int>> amounts)
        {
            if (!container || amounts == null) return false;
            foreach (KeyValuePair<string, int> amount in amounts)
                if (amount.Key != null && amount.Key.StartsWith(PartPrefix, StringComparison.Ordinal) && amount.Value > 0) return false;
            return true;
        }
    }

    internal static class Bridge
    {
        internal const string EconomyId = "suoer.stationeconomy";
        internal static string HarmonyId = "mod.rnk.stacking";
        internal static bool Active, Installed;
        private static bool gaveUp, running;
        [ThreadStatic] private static int sweepDepth;
        private static readonly HashSet<string> reported = new HashSet<string>();

        // All game and Workshop access is replaceable outside Unity.
        internal static Action<string> Log = text => Debug.LogWarning(text);
        internal static Func<bool> Running = () => ModHelper.IsModRunning(EconomyId);
        internal static Func<string, bool> Switch = key => !StackingMod.Switches.TryGetValue(key, out ConfigEntry<bool> entry) || entry.Value;
        internal static Func<string, int> Setting = key => StackingMod.Limits.TryGetValue(key, out ConfigEntry<int> entry)
            ? entry.Value : key == "backpack_max" ? 5 : Rules.DefaultLimit(key);
        internal static Func<GameInventory, GameItem> Parent = inv => GraphUtils.FindParentType<GameItem>(inv);
        internal static Func<GameItem, bool> Owned = item => GeneralHelper.IsItemOwned(item);
        internal static Func<GameItem, bool> Backpack = item => item.IsTag("BACKPACK_TAG");
        internal static Func<GameItem, int> Upgrade = item => item.GetTagReadonly("MACHINE_UPGRADES_STACKING_INT").GetInt();
        internal static Func<GameInventory, bool> Restricted = inv => inv.maxSlotAmountItemFunc != null || inv.maxSlotAddItemFunc != null;
        internal static Func<bool> TypeAvailable = () => AccessTools.TypeByName("StationEconomy.Stacking") != null;
        internal static Func<MethodInfo> FindOn = () => AccessTools.PropertyGetter(AccessTools.TypeByName("StationEconomy.Stacking"), "On");
        internal static Func<MethodInfo> FindHolder = () => AccessTools.Method("StationEconomy.Stacking:OkHolder");
        internal static Func<MethodInfo> FindOwn = () => AccessTools.Method("StationEconomy.Stacking:HasOwnRule");
        internal static Func<MethodInfo> FindSweep = () => AccessTools.Method("StationEconomy.Stacking:AutoMergeTick");
        // Station Economy's own test for a tag that does not matter when two items are compared.
        internal static Func<MethodInfo> FindCosmetic = () => AccessTools.Method("StationEconomy.Stacking:Cosmetic");
        internal static bool StampInstalled;
        // The two sets of tags Station Economy is comparing right now, so that a tag can
        // be judged on both items. Set around its comparison, never kept.
        internal static Func<MethodInfo> FindSameTags = () => AccessTools.Method("StationEconomy.Stacking:SameTags");
        internal static Func<TagSystem, bool> Stolen = tags => tags != null && tags.IsTag(Rules.StolenMark);
        internal static Func<TagSystem, bool> OwnedTags = tags => tags != null && tags.IsTag("IS_OWNED_TAG");
        internal static Func<TagSystem, string, bool> TagOn = (tags, key) => tags != null && tags.IsTag(key);
        // The source an item names; null for none. A switched-off tag that still holds
        // another mod's id counts as that source, so such goods stay apart.
        internal static Func<TagSystem, string> Source = tags =>
        {
            if (tags?.dict == null || !tags.dict.TryGetValue(Rules.SourceId, out TagState tag) || tag == null) return null;
            return Rules.NamedSource(tags.IsTag(Rules.SourceId), tag.GetString());
        };
        internal static Func<TagSystem, bool> EmptyBottle = tags => tags != null && tags.dict != null
            && Rules.EmptyContainer(tags.IsTag(Rules.LiquidContainer), Amounts(tags));
        private static IEnumerable<KeyValuePair<string, int>> Amounts(TagSystem tags)
        {
            foreach (KeyValuePair<string, TagState> tag in tags.dict)
                if (tag.Key != null && tag.Value != null && tag.Key.StartsWith(Rules.PartPrefix, StringComparison.Ordinal))
                    yield return new KeyValuePair<string, int>(tag.Key, tag.Value.GetInt());
        }

        // Economy counts a tag even when switched off; the own engine does not.
        internal static bool SameWear(TagSystem a, TagSystem b, bool enabledOnly) =>
            Rules.HonestWear(Rules.StatedWear, WearInt(a, Rules.RealWear, enabledOnly), WearInt(b, Rules.RealWear, enabledOnly),
                WearInt(a, Rules.StatedWear, enabledOnly), WearInt(b, Rules.StatedWear, enabledOnly));

        private static int? WearInt(TagSystem tags, string key, bool enabledOnly) =>
            tags?.dict != null && tags.dict.TryGetValue(key, out TagState tag) && tag != null && (!enabledOnly || tag.IsEnabled())
                ? tag.GetInt() : (int?)null;
        // Worked out at most once for a comparison, and only when a bottle key comes up.
        [ThreadStatic] private static int pairEmpty; // 0 not asked, 1 both empty, 2 not
        private static bool BothEmpty()
        {
            if (pairEmpty == 0) pairEmpty = EmptyBottle(pairA) && EmptyBottle(pairB) ? 1 : 2;
            return pairEmpty == 1;
        }
        internal struct Pair { internal TagSystem A, B; internal bool Known; internal int Empty; }
        internal static bool PairInstalled;
        [ThreadStatic] private static TagSystem pairA, pairB;
        [ThreadStatic] private static bool pairKnown;
        internal static Func<bool> EconomyOn;
        internal static Func<GameInventory, bool> OkHolder;
        internal static Func<GameItem, bool> HasOwnRule;

        internal static void Reset()
        {
            TypeGuard.Reset();
            RestGuard.Reset();
            FaucetGuard.Reset();
            VarietyGuard.Reset();
            RequestGuard.Reset();
            BarterGuard.Reset();
            Active = Installed = gaveUp = running = StampInstalled = PairInstalled = false;
            pairA = pairB = null; pairKnown = false; pairEmpty = 0;
            sweepDepth = 0;
            EconomyOn = null;
            OkHolder = null;
            HasOwnRule = null;
            reported.Clear();
        }

        internal static void Report(string key, string message)
        {
            try { if (reported.Add(key)) Log?.Invoke("[Stacking] " + message); }
            catch (Exception) { }
        }

        internal static void Fault(string where, Exception error)
        {
            try { Report(where, "a fault in " + where + "; leaving the original answer. " + error.GetType().Name); }
            catch (Exception) { }
        }

        // Asked on every slot question, so whether Station Economy runs is looked up
        // when mods are loaded and when a game is loaded, not on every call.
        internal static bool On() => Active && Installed && running && Switch("enabled") && EconomyOn();

        internal static string Missing(MethodInfo on, MethodInfo holder, MethodInfo own, MethodInfo sweep)
        {
            if (!Fits(on, typeof(bool))) return "On";
            if (!Fits(holder, typeof(bool), typeof(GameInventory))) return "OkHolder";
            if (!Fits(own, typeof(bool), typeof(GameItem))) return "HasOwnRule";
            if (!Fits(sweep, typeof(void))) return "AutoMergeTick";
            return null;
        }

        private static bool Fits(MethodInfo method, Type result, params Type[] args)
        {
            if (method == null || !method.IsPublic || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < args.Length; i++) if (parameters[i].ParameterType != args[i]) return false;
            return true;
        }

        // When a game is loaded every mod has been loaded, so an absent Station Economy
        // is then worth a line in the log. Before that it may only not be loaded yet.
        internal static void InstallOrSayWhy()
        {
            TryInstall();
            try
            {
                if (!Active || Installed || gaveUp) return;
                if (!running) Report("absent", "Station Economy is not running; waiting for the startup mode decision.");
                else Report("no type", "Station Economy runs, but its stacking was not found; nothing was patched.");
            }
            catch (Exception error) { Fault("looking for Station Economy", error); }
        }

        internal static void TryInstall()
        {
            TypeGuard.TryInstall();
            RestGuard.TryInstall();
            FaucetGuard.TryInstall();
            VarietyGuard.TryInstall();
            RequestGuard.TryInstall();
            BarterGuard.TryInstall();
            if (!Active || gaveUp) return;
            if (Installed)
            {
                try { running = Running(); }
                catch (Exception error) { Fault("looking for Station Economy", error); }
                return;
            }
            Harmony harmony = null;
            MethodInfo holder = null, sweep = null;
            MethodInfo holderPostfix = null, sweepPrefix = null, sweepFinalizer = null;
            try
            {
                running = Running();
                if (!running) return;
                if (!TypeAvailable()) return; // Local mods load before Workshop assemblies.
                MethodInfo on = FindOn(), own = FindOwn();
                holder = FindHolder();
                sweep = FindSweep();
                string missing = Missing(on, holder, own, sweep);
                if (missing != null)
                {
                    gaveUp = true;
                    Report("mode", "Station Economy is laid out differently (" + missing + "); nothing was patched. ItemStacks must be switched off.");
                    return;
                }
                EconomyOn = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), on);
                OkHolder = (Func<GameInventory, bool>)Delegate.CreateDelegate(typeof(Func<GameInventory, bool>), holder);
                HasOwnRule = (Func<GameItem, bool>)Delegate.CreateDelegate(typeof(Func<GameItem, bool>), own);
                harmony = new Harmony(HarmonyId);
                holderPostfix = AccessTools.Method(typeof(Bridge), nameof(HolderPostfix));
                sweepPrefix = AccessTools.Method(typeof(Bridge), nameof(SweepPrefix));
                sweepFinalizer = AccessTools.Method(typeof(Bridge), nameof(SweepFinalizer));
                // Track targets before patching, so even a failed patch attempt is rolled back.
                harmony.Patch(holder, postfix: new HarmonyMethod(holderPostfix));
                harmony.Patch(sweep, prefix: new HarmonyMethod(sweepPrefix), finalizer: new HarmonyMethod(sweepFinalizer));
                Installed = true;
                Report("connected", "connected to Station Economy's stacking.");
                InstallStamp(harmony);
            }
            catch (Exception error)
            {
                gaveUp = true;
                Installed = false;
                if (harmony != null)
                {
                    Remove(harmony, holder, holderPostfix);
                    Remove(harmony, sweep, sweepPrefix);
                    Remove(harmony, sweep, sweepFinalizer);
                }
                Fault("connecting to Station Economy", error);
            }
        }

        internal static bool CosmeticFits(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(bool)) return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(object);
        }

        // On its own: when this part is laid out differently the rest stays connected, and
        // Station Economy compares goods as it does without us.
        private static void InstallStamp(Harmony harmony)
        {
            MethodInfo cosmetic = null;
            MethodInfo cosmeticPostfix = null;
            try
            {
                cosmetic = FindCosmetic();
                if (!CosmeticFits(cosmetic))
                {
                    Report("stamp shape", "Station Economy's test for tags that do not matter is laid out differently; Agri Expansion's visit stamp still keeps goods apart.");
                    return;
                }
                cosmeticPostfix = AccessTools.Method(typeof(Bridge), nameof(CosmeticPostfix));
                harmony.Patch(cosmetic, postfix: new HarmonyMethod(cosmeticPostfix));
                StampInstalled = true;
            }
            catch (Exception error)
            {
                StampInstalled = false;
                Remove(harmony, cosmetic, cosmeticPostfix);
                Fault("ignoring the visit stamp", error);
                return;
            }
            MethodInfo same = null;
            MethodInfo samePrefix = null, sameFinalizer = null;
            try
            {
                same = FindSameTags();
                if (!SameTagsFits(same))
                {
                    Report("pair shape", "Station Economy's comparison of two items' tags is laid out differently; an empty stolen mark still keeps goods apart.");
                    return;
                }
                samePrefix = AccessTools.Method(typeof(Bridge), nameof(SameTagsPrefix));
                sameFinalizer = AccessTools.Method(typeof(Bridge), nameof(SameTagsFinalizer));
                harmony.Patch(same, prefix: new HarmonyMethod(samePrefix), finalizer: new HarmonyMethod(sameFinalizer));
                PairInstalled = true;
            }
            catch (Exception error)
            {
                PairInstalled = false;
                Remove(harmony, same, samePrefix);
                Remove(harmony, same, sameFinalizer);
                Fault("ignoring an empty stolen mark", error);
            }
        }

        internal static bool SameTagsFits(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(bool)) return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 2 && parameters[0].ParameterType == typeof(TagSystem) && parameters[1].ParameterType == typeof(TagSystem);
        }

        // A comparison may run inside another; each puts back what it found.
        private static void SameTagsPrefix(TagSystem __0, TagSystem __1, out Pair __state)
        {
            __state = new Pair { A = pairA, B = pairB, Known = pairKnown, Empty = pairEmpty };
            pairA = __0; pairB = __1; pairKnown = true; pairEmpty = 0;
        }

        // A finalizer, so the pair is put back also when the comparison throws; it passes
        // the other mod's own error on untouched.
        private static void SameTagsFinalizer(Pair __state)
        {
            pairA = __state.A; pairB = __state.B; pairKnown = __state.Known; pairEmpty = __state.Empty;
        }

        private static void CosmeticPostfix(object __0, ref bool __result)
        {
            // The key first: this is asked for every tag of every pair of items compared.
            if (__result || !StampInstalled || !Active || !(__0 is string key)) return;
            try
            {
                if (key == Rules.VisitStamp)
                    __result = Rules.Cosmetic(false, Switch("enabled") && Switch("ignore_visit_stamp"), key);
                else if (key == Rules.SourceTable)
                    __result = PairInstalled && Rules.SourceNote(Switch("enabled") && Switch("ignore_visit_stamp"), key, pairKnown, OwnedTags(pairA), OwnedTags(pairB));
                else if (PairInstalled && pairKnown && Rules.StolenKey(key))
                    __result = Rules.EmptyStolenMark(Switch("enabled") && Switch("ignore_empty_stolen_mark"), key, true, Stolen(pairA), Stolen(pairB));
                else if (PairInstalled && pairKnown && Rules.SwitchedOffKey(key))
                    __result = Rules.SwitchedOffMark(Switch("enabled") && Switch(key == Rules.LockMark ? "ignore_lock_mark" : "ignore_empty_stolen_mark"), key, true, TagOn(pairA, key), TagOn(pairB, key));
                else if (PairInstalled && pairKnown && Rules.BottleStateKey(key))
                    __result = Switch("enabled") && Switch("ignore_empty_bottle_state") && Rules.EmptyBottleState(true, key, true, BothEmpty(), BothEmpty());
                else if (PairInstalled && pairKnown && Rules.SourceKey(key))
                    __result = Rules.PhoneOrderMark(Switch("enabled") && Switch("ignore_phone_order_mark"), true, Source(pairA), Source(pairB));
                else if (PairInstalled && pairKnown && key == Rules.StatedWear)
                    __result = Switch("enabled") && Switch("ignore_stated_wear") && SameWear(pairA, pairB, false);
            }
            catch (Exception error) { Fault("judging a tag that may not matter", error); }
        }

        private static void Remove(Harmony harmony, MethodInfo target, MethodInfo patch)
        {
            try { if (target != null && patch != null) harmony.Unpatch(target, patch); }
            catch (Exception error) { Fault("removing an incomplete patch", error); }
        }

        internal static bool IsBackpack(GameInventory inventory)
        {
            if (!(inventory is GameGridInventory)) return false;
            GameItem parent = Parent(inventory);
            return parent != null && Backpack(parent);
        }

        internal static bool Extra(GameInventory inventory)
        {
            if (sweepDepth > 0 || !(inventory is GameGridInventory) || Restricted(inventory)) return false;
            GameItem parent = Parent(inventory);
            if (parent == null || !Owned(parent)) return false;
            if (Backpack(parent)) return Switch("backpacks") && (!Switch("backpack_needs_upgrade") || Upgrade(parent) >= 1);
            string group = Rules.ExtraPlace(parent.identifier);
            return group != null && Switch(group);
        }

        internal static void HolderPostfix(GameInventory __0, ref bool __result)
        {
            try { if (!__result && On() && Extra(__0)) __result = true; }
            catch (Exception error) { Fault("checking a holder", error); }
        }

        internal static void SweepPrefix(out int __state)
        {
            __state = sweepDepth;
            try { sweepDepth++; }
            catch (Exception error) { Fault("starting the sweep", error); }
        }

        // A void finalizer preserves any exception from Station Economy. Restore the
        // previous depth even after an exception or a nested sweep.
        internal static void SweepFinalizer(int __state)
        {
            try { sweepDepth = __state; }
            catch (Exception error) { Fault("ending the sweep", error); }
        }

        internal static void Clamp(GameItem item, SlotMarker slot, ref int result)
        {
            try
            {
                if (!On() || item == null || slot == null || slot.inventory == null || HasOwnRule(item) || !OkHolder(slot.inventory)) return;
                GameItem target = slot.targetItem;
                if (target != null && HasOwnRule(target)) return;
                string category = Rules.Category(item.itemTypes);
                bool backpack = IsBackpack(slot.inventory);
                bool needsUpgrade = backpack && Switch("backpack_needs_upgrade");
                int limit = backpack
                    ? Rules.BackpackLimit(Setting(category), Setting("backpack_max"), needsUpgrade ? Upgrade(Parent(slot.inventory)) : 0, needsUpgrade)
                    : Rules.Limit(Setting(category), false, Setting("backpack_max"));
                result = Rules.Clamp(result, limit, target == null ? 0 : target.unitCount, target == null);
            }
            catch (Exception error) { Fault("limiting a stack", error); }
        }

        internal static void Refuse(GameItem item, ref bool result, bool request = false)
        {
            try
            {
                if (request && RequestGuard.Bypass) return;
                if (!request && BarterGuard.Bypass) return;
                if (Runtime.Own)
                {
                    if (result && Switch("requests_refuse_stacks") && item != null && item.unitCount > 1) result = false;
                    return;
                }
                if (result && On() && Switch("requests_refuse_stacks") && item != null && item.unitCount > 1 && !HasOwnRule(item)) result = false;
            }
            catch (Exception error) { Fault("checking a request or barter", error); }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MaxNumAdd), new[] { typeof(SlotMarker) })]
    [HarmonyPriority(Priority.Last)]
    internal static class MaxNumAddPatch
    {
        private static void Postfix(GameItem __instance, SlotMarker slot, ref int __result) => Bridge.Clamp(__instance, slot, ref __result);
    }

    [HarmonyPatch(typeof(StoreRequest), nameof(StoreRequest.CanThisItemFillRequest))]
    internal static class RequestPatch
    {
        private static void Postfix(GameItem item, ref bool __result) => Bridge.Refuse(item, ref __result, true);
    }

    [HarmonyPatch(typeof(BarterCondition), nameof(BarterCondition.IsItemValid))]
    internal static class BarterPatch
    {
        private static void Postfix(GameItem gameItem, ref bool __result) => Bridge.Refuse(gameItem, ref __result);
    }
}

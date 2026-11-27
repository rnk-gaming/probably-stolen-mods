using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Globalization;
using HarmonyLib;

using LocalMods;

namespace Backpacks
{
    internal enum BackpackSource { Jackson, Dump, Sellers }

    // All chances are integers per ten thousand. Only Jackson has day gates.
    internal static class BackpackSourceRules
    {
        internal static readonly string[] Items = { "fanny_pack", "satchel", "kids_backpack", "backpack_small",
            "backpack_medium", "backpack_large", Military.Medium, Military.Large };
        // Searches are frequent, so extra bags at the Dumping Grounds and sellers
        // are rare. The game gives only the first Satchel and Kid's Backpack there.
        private static readonly int[,] Chances = { {1500,20,50}, {1000,15,40}, {0,10,20}, {800,5,20},
            {400,0,10}, {200,0,5}, {200,2,3}, {100,1,2} };
        private static readonly int[] Days = { 0, 0, 0, 0, 13, 31, 20, 40 };
        internal static int FromDay(int row) => Days[row];

        // The game offers a toolbox only at Dr. Jackson while none is owned.
        // One scavenger seller in a hundred brings an extra worn one.
        internal const int ToolboxChance = 100;
        internal static bool Toolbox(bool enabled, int roll) => enabled && roll >= 1 && roll <= ToolboxChance;

        internal static int Chance(int row, BackpackSource source, int scale, int day, bool military)
        {
            if (row < 0 || row >= Items.Length || source < BackpackSource.Jackson || source > BackpackSource.Sellers
                || (row >= 6 && !military) || (source == BackpackSource.Jackson && day < Days[row])) return 0;
            return Chances[row, (int)source] * Math.Max(0, Math.Min(300, scale)) / 100;
        }
    }

    internal static class BackpackSources
    {
        internal static string Description(BackpackSource source)
        {
            try
            {
                string key = source == BackpackSource.Jackson ? "jackson" : source == BackpackSource.Dump ? "dump" : "sellers";
                int scale = Scale("backpack_chance_" + key); bool own = Military.Enabled();
                var rows = new List<string>();
                for (int i = 0; i < BackpackSourceRules.Items.Length; i++)
                {
                    int chance = BackpackSourceRules.Chance(i, source, scale, int.MaxValue, own);
                    if (chance <= 0) continue;
                    string id = BackpackSourceRules.Items[i];
                    string name = UpgradeViewRules.Name(id);
                    rows.Add(source == BackpackSource.Jackson && BackpackSourceRules.FromDay(i) > 0
                        ? TextTable.Get(TextKeys.SourceChanceDay, name, (chance / 100m).ToString("0.##", CultureInfo.InvariantCulture), BackpackSourceRules.FromDay(i))
                        : TextTable.Get(TextKeys.SourceChance, name, (chance / 100m).ToString("0.##", CultureInfo.InvariantCulture)));
                }
                string chancesText = rows.Count == 0 ? TextTable.Get(TextKeys.SourceNone) : string.Join(", ", rows);
                return TextTable.Get(source == BackpackSource.Jackson ? TextKeys.SourceJackson
                    : source == BackpackSource.Dump ? TextKeys.SourceDump : TextKeys.SourceSellers, chancesText);
            }
            catch (Exception error) { Storage.Fault("backpack source description", error); return TextTable.Get(TextKeys.SourceUnavailable); }
        }
        private static ConfigEntry<bool> enabled, toolbox;
        internal static Func<bool> ToolboxEnabled = () => toolbox == null || toolbox.Value;
        internal static Action SellToolbox = () => PlayerStore.Instance.AddDirectSellingItemToTable(ItemFeatureHelper.SetShabby(DirectoryMaster.Item("toolbox")));
        private static readonly Dictionary<string, ConfigEntry<int>> chances = new Dictionary<string, ConfigEntry<int>>();
        internal static Func<bool> Enabled = () => enabled == null || enabled.Value;
        internal static Func<string, int> Scale = key => chances.TryGetValue(key, out ConfigEntry<int> entry) ? entry.Value : 100;
        internal static Func<int, int, int> Random = (min, max) => RNG.GetRandomInt(min, max);
        internal static Func<int> Day = () => StoreStation.Instance.dayCounter;
        internal static Action<string> Sell = id => PlayerStore.Instance.AddDirectSellingItemToTable(id);
        internal static Func<string, GameItem> Create = id => DirectoryMaster.Item(id);
        internal static Func<StoreClient, bool> IsScavenger = client => client.IsTag("SCAV");
        internal static Func<Dialogue, Dialogue> Next = dialogue => (Dialogue)NextField().GetValue(dialogue);
        private static readonly ConditionalWeakTable<StoreClient, object> wrapped = new ConditionalWeakTable<StoreClient, object>();
        [ThreadStatic] internal static bool Visiting;
        [ThreadStatic] internal static bool Placing;

        internal static void Configure(string id)
        {
            ModConfig config = ModConfig.For(id);
            enabled = config.Define("backpack_sources_enabled", true, TextTable.Early(TextKeys.SectionSources), TextTable.Later(TextKeys.OptionBackpackSourcesEnabled),
                TextTable.Later(TextKeys.OptionBackpackSourcesEnabledDesc));
            foreach (string source in new[] { "jackson", "dump", "sellers" })
            {
                string key = "backpack_chance_" + source;
                BackpackSource kind = source == "jackson" ? BackpackSource.Jackson : source == "dump" ? BackpackSource.Dump : BackpackSource.Sellers;
                chances[key] = config.Define(key, 100, TextTable.Early(TextKeys.SectionSources), () => source == "jackson" ? TextTable.Get(TextKeys.OptionBackpackChanceJackson) : source == "dump" ? TextTable.Get(TextKeys.OptionBackpackChanceDump) : TextTable.Get(TextKeys.OptionBackpackChanceSellers), 0, 300,
                    () => Description(kind));
            }
            toolbox = config.Define("toolbox_from_sellers", true, TextTable.Early(TextKeys.SectionSources), TextTable.Later(TextKeys.OptionToolboxFromSellers),
                TextTable.Later(TextKeys.OptionToolboxFromSellersDesc));
        }

        internal static List<string> Choose(BackpackSource source)
        {
            var result = new List<string>();
            if (!BackpacksMod.Running || !Enabled()) return result;
            string key = source == BackpackSource.Jackson ? "jackson" : source == BackpackSource.Dump ? "dump" : "sellers";
            int scale = Scale("backpack_chance_" + key);
            if (scale <= 0) return result;
            int day = source == BackpackSource.Jackson ? Day() : 0;
            bool military = Military.Enabled();
            int cap = source == BackpackSource.Jackson ? 2 : 1;
            for (int i = 0; i < BackpackSourceRules.Items.Length && result.Count < cap; i++)
            {
                int row = source == BackpackSource.Jackson ? BackpackSourceRules.Items.Length - 1 - i : i;
                int chance = BackpackSourceRules.Chance(row, source, scale, day, military);
                if (chance > 0 && Random(1, 10000) <= chance) result.Add(BackpackSourceRules.Items[row]);
            }
            return result;
        }

        internal static void Jackson(List<GameItem> owned)
        {
            Storage.Guard("backpack Jackson offers", () =>
            {
                if (!Placing || Visiting) return;
                foreach (string id in Choose(BackpackSource.Jackson)) Sell(id);
            });
        }

        internal static void Dump(List<GameItem> items)
        {
            Storage.Guard("backpack dump finds", () =>
            {
                if (items == null) return;
                foreach (string id in Choose(BackpackSource.Dump))
                {
                    GameItem item = Create(id);
                    if (item != null) items.Add(item);
                }
            });
        }

        internal static FieldInfo NextField() => AccessTools.Field(typeof(Dialogue), "nextDialogue");
        internal static bool DialogueFits() => NextField()?.FieldType == typeof(Dialogue)
            && AccessTools.Field(typeof(Dialogue), "endAction")?.FieldType == typeof(Action);

        internal static void Wrap(StoreClient client)
        {
            Storage.Guard("backpack seller dialogue", () =>
            {
                if (client == null || wrapped.TryGetValue(client, out _)
                    || !((Enabled() && Scale("backpack_chance_sellers") > 0) || ToolboxEnabled())
                    || (client.clientIntent != StoreClient.ClientIntent.SELL && client.clientIntent != StoreClient.ClientIntent.SELLNBUY)
                    || !IsScavenger(client)) return;
                if (!DialogueFits()) throw new InvalidOperationException("Seller dialogue shape differs");
                var seen = new HashSet<Dialogue>();
                Dialogue last = null;
                for (Dialogue dialogue = client.mainDialogue; dialogue != null; dialogue = Next(dialogue))
                {
                    if (!seen.Add(dialogue)) throw new InvalidOperationException("Seller dialogue cycle");
                    if (dialogue.endAction != null) last = dialogue;
                }
                if (last == null) return;
                Action original = last.endAction;
                bool used = false;
                last.endAction = () =>
                {
                    // Errors in the game's action keep their original behaviour.
                    original();
                    if (used || !BackpacksMod.Running) return;
                    used = true;
                    Storage.Guard("backpack seller offers", () => { foreach (string id in Choose(BackpackSource.Sellers)) Sell(id); });
                    Storage.Guard("toolbox seller offer", () => { if (ToolboxEnabled() && BackpackSourceRules.Toolbox(true, Random(1, 10000))) SellToolbox(); });
                };
                wrapped.Add(client, new object());
            });
        }

        internal static void Sellers(StoreClientManager manager)
        {
            Storage.Guard("backpack sellers", () =>
            {
                if (manager?.clientStack == null) return;
                foreach (StoreClient client in manager.clientStack) Wrap(client);
            });
        }
    }

    [HarmonyPatch]
    internal static class BackpackJacksonPatch
    {
        internal static MethodBase TargetMethod() => AccessTools.Method(typeof(StoreClientList), "PlaceInventorInventory", new[] { typeof(bool) });
        internal static bool Prepare() => TargetMethod() is MethodInfo method && method.IsStatic && method.ReturnType == typeof(void);
        internal static void Prefix(bool isVisitingPlayerStore, out bool[] __state)
        {
            __state = null;
            try
            {
                __state = new[] { BackpackSources.Placing, BackpackSources.Visiting };
                if (!BackpackSources.Enabled()) return;
                BackpackSources.Placing = true;
                BackpackSources.Visiting = isVisitingPlayerStore;
            }
            catch (Exception error)
            {
                BackpackSources.Placing = false;
                Storage.Fault("backpack Jackson visit", error);
            }
        }
        internal static void Finalizer(bool[] __state)
        {
            if (__state == null || __state.Length != 2) return;
            BackpackSources.Placing = __state[0]; BackpackSources.Visiting = __state[1];
        }
    }

    [HarmonyPatch]
    internal static class BackpackDumpPatch
    {
        internal static MethodBase TargetMethod() => AccessTools.Method(typeof(ScavHelper), "GetRandomScavengedItem", new[] { typeof(GameItem).MakeByRefType() });
        internal static bool Prepare() => TargetMethod() is MethodInfo method && method.IsStatic && method.ReturnType == typeof(List<GameItem>)
            && method.GetParameters()[0].IsOut;
        internal static void Postfix(List<GameItem> __result) => BackpackSources.Dump(__result);
    }
}

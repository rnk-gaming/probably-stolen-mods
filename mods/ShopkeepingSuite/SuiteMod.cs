using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopkeepingSuite
{
    public sealed class SuiteMod : IMod
    {
        internal const string Id = "rnk.shopkeeping-suite";
        internal const string Version = "0.7.2";
        public void Init(ModManifest manifest)
        {
            try
            {
                ModConfig config = ModConfig.For(Id);
                foreach (string name in Settings.Keys)
                {
                    string key = name;
                    ConfigEntry<bool> entry = config.Define(key, true, "Shopkeeping Suite", () => Texts.Get(key + "_label"), () => Texts.Get(key + "_description"));
                    Settings.Entries[key] = entry;
                    entry.OnChanged += value => SuiteRuntime.SettingsChanged();
                }
                Settings.Key = config.Define("open_key", "F6", "Shopkeeping Suite", () => Texts.Get("open_key_label"));
                Settings.MarkSize = config.Define("mark_size", 2, "Shopkeeping Suite", () => Texts.Get("mark_size_label"), 1, 3,
                    () => Texts.Get("mark_size_description"));
                Settings.MarkSize.OnChanged += value => Marks.RequestAll();
                Settings.PriceMarginEntry = config.Define("price_margin", 5, "Shopkeeping Suite", () => Texts.Get("price_margin_label"), 1, 100,
                    () => Texts.Get("price_margin_description"));
                Settings.PriceMarginStrongEntry = config.Define("price_margin_strong", 20, "Shopkeeping Suite", () => Texts.Get("price_margin_strong_label"), 1, 300,
                    () => Texts.Get("price_margin_strong_description"));
                Settings.PriceMarginEntry.OnChanged += value => Marks.RequestAll();
                Settings.PriceMarginStrongEntry.OnChanged += value => Marks.RequestAll();
                LocalMods.OptionsMenu.Register(Id, "Off the Books: Shopkeeping Suite", Fault);
                GameOptionRegistry.GetOrAddSection(LocalMods.OptionsRules.SectionId(Id, "Shopkeeping Suite"), () => Texts.Get("title"), Id).label = () => Texts.Get("title");
                ModHelper.AddGameOptionKeyBind(Id, ModHelper.OPTION_SECTION_KEYBIND, "open_key",
                    () => Texts.Get("open_key_label"), Settings.KeyCode,
                    value => { try { Settings.Key.Set(value.ToString()); } catch (Exception error) { Fault("key setting", error); } },
                    () => Texts.Get("open_key_description"));
            }
            catch (Exception error) { Fault("settings", error); }
        }
        public void OnEnable()
        {
            try
            {
                Shapes.Apply();
                ModHook.OnCreateTooltipLate -= SuiteRuntime.Tooltip;
                ModHook.OnGameLoadedEarly -= SuiteRuntime.Loaded;
                ModHook.OnShutterOpenedLate -= SuiteRuntime.Opened;
                ModHook.OnShutterClosedLate -= SuiteRuntime.Closed;
                ModHook.OnModAssetsLoaded -= SuiteRuntime.AssetsLoaded;
                ModHook.OnCreateTooltipLate += SuiteRuntime.Tooltip;
                ModHook.OnGameLoadedEarly += SuiteRuntime.Loaded;
                ModHook.OnShutterOpenedLate += SuiteRuntime.Opened;
                ModHook.OnShutterClosedLate += SuiteRuntime.Closed;
                ModHook.OnModAssetsLoaded += SuiteRuntime.AssetsLoaded;
                SuiteRuntime.Active = true;
                var status = new List<string>();
                foreach (string key in Settings.Keys) status.Add(key + "=" + (Settings.Disabled.Contains(key) ? "unavailable" : Settings.On(key) ? "on" : "off"));
                Log("[Shopkeeping Suite] loaded " + Version + ": " + string.Join(", ", status) + "; price_margin=" + Settings.PriceMargin() + "; price_margin_strong=" + Settings.PriceMarginStrong() + "; unavailable targets: " + string.Join(", ", Shapes.Unavailable));
            }
            catch (Exception error) { Fault("enabling", error); }
            SuiteRuntime.TryStartLoop(false);
        }
        public void OnDisable()
        {
            Counterfeit.Reset();
            try { SuiteRuntime.StopLoop(); }
            catch (Exception error) { Fault("stopping", error); }
            try
            {
                ModHook.OnCreateTooltipLate -= SuiteRuntime.Tooltip;
                ModHook.OnGameLoadedEarly -= SuiteRuntime.Loaded;
                ModHook.OnShutterOpenedLate -= SuiteRuntime.Opened;
                ModHook.OnShutterClosedLate -= SuiteRuntime.Closed;
                ModHook.OnModAssetsLoaded -= SuiteRuntime.AssetsLoaded;
                Shapes.Unpatch(); SuiteWindow.Close(); Marks.Reset(); ModHelper.RemoveGameOptions(Id);
            }
            catch (Exception error) { Fault("disabling", error); }
        }
        internal static Action<string> Log = message => Debug.LogWarning(message);
        static readonly HashSet<string> reported = new HashSet<string>();
        internal static void Fault(string where, Exception error)
        {
            try { if (reported.Add(where)) Log("[Shopkeeping Suite] " + where + ": " + error.Message); }
            catch (Exception) { }
        }
    }
    internal static class Settings
    {
        internal static readonly string[] Keys = { "enabled", "hands_home", "mark_same_kind", "mark_risk", "mark_inspection", "mark_wanted", "mark_wanted_inside", "mark_price", "tooltip_price", "tooltip_wanted", "tooltip_risk", "tooltip_appraisal", "appraisal_fill", "certificate_first", "counterfeit_check", "reputation_feed", "tablet_feed", "standing_tally", "ledger", "window", "paper_window", "ledger_at_closing" };
        internal static readonly string[] PriceKeys = { "mark_price", "tooltip_price", "price_margin", "price_margin_strong" };
        internal static readonly Dictionary<string, ConfigEntry<bool>> Entries = new Dictionary<string, ConfigEntry<bool>>();
        internal static readonly HashSet<string> Disabled = new HashSet<string>();
        internal static ConfigEntry<string> Key;
        internal static ConfigEntry<int> MarkSize;
        internal static ConfigEntry<int> PriceMarginEntry, PriceMarginStrongEntry;
        internal static Func<int> PriceMargin = () => PriceMarginEntry?.Value ?? 5;
        internal static Func<int> PriceMarginStrong = () => PriceMarginStrongEntry?.Value ?? 20;
        internal static Func<int> Size = () => MarkSize?.Value ?? 2;
        internal static Func<string, bool> Get = key => !Entries.TryGetValue(key, out var entry) || entry.Value;
        internal static bool On(string key) => !Disabled.Contains(key) && Get(key);
        internal static KeyCode ParseKey(string text) => Enum.TryParse(text, out KeyCode key) && Enum.IsDefined(typeof(KeyCode), key) && key != UnityEngine.KeyCode.None ? key : UnityEngine.KeyCode.F6;
        internal static KeyCode KeyCode() => ParseKey(Key?.Value);
    }
}

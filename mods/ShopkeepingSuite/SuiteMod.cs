using LocalMods;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShopkeepingSuite
{
    public sealed class SuiteMod : IMod
    {
        internal const string Id = "rnk.shopkeeping-suite";
        internal const string Version = "0.9.0";
        public void Init(ModManifest manifest)
        {
            try
            {
                TextTable.Start(manifest.ID, Fault);
                // Mods it does not go together with: add one list line and two rows in en.csv.
                Incompatible.Register(manifest.ID, "[Shopkeeping Suite]", new IncompatibleMod[]
                {
                    new IncompatibleMod("loyce_shop_assist", "shop_assist"),
                }, NoteFault);
                ModConfig config = ModConfig.For(Id);
                string section = TextTable.Early(TextKeys.SectionShopkeepingSuite);
                (string Key, string Label, string Tip)[] options =
                {
                    ("enabled", TextKeys.EnabledLabel, TextKeys.EnabledDescription),
                    ("hands_home", TextKeys.HandsHomeLabel, TextKeys.HandsHomeDescription),
                    ("mark_same_kind", TextKeys.MarkSameKindLabel, TextKeys.MarkSameKindDescription),
                    ("mark_risk", TextKeys.MarkRiskLabel, TextKeys.MarkRiskDescription),
                    ("mark_inspection", TextKeys.MarkInspectionLabel, TextKeys.MarkInspectionDescription),
                    ("mark_wanted", TextKeys.MarkWantedLabel, TextKeys.MarkWantedDescription),
                    ("mark_wanted_inside", TextKeys.MarkWantedInsideLabel, TextKeys.MarkWantedInsideDescription),
                    ("mark_price", TextKeys.MarkPriceLabel, TextKeys.MarkPriceDescription),
                    ("tooltip_price", TextKeys.TooltipPriceLabel, TextKeys.TooltipPriceDescription),
                    ("tooltip_wanted", TextKeys.TooltipWantedLabel, TextKeys.TooltipWantedDescription),
                    ("tooltip_risk", TextKeys.TooltipRiskLabel, TextKeys.TooltipRiskDescription),
                    ("tooltip_appraisal", TextKeys.TooltipAppraisalLabel, TextKeys.TooltipAppraisalDescription),
                    ("appraisal_fill", TextKeys.AppraisalFillLabel, TextKeys.AppraisalFillDescription),
                    ("certificate_first", TextKeys.CertificateFirstLabel, TextKeys.CertificateFirstDescription),
                    ("counterfeit_check", TextKeys.CounterfeitCheckLabel, TextKeys.CounterfeitCheckDescription),
                    ("reputation_feed", TextKeys.ReputationFeedLabel, TextKeys.ReputationFeedDescription),
                    ("tablet_feed", TextKeys.TabletFeedLabel, TextKeys.TabletFeedDescription),
                    ("standing_tally", TextKeys.StandingTallyLabel, TextKeys.StandingTallyDescription),
                    ("ledger", TextKeys.LedgerLabel, TextKeys.LedgerDescription),
                    ("window", TextKeys.WindowLabel, TextKeys.WindowDescription),
                    ("paper_window", TextKeys.PaperWindowLabel, TextKeys.PaperWindowDescription),
                    ("ledger_at_closing", TextKeys.LedgerAtClosingLabel, TextKeys.LedgerAtClosingDescription),
                };
                foreach (var option in options)
                {
                    string key = option.Key;
                    ConfigEntry<bool> entry = config.Define(key, true, section, TextTable.Later(option.Label), TextTable.Later(option.Tip));
                    Settings.Entries[key] = entry;
                    entry.OnChanged += value => SuiteRuntime.SettingsChanged();
                }
                Settings.Key = config.Define("open_key", "F6", section, TextTable.Later(TextKeys.OpenKeyLabel));
                Settings.MarkSize = config.Define("mark_size", 2, section, TextTable.Later(TextKeys.MarkSizeLabel), 1, 3,
                    TextTable.Later(TextKeys.MarkSizeDescription));
                Settings.MarkSize.OnChanged += value => Marks.RequestAll();
                Settings.PriceMarginEntry = config.Define("price_margin", 5, section, TextTable.Later(TextKeys.PriceMarginLabel), 1, 100,
                    TextTable.Later(TextKeys.PriceMarginDescription));
                Settings.PriceMarginStrongEntry = config.Define("price_margin_strong", 20, section, TextTable.Later(TextKeys.PriceMarginStrongLabel), 1, 300,
                    TextTable.Later(TextKeys.PriceMarginStrongDescription));
                Settings.PriceMarginEntry.OnChanged += value => Marks.RequestAll();
                Settings.PriceMarginStrongEntry.OnChanged += value => Marks.RequestAll();
                LocalMods.OptionsMenu.Register(Id, TextTable.Early(TextKeys.Title), Fault);
                ModHelper.AddGameOptionKeyBind(Id, ModHelper.OPTION_SECTION_KEYBIND, "open_key",
                    TextTable.Later(TextKeys.OpenKeyLabel), Settings.KeyCode,
                    value => { try { Settings.Key.Set(value.ToString()); } catch (Exception error) { Fault("key setting", error); } },
                    TextTable.Later(TextKeys.OpenKeyDescription));
            }
            catch (Exception error) { Fault("settings", error); }
        }
        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Shopkeeping Suite] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
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
            Incompatible.Disable();
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

using LocalMods;
using System;

namespace StorageUpgrades
{
    public class StorageUpgradesMod : IMod
    {
        internal const string Version = "1.5.0";
        internal static bool Running;

        public void Init(ModManifest manifest)
        {
            Storage.Prefix = "[Storage Upgrades]";
            TextTable.Start(manifest.ID, TextFault);
            // Mods it does not go together with: add one list line and two rows in en.csv.
            Incompatible.Register(manifest.ID, "[Storage Upgrades]", new IncompatibleMod[]
            {
                new IncompatibleMod("xiaowo_trade_perks", "trade_perks"),
            }, NoteFault);
            Storage.Guard("storage version", () => Storage.Log?.Invoke(Storage.Prefix + " v" + Version));
            Storage.Guard("storage settings", () => Storage.Configure(manifest.ID));
            OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Storage.Fault);
            ModHook.OnCreateTooltipLate += AddTooltip;
        }

        // TextTable reports each text fault once; keep gameplay fault counts untouched.
        private static void TextFault(string where, Exception error) =>
            Storage.Log?.Invoke(Storage.Prefix + " a fault in " + where + "; using text fallback. " + error.GetType().Name + ": " + error.Message);

        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Storage Upgrades] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
            Running = true;
            Storage.Guard("enabling Red Imp Beer Case picture", BeerCaseSprite.Install);
            Storage.Guard("enabling bay trade", () => { BayTrade.Install(); BayTrade.Active = true; });
            Storage.Guard("registering removal list", () => RemovalList.Register("rnk.storage-upgrades", TextTable.Early(TextKeys.OptionsTitle), TextTable.Early(TextKeys.SectionStorage), Removal.Describe));
        }
        public void OnDisable()
        {
            Incompatible.Disable();
            Running = false;
            Storage.Guard("disabling Red Imp Beer Case picture", BeerCaseSprite.Disable);
            Storage.Guard("disabling bay trade", BayTrade.Disable);
            Storage.Guard("removing tooltip hook", () => ModHook.OnCreateTooltipLate -= AddTooltip);
            Storage.Guard("disabling removal list", RemovalList.Disable);
        }

        private static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            Storage.AddTooltip(builder, item);
            MachineStorage.AddTooltip(builder, item);
        }
    }
}

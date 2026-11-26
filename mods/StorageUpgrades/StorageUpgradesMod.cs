using LocalMods;

namespace StorageUpgrades
{
    public class StorageUpgradesMod : IMod
    {
        internal const string Version = "1.3.4";
        internal static bool Running;

        public void Init(ModManifest manifest)
        {
            Storage.Prefix = "[Storage Upgrades]";
            Storage.Guard("storage version", () => Storage.Log?.Invoke(Storage.Prefix + " v" + Version));
            Storage.Guard("storage settings", () => Storage.Configure(manifest.ID));
            OptionsMenu.Register(manifest.ID, "Off the Books: Storage Upgrades", Storage.Fault);
            ModHook.OnCreateTooltipLate += AddTooltip;
        }

        public void OnEnable()
        {
            Running = true;
            Storage.Guard("enabling beer case picture", BeerCaseSprite.Install);
            Storage.Guard("enabling bay trade", () => { BayTrade.Install(); BayTrade.Active = true; });
            Storage.Guard("registering removal list", () => RemovalList.Register("rnk.storage-upgrades", "Off the Books: Storage Upgrades", "Storage", Removal.Describe));
        }
        public void OnDisable()
        {
            Running = false;
            Storage.Guard("disabling beer case picture", BeerCaseSprite.Disable);
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

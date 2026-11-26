using LocalMods;

namespace Backpacks
{
    public class BackpacksMod : IMod
    {
        internal const string Version = "1.6.0";
        internal static bool Running;
        public void Init(ModManifest manifest)
        {
            Storage.Prefix = "[Backpacks]";
            Storage.Guard("backpack version", () => Storage.Log?.Invoke(Storage.Prefix + " v" + Version));
            Storage.Guard("backpack settings", () => Backpacks.Configure(manifest.ID));
            Storage.Guard("own pack settings", () => Military.Configure(manifest.ID));
            Storage.Guard("starting perk settings", () => BackpackPerks.Configure(manifest.ID));
            Military.Subscribe();
            DebugTools.Register(manifest.ID);
            Storage.Guard("backpack source settings", () => BackpackSources.Configure(manifest.ID));
            ModHook.OnPlaceInventorInventoryItemLate += BackpackSources.Jackson;
            ModHook.OnGenerateCustomerVeryLate += BackpackSources.Sellers;
            OptionsMenu.Register(manifest.ID, "Off the Books: Backpacks", Storage.Fault);
            ModHook.OnCreateTooltipLate += AddTooltip;
        }

        public void OnEnable()
        {
            Running = true;
            TabHover.Enable();
            ModHook.OnModItemDirectoryInit -= MilitaryItems.Register;
            ModHook.OnModItemDirectoryInit += MilitaryItems.Register;
            ModHook.OnCreateTooltipLate -= AddTooltip;
            ModHook.OnCreateTooltipLate += AddTooltip;
            ModHook.OnPlaceInventorInventoryItemLate -= BackpackSources.Jackson;
            ModHook.OnPlaceInventorInventoryItemLate += BackpackSources.Jackson;
            ModHook.OnGenerateCustomerVeryLate -= BackpackSources.Sellers;
            ModHook.OnGenerateCustomerVeryLate += BackpackSources.Sellers;
            BackpackPerks.Enable();
            RemovalList.Register(Texts.ModId, "Off the Books: Backpacks", "Backpacks", Removal.Describe);
        }

        public void OnDisable()
        {
            Running = false;
            UpgradeView.Disable();
            TabHover.Disable();
            ModHook.OnModItemDirectoryInit -= MilitaryItems.Register;
            BackpackPerks.Disable();
            RemovalList.Disable();
            ModHook.OnCreateTooltipLate -= AddTooltip;
            ModHook.OnPlaceInventorInventoryItemLate -= BackpackSources.Jackson;
            ModHook.OnGenerateCustomerVeryLate -= BackpackSources.Sellers;
        }

        private static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            Backpacks.AddTooltip(builder, item);
            Pockets.AddTooltip(builder, item);
        }
    }
}

using LocalMods;
using System;

namespace Backpacks
{
    public class BackpacksMod : IMod
    {
        internal const string Version = "1.8.0";
        internal static bool Running;
        public void Init(ModManifest manifest)
        {
            Storage.Prefix = "[Backpacks]";
            TextTable.Start(manifest.ID, TextFault);
            // Mods it does not go together with: add one list line and two rows in en.csv.
            Incompatible.Register(manifest.ID, "[Backpacks]", new IncompatibleMod[]
            {
                // No confirmed conflicts yet.
            }, NoteFault);
            Storage.Guard("backpack version", () => Storage.Log?.Invoke(Storage.Prefix + " v" + Version));
            Storage.Guard("backpack settings", () => Backpacks.Configure(manifest.ID));
            Storage.Guard("own pack settings", () => Military.Configure(manifest.ID));
            Storage.Guard("starting perk settings", () => BackpackPerks.Configure(manifest.ID));
            Military.Subscribe();
            DebugTools.Register(manifest.ID);
            Storage.Guard("backpack source settings", () => BackpackSources.Configure(manifest.ID));
            ModHook.OnPlaceInventorInventoryItemLate += BackpackSources.Jackson;
            ModHook.OnGenerateCustomerVeryLate += BackpackSources.Sellers;
            OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Storage.Fault);
            ModHook.OnCreateTooltipLate += AddTooltip;
        }

        // TextTable reports each text fault once; keep gameplay fault counts untouched.
        private static void TextFault(string where, Exception error) =>
            Storage.Log?.Invoke(Storage.Prefix + " a fault in " + where + "; using text fallback. " + error.GetType().Name + ": " + error.Message);

        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Backpacks] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
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
            RemovalList.Register(Texts.ModId, TextTable.Early(TextKeys.OptionsTitle), TextTable.Early(TextKeys.SectionBackpacks), Removal.Describe);
        }

        public void OnDisable()
        {
            Incompatible.Disable();
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

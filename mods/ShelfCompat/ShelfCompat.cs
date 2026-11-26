using System;

namespace ShelfCompat
{
    public class ShelfCompatMod : IMod
    {
        internal const string Id = "rnk.shelf-compat";
        internal const string Version = "1.1.4";
        internal static ConfigEntry<bool> Enabled, WaterExpanded, BackRoom;

        public void Init(ModManifest manifest)
        {
            try
            {
                ModConfig config = ModConfig.For(manifest.ID);
                Enabled = config.Define("enabled", true, "Water Expanded Compatibility",
                    () => Text("option_enabled", "Water Expanded Compatibility enabled"),
                    () => Text("option_enabled_desc", "Allow the selected devices to work on Shop Expansion's shelves and in the back room."));
                WaterExpanded = config.Define("water_expanded", true, "Water Expanded Compatibility",
                    () => Text("option_water_expanded", "Water Expanded works on the shelves"),
                    () => Text("option_water_expanded_desc", "Water Expanded's faucet, pipes, bottler and bridge only work in the shop's own grid. With this on they also work on Shop Expansion's shelves, also inside a Machine Bay that stands on a shelf. Does nothing unless both mods are running."));
                BackRoom = config.Define("back_room", true, "Water Expanded Compatibility",
                    () => Text("option_back_room", "Water Expanded works in the back room"),
                    () => Text("option_back_room_desc", "Allow Water Expanded's devices to work in the back room, also inside containers. Needs only Water Expanded."));
                LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Water Expanded Compatibility", Bridge.Fault);
            }
            catch (Exception error) { Bridge.Fault("settings", error); }
        }

        private static string Text(string key, string fallback)
        {
            try
            {
                string text = ModHelper.GetLocalized(Id, key);
                return string.IsNullOrEmpty(text) || text == key ? fallback : text;
            }
            catch (Exception) { return fallback; }
        }

        public void OnEnable()
        {
            Bridge.Begin();
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
        }

        public void OnDisable()
        {
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            Bridge.Stop();
        }

        private static void OnModAssetsLoaded(LoadedMod mod) => Bridge.Start();
    }
}

using System;
using LocalMods;

namespace ShelfCompat
{
    public class ShelfCompatMod : IMod
    {
        internal const string Id = "rnk.shelf-compat";
        internal const string Version = "1.3.0";
        // Retained settings and patch code are historical; the farewell build never activates them.
        internal static ConfigEntry<bool> Enabled, WaterExpanded, BackRoom;
        private const string RetirementNote = "news/retired";
        private static bool retirementLogged;

        public void Init(ModManifest manifest)
        {
            TextTable.Start(manifest.ID, TextFault);
            // Mods it does not go together with: add one list line and two rows in en.csv.
            Incompatible.Register(manifest.ID, "[Shelf Compat]", new IncompatibleMod[]
            {
                // No confirmed conflicts yet.
            }, NoteFault);
            Notes.Register(new NoteDefinition(RetirementNote, TextKeys.RetiredTitle, TextKeys.RetiredBody,
                context => context.Record(RetirementNote).Delivered ? null : new NotePlan()));
            try
            {
                ModConfig config = ModConfig.For(manifest.ID);
                Enabled = config.Define("enabled", true, TextTable.Early(TextKeys.SectionShelfcompat),
                    TextTable.Later(TextKeys.OptionEnabled),
                    TextTable.Later(TextKeys.OptionEnabledDesc));
                WaterExpanded = config.Define("water_expanded", true, TextTable.Early(TextKeys.SectionShelfcompat),
                    TextTable.Later(TextKeys.OptionWaterExpanded),
                    TextTable.Later(TextKeys.OptionWaterExpandedDesc));
                BackRoom = config.Define("back_room", true, TextTable.Early(TextKeys.SectionShelfcompat),
                    TextTable.Later(TextKeys.OptionBackRoom),
                    TextTable.Later(TextKeys.OptionBackRoomDesc));
                OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Bridge.Fault);
            }
            catch (Exception error) { Bridge.Fault("settings", error); }
        }

        // TextTable reports each text fault once without consuming Bridge's fault report.
        private static void TextFault(string where, Exception error) =>
            Bridge.Log?.Invoke("[Shelf Compat] a fault in " + where + "; using text fallback. " + error.GetType().Name + ": " + error.Message);

        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Shelf Compat] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
            if (retirementLogged) return;
            retirementLogged = true;
            try { Bridge.Log?.Invoke("[Shelf Compat] retired; does nothing. Water Expanded 1.2.0 or later supports Shop Expansion's shelves itself."); }
            catch (Exception) { } // A failed logger must not throw into the loader.
        }

        public void OnDisable() => Incompatible.Disable();
    }
}

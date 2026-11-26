using System;
using System.Collections.Generic;

namespace Backpacks
{
    internal static class Texts
    {
        internal const string ModId = "rnk.backpacks";
        internal static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "tab_hover_name", "Compartment tabs take dragged items" },
            { "tab_hover_description", "Hold a dragged item or group over a tab for 0.35 seconds to open that compartment. Let one item or a group go on a tab to put the items there, wherever the backpack is shown, including when it sits in a slot. Anything that does not fit stays where it was. Applies to every tab strip, including backpacks and storage bays." },
            { "scavengers_pack_name", "Scavenger's Pack" },
            { "haulers_pack_name", "Hauler's Pack" },
            { "scavengers_pack_description", "A pack stitched together from salvage. Plain, tough and roomy." },
            { "haulers_pack_description", "A tall frame pack, built to carry a night's haul home." },
            { "perk_courier_name", "Seasoned Courier" },
            { "perk_courier_description", "Start with a Scavenger's Pack that already has an extra column, a second compartment, stacks of two and a toolbox slot." },
            { "perk_quartermaster_name", "Quartermaster's Issue" },
            { "perk_quartermaster_description", "Start with a fully upgraded Hauler's Pack." }
        };
        internal static Func<string, string> Lookup = key => ModHelper.GetLocalized(ModId, key);
        internal static string ItemName(string id, string fallback)
        {
            try
            {
                string key = "item_" + id + "_name";
                string value = LocHelper.GetLocalizedItem(key);
                if (!string.IsNullOrEmpty(value) && value != key) return value;
            }
            catch (Exception error) { LocalMods.Storage.Fault("backpack item localisation", error); }
            return fallback;
        }
        internal static string Get(string key)
        {
            string value = null;
            try { value = Lookup(key); }
            catch (Exception error) { LocalMods.Storage.Fault("backpack localisation", error); }
            return string.IsNullOrEmpty(value) || value == key ? English[key] : value;
        }
    }
}

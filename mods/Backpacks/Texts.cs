using System;
using LocalMods;

namespace Backpacks
{
    internal static class Texts
    {
        internal const string ModId = "rnk.backpacks";
        internal static string ItemName(string id, string fallback)
        {
            try
            {
                string key = "item_" + id + "_name";
                string value = LocHelper.GetLocalizedItem(key);
                if (!string.IsNullOrEmpty(value) && value != key) return value;
            }
            catch (Exception error) { Storage.Fault("backpack item localisation", error); }
            return fallback;
        }
    }
}

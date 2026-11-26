using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    internal static class EnglishTable
    {
        internal const string Header = "# Off the Books: Station Economy Compat, English additions";
        internal static string Build(IDictionary<string, string> rows, out int count)
        {
            var ordered = new SortedDictionary<string, string>(rows, StringComparer.Ordinal);
            var last = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = new List<KeyValuePair<string, string>>();
            foreach (var row in ordered)
            {
                string value = row.Value;
                int separator = value == null ? -1 : value.IndexOf('\u241e');
                if (separator <= 0) continue;
                string chinese = value.Substring(0, separator);
                if (chinese[0] == '#' || string.IsNullOrWhiteSpace(value.Substring(separator + 1))) continue;
                bool hasChinese = false;
                foreach (char c in chinese) if (c >= '\u3400' && c <= '\u9fff') { hasChinese = true; break; }
                if (!hasChinese) continue;
                last[chinese] = row.Key;
                lines.Add(new KeyValuePair<string, string>(row.Key, chinese));
            }
            var block = new StringBuilder(Header);
            count = 0;
            foreach (var line in lines)
            {
                if (last[line.Value] != line.Key) continue;
                string value = ordered[line.Key];
                block.Append('\n').Append(Escape(line.Value)).Append('\t').Append(Escape(value.Substring(value.IndexOf('\u241e') + 1)));
                count++;
            }
            return block.ToString();
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r");
    }

    internal static class EnglishAdditions
    {
        internal const string ModId = "rnk.station-economy-compat", TableName = "station_economy_additions";
        internal const string DataType = "StationEconomy.LocData", LocType = "StationEconomy.Loc";
        internal const string PartsName = "Parts", LoadName = "Load", LoadedName = "loaded", CacheName = "cache";
        internal static bool Active;
        internal static Func<bool> On = () => StationEconomyCompatMod.English == null || StationEconomyCompatMod.English.Value;
        internal static Func<IDictionary<string, string>> Rows = ReadRows;
        internal static Func<List<LoadedMod>> Mods = () => ModLoader.Mods;
        internal static Func<FieldInfo> FindParts = () => AccessTools.Field(AccessTools.TypeByName(DataType), PartsName);
        internal static Func<FieldInfo> FindLoaded = () => AccessTools.Field(AccessTools.TypeByName(LocType), LoadedName);
        internal static Func<FieldInfo> FindCache = () => AccessTools.Field(AccessTools.TypeByName(LocType), CacheName);
        internal static Func<MethodInfo> FindLoad = () => AccessTools.Method(AccessTools.TypeByName(LocType), LoadName, Type.EmptyTypes);
        private static bool installed, shapeSaid;
        private static string[] installedParts;
        private static int lastIndex;
        private static string original, addition;
        private static FieldInfo loadedField;
        private static Dictionary<string, string> translatorCache;

        internal static IDictionary<string, string> ReadRows()
        {
            foreach (LoadedMod mod in Mods())
                if (mod?.Manifest?.ID == ModId && mod.Localization != null && mod.Localization.TryGetValue(TableName, out var rows)) return rows;
            return null;
        }

        // Metadata only: the offline real-DLL check must not initialise either type.
        internal static bool Fits(FieldInfo parts, MethodInfo load, FieldInfo loaded, FieldInfo cache) =>
            parts != null && parts.IsStatic && parts.FieldType == typeof(string[])
            && load != null && load.IsStatic && !load.ContainsGenericParameters && load.ReturnType == typeof(bool) && load.GetParameters().Length == 0
            && loaded != null && loaded.IsStatic && !loaded.IsInitOnly && !loaded.IsLiteral && loaded.FieldType == typeof(bool)
            && cache != null && cache.IsStatic && cache.FieldType == typeof(Dictionary<string, string>);

        private static void SayShape()
        {
            if (shapeSaid) return;
            shapeSaid = true;
            Bridge.Log("[Station Economy Compat] Station Economy's English table has changed shape; it is left as it is.");
        }

        internal static void SettingChanged(bool value) => TryInstall();

        internal static void TryInstall()
        {
            try
            {
                if (!Active || !On()) { Restore(); return; }
                if (installed) return;
                IDictionary<string, string> rows = Rows();
                if (rows == null || rows.Count == 0) return;
                string block = EnglishTable.Build(rows, out int count);
                if (count == 0) return;
                FieldInfo parts = FindParts(), loaded = FindLoaded(), cache = FindCache();
                MethodInfo load = FindLoad();
                if (parts == null && load == null && loaded == null && cache == null) return; // Not loaded yet.
                if (!Fits(parts, load, loaded, cache)) { SayShape(); return; }
                var values = (string[])parts.GetValue(null);
                var answers = (Dictionary<string, string>)cache.GetValue(null);
                if (values == null || values.Length == 0 || answers == null) { SayShape(); return; }
                bool wasLoaded = (bool)loaded.GetValue(null);
                // Remember everything needed to undo before the first write. Load is never called.
                installedParts = values; lastIndex = values.Length - 1;
                original = values[lastIndex]; addition = original + "\n" + block;
                loadedField = loaded; translatorCache = answers; installed = true;
                values[lastIndex] = addition;
                if (wasLoaded) Invalidate();
                Bridge.Log("[Station Economy Compat] added " + count + " English lines to Station Economy's table.");
            }
            catch (Exception error)
            {
                try { Restore(); } catch (Exception rollback) { Bridge.Fault("restoring the English table", rollback); }
                Bridge.Fault("adding English", error);
            }
        }

        private static void Invalidate()
        {
            loadedField.SetValue(null, false);
            translatorCache.Clear();
        }

        private static void Restore()
        {
            if (!installed) return;
            // Another writer owns its replacement, even if it has the same text.
            if (ReferenceEquals(installedParts[lastIndex], addition)) installedParts[lastIndex] = original;
            try { Invalidate(); }
            finally
            {
                installed = false; installedParts = null; loadedField = null; translatorCache = null; original = null; addition = null;
            }
        }

        internal static void Disable()
        {
            Active = false;
            try { Restore(); }
            catch (Exception error) { Bridge.Fault("restoring the English table", error); }
        }
    }
}

// Every text a player sees comes from the mod's Localization/<language>.csv, by key.
// Compiled separately into each mod: the state below belongs to one mod.
//
//   TextTable.Start(manifest.ID, report)   first thing in Init
//   TextTable.Early("section_x")           a plain string that is needed during Init
//   TextTable.Later("option_x")            a Func<string> for labels and tooltips
//   TextTable.Get("tooltip_x", 3)          any text once the game runs
//
// The game reads the tables only after every mod's Init, and its scanner reports a mod
// that reads files itself. So the build puts a copy of each dist/Localization/*.csv into
// the DLL (see the project file), and Start reads that copy with the game's parsing rules:
// en.csv, and the start-up language over it for Early. Get asks the game first, which
// reads the files in the mod's folder and follows the language selected now; when the
// game has no text for the key it uses the built-in English, never a built-in
// translation, because the language may have changed since the start. A key with no
// line anywhere is returned as it is, like the game does. Nothing here throws.
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace LocalMods
{
    internal static class TextTable
    {
        internal const string English = "en";
        internal static Func<string, string, string> Game = (id, key) => ModHelper.GetLocalized(id, key);
        internal static Func<string> Locale = () => LocHelper.ResolveStartupLocale();
        // The lines of the built-in copy of Localization/<language>.csv, or null without one.
        internal static Func<string, string[]> Lines = BuiltIn;

        // The built-in English, and the same with the start-up language's lines over it.
        private static readonly Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> own = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> reported = new HashSet<string>();
        private static string modId;
        private static Action<string, Exception> report;

        internal static void Start(string id, Action<string, Exception> faults)
        {
            try
            {
                modId = id;
                report = faults;
                english.Clear();
                own.Clear();
                if (!Load(English, english))
                    Fault("texts: reading the built-in " + Name(English), new FileNotFoundException("The DLL was built without this table."));
                foreach (KeyValuePair<string, string> row in english) own[row.Key] = row.Value;
                string locale = null;
                try { locale = Locale(); }
                catch (Exception error) { Fault("texts: reading the game's language", error); }
                if (Plain(locale) && !string.Equals(locale, English, StringComparison.OrdinalIgnoreCase)) Load(locale, own);
            }
            catch (Exception error) { Fault("texts: starting", error); }
        }

        // For Init: the built-in copy only, in the start-up language where it has a line and
        // in English otherwise. The game has no table yet and would log a warning.
        internal static string Early(string key, params object[] args)
        {
            try
            {
                if (key == null) return "";
                if (own.TryGetValue(key, out string text)) return Format(key, text, args);
                Fault("text '" + key + "'", new KeyNotFoundException("No line with this key in the built-in " + Name(English) + "."));
                return key;
            }
            catch (Exception) { return key ?? ""; }
        }

        internal static string Get(string key, params object[] args)
        {
            try
            {
                if (key == null) return "";
                string text = null;
                try { if (modId != null) text = Game(modId, key); }
                catch (Exception error) { Fault("texts: asking the game", error); }
                // Nothing from the game: no line in the language selected now or in English, or
                // an empty one. English, whatever language the game started in.
                if (string.IsNullOrEmpty(text) || text == key)
                    if (!english.TryGetValue(key, out text)) return key;
                return Format(key, text, args);
            }
            catch (Exception) { return key ?? ""; }
        }

        internal static Func<string> Later(string key, params object[] args) => () => Get(key, args);

        private static string Format(string key, string text, object[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(CultureInfo.InvariantCulture, text, args); }
            catch (Exception error) { Fault("formatting text '" + key + "'", error); return text; }
        }

        // A language code names a table: letters, digits, '-' and '_' only.
        internal static bool Plain(string locale)
        {
            if (string.IsNullOrEmpty(locale) || locale.Length > 16) return false;
            foreach (char c in locale)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return false;
            return true;
        }

        internal static string Name(string locale) => "Localization/" + locale + ".csv";

        private static string[] BuiltIn(string locale)
        {
            using (Stream stream = typeof(TextTable).Assembly.GetManifestResourceStream(Name(locale)))
            {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd().Split('\n');
            }
        }

        // Whether there was a table. Lines of a later table replace those of an earlier
        // one, except that an empty translation leaves the earlier text standing.
        private static bool Load(string locale, Dictionary<string, string> into)
        {
            try
            {
                string[] lines = Lines(locale);
                if (lines == null) return false;
                foreach (KeyValuePair<string, string> row in Parse(lines))
                    if (row.Value.Length > 0 || !into.ContainsKey(row.Key)) into[row.Key] = row.Value;
                return true;
            }
            catch (Exception error) { Fault("texts: reading the built-in " + Name(locale), error); return true; }
        }

        // The game's rules for a table: one "key,value" per line; blank lines and lines that
        // start with '#' are skipped; the key is the trimmed text before the first comma; the
        // value is trimmed, loses surrounding double quotes ("" inside them is one quote),
        // and \n in it is a line break. A line without a comma or without a key is skipped.
        internal static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines == null) return rows;
            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.Trim().TrimStart(ByteOrderMark).TrimStart();
                if (line.Length == 0 || line[0] == '#') continue;
                int comma = line.IndexOf(',');
                if (comma <= 0) continue;
                string key = line.Substring(0, comma).Trim();
                if (key.Length == 0) continue;
                string value = line.Substring(comma + 1).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
                rows[key] = value.Replace("\\n", "\n");
            }
            return rows;
        }

        // A file saved as "UTF-8 with signature" starts with this character.
        private const char ByteOrderMark = (char)0xFEFF;

        private static void Fault(string where, Exception error)
        {
            try { if (reported.Add(where)) report?.Invoke(where, error); }
            catch (Exception) { }
        }
    }
}

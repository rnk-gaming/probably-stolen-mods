using System;
using System.Text;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    internal static class EnglishNameRules
    {
        // U+9A6C: default family name (Ma); U+6770 and U+514B: default given name (Jake).
        internal const string DefaultSur = "\u9a6c", DefaultGiv = "\u6770\u514b";
        // U+8001: old; U+677F: board; together: boss. U+7237: lord.
        // U+5148: first, U+751F: born; together: gentleman. U+4E1C: east, U+5BB6: home; together: proprietor.
        internal const string Old = "\u8001", Boss = "\u8001\u677f", Lord = "\u7237", Gentleman = "\u5148\u751f", Proprietor = "\u4e1c\u5bb6";
        internal static readonly string[] DefaultForms = { DefaultSur + DefaultGiv, DefaultSur + Boss, DefaultSur + Boss, DefaultSur + Boss, DefaultSur + Boss, Old + DefaultSur };
        internal static readonly string[] EnglishForms = { "Jake Ma", "Boss Ma", "Mr. Ma", "Old Ma", "Ma" };
        private static readonly string[] ChineseForms = { DefaultSur + DefaultGiv, DefaultSur + Boss, Old + DefaultSur };

        internal static string ToDefault(string text, ShopkeeperNames names)
        {
            string sur = names.Sur;
            if (text == null || string.IsNullOrEmpty(sur) || text.IndexOf(sur, StringComparison.Ordinal) < 0) return null;
            if (names.IsDefault && text.IndexOf(DefaultSur + Lord, StringComparison.Ordinal) < 0
                && text.IndexOf(DefaultSur + Gentleman, StringComparison.Ordinal) < 0 && text.IndexOf(DefaultSur + Proprietor, StringComparison.Ordinal) < 0) return null;
            // Station Economy substitutes the same family-name/title match in the other direction.
            // Text alone cannot distinguish an ordinary word or another character sharing that name.
            return Replace(text, names.PlayerForms, DefaultForms, false);
        }

        // Choose the longest original form at each position. Inserted names are never
        // searched again: a player's name can itself contain one of the default forms.
        private static string Replace(string text, string[] from, string[] to, bool englishWords)
        {
            StringBuilder result = null;
            int copied = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int chosen = -1, length = 0;
                for (int k = 0; k < from.Length; k++)
                {
                    string key = from[k];
                    if (string.IsNullOrEmpty(key) || to[k] == null || key == to[k] || key.Length <= length || key.Length > text.Length - i) continue;
                    if (string.CompareOrdinal(text, i, key, 0, key.Length) != 0) continue;
                    if (englishWords && ((i > 0 && Word(text[i - 1])) || (i + key.Length < text.Length && Word(text[i + key.Length])))) continue;
                    chosen = k; length = key.Length;
                }
                if (chosen < 0) continue;
                if (result == null) result = new StringBuilder(text.Length);
                result.Append(text, copied, i - copied).Append(to[chosen]);
                i += length - 1; copied = i + 1;
            }
            return result == null ? null : result.Append(text, copied, text.Length - copied).ToString();
        }

        private static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_'
            || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark
            || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.ConnectorPunctuation;
        internal static string ToPlayer(string english, ShopkeeperNames names)
        {
            if (english == null || string.IsNullOrEmpty(names.Sur) || english.IndexOf("Ma", StringComparison.Ordinal) < 0) return english;
            return Replace(english, EnglishForms, names.EnglishForms, true) ?? english;
        }

        internal static string RestoreChinese(string text, ShopkeeperNames names) => text == null || names.IsDefault
            ? text : Replace(text, ChineseForms, names.ChineseForms, false) ?? text;

        internal static bool Latin(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (char c in name)
                if (!char.IsLetter(c) || !(c <= '\u007f' || (c >= '\u00c0' && c <= '\u024f') || (c >= '\u1e00' && c <= '\u1eff'))) return false;
            return true;
        }
    }

    internal sealed class ShopkeeperNames
    {
        internal readonly string Sur;
        internal readonly bool IsDefault;
        internal readonly string[] PlayerForms, EnglishForms, ChineseForms;
        internal ShopkeeperNames(string sur, string giv, string full)
        {
            Sur = sur;
            IsDefault = sur == EnglishNameRules.DefaultSur && giv == EnglishNameRules.DefaultGiv;
            string fullKey = !string.IsNullOrEmpty(giv) && full != null && full.Length > (sur?.Length ?? 0)
                && full.IndexOf(giv, StringComparison.Ordinal) >= 0 ? full : null;
            PlayerForms = new[] { fullKey, sur + EnglishNameRules.Boss, sur + EnglishNameRules.Lord,
                sur + EnglishNameRules.Gentleman, sur + EnglishNameRules.Proprietor, EnglishNameRules.Old + sur };
            bool defaultSur = sur == EnglishNameRules.DefaultSur, defaultGiv = giv == EnglishNameRules.DefaultGiv;
            string surE = defaultSur ? "Ma" : sur, givE = defaultGiv ? "Jake" : giv;
            bool latinSur = EnglishNameRules.Latin(sur), latinGiv = EnglishNameRules.Latin(giv);
            string englishFull = string.IsNullOrEmpty(giv) ? surE
                : !defaultSur && !defaultGiv && !latinSur && !latinGiv ? sur + giv : givE + " " + surE;
            EnglishForms = new[] { englishFull, "Boss " + surE, "Mr. " + surE, "Old " + surE, surE };
            ChineseForms = new[] { full, sur + EnglishNameRules.Boss, EnglishNameRules.Old + sur };
        }
    }

    internal sealed class NameTranslationState
    {
        internal readonly ShopkeeperNames Names;
        internal readonly string Original, Normalised;
        internal NameTranslationState(ShopkeeperNames names, string original, string normalised)
        { Names = names; Original = original; Normalised = normalised; }
    }

    internal sealed class ShopkeeperNameCache
    {
        private readonly Func<string> sur, giv, full;
        private readonly Func<double> clock;
        private bool attempted;
        private double lastAttempt;
        private ShopkeeperNames names;

        internal ShopkeeperNameCache(Func<string> sur, Func<string> giv, Func<string> full, Func<double> clock)
        { this.sur = sur; this.giv = giv; this.full = full; this.clock = clock; }

        internal ShopkeeperNames Read()
        {
            double now = clock();
            if (attempted && now >= lastAttempt && now - lastAttempt < 1d) return names;
            // Failed reads are throttled too, and never return an older, partial name.
            attempted = true; lastAttempt = now; names = null;
            var read = new ShopkeeperNames(sur(), giv(), full());
            names = read;
            return read;
        }
    }

    internal static class EnglishNames
    {
        internal const string LocType = "StationEconomy.Loc", MeType = "StationEconomy.Me";
        internal const string TranslateName = "T", EnglishName = "En", SurName = "Sur", GivName = "Giv", FullName = "Full";
        internal static bool Active, Installed;
        private static bool shapeSaid;
        internal static Func<MethodInfo> FindTranslate = () => AccessTools.Method(AccessTools.TypeByName(LocType), TranslateName, new[] { typeof(string) });
        internal static Func<MethodInfo> FindEnglish = () => AccessTools.PropertyGetter(AccessTools.TypeByName(LocType), EnglishName);
        internal static Func<MethodInfo> FindSur = () => AccessTools.PropertyGetter(AccessTools.TypeByName(MeType), SurName);
        internal static Func<MethodInfo> FindGiv = () => AccessTools.PropertyGetter(AccessTools.TypeByName(MeType), GivName);
        internal static Func<MethodInfo> FindFull = () => AccessTools.PropertyGetter(AccessTools.TypeByName(MeType), FullName);
        internal static Func<bool> On = () => StationEconomyCompatMod.Names == null || StationEconomyCompatMod.Names.Value;
        internal static Func<double> Clock = () => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        private static Func<bool> inEnglish;
        private static ShopkeeperNameCache cache;

        private static bool GetterFits(MethodInfo getter, string name, Type result) =>
            getter != null && getter.IsPublic && getter.IsStatic && !getter.ContainsGenericParameters && getter.IsSpecialName
            && getter.Name == "get_" + name && getter.ReturnType == result && getter.GetParameters().Length == 0;

        // Metadata only; never invoke the real translator or name getters in the harness.
        internal static bool Fits(MethodInfo translate, MethodInfo english, MethodInfo sur, MethodInfo giv, MethodInfo full)
        {
            if (translate == null || !translate.IsPublic || !translate.IsStatic || translate.ContainsGenericParameters || translate.ReturnType != typeof(string)) return false;
            ParameterInfo[] args = translate.GetParameters();
            return args.Length == 1 && args[0].ParameterType == typeof(string) && GetterFits(english, EnglishName, typeof(bool))
                && GetterFits(sur, SurName, typeof(string)) && GetterFits(giv, GivName, typeof(string)) && GetterFits(full, FullName, typeof(string));
        }

        internal static void TryInstall()
        {
            MethodInfo translate = null;
            try
            {
                if (!Active || Installed) return;
                translate = FindTranslate();
                MethodInfo english = FindEnglish(), sur = FindSur(), giv = FindGiv(), full = FindFull();
                if (translate == null && english == null && sur == null && giv == null && full == null) return; // Not loaded yet.
                if (!Fits(translate, english, sur, giv, full))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's name translation has changed shape; this part is left off."); }
                    return;
                }
                inEnglish = (Func<bool>)english.CreateDelegate(typeof(Func<bool>));
                cache = new ShopkeeperNameCache((Func<string>)sur.CreateDelegate(typeof(Func<string>)),
                    (Func<string>)giv.CreateDelegate(typeof(Func<string>)), (Func<string>)full.CreateDelegate(typeof(Func<string>)), () => Clock());
                new Harmony(Bridge.HarmonyId).Patch(translate, prefix: new HarmonyMethod(typeof(EnglishNames), nameof(Prefix)),
                    postfix: new HarmonyMethod(typeof(EnglishNames), nameof(Postfix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] keeping Station Economy's English with your own shopkeeper name.");
            }
            catch (Exception error)
            {
                Installed = false; cache = null; inEnglish = null;
                try
                {
                    if (translate != null)
                    {
                        var harmony = new Harmony(Bridge.HarmonyId);
                        harmony.Unpatch(translate, AccessTools.Method(typeof(EnglishNames), nameof(Prefix)));
                        harmony.Unpatch(translate, AccessTools.Method(typeof(EnglishNames), nameof(Postfix)));
                    }
                }
                catch (Exception rollback) { Bridge.Fault("removing English name patches", rollback); }
                Bridge.Fault("installing English names", error);
            }
        }

        internal static void Disable() { Active = false; Installed = false; cache = null; inEnglish = null; }

        private static bool HasChinese(string text)
        {
            foreach (char c in text) if (c >= '\u3400' && c <= '\u9fff') return true;
            return false;
        }

        internal static void Prefix(ref string __0, out NameTranslationState __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __0.Length < 2 || !Active || !Installed || !HasChinese(__0) || !On() || !inEnglish()) return;
                ShopkeeperNames names = cache.Read();
                if (names == null) return;
                string normal = EnglishNameRules.ToDefault(__0, names);
                if (normal == null) return;
                __state = new NameTranslationState(names, __0, normal);
                __0 = normal;
            }
            catch (Exception error) { Bridge.Fault("normalising English names", error); }
        }

        internal static void Postfix(ref string __result, NameTranslationState __state)
        {
            if (__state == null) return;
            try
            {
                __result = string.Equals(__result, __state.Normalised, StringComparison.Ordinal) ? __state.Original
                    : EnglishNameRules.RestoreChinese(EnglishNameRules.ToPlayer(__result, __state.Names), __state.Names);
            }
            catch (Exception error) { Bridge.Fault("restoring English names", error); }
        }
    }
}

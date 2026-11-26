using System;
using System.Reflection;
using HarmonyLib;

namespace StationEconomyCompat
{
    internal static class EnglishInitials
    {
        internal static bool Active, Installed, shapeSaid;
        internal static Func<bool> On = () => StationEconomyCompatMod.Initials == null || StationEconomyCompatMod.Initials.Value;
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.PhoneUI");
        internal static Func<MethodInfo> FindInitial = () => AccessTools.Method("StationEconomy.PhoneUI:Initial");
        private static Func<string, string> translate;

        internal static bool Fits(Type type, MethodInfo initial, MethodInfo translator) => PhoneTextShape.Fits(type, initial, translator,
            "Initial", typeof(string));

        internal static void TryInstall()
        {
            MethodInfo target = null;
            try
            {
                if (!Active || Installed) return;
                Type type = FindType();
                if (type == null) return; // Not loaded yet: another asset/game hook retries.
                MethodInfo initial = FindInitial(), translator = EnglishNames.FindTranslate();
                if (!Fits(type, initial, translator))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's avatar initial has changed shape; this part is left off."); }
                    return;
                }
                translate = (Func<string, string>)translator.CreateDelegate(typeof(Func<string, string>));
                target = initial;
                new Harmony(Bridge.HarmonyId).Patch(target, postfix: new HarmonyMethod(typeof(EnglishInitials), nameof(Postfix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] avatars use the initial of the translated name.");
            }
            catch (Exception error)
            {
                Installed = false; translate = null;
                try { if (target != null) new Harmony(Bridge.HarmonyId).Unpatch(target, AccessTools.Method(typeof(EnglishInitials), nameof(Postfix))); }
                catch (Exception rollback) { Bridge.Fault("removing English initial patch", rollback); }
                Bridge.Fault("installing English initials", error);
            }
        }

        internal static void Disable() { Active = false; Installed = false; translate = null; }

        internal static void Postfix(string __0, ref string __result)
        {
            try
            {
                if (!Active || !Installed || __0 == null || __result == null || __result.Length != 1
                    || !EnglishPreviewRules.Chinese(__result[0]) || !On()) return;
                int dot = __0.LastIndexOf('\u00b7');
                string name = (dot >= 0 && dot < __0.Length - 1 ? __0.Substring(dot + 1) : __0).Trim();
                string english = translate(name);
                if (string.Equals(english, name, StringComparison.Ordinal)) return;
                foreach (char c in english)
                    if (char.IsLetterOrDigit(c) && !EnglishPreviewRules.Chinese(c))
                    {
                        __result = char.ToUpperInvariant(c).ToString();
                        return;
                    }
            }
            catch (Exception error) { Bridge.Fault("translating an avatar initial", error); }
        }
    }
}

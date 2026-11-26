using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StationEconomyCompat
{
    internal static class PhoneTextShape
    {
        internal static bool Fits(Type type, MethodInfo target, MethodInfo translate, string name, params Type[] parameters)
        {
            if (type == null || type.ContainsGenericParameters || target == null || target.DeclaringType != type
                || target.Name != name || !target.IsStatic || target.IsGenericMethod || target.ContainsGenericParameters
                || target.ReturnType != typeof(string)) return false;
            ParameterInfo[] args = target.GetParameters();
            if (args.Length != parameters.Length) return false;
            for (int i = 0; i < args.Length; i++) if (args[i].ParameterType != parameters[i]) return false;
            // The translator portion of EnglishNames.Fits, without the unrelated name getters.
            if (translate == null || !translate.IsPublic || !translate.IsStatic || translate.ContainsGenericParameters
                || translate.ReturnType != typeof(string)) return false;
            args = translate.GetParameters();
            return args.Length == 1 && args[0].ParameterType == typeof(string);
        }
    }

    internal static class EnglishPreviews
    {
        internal static bool Active, Installed, shapeSaid;
        internal static Func<bool> On = () => StationEconomyCompatMod.Previews == null || StationEconomyCompatMod.Previews.Value;
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.PhoneUI");
        internal static Func<MethodInfo> FindClip = () => AccessTools.Method("StationEconomy.PhoneUI:Clip");
        internal static Func<GUIStyle, string, float> Measure = (style, text) => style.CalcSize(new GUIContent(text)).x;
        private static Func<string, string> translate;

        internal static bool Fits(Type type, MethodInfo clip, MethodInfo translator) => PhoneTextShape.Fits(type, clip, translator,
            "Clip", typeof(string), typeof(GUIStyle), typeof(float));

        internal static void TryInstall()
        {
            MethodInfo target = null;
            try
            {
                if (!Active || Installed) return;
                Type type = FindType();
                if (type == null) return; // Not loaded yet: another asset/game hook retries.
                MethodInfo clip = FindClip(), translator = EnglishNames.FindTranslate();
                if (!Fits(type, clip, translator))
                {
                    if (!shapeSaid) { shapeSaid = true; Bridge.Log("[Station Economy Compat] Station Economy's preview clipping has changed shape; this part is left off."); }
                    return;
                }
                translate = (Func<string, string>)translator.CreateDelegate(typeof(Func<string, string>));
                target = clip;
                new Harmony(Bridge.HarmonyId).Patch(target, prefix: new HarmonyMethod(typeof(EnglishPreviews), nameof(Prefix)));
                Installed = true;
                Bridge.Log("[Station Economy Compat] cutting long lines after they are translated.");
            }
            catch (Exception error)
            {
                Installed = false; translate = null;
                try { if (target != null) new Harmony(Bridge.HarmonyId).Unpatch(target, AccessTools.Method(typeof(EnglishPreviews), nameof(Prefix))); }
                catch (Exception rollback) { Bridge.Fault("removing English preview patch", rollback); }
                Bridge.Fault("installing English previews", error);
            }
        }

        internal static void Disable() { Active = false; Installed = false; translate = null; }

        internal static bool Prefix(string __0, GUIStyle __1, float __2, ref string __result)
        {
            try
            {
                if (!Active || !Installed || string.IsNullOrEmpty(__0) || !EnglishPreviewRules.HasChinese(__0) || !On()) return true;
                string text = __0.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                string marker = text.StartsWith(EnglishPreviewRules.ReplyMarker, StringComparison.Ordinal) ? EnglishPreviewRules.ReplyMarker : "";
                string rest = text.Substring(marker.Length);
                string english = translate(rest);
                if (english == null || string.Equals(english, rest, StringComparison.Ordinal)) return true;
                string shownMarker = marker.Length == 0 ? "" : translate(marker);
                // Do all fallible work before touching the result so a fault can use the original.
                string result = marker + EnglishPreviewRules.Cut(english, shownMarker, __2, line => Measure(__1, line));
                __result = result;
                return false;
            }
            catch (Exception error)
            {
                Bridge.Fault("cutting English previews", error);
                return true;
            }
        }
    }
}

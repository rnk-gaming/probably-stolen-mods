using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using LocalMods;

namespace StationEconomyCompat
{
    // The compatibility mod's own texts; Station Economy's English additions stay separate.
    internal static class TextKeys
    {

        internal const string OptionsTitle = "options_title";
        internal const string Section = "section_station_economy";
        internal const string PhoneInView = "option_phone_in_view";
        internal const string PhoneInViewTip = "option_phone_in_view_desc";
        internal const string PhoneOverSides = "option_phone_over_sides";
        internal const string PhoneOverSidesTip = "option_phone_over_sides_desc";
        internal const string PhoneFitText = "option_phone_fit_text";
        internal const string PhoneFitTextTip = "option_phone_fit_text_desc";
        internal const string PhoneFitShare = "option_phone_fit_share";
        internal const string PhoneFitShareTip = "option_phone_fit_share_desc";
        internal const string EnglishAdditions = "option_english_additions";
        internal const string EnglishAdditionsTip = "option_english_additions_desc";
        internal const string EnglishNames = "option_english_names";
        internal const string EnglishNamesTip = "option_english_names_desc";
        internal const string EnglishPreviews = "option_english_previews";
        internal const string EnglishPreviewsTip = "option_english_previews_desc";
        internal const string EnglishInitials = "option_english_initials";
        internal const string EnglishInitialsTip = "option_english_initials_desc";
        internal const string FurnaceFeed = "option_furnace_feed";
        internal const string FurnaceFeedTip = "option_furnace_feed_desc";
        internal const string FurnaceHopper = "option_furnace_hopper";
        internal const string FurnaceHopperTip = "option_furnace_hopper_desc";
        internal const string WholesaleSign = "option_wholesale_sign";
        internal const string WholesaleSignTip = "option_wholesale_sign_desc";
    }

    // Station Economy draws its phone (F7) on a layer of its own over the whole screen and
    // places it from the full screen width. On a screen wider than 16:9 the game's picture
    // is 16:9 in the middle, and the phone lands beside it where it is not seen.
    // After its own routine has limited the phone's place, the place is limited once more
    // to the game's picture.
    internal static class Rules
    {
        // Station Economy's layer is laid out as 1080 units high; a 16:9 picture is then
        // 1920 units wide. Its phone is 400 units wide.
        internal const float PictureWidth = 1920f, PhoneWidth = 400f, Margin = 10f;

        internal static float Limit(float canvasWidth)
        {
            // Only a screen wider than 16:9 is touched; on any other screen Station
            // Economy's own limit stands as it is.
            if (float.IsNaN(canvasWidth) || canvasWidth <= PictureWidth) return float.PositiveInfinity;
            return PictureWidth / 2f - PhoneWidth / 2f - Margin;
        }

        // The phone's texts are laid out for Chinese; the same line in English is longer
        // and is cut off with dots. A text that does not fit is made smaller first, down
        // to a share of its size, and only then cut.
        internal const int LeastShare = 50, MostShare = 100, DefaultShare = 65;
        internal static float Smallest(float size, int sharePercent)
        {
            if (float.IsNaN(size) || float.IsInfinity(size) || size <= 0f) return size;
            int share = Math.Max(LeastShare, Math.Min(MostShare, sharePercent));
            return size * share / 100f;
        }

        internal static float Clamp(float x, float canvasWidth)
        {
            float limit = Limit(canvasWidth);
            if (float.IsPositiveInfinity(limit)) return x;
            if (float.IsNaN(x)) return 0f;
            return Math.Max(-limit, Math.Min(limit, x));
        }
    }

    public class StationEconomyCompatMod : IMod
    {
        internal static ConfigEntry<bool> Enabled, OverSides, FitText, English, Names, Furnace, Hoppers, Previews, Initials, Wholesale;
        internal static ConfigEntry<int> FitShare;

        public void Init(ModManifest manifest)
        {
            TextTable.Start(manifest.ID, Bridge.Fault);
            // Mods it does not go together with: add one list line and two rows in en.csv.
            Incompatible.Register(manifest.ID, "[Station Economy Compat]", new IncompatibleMod[]
            {
                // No confirmed conflicts yet.
            }, NoteFault);
            Bridge.HarmonyId = "mod." + manifest.ID;
            try
            {
                var config = ModConfig.For(manifest.ID);
                string section = TextTable.Early(TextKeys.Section);
                Enabled = config.Define("phone_in_view", true, section, TextTable.Later(TextKeys.PhoneInView),
                    TextTable.Later(TextKeys.PhoneInViewTip));
                OverSides = config.Define("phone_over_sides", true, section, TextTable.Later(TextKeys.PhoneOverSides),
                    TextTable.Later(TextKeys.PhoneOverSidesTip));
                FitText = config.Define("phone_fit_text", true, section, TextTable.Later(TextKeys.PhoneFitText),
                    TextTable.Later(TextKeys.PhoneFitTextTip));
                FitShare = config.Define("phone_fit_share", Rules.DefaultShare, section, TextTable.Later(TextKeys.PhoneFitShare), Rules.LeastShare, Rules.MostShare,
                    TextTable.Later(TextKeys.PhoneFitShareTip));
                English = config.Define("english_additions", true, section, TextTable.Later(TextKeys.EnglishAdditions),
                    TextTable.Later(TextKeys.EnglishAdditionsTip));
                English.OnChanged += EnglishAdditions.SettingChanged;
                Names = config.Define("english_names", true, section, TextTable.Later(TextKeys.EnglishNames),
                    TextTable.Later(TextKeys.EnglishNamesTip));
                Previews = config.Define("english_previews", true, section, TextTable.Later(TextKeys.EnglishPreviews),
                    TextTable.Later(TextKeys.EnglishPreviewsTip));
                Initials = config.Define("english_initials", true, section, TextTable.Later(TextKeys.EnglishInitials),
                    TextTable.Later(TextKeys.EnglishInitialsTip));
                Furnace = config.Define("furnace_feed", true, section, TextTable.Later(TextKeys.FurnaceFeed),
                    TextTable.Later(TextKeys.FurnaceFeedTip));
                Hoppers = config.Define("furnace_hopper", true, section, TextTable.Later(TextKeys.FurnaceHopper),
                    TextTable.Later(TextKeys.FurnaceHopperTip));
                Wholesale = config.Define("wholesale_sign", true, section, TextTable.Later(TextKeys.WholesaleSign),
                    TextTable.Later(TextKeys.WholesaleSignTip));
                OptionsMenu.Register(manifest.ID, TextTable.Early(TextKeys.OptionsTitle), Bridge.Fault);
            }
            catch (Exception error) { Bridge.Fault("settings", error); }
        }

        private static void NoteFault(string id, Exception error) =>
            UnityEngine.Debug.LogWarning("[Station Economy Compat] a fault in note " + id + "; note delivery skipped. " + error.GetType().Name + ": " + error.Message);

        public void OnEnable()
        {
            Incompatible.Enable();
            EnglishAdditions.Active = true;
            EnglishNames.Active = true;
            EnglishPreviews.Active = true;
            EnglishInitials.Active = true;
            FurnaceFeed.Active = true;
            FurnaceHopper.Active = true;
            WholesaleSign.Active = true;
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly -= Bridge.InstallOrSayWhy;
            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly += Bridge.InstallOrSayWhy;
            Bridge.TryInstall();
        }

        public void OnDisable()
        {
            Incompatible.Disable();
            EnglishAdditions.Disable();
            EnglishNames.Disable();
            EnglishPreviews.Disable();
            EnglishInitials.Disable();
            FurnaceFeed.Disable();
            FurnaceHopper.Disable();
            WholesaleSign.Disable();
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedEarly -= Bridge.InstallOrSayWhy;
            Bridge.Installed = false; Bridge.ConvertInstalled = false; Bridge.FitInstalled = false; // The loader removes all patches under HarmonyId afterwards.
        }

        private static void OnModAssetsLoaded(LoadedMod mod) => Bridge.TryInstall();
    }

    internal static class Bridge
    {
        internal static string HarmonyId = "mod.rnk.station-economy-compat";
        internal static bool Installed;
        // Only the place Station Economy gives the phone by itself is moved, once in a
        // session of the game. After that the player may drag the phone anywhere Station
        // Economy allows, also onto the black sides, like any other window.
        internal static bool Placed;
        private static bool said;
        private static readonly System.Collections.Generic.HashSet<string> faulted = new System.Collections.Generic.HashSet<string>();
        internal static Action<string> Log = text => Debug.Log(text);
        internal static Func<bool> On = () => StationEconomyCompatMod.Enabled == null || StationEconomyCompatMod.Enabled.Value;
        internal static Func<bool> OverOn = () => StationEconomyCompatMod.OverSides == null || StationEconomyCompatMod.OverSides.Value;
        // The game, with black sides, turns every layer that covers the screen into one
        // drawn by a camera that only covers the game's picture, except its own top layer,
        // which is drawn by a second camera over the black sides too. The game's windows
        // lie on that top layer; the phone's layer does not, so the phone disappears
        // behind the black. The phone's layer is given the
        // second camera. All Unity calls are behind these, for the tests.
        internal static Func<Camera> SideCamera = () => { Canvas dock = LetterboxManager.DockCanvas; return dock == null ? null : dock.worldCamera; };
        internal static Func<Type> FindPhone = () => AccessTools.TypeByName("StationEconomy.PhoneUI");
        internal static Func<FieldInfo> FindLayer = () => AccessTools.Field(AccessTools.TypeByName("StationEconomy.PhoneUI"), "canvas");
        internal static Func<MethodInfo> FindConvert = () => AccessTools.Method(typeof(LetterboxManager), "ConvertCanvases");
        internal static Func<Canvas> PhoneLayer = () =>
        {
            FieldInfo layer = FindLayer();
            return layer == null || !layer.IsStatic || layer.FieldType != typeof(Canvas) ? null : (Canvas)layer.GetValue(null);
        };
        internal static Func<Canvas, bool> ByCamera = layer => layer.renderMode == RenderMode.ScreenSpaceCamera;
        internal static Func<Canvas, Camera> CameraOf = layer => layer.worldCamera;
        internal static Action<Canvas, Camera> SetCamera = (layer, camera) => layer.worldCamera = camera;
        internal static bool ConvertInstalled;
        // Station Economy makes every text of its phone in one place, a private static
        // method that takes the place, the line, the size, the colour, the alignment and
        // whether it is bold, and gives the text back.
        internal static bool FitInstalled;
        internal static Func<MethodInfo> FindText = () => AccessTools.Method("StationEconomy.PhoneUI:T");
        internal static Func<bool> FitOn = () => StationEconomyCompatMod.FitText == null || StationEconomyCompatMod.FitText.Value;
        internal static Func<int> FitShare = () => StationEconomyCompatMod.FitShare == null ? Rules.DefaultShare : StationEconomyCompatMod.FitShare.Value;
        internal static Action<TMPro.TMP_Text, float, float> Shrink = (text, most, least) =>
        {
            text.fontSizeMax = most; text.fontSizeMin = least; text.enableAutoSizing = true;
        };

        internal static bool TextFits(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || !typeof(TMPro.TMP_Text).IsAssignableFrom(method.ReturnType)) return false;
            ParameterInfo[] p = method.GetParameters();
            return p.Length == 6 && p[0].ParameterType == typeof(RectTransform) && p[1].ParameterType == typeof(string) && p[2].ParameterType == typeof(float)
                && p[3].ParameterType == typeof(Color) && p[4].ParameterType == typeof(TMPro.TextAlignmentOptions) && p[5].ParameterType == typeof(bool);
        }

        // The size the other mod asked for stays the largest; nothing else of the text is touched.
        internal static void AfterText(object __result, float __2)
        {
            try
            {
                if (!FitInstalled || !FitOn() || !(__result is TMPro.TMP_Text text) || (object)text == null) return;
                float least = Rules.Smallest(__2, FitShare());
                if (float.IsNaN(least) || least <= 0f || least >= __2) return;
                Shrink(text, __2, least);
            }
            catch (Exception error) { Fault("fitting a phone text", error); }
        }

        internal static void TryInstallFit()
        {
            MethodInfo text = null;
            try
            {
                if (FitInstalled) return;
                text = FindText();
                if (text == null) return; // Station Economy is not loaded (yet).
                if (!TextFits(text))
                {
                    if (faulted.Add("text shape")) Log("[Station Economy Compat] Station Economy's phone texts are made differently; they are left as they are.");
                    return;
                }
                new Harmony(HarmonyId).Patch(text, postfix: new HarmonyMethod(typeof(Bridge), nameof(AfterText)));
                FitInstalled = true;
            }
            catch (Exception error)
            {
                FitInstalled = false;
                try { if (text != null) new Harmony(HarmonyId).Unpatch(text, AccessTools.Method(typeof(Bridge), nameof(AfterText))); } catch (Exception) { }
                Fault("installing the text fit", error);
            }
        }

        internal static void RaisePhone()
        {
            try
            {
                if (!ConvertInstalled || !OverOn()) return;
                Camera side = SideCamera();
                Canvas phone = (object)side == null ? null : PhoneLayer();
                if ((object)side == null || (object)phone == null) return;
                if (ByCamera(phone) && !ReferenceEquals(CameraOf(phone), side)) SetCamera(phone, side);
            }
            catch (Exception error) { Fault("showing the phone over the sides", error); }
        }

        internal static Func<float> CanvasWidth = () => Screen.height <= 0 ? Rules.PictureWidth : Screen.width * 1080f / Screen.height;

        // Each phone part checks only its own members before patching anything.
        internal static Func<MethodInfo> FindClamp = () => AccessTools.Method("StationEconomy.PhoneUI:ClampPos");
        internal static Func<FieldInfo> FindPlace = () => AccessTools.Field(AccessTools.TypeByName("StationEconomy.PhoneUI"), "pos");

        internal static bool Fits(MethodInfo clamp, FieldInfo place) =>
            clamp != null && clamp.IsStatic && !clamp.ContainsGenericParameters && clamp.ReturnType == typeof(void) && clamp.GetParameters().Length == 0
            && place != null && place.IsStatic && !place.IsInitOnly && place.FieldType == typeof(Vector2);

        internal static void Fault(string where, Exception error)
        {
            try { if (faulted.Add(where)) { Log("[Station Economy Compat] a fault in " + where + "; leaving Station Economy as it is. " + error.GetType().Name + ": " + error.Message); } }
            catch (Exception) { }
        }

        internal static void TryInstall()
        {
            FurnaceFeed.TryInstall();
            FurnaceHopper.TryInstall();
            WholesaleSign.TryInstall();
            EnglishAdditions.TryInstall();
            EnglishNames.TryInstall();
            EnglishPreviews.TryInstall();
            EnglishInitials.TryInstall();
            TryInstallFit();
            TryInstallPlace();
            TryInstallConvert();
        }

        private static void PhoneShape(string part, string member)
        {
            if (faulted.Add("phone " + part + " shape: " + member))
                Log("[Station Economy Compat] the phone " + part + " member " + member + " has changed shape; this part is left off.");
        }

        private static void TryInstallPlace()
        {
            MethodInfo clamp = null;
            try
            {
                if (Installed) return;
                if (FindPhone() == null) return; // Station Economy is not loaded (yet).
                said = true;
                clamp = FindClamp();
                if (clamp == null || !clamp.IsStatic || clamp.ContainsGenericParameters || clamp.ReturnType != typeof(void) || clamp.GetParameters().Length != 0)
                {
                    PhoneShape("place", "ClampPos");
                    return;
                }
                if (!Fits(clamp, FindPlace()))
                {
                    PhoneShape("place", "pos");
                    return;
                }
                new Harmony(HarmonyId).Patch(clamp, postfix: new HarmonyMethod(typeof(Bridge), nameof(AfterClamp)));
                Installed = true;
                Log("[Station Economy Compat] keeping Station Economy's phone inside the game's picture.");
            }
            catch (Exception error)
            {
                said = true; Installed = false;
                try { if (clamp != null) new Harmony(HarmonyId).Unpatch(clamp, AccessTools.Method(typeof(Bridge), nameof(AfterClamp))); }
                catch (Exception removal) { Fault("removing the phone position patch", removal); }
                Fault("installing the phone place", error);
            }
        }

        private static void TryInstallConvert()
        {
            MethodInfo convert = null;
            try
            {
                if (ConvertInstalled) return;
                if (FindPhone() == null) return; // Station Economy is not loaded (yet).
                said = true;
                FieldInfo layer = FindLayer();
                if (layer == null || !layer.IsStatic || layer.FieldType != typeof(Canvas))
                {
                    PhoneShape("camera", "canvas");
                    return;
                }
                convert = FindConvert();
                if (convert == null || convert.IsStatic || convert.ContainsGenericParameters || convert.ReturnType != typeof(void) || convert.GetParameters().Length != 0)
                {
                    PhoneShape("camera", "ConvertCanvases");
                    return;
                }
                // The game does its conversion again from time to time; ours follows it.
                new Harmony(HarmonyId).Patch(convert, postfix: new HarmonyMethod(typeof(Bridge), nameof(RaisePhone)));
                ConvertInstalled = true;
                Log("[Station Economy Compat] showing Station Economy's phone over the black sides.");
            }
            catch (Exception error)
            {
                said = true; ConvertInstalled = false;
                try { if (convert != null) new Harmony(HarmonyId).Unpatch(convert, AccessTools.Method(typeof(Bridge), nameof(RaisePhone))); }
                catch (Exception removal) { Fault("removing the phone camera patch", removal); }
                Fault("installing the phone camera", error);
            }
        }

        internal static void InstallOrSayWhy()
        {
            TryInstall();
            try { if (!Installed && !said) { said = true; Log("[Station Economy Compat] Station Economy is not running; nothing to do."); } }
            catch (Exception) { }
        }

        internal static Vector2 Move(Vector2 place, float canvasWidth) => new Vector2(Rules.Clamp(place.x, canvasWidth), place.y);

        internal static void AfterClamp()
        {
            RaisePhone();
            try
            {
                if (!Installed || Placed) return;
                // Switched off, the first place still counts as given: switching the setting on
                // later must not move a place the player has chosen since.
                if (!On()) { Placed = true; return; }
                FieldInfo place = FindPlace();
                if (place == null || !place.IsStatic || place.IsInitOnly || place.FieldType != typeof(Vector2)) return;
                Vector2 now = (Vector2)place.GetValue(null);
                Vector2 moved = Move(now, CanvasWidth());
                Placed = true;
                if (moved.x != now.x) place.SetValue(null, moved);
            }
            catch (Exception error) { Fault("placing the phone", error); }
        }
    }
}

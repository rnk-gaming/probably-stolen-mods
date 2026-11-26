using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StationEconomyCompat
{
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
            Bridge.HarmonyId = "mod." + manifest.ID;
            try
            {
                Enabled = ModConfig.For(manifest.ID).Define("phone_in_view", true, "Station Economy", () => "Keep the phone inside the game's picture",
                    () => "On a screen wider than 16:9, keep Station Economy's phone (F7) inside the 16:9 picture.");
                OverSides = ModConfig.For(manifest.ID).Define("phone_over_sides", true, "Station Economy", () => "Show the phone over the black sides",
                    () => "On a screen wider than 16:9, draw Station Economy's phone over the black sides, so that it can be dragged there like the game's own windows.");
                FitText = ModConfig.For(manifest.ID).Define("phone_fit_text", true, "Station Economy", () => "Shrink phone texts that do not fit",
                    () => "Station Economy's phone cuts a line that is too long for its place. With this on, the line is first made smaller so that more of it shows. Applies to texts made after the change.");
                FitShare = ModConfig.For(manifest.ID).Define("phone_fit_share", Rules.DefaultShare, "Station Economy", () => "Smallest text size (% of its own)", Rules.LeastShare, Rules.MostShare,
                    () => "How small a phone text may become before it is cut. 100 switches the shrinking off.");
                English = ModConfig.For(manifest.ID).Define("english_additions", true, "Station Economy", () => "Add missing English to Station Economy",
                    () => "Add English lines from the table in this mod's Localization folder to Station Economy's own translator. Without the table this does nothing. Switching it off restores the original table for the next translation.");
                English.OnChanged += EnglishAdditions.SettingChanged;
                Names = ModConfig.For(manifest.ID).Define("english_names", true, "Station Economy", () => "Keep Station Economy's English with your own shopkeeper name",
                    () => "Only acts when the game is not in Chinese. A Chinese name is kept as written, a Latin name is written given name first, and a default part uses its English form. A family name shared with another character can show your name in a line about them.");
                Previews = ModConfig.For(manifest.ID).Define("english_previews", true, "Station Economy", () => "Cut long lines after translating them",
                    () => "Translate message previews before cutting them to fit, so that long English lines stay in English. Chinese text is left as it is.");
                Initials = ModConfig.For(manifest.ID).Define("english_initials", true, "Station Economy", () => "Latin initial for a translated name",
                    () => "Use the first letter or digit of a translated name on its avatar. Chinese names without a translation are left as they are.");
                Furnace = ModConfig.For(manifest.ID).Define("furnace_feed", true, "Station Economy", () => "Feed the Furnace in full through a hopper",
                    () => "When Station Economy pulls feed for the Furnace itself (it does so when the input holds fewer than three pieces), every piece it takes is counted and what a run did not use goes back.");
                Hoppers = ModConfig.For(manifest.ID).Define("furnace_hopper", true, "Station Economy", () => "Hoppers fill the Furnace as in the base game",
                    () => "Station Economy stops a hopper from moving items into a Furnace and feeds the Furnace itself, a few pieces at a time. With this on, a hopper fills the Furnace's input as it fills any other inventory: what the filter lets through, as much as there is room for.");
                Wholesale = ModConfig.For(manifest.ID).Define("wholesale_sign", true, "Station Economy", () => "Wholesale offers of Station Economy's sellers are a discount",
                    () => "Buying a seller's whole lot costs less, as the seller says. Applies to the next visitor.");
                LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Station Economy Compatibility", Bridge.Fault);
            }
            catch (Exception error) { Bridge.Fault("settings", error); }
        }

        public void OnEnable()
        {
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

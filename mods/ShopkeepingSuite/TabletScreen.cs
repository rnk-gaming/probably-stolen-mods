using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShopkeepingSuite
{
    // The controller has no engine calls. Tests replace all four visual boundaries.
    internal static class TabletScreen
    {
        internal static readonly TabletQueue Pages = new TabletQueue();
        internal static Func<bool> Find = TabletVisual.Find;
        internal static Func<bool> Alive = TabletVisual.Alive;
        internal static Func<string, bool> Draw = TabletVisual.Draw;
        internal static Action Clear = TabletVisual.Clear;
        internal static Func<string, double> Measure = text => text.Length;
        private static bool bound, failed, visualTouched;
        private static float nextFind = float.NegativeInfinity;
        private static float missingSince = float.NaN;
        private static int attempts;
        private static bool abandoned;
        private static void RestartSearch() { nextFind = float.NegativeInfinity; missingSince = float.NaN; attempts = 0; abandoned = false; }
        internal static void Enqueue(string id, string faction, double amount, IEnumerable<string> crossed)
        {
            try
            {
                if (failed || !Settings.On("enabled") || !Settings.On("tablet_feed")) return;
                if (!Pages.HasPages || abandoned) RestartSearch();
                if (!bound && float.IsNaN(missingSince)) missingSince = SuiteRuntime.Clock();
                // Conservative character widths; TMP additionally shrinks every page to the actual screen.
                foreach (TabletPage page in TabletRules.Pages(id, faction, amount, crossed, Measure, 18)) Pages.Add(page);
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void Tick()
        {
            try
            {
                if (failed) return;
                if (!Settings.On("enabled") || !Settings.On("tablet_feed") || !SuiteRuntime.HasRun()) { Reset(); return; }
                float now = SuiteRuntime.Clock();
                if (abandoned) return;
                object item = Settings.On("counterfeit_check") ? InspectionScreen.Item : null;
                if (!ReferenceEquals(Pages.HeldItem, item))
                    Pages.Hold(item, item == null ? null : TabletRules.InspectionPages(InspectionScreen.Issues, Measure, 18), now);
                if (!Pages.HasPages) { Release(); return; }
                if (bound && !Alive()) { Reset(); return; }
                if (!bound)
                {
                    Pages.Shown(now, false);
                    if (float.IsNaN(missingSince)) missingSince = now;
                    if (now - missingSince > 60) { Pages.Reset(); Release(); abandoned = true; return; }
                    if (now < nextFind) return;
                    visualTouched = true;
                    bound = Find();
                    if (!bound) { attempts++; nextFind = now + (attempts < 5 ? 1 : 10); return; }
                    RestartSearch();
                }
                TabletPage page = Pages.Step(now);
                if (page == null) { Release(); return; }
                Pages.Shown(now, Draw(page.Text));
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void SettingsChanged()
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("tablet_feed")) Reset();
                else if (!Settings.On("counterfeit_check") && Pages.HeldItem != null) Pages.Hold(null, null, SuiteRuntime.Clock());
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void Reset()
        {
            Pages.Reset(); RestartSearch();
            try { Release(); }
            catch (Exception error) { Fail(error); }
        }
        private static void Release()
        {
            if (!bound && !visualTouched) return;
            bound = visualTouched = false; Clear();
        }
        private static void Fail(Exception error)
        {
            failed = true; Pages.Reset();
            // Clear even a partially built visual; its method owns every allocation from creation.
            try { Clear(); } catch (Exception) { }
            bound = visualTouched = false;
            SuiteMod.Fault("tablet screen", error);
        }
    }
    // Thin engine methods, never executed by the offline harness.
    internal static class TabletVisual
    {
        private static SpriteRenderer renderer, plate;
        private static TextMeshProUGUI calendar, text;
        private static Canvas canvas;
        private static GameObject plateObject, textObject;
        private static Texture2D texture;
        private static Sprite sprite;
        private static StoreCalendar source;
        private static VisualValue<Vector3> platePosition, plateSize, textPosition;
        private static VisualValue<Vector2> textSize;
        private static VisualValue<Quaternion> textRotation;
        private static VisualValue<Material> plateMaterial, fontMaterial;
        private static VisualValue<TMP_FontAsset> font;
        private static VisualValue<float> fontSize;
        private static VisualValue<int> sortingLayer, sortingOrder;
        private static VisualValue<Color> plateColour, textColour;
        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= .00000001f;
        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= .00000001f;
        private static bool Near(Quaternion a, Quaternion b) => Quaternion.Angle(a, b) <= .001f;
        private static bool Near(Color a, Color b) => VisualTolerance.Near(a.r, b.r) && VisualTolerance.Near(a.g, b.g) && VisualTolerance.Near(a.b, b.b) && VisualTolerance.Near(a.a, b.a);
        private static readonly Vector2[] corners = new Vector2[4];
        internal static bool Find()
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null) return false;
            SpriteRenderer found = renderer != null ? renderer : FindUnder(shop.storeInteractables) ?? FindUnder(shop.storePhysical);
            if (found == null) return false;
            renderer = found;
            if (source == null) source = UnityEngine.Object.FindObjectOfType<StoreCalendar>();
            if (source == null || source.dayTMP == null || source.dayTMP.canvas == null) return false;
            renderer = found; calendar = source.dayTMP; canvas = calendar.canvas;
            return true;
        }
        private static SpriteRenderer FindUnder(GameObject root)
        {
            if (root == null) return null;
            foreach (SpriteRenderer candidate in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (candidate != null && candidate.sprite != null && TabletRules.IsTablet(candidate.sprite.name)) return candidate;
            return null;
        }
        internal static bool Alive() => renderer != null && calendar != null && canvas != null;
        private static void Create()
        {
            platePosition = new VisualValue<Vector3>(); plateSize = new VisualValue<Vector3>(); textPosition = new VisualValue<Vector3>();
            textSize = new VisualValue<Vector2>(); textRotation = new VisualValue<Quaternion>();
            plateMaterial = new VisualValue<Material>(); fontMaterial = new VisualValue<Material>(); font = new VisualValue<TMP_FontAsset>();
            fontSize = new VisualValue<float>(); sortingLayer = new VisualValue<int>(); sortingOrder = new VisualValue<int>();
            plateColour = new VisualValue<Color>(); textColour = new VisualValue<Color>();
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixel(0, 0, Color.white); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            plateObject = new GameObject("Shopkeeping tablet plate");
            plateObject.layer = renderer.gameObject.layer;
            plateObject.transform.SetParent(renderer.transform, false);
            plate = plateObject.AddComponent<SpriteRenderer>(); plate.enabled = false; plate.sprite = sprite;
            textObject = new GameObject("Shopkeeping tablet text", typeof(RectTransform));
            textObject.layer = calendar.gameObject.layer;
            textObject.transform.SetParent(canvas.transform, false);
            text = textObject.AddComponent<TextMeshProUGUI>(); text.enabled = false;
            text.richText = false; text.raycastTarget = false; text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false; text.enableAutoSizing = true; text.fontSizeMin = .1f;
            text.overflowMode = TextOverflowModes.Masking;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, .5f);
        }
        private static void Visible(bool value)
        {
            if (plate != null && plate.enabled != value) plate.enabled = value;
            if (text != null && text.enabled != value) text.enabled = value;
        }
        internal static bool Draw(string page)
        {
            if (!Alive()) { Visible(false); return false; }
            if (plateObject == null || textObject == null) { ClearObjects(); Create(); }
            bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null &&
                calendar.isActiveAndEnabled && canvas.isActiveAndEnabled;
            if (!visible) { Visible(false); return false; }
            Sprite picture = renderer.sprite;
            TabletRectangle rect = TabletRules.Layout(picture.rect.width, picture.rect.height, picture.pivot.x, picture.pivot.y, picture.pixelsPerUnit, renderer.flipX, renderer.flipY);
            platePosition.Set(new Vector3((float)rect.X, (float)rect.Y, 0), v => plateObject.transform.localPosition = v, Near);
            plateSize.Set(new Vector3((float)rect.Width, (float)rect.Height, 1), v => plateObject.transform.localScale = v, Near);
            plateMaterial.Set(renderer.sharedMaterial, v => plate.sharedMaterial = v); sortingLayer.Set(renderer.sortingLayerID, v => plate.sortingLayerID = v);
            sortingOrder.Set(renderer.sortingOrder + 1, v => plate.sortingOrder = v);
            plateColour.Set(new Color(236 / 255f, 214 / 255f, 150 / 255f, 1) * renderer.color, v => plate.color = v, Near);
            Camera worldCamera = Camera.main;
            if (worldCamera == null || !(canvas.transform is RectTransform canvasRect)) { Visible(false); return false; }
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // Inset by two source-screen pixels on every edge, scaled with the sprite.
            float halfWidth = (float)(rect.Width * (0.5 - 2d / 67)), halfHeight = (float)(rect.Height * (0.5 - 2d / 33));
            for (int i = 0; i < 4; i++)
            {
                Vector3 world = renderer.transform.TransformPoint(new Vector3((float)rect.X + (i == 0 || i == 3 ? -halfWidth : halfWidth),
                    (float)rect.Y + (i < 2 ? -halfHeight : halfHeight), 0));
                Vector3 screen = worldCamera.WorldToScreenPoint(world);
                if (screen.z <= 0 || !worldCamera.pixelRect.Contains(screen) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out corners[i])) { Visible(false); return false; }
            }
            Vector2 horizontal = corners[1] - corners[0], vertical = corners[3] - corners[0];
            // localPosition uses the canvas' origin regardless of its pivot.
            textPosition.Set((corners[0] + corners[1] + corners[2] + corners[3]) / 4, v => text.rectTransform.localPosition = v, Near);
            textRotation.Set(Quaternion.Euler(0, 0, Mathf.Atan2(horizontal.y, horizontal.x) * Mathf.Rad2Deg), v => text.rectTransform.localRotation = v, Near);
            textSize.Set(new Vector2(horizontal.magnitude, vertical.magnitude), v => text.rectTransform.sizeDelta = v, Near);
            font.Set(calendar.font, v => text.font = v); fontMaterial.Set(calendar.fontSharedMaterial, v => text.fontSharedMaterial = v);
            fontSize.Set(calendar.fontSize, v => { text.fontSizeMax = v; text.fontSize = v; }, VisualTolerance.Near);
            textColour.Set(new Color(150 / 255f, 62 / 255f, 44 / 255f, 1) * renderer.color, v => text.color = v, Near);
            if (text.text != page) text.text = page;
            Visible(true); return true;
        }
        private static void ClearObjects()
        {
            // Attempt every destruction even if one native call fails.
            UnityEngine.Object[] owned = { textObject, plateObject, sprite, texture };
            Exception fault = null;
            try { if (text != null) text.enabled = false; } catch (Exception error) { fault = error; }
            try { if (plate != null) plate.enabled = false; } catch (Exception error) { fault ??= error; }
            textObject = plateObject = null; text = null; plate = null; sprite = null; texture = null;
            foreach (UnityEngine.Object item in owned)
                try { if (item != null) UnityEngine.Object.Destroy(item); }
                catch (Exception error) { fault ??= error; }
            if (fault != null) throw fault;
        }
        internal static void Clear()
        {
            try { ClearObjects(); }
            finally { renderer = null; calendar = null; canvas = null; source = null; }
        }
    }
}

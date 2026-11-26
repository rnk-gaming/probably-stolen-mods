using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopkeepingSuite
{
    internal sealed class InspectionState
    {
        internal object Manager, Item;
        internal bool PanelActive, CheckActive;
    }
    internal static class InspectionScreen
    {
        // The controller calls no engine method; every native boundary is replaceable.
        internal static Func<InspectionState> Read = InspectionVisual.Read;
        internal static Func<object, List<string>> ReadIssues = InspectionVisual.ReadIssues;
        internal static Action<object, string, bool> Draw = InspectionVisual.Draw;
        internal static Action Hide = InspectionVisual.Hide;
        internal static Action Clear = InspectionVisual.Clear;
        internal static object Item;
        internal static List<string> Issues;
        private static object manager, cachedItem;
        private static string line;
        private static bool visual, failed;
        internal static void Tick()
        {
            try
            {
                if (failed) return;
                if (!Settings.On("enabled") || !Settings.On("counterfeit_check") || !SuiteRuntime.HasRun()) { Reset(); return; }
                InspectionState state = Read();
                if (state?.Manager == null) { Reset(); return; }
                if (!ReferenceEquals(manager, state.Manager)) { Reset(); manager = state.Manager; }
                if (!state.PanelActive || state.Item == null)
                {
                    Item = cachedItem = null; Issues = null; line = null;
                    if (visual) Hide();
                    return;
                }
                if (!ReferenceEquals(cachedItem, state.Item))
                {
                    cachedItem = state.Item;
                    Issues = ReadIssues(state.Item);
                    line = Issues == null ? null : Texts.Get("counterfeit_line", Issues.Count == 0 ? Texts.Get("counterfeit_sound") : string.Join(", ", Issues));
                }
                // A null list means another item kind; an empty list means no faults.
                Item = Issues == null ? null : state.Item;
                if (!Rules.CounterfeitVisible(true, true, state.PanelActive, state.Item != null, Issues, state.CheckActive))
                {
                    if (visual) Hide();
                    return;
                }
                visual = true; // Clear owns a partial creation too if Draw throws.
                Draw(manager, line, Issues.Count > 0);
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void SettingsChanged()
        {
            try { if (!Settings.On("enabled") || !Settings.On("counterfeit_check")) { Counterfeit.Reset(); Reset(); } }
            catch (Exception error) { Fail(error); }
        }
        internal static void Reset()
        {
            Item = cachedItem = manager = null; Issues = null; line = null;
            try { if (visual) { visual = false; Clear(); } }
            catch (Exception error) { Fail(error); }
        }
        private static void Fail(Exception error)
        {
            failed = true; Item = cachedItem = manager = null; Issues = null; line = null;
            try { Clear(); } catch (Exception) { }
            visual = false;
            SuiteMod.Fault("magnifier answer", error);
        }
    }
    // Thin engine methods, never executed by the offline harness.
    internal static class InspectionVisual
    {
        private static GameObject owned;
        private static TextMeshProUGUI text;
        private static VisualValue<TMP_FontAsset> font;
        private static VisualValue<Material> fontMaterial;
        private static VisualValue<float> fontSize;
        private static VisualValue<Color> colour;
        private static readonly InspectionState state = new InspectionState();
        internal static InspectionState Read()
        {
            InspectionUIManager manager = InspectionUIManager.Instance;
            if (manager == null) return null;
            state.Manager = manager; state.Item = manager.currentInspectingItem;
            state.PanelActive = manager.panel != null && manager.panel.activeInHierarchy;
            state.CheckActive = manager.confrontPanel != null && manager.confrontPanel.activeInHierarchy;
            return state;
        }
        internal static List<string> ReadIssues(object item) => Rules.CounterfeitIssues(tag => Marks.Tag((GameItem)item, tag));
        internal static void Draw(object owner, string line, bool faulty)
        {
            var manager = (InspectionUIManager)owner;
            TextMeshProUGUI source = manager.confrontTitleTMP;
            if (source == null || manager.panel == null) { Hide(); return; }
            if (owned == null)
            {
                owned = new GameObject("Shopkeeping magnifier answer", typeof(RectTransform));
                owned.SetActive(false);
                owned.layer = manager.panel.layer;
                owned.transform.SetParent(manager.panel.transform, false);
                owned.AddComponent<LayoutElement>().ignoreLayout = true;
                text = owned.AddComponent<TextMeshProUGUI>();
                font = new VisualValue<TMP_FontAsset>(); fontMaterial = new VisualValue<Material>();
                fontSize = new VisualValue<float>(); colour = new VisualValue<Color>();
                text.raycastTarget = false; text.richText = false; text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = true; text.enableAutoSizing = true; text.fontSizeMin = .1f;
                text.maxVisibleLines = 2; text.overflowMode = TextOverflowModes.Masking;
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(.08f, .08f); rect.anchorMax = new Vector2(.92f, .22f);
                rect.pivot = new Vector2(.5f, 0); rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            font.Set(source.font, v => text.font = v); fontMaterial.Set(source.fontSharedMaterial, v => text.fontSharedMaterial = v);
            fontSize.Set(source.fontSize, v => { text.fontSizeMax = v; text.fontSize = v; }, VisualTolerance.Near);
            colour.Set(faulty ? (Color)RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.Red) : new Color(230 / 255f, 230 / 255f, 220 / 255f, 1), v => text.color = v);
            if (text.text != line) text.text = line;
            if (!owned.activeSelf) owned.SetActive(true);
        }
        internal static void Hide() { if (owned != null && owned.activeSelf) owned.SetActive(false); }
        internal static void Clear()
        {
            GameObject previous = owned; owned = null; text = null;
            try { if (previous != null) previous.SetActive(false); }
            finally { if (previous != null) UnityEngine.Object.Destroy(previous); }
        }
    }
}

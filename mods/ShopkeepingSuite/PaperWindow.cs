using LocalMods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ShopkeepingSuite
{
    // Replaceable engine boundary; the window controller can be exercised without Unity.
    internal sealed class PaperView
    {
        internal Func<bool> Alive, Visible;
        internal Func<int> Current;
        internal Action<int> Select;
        internal Action<string, string> Text;
        internal Action<string, bool> Visibility = (_, __) => { };
        internal Action<string, float> Rotate = (_, __) => { };
        internal Action Upright = () => { };
        internal Action Close;
        internal readonly Dictionary<string, string> ShownText = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> shownVisibility = new Dictionary<string, bool>();
        private readonly HashSet<string> turned = new HashSet<string>();
        private int turnTab = -1, turnPart = -1;
        internal void PageShown()
        {
            turnTab = turnPart = -1; turned.Clear();
            ShownText.Clear(); shownVisibility.Clear();
        }
        internal void TurnOnce(WindowPage page, string id, float angle)
        {
            ObservePage(page);
            if (turned.Contains(id)) return;
            Rotate(id, angle); turned.Add(id);
        }
        internal void ObservePage(WindowPage page)
        {
            if (turnTab == page.Tab && turnPart == page.Part) return;
            turned.Clear(); turnTab = page.Tab; turnPart = page.Part;
        }
        internal void SetVisible(WindowPage page, string id, bool visible)
        {
            string key = page.Tab + ":" + page.Part + ":" + id;
            if (shownVisibility.TryGetValue(key, out bool shown) && shown == visible) return;
            Visibility(id, visible); shownVisibility[key] = visible;
        }
        internal void SetText(WindowPage page, string id, string text)
        {
            string key = page.Tab + ":" + page.Part + ":" + id;
            if (ShownText.TryGetValue(key, out string shown) && shown == text) return;
            Text(id, text); ShownText[key] = text;
        }
    }
    internal static class PaperWindow
    {
        internal const string HeadingId = "heading", HintId = "reopen_hint", StampId = "closed_stamp", ClosedLabelId = "closed_label";
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        internal static Action Verify = CheckShape;
        internal static Func<MethodInfo> ElementGet = () => typeof(PaperUI).GetMethod("Get", Public, null, new[] { typeof(string) }, null);
        internal static Func<PropertyInfo> ElementRect = () => typeof(CustomUIElement).GetProperty("Rect", Public);
        internal static Func<MethodInfo> ElementHas = () => typeof(PaperUI).GetMethod("Has", Public, null, new[] { typeof(string) }, null);
        private static bool canRotate;
        internal static bool rotationFailed;
        private static Func<PaperUI, string, CustomUIElement> getElement;
        private static Func<CustomUIElement, UnityEngine.RectTransform> getRect;
        private static Func<PaperUI, string, bool> hasElement;
        internal static bool RotationShape(MethodInfo get, PropertyInfo rect) => Method(get, typeof(CustomUIElement), false, typeof(string)) &&
            get.DeclaringType == typeof(PaperUI) && rect != null && rect.DeclaringType == typeof(CustomUIElement) && rect.GetIndexParameters().Length == 0 &&
            rect.PropertyType == typeof(UnityEngine.RectTransform) && Method(rect.GetGetMethod(), typeof(UnityEngine.RectTransform), false) &&
            Method(ElementHas(), typeof(bool), false, typeof(string)) && ElementHas().DeclaringType == typeof(PaperUI);
        private static void RotationFault(Exception error)
        {
            if (rotationFailed) return;
            rotationFailed = true; canRotate = false; SuiteMod.Fault("paper stamp turn", error);
        }
        private static void BindRotation()
        {
            canRotate = false;
            if (rotationFailed) return;
            try
            {
                MethodInfo get = ElementGet(); PropertyInfo rect = ElementRect();
                if (!RotationShape(get, rect)) return;
                getElement = (Func<PaperUI, string, CustomUIElement>)Delegate.CreateDelegate(typeof(Func<PaperUI, string, CustomUIElement>), get);
                getRect = (Func<CustomUIElement, UnityEngine.RectTransform>)Delegate.CreateDelegate(typeof(Func<CustomUIElement, UnityEngine.RectTransform>), rect.GetGetMethod());
                hasElement = (Func<PaperUI, string, bool>)Delegate.CreateDelegate(typeof(Func<PaperUI, string, bool>), ElementHas());
                canRotate = true;
            }
            catch (Exception error) { RotationFault(error); }
        }
        internal static Func<MethodInfo> PageSelector = () => AccessTools.Method(typeof(PaperUI), "FlipTo", new[] { typeof(int) });
        private static void Require(bool yes, string name) { if (!yes) throw new InvalidOperationException("PaperUI " + name + " is laid out differently"); }
        internal static bool Method(MethodInfo method, Type returns, bool isStatic, params Type[] args) => method != null && method.IsPublic &&
            !method.ContainsGenericParameters && method.IsStatic == isStatic && method.ReturnType == returns && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(args);
        private static void Property(PropertyInfo property, Type type) => Require(property != null && property.GetIndexParameters().Length == 0 &&
            property.PropertyType == type && Method(property.GetGetMethod(), type, false), property?.Name ?? "property");
        internal static void CheckShape()
        {
            Type p = typeof(PaperUI);
            Require(p.IsClass && !p.ContainsGenericParameters, "type");
            Require(Method(typeof(PaperUI).GetMethod("Create", Public, null, new[] { typeof(string), typeof(string), typeof(float), typeof(float), typeof(string) }, null), p, true, typeof(string), typeof(string), typeof(float), typeof(float), typeof(string)), "Create");
            Require(Method(typeof(PaperUI).GetMethod("AddPage", Public), p, false, typeof(string), typeof(string)), "AddPage");
            Require(Method(typeof(PaperUI).GetMethod("AddText", Public), p, false, typeof(float), typeof(float), typeof(float), typeof(float), typeof(string), typeof(string), typeof(string), typeof(string), typeof(float)), "AddText");
            Require(Method(typeof(PaperUI).GetMethod("AddLine", Public), p, false, typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(string), typeof(string), typeof(string)), "AddLine");
            Require(Method(typeof(PaperUI).GetMethod("SetChrome", Public), p, false, typeof(bool), typeof(bool), typeof(bool), typeof(bool)), "SetChrome");
            Require(Method(p.GetMethod("AddIconButton", Public), p, false, typeof(float), typeof(float), typeof(float), typeof(float), typeof(string), typeof(Action), typeof(string), typeof(string), typeof(string)), "AddIconButton");
            Require(Method(p.GetMethod("AddImage", Public), p, false, typeof(float), typeof(float), typeof(float), typeof(float), typeof(string), typeof(string), typeof(string)), "AddImage");
            Require(Method(p.GetMethod("SetVisible", Public), p, false, typeof(string), typeof(bool)), "SetVisible");
            BindRotation(); // Optional: missing members leave an upright stamp.
            Require(Method(typeof(PaperAssets).GetMethod("FromMod", Public), typeof(UnityEngine.Sprite), true, typeof(string), typeof(string)), "PaperAssets.FromMod");
            EventInfo closed = p.GetEvent("Closed", Public);
            Require(closed != null && closed.EventHandlerType == typeof(Action<PaperUI>) && Method(closed.GetAddMethod(), typeof(void), false, typeof(Action<PaperUI>)), "Closed");
            Require(Method(typeof(PaperUI).GetMethod("Show", Public), p, false), "Show");
            Require(Method(typeof(PaperUI).GetMethod("Close", Public), typeof(void), false), "Close");
            Require(Method(typeof(PaperUI).GetMethod("SetText", Public), p, false, typeof(string), typeof(string)), "SetText");
            Property(typeof(PaperUI).GetProperty("IsAlive", Public), typeof(bool));
            Property(typeof(PaperUI).GetProperty("IsVisible", Public), typeof(bool));
            Property(typeof(PaperUI).GetProperty("CurrentPage", Public), typeof(int));
            MethodInfo select = PageSelector();
            Require(Method(select, typeof(void), false, typeof(int)) && select.DeclaringType == p, "FlipTo");
            EventInfo shown = typeof(PaperUI).GetEvent("PageShown", Public);
            Require(shown != null && shown.EventHandlerType == typeof(Action<PaperUI, int>) && Method(shown.GetAddMethod(), typeof(void), false, typeof(Action<PaperUI, int>)), "PageShown");
        }
        internal static Action<int> Navigation(object paper, MethodInfo select, Func<int> current)
        {
            Require(Method(select, typeof(void), false, typeof(int)) && select.DeclaringType.IsInstanceOfType(paper), "FlipTo");
            return index => { if (current() != index) select.Invoke(paper, new object[] { index }); };
        }
        private static string Escape(string value) => (value ?? "").Replace("<", "\u2039").Replace(">", "\u203a");
        internal static string Fit(string value, bool wide)
        {
            value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
            float width = wide ? 142 : 196;
            float size = PaperRules.LabelSize(value, width);
            return "<size=" + size.ToString("0.###", CultureInfo.InvariantCulture) + ">" + Escape(PaperRules.Label(value, width)) + "</size>";
        }
        private static string Colour(string text) => "<color=#" + PaperRules.Ink + ">" + (text ?? "").Replace("#" + WindowRules.Green, "#" + PaperRules.Green).Replace("#" + WindowRules.Red, "#" + PaperRules.Red) + "</color>";
        private static string Label(WindowRow row) => row.Section ? Escape(row.Label) : row.Note ? Fitted(row.Label, 196, 8) : Fit(row.Sum || (row.Group == "standing" && row.Value.Length == 0) ? row.Label.ToUpperInvariant() : row.Label, false);
        internal static string[] RowIds(WindowRow row, int i) => row.StandingRow
            ? new[] { "label_" + i, "start_" + i, "today_" + i, "now_" + i }
            : row.Group == "standing_changes" ? new[] { "label_" + i } : new[] { "label_" + i, "value_" + i };
        // Create and Update share this plan. Every id in it is made on that page.
        internal static List<string> ElementIds(WindowPage page)
        {
            var ids = new List<string> { HeadingId, HintId };
            if (page.Tab == 0) { ids.Add(StampId); ids.Add(ClosedLabelId); }
            for (int i = 0; i < page.Rows.Count; i++) ids.AddRange(RowIds(page.Rows[i], i));
            return ids;
        }
        private static string Today(WindowRow row) => row.Colour == null ? Escape(row.Today) : "<color=#" + row.Colour + ">" + Escape(row.Today) + "</color>";
        private static string Fitted(string text, float width, float maximum = 9) => "<size=" + Math.Min(maximum, PaperRules.LabelSize(text, width)).ToString("0.###", CultureInfo.InvariantCulture) + ">" + Escape(PaperRules.Label(text, width)) + "</size>";
        private static string FullLabel(WindowRow row) => Fitted(row.Label, 266);
        private static string Value(WindowRow row) => string.IsNullOrEmpty(row.Colour) ? row.Value : "<color=#" + row.Colour + ">" + row.Value + "</color>";
        internal static void Update(PaperView paper, WindowPage page)
        {
            paper.ObservePage(page);
            paper.SetText(page, HeadingId, Colour(Escape(page.Title.ToUpperInvariant())));
            paper.SetText(page, HintId, Colour(Escape(SuiteWindow.ReopenHint())));
            if (page.Tab == 0)
            {
                bool stamp = SuiteWindow.DayState.Stamp(SuiteWindow.CurrentDay());
                paper.SetVisible(page, StampId, stamp); paper.SetVisible(page, ClosedLabelId, stamp);
                paper.SetText(page, ClosedLabelId, "<color=#" + PaperRules.Red + ">" + Escape(TextTable.Get(TextKeys.DayClosed, SuiteWindow.CurrentDay())) + "</color>");
                if (!rotationFailed)
                {
                    try { paper.TurnOnce(page, StampId, -17); paper.TurnOnce(page, ClosedLabelId, -17); }
                    catch (Exception error)
                    {
                        RotationFault(error);
                        try { paper.Upright(); } catch (Exception) { } // Best effort if the engine rectangle itself has failed.
                    }
                }
            }
            for (int i = 0; i < page.Rows.Count; i++)
            {
                WindowRow row = page.Rows[i];
                string[] ids = RowIds(row, i);
                paper.SetText(page, ids[0], Colour(row.StandingRow ? Fitted(row.Header && !row.DailyStanding ? row.Label.ToUpperInvariant() : row.Label, 104) : row.Group == "standing_changes" ? FullLabel(row) : page.Tab == 2 ? Fitted(row.Label, 124) : Label(row)));
                if (row.StandingRow)
                {
                    paper.SetText(page, ids[1], Colour(StandingCell(row, 0))); paper.SetText(page, ids[2], Colour(StandingCell(row, 1))); paper.SetText(page, ids[3], Colour(StandingCell(row, 2)));
                }
                else if (ids.Length > 1) paper.SetText(page, ids[1], Colour(page.Tab == 2 ? Fit(row.Value, true) : Value(row)));
            }
        }
        internal static PaperView Create(List<WindowPage> pages, int initial)
        {
            if (!PaperRules.Fits(pages)) return null;
            Verify();
            // The game's paper API otherwise draws missing sprites as beige sheets/blue boxes.
            foreach (string name in new[] { "paper", "close", "close_hover", "flip_left", "flip_left_hover", "flip_right", "flip_right_hover", "stamp" })
            {
                UnityEngine.Sprite sprite = PaperAssets.FromMod(SuiteMod.Id, "rnk_suite_" + name);
                if (sprite == null) throw new InvalidOperationException("missing Suite paper picture: " + name);
                if (name == "paper" && (sprite.rect.width != 314 || sprite.rect.height != 402)) throw new InvalidOperationException("Suite paper picture must be 314 by 402");
            }
            PaperUI paper = null;
            try
            {
                paper = PaperUI.Create(SuiteWindow.Id + ".paper", SuiteMod.Id + ":rnk_suite_paper", 0, 0, SuiteMod.Id);
                if (paper == null) throw new InvalidOperationException("PaperUI.Create returned no paper");
                paper.SetChrome(false, false, false, true);
                foreach (WindowPage page in pages)
                {
                    paper.AddPage(page.Title, null);
                    string Sprite(string name) => SuiteMod.Id + ":rnk_suite_" + name;
                    paper.AddIconButton(297, 2.5f, 14, 15, Sprite("close"), null, "close", "close", Sprite("close_hover"));
                    paper.AddIconButton(PaperRules.FlipLeftX, 380, 16, 16, Sprite("flip_left"), null, "left", "flip_left", Sprite("flip_left_hover"));
                    paper.AddIconButton(PaperRules.FlipRightX, 380.5f, 14, 15, Sprite("flip_right"), null, "right", "flip_right", Sprite("flip_right_hover"));
                    paper.AddLine(24, 22, 290, 22, 2, PaperRules.Ink, "none", null);
                    paper.AddText(24, 27, 266, 20, Colour(Escape(page.Title.ToUpperInvariant())), "title", HeadingId, "center", 11.5f);
                    paper.AddLine(24, 50, 290, 50, 2, PaperRules.Ink, "none", null);
                    for (int i = 0; i < page.Rows.Count; i++)
                    {
                        WindowRow row = page.Rows[i];
                        string[] ids = RowIds(row, i);
                        float y = row.Y, height = page.Pitch;
                        if (row.StandingRow)
                        {
                            paper.AddText(24, y, 104, height, Colour(Fitted(row.Header && !row.DailyStanding ? row.Label.ToUpperInvariant() : row.Label, 104)), "body", ids[0], "left", 9);
                            paper.AddText(130, y, 50, height, Colour(StandingCell(row, 0)), "body", ids[1], "right", 9);
                            paper.AddText(183, y, 52, height, Colour(StandingCell(row, 1)), "body", ids[2], "right", 9);
                            paper.AddText(238, y, 52, height, Colour(StandingCell(row, 2)), "body", ids[3], "right", 9);
                        }
                        else if (row.Group == "standing_changes")
                            paper.AddText(24, y, 266, height, Colour(FullLabel(row)), "body", ids[0], "left", 9);
                        else
                        {
                            float labelWidth = page.Tab == 2 ? 124 : 196;
                            paper.AddText(24, y, row.Section ? 266 : labelWidth, height, Colour(page.Tab == 2 ? Fitted(row.Label, 124) : Label(row)), row.Section ? "label" : "body", ids[0], "left", row.Section ? 7.5f : row.Note ? 8 : 9);
                            paper.AddText(24 + labelWidth, y, 266 - labelWidth, height, Colour(page.Tab == 2 ? Fit(row.Value, true) : Value(row)), "body", ids[1], "right", row.Note ? 8 : 9);
                        }
                        if (row.Sum) paper.AddLine(24, y - 2, 290, y - 2, .5f, PaperRules.Ink, "none", null);
                        if (row.Section) paper.AddLine(24, y + height - 2, 290, y + height - 2, .5f, PaperRules.Ink, "none", null);
                        if (row.Sum && page.Tab < 2)
                        {
                            paper.AddLine(24, y + height, 290, y + height, .5f, PaperRules.Ink, "none", null);
                            paper.AddLine(24, y + height + 2, 290, y + height + 2, .5f, PaperRules.Ink, "none", null);
                        }
                    }
                    paper.AddText(24, 350, 266, 18, Colour(Escape(SuiteWindow.ReopenHint())), "handwritten", HintId, "center", 9);
                    if (page.Tab == 0)
                    {
                        float[] stamp = PaperRules.StampGeometry();
                        paper.AddImage(stamp[0], stamp[1], stamp[2], stamp[3], Sprite("stamp"), "fit", StampId);
                        paper.AddText(PaperRules.LetteringX, PaperRules.LetteringY, PaperRules.StampWidth, PaperRules.LetteringHeight, "", "stamp", ClosedLabelId, "center", 8);
                    }
                }
                var stampTurn = new StampTurn(paper);
                var view = new PaperView { Alive = () => paper.IsAlive, Visible = () => paper.IsVisible, Current = () => paper.CurrentPage,
                    Text = (id, text) => paper.SetText(id, text), Visibility = (id, visible) => paper.SetVisible(id, visible),
                    Rotate = stampTurn.Rotate, Upright = stampTurn.Upright, Close = () => paper.Close() };
                paper.PageShown += (_, index) =>
                {
                    try
                    {
                        if (index >= 0 && index < pages.Count)
                        {
                            SuiteWindow.Page = pages[index].Tab;
                            stampTurn.PageShown(); view.PageShown();
                            Update(view, pages[index]);
                        }
                    }
                    catch (Exception error) { SuiteMod.Fault("paper page shown", error); }
                };
                // A failed Show/navigation may close a partial paper; that is not a
                // player closing the report and must not consume its reopening hint.
                bool ready = false;
                paper.Closed += _ => { if (ready) SuiteWindow.PaperClosed(); };
                paper.Show();
                Action<int> navigate = Navigation(paper, PageSelector(), () => paper.CurrentPage);
                view.Select = navigate;
                navigate(initial);
                ready = true;
                return view;
            }
            catch
            {
                if (paper != null) { try { paper.Close(); } catch (Exception) { } }
                throw;
            }
        }
        private static string StandingCell(WindowRow row, int column)
        {
            string text = column == 0 ? row.Start : column == 1 ? row.Today : row.Now;
            if (!row.DailyStanding) return column == 1 ? Today(row) : Escape(text);
            string colour = row.Header || text == "-" ? null : column == 0 ? PaperRules.Green : column == 1 ? PaperRules.Red : row.Colour;
            return colour == null ? Escape(text) : "<color=#" + colour + ">" + Escape(text) + "</color>";
        }
        private sealed class StampRect
        {
            internal UnityEngine.RectTransform Rect;
            internal UnityEngine.Vector2 Pivot;
            internal UnityEngine.Vector3 Position;
            internal UnityEngine.Quaternion Rotation;
            internal void Restore() { if (Rect != null) { Rect.pivot = Pivot; Rect.localPosition = Position; Rect.localRotation = Rotation; } }
        }
        private sealed class StampTurn
        {
            private readonly PaperUI paper;
            private readonly ConditionalWeakTable<UnityEngine.RectTransform, StampRect> original = new ConditionalWeakTable<UnityEngine.RectTransform, StampRect>();
            private readonly List<StampRect> active = new List<StampRect>();
            internal StampTurn(PaperUI paper) { this.paper = paper; }
            internal void PageShown() => active.Clear();
            internal void Upright()
            {
                foreach (StampRect saved in active) { try { saved.Restore(); } catch (Exception) { } }
            }
            internal void Rotate(string id, float angle)
            {
                if (!canRotate || !hasElement(paper, id)) return;
                CustomUIElement element = getElement(paper, id);
                if (element == null) return;
                UnityEngine.RectTransform rect = getRect(element);
                if (rect == null) return;
                float width = rect.rect.width, height = rect.rect.height;
                if (!PaperRules.CanTurn(width, height, rect.anchorMin == rect.anchorMax)) return;
                StampRect saved = original.GetValue(rect, r => new StampRect { Rect = r, Pivot = r.pivot, Position = r.localPosition, Rotation = r.localRotation });
                if (!active.Contains(saved)) active.Add(saved);
                // Derive every position from the upright rectangle, so a repeated
                // PageShown on the same engine object cannot accumulate an offset.
                float[] shift = PaperRules.CentreShift(width, height, saved.Pivot.x, saved.Pivot.y);
                var position = saved.Position + saved.Rotation * new UnityEngine.Vector3(shift[0], shift[1], 0);
                if (id == ClosedLabelId)
                {
                    float[] stamp = PaperRules.StampGeometry();
                    position += new UnityEngine.Vector3((stamp[5] - PaperRules.StampCentreX) * width / PaperRules.StampWidth, -(stamp[6] - (PaperRules.StampCentreY + PaperRules.LetteringBelow)) * height / PaperRules.LetteringHeight, 0);
                }
                rect.pivot = new UnityEngine.Vector2(.5f, .5f);
                rect.localPosition = position;
                var rotation = UnityEngine.Quaternion.Euler(0, 0, angle);
                if (rect.localRotation != rotation) rect.localRotation = rotation;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ShopkeepingSuite
{
    internal sealed class CustomerView
    {
        internal bool Present, HasBudget;
        internal string Name, Faction, Intent;
        internal int Budget, Wanted, Affordable;
    }
    internal sealed class StandingView
    {
        internal string Id, Faction, Status = "";
        internal double Value;
        internal List<StandingStep> Steps = new List<StandingStep>();
    }
    internal static partial class WindowRules
    {
        internal static List<string> Lines(int tab, LedgerState ledger, CustomerView customer, IEnumerable<StandingView> standing)
        {
            return PlainPages(ledger, customer, standing, Settings.On("standing_tally")).Where(p => p.Tab == tab)
                .SelectMany(p => p.Rows).Select(PlainLine).ToList();
        }
        internal static string PlainLine(WindowRow row) => row.Label + (row.Value.Length == 0 ? "" : ": " + row.Value);
    }
    internal static class SuiteWindow
    {
        internal static bool Dirty;
        internal static PaperDayState DayState = new PaperDayState();
        internal static Func<int> CurrentDay = () => Settings.On("ledger_data") ? Ledger.Day() : -1;
        internal static Func<string> BoundKey = () => Settings.KeyCode().ToString();
        internal static Func<string, string> ServiceName = ReadServiceName;
        private static bool suppressClosed;
        internal static string ReopenHint() => PaperRules.Hint(Texts.Get("reopen_hint", "{0}"), BoundKey());
        internal static void PaperClosed()
        {
            if (suppressClosed) return;
            try { if (DayState.TakeHint(CurrentDay())) ReputationFeed.Notify(ReopenHint(), "default"); }
            catch (Exception error) { SuiteMod.Fault("paper reopening hint", error); }
        }
        internal static void OpenAtClosing()
        {
            DayState.ShutterClosed(CurrentDay()); Dirty = true;
            if (!Settings.On("enabled") || !Settings.On("ledger_at_closing") || !Settings.On("window")) return;
            Page = 0; Open();
            if (IsShown()) DayState.AutoOpened(CurrentDay());
        }
        internal static int Page;
        internal static void SelectPage(int page)
        {
            try { if (page >= 0 && page < 4) { Page = page; if (paper != null) Update(false); else Refresh(); } }
            catch (Exception error) { Close(); SuiteMod.Fault("selecting page", error); }
        }
        internal static void AddNavigation(Action<string, Action> add)
        {
            string[] titles = { "today", "run", "customer", "standing" };
            for (int i = 0; i < titles.Length; i++)
            {
                int page = i; string label = Texts.Get(titles[i]);
                add(page == Page ? "[" + label + "]" : label, () => SelectPage(page));
            }
        }
        internal const string Id = SuiteMod.Id + ".main";
        internal static Func<bool> IsOpen = () =>
        {
            if (CustomUIManager.Instance == null) return false;
            CustomUIWindow window = CustomUIManager.Instance.GetWindow(Id);
            return window != null && window.IsAlive && window.IsVisible;
        };
        internal static Action CloseUI = () => { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow(Id); };
        internal static Action<List<string>[]> Build = BuildUI;
        internal static Func<bool> PreferPaper = () => Settings.On("paper_window");
        internal static Func<List<WindowPage>, int, PaperView> MakePaper = PaperWindow.Create;
        private static PaperView paper;
        private static bool paperFailed;
        private static List<WindowPage> paperPages;
        private static List<WindowPage> cachedPages;
        private static string inputKey;
        private static float nextRefresh = float.NegativeInfinity;
        internal static bool IsShown()
        {
            if (paper == null) return IsOpen();
            try { return paper.Alive() && paper.Visible(); }
            catch (Exception error) { FailPaper(error); Open(); return IsOpen(); }
        }
        private static List<string>[] shown;
        private static int shownPage = -1;
        internal static Func<LedgerState> Figures = () => Settings.On("ledger_data") ? Ledger.Snapshot() : new LedgerState();
        internal static Func<CustomerView> Customer = ReadCustomer;
        internal static Func<List<StandingView>> Standing = ReadStanding;
        internal static void Close()
        {
            try { DayState.CancelHint(); ClosePaper(); CloseUI(); }
            catch (Exception error) { SuiteMod.Fault("closing window", error); }
            finally { Dirty = false; shown = null; shownPage = -1; cachedPages = null; inputKey = null; nextRefresh = float.NegativeInfinity; }
        }
        internal static void Open() => Update(false);
        internal static void Refresh() => Update(true);
        private static void Update(bool refresh)
        {
            try
            {
                if (!Settings.On("enabled") || !Settings.On("window")) return;
                if (refresh && !IsShown()) return;
                if (paper != null && refresh) RememberPaperPage();
                int selected = Page;
                if (selected < 0 || selected >= 4) selected = 0;
                Page = selected;
                float now = SuiteRuntime.Clock();
                if (refresh && !Dirty && now < nextRefresh && (paper != null || selected == shownPage))
                {
                    if (paper != null && paperPages != null) PaperWindow.Update(paper, paperPages[Math.Max(0, Math.Min(paperPages.Count - 1, paper.Current()))]);
                    return;
                }
                nextRefresh = now + 1;
                if (!refresh && paper == null) ReputationFeed.ReadForWindow();
                LedgerState figures = Figures(); CustomerView customer = Customer(); var standing = Standing();
                bool tally = Settings.On("standing_tally");
                string key = InputKey(figures, customer, standing, tally);
                if (cachedPages == null || key != inputKey)
                {
                    cachedPages = WindowRules.Pages(figures, customer, standing, tally);
                    foreach (WindowRow row in cachedPages.SelectMany(p => p.Rows).Where(r => r.ServiceId != null))
                        row.Label = Texts.Get("service_name", ServiceName(row.ServiceId));
                    inputKey = key;
                }
                var pages = cachedPages;
                if (PaperRules.Fits(pages) && (paper != null || (!refresh && !paperFailed && PreferPaper())))
                {
                    try
                    {
                        int target = pages.FindIndex(p => p.Tab == selected);
                        if (paper != null && refresh)
                        {
                            int part = paperPages[Math.Max(0, Math.Min(paperPages.Count - 1, paper.Current()))].Part;
                            int samePart = pages.FindIndex(p => p.Tab == selected && p.Part == part);
                            if (samePart >= 0) target = samePart;
                        }
                        bool rebuild = paper == null || !paper.Alive() || !paper.Visible() || paperPages.Count != pages.Count ||
                            paperPages.Where((p, i) => p.Tab != pages[i].Tab || p.Pitch != pages[i].Pitch || p.Rows.Count != pages[i].Rows.Count ||
                                p.Rows.Where((r, j) => r.Y != pages[i].Rows[j].Y || r.StandingRow != pages[i].Rows[j].StandingRow || r.Header != pages[i].Rows[j].Header ||
                                    r.Section != pages[i].Rows[j].Section || r.Note != pages[i].Rows[j].Note || r.Sum != pages[i].Rows[j].Sum || r.Caption != pages[i].Rows[j].Caption || r.Group != pages[i].Rows[j].Group).Any()).Any();
                        if (rebuild)
                        {
                            ClosePaper();
                            CloseUI();
                            paper = MakePaper(pages, target);
                            if (paper != null && !paper.Alive()) throw new InvalidOperationException("the paper could not be made");
                        }
                        else if (!refresh) paper.Select(target);
                        if (paper != null)
                        {
                            paperPages = pages;
                            int current = Math.Max(0, Math.Min(pages.Count - 1, paper.Current()));
                            PaperWindow.Update(paper, pages[current]);
                            shownPage = selected;
                            return;
                        }
                    }
                    catch (Exception error)
                    {
                        FailPaper(error);
                    }
                }
                if (paper != null) ClosePaper();
                var tabs = new List<string>[4];
                var plainPages = WindowRules.PlainPages(figures, customer, standing, tally);
                for (int i = 0; i < tabs.Length; i++) tabs[i] = plainPages.Where(p => p.Tab == i).SelectMany(p => p.Rows).Select(WindowRules.PlainLine).ToList();
                if (refresh && shownPage == selected && shown != null && tabs[selected].SequenceEqual(shown[selected])) return;
                Build(tabs); shown = tabs; shownPage = selected;
            }
            catch (Exception error) { Close(); SuiteMod.Fault("building window", error); }
            finally { Dirty = false; }
        }
        private static string InputKey(LedgerState figures, CustomerView customer, List<StandingView> standing, bool tally)
        {
            string Part(string value) => (value ?? "").Length.ToString(CultureInfo.InvariantCulture) + ":" + (value ?? "");
            return Part(LedgerRules.Serialise(figures)) + Part(LedgerRules.SerialiseStanding(figures)) + tally + "|" +
                customer.Present + "|" + customer.HasBudget + "|" + Part(customer.Name) + Part(customer.Faction) + Part(customer.Intent) +
                customer.Budget + "|" + customer.Wanted + "|" + customer.Affordable + "|" +
                string.Concat(standing.Select(f => Part(f.Id) + Part(f.Faction) + Part(f.Value.ToString("R", CultureInfo.InvariantCulture))));
        }
        internal static void Toggle()
        {
            try { if (IsShown()) { PaperClosed(); Close(); } else Open(); }
            catch (Exception error) { SuiteMod.Fault("toggling window", error); }
        }
        private static void RememberPaperPage()
        {
            if (paperPages == null) return;
            int index = paper.Current();
            if (index >= 0 && index < paperPages.Count) Page = paperPages[index].Tab;
        }
        private static void ClosePaper()
        {
            PaperView old = paper; paper = null; paperPages = null;
            if (old == null) return;
            suppressClosed = true;
            try { old.Close(); } catch (Exception error) { SuiteMod.Fault("closing paper", error); }
            finally { suppressClosed = false; }
        }
        private static string ReadServiceName(string id)
        {
            try
            {
                if (Settings.Disabled.Contains("service_names")) return id;
                var find = Shapes.ServiceFind;
                var name = Shapes.ServiceDisplayName;
                if (!PaperWindow.Method(find, typeof(StoreService), true, typeof(string)) || name == null || !name.IsPublic || name.IsStatic || name.FieldType != typeof(string)) return id;
                var service = find.Invoke(null, new object[] { id });
                string text = service == null ? null : (string)name.GetValue(service);
                return string.IsNullOrWhiteSpace(text) ? id : text;
            }
            catch (Exception error) { SuiteMod.Fault("service name", error); return id; }
        }
        private static void FailPaper(Exception error)
        {
            paperFailed = true; ClosePaper(); shown = null; shownPage = -1;
            SuiteMod.Fault("paper window", error);
        }
        private static void BuildUI(List<string>[] tabs)
        {
            if (CustomUIManager.Instance == null) return;
            var b = CustomUIManager.Window(Id, Texts.Get("title"), CustomUIManager.LAYER_PAPER).SetSize(620, 590).Center().SetDraggable(true);
            b.BeginRow();
            AddNavigation((label, click) => b.AddButton(label, click));
            b.End().BeginScroll(440);
            foreach (string line in tabs[Page]) b.AddLabel(line);
            b.End().AddCloseButton(() => { PaperClosed(); Close(); }).Show();
        }
        private static CustomerView ReadCustomer()
        {
            var view = new CustomerView();
            StoreClient client = !Settings.On("customer_data") || Marks.Outside() ? null : Marks.Current();
            if (client == null) return view;
            view.Present = true; view.Name = string.IsNullOrWhiteSpace(client.displayName) ? Rules.Readable(client.identifier, "") : client.displayName;
            view.Faction = Rules.Readable(client.clientFaction, "FACTION_"); view.Intent = Rules.Intent(client.clientIntent.ToString());
            view.HasBudget = client.useClientBudget; view.Budget = Wanted.Budget(client);
            if (Marks.IsBuyer(client) && Settings.On("wanted_data") && Settings.On("sale_data"))
            {
                foreach (GameItem item in Marks.SaleItems())
                {
                    if (!Marks.Owned(item)) continue;
                    int demand = Wanted.Get(client, item);
                    if (demand > 0) view.Wanted++;
                    if (demand == 2) view.Affordable++;
                }
            }
            return view;
        }
        private static List<StandingView> ReadStanding()
        {
            return ReputationFeed.Latest;
        }
    }
}


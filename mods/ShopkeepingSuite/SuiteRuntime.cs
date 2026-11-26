using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShopkeepingSuite
{
    internal static class SuiteRuntime
    {
        internal static bool Running;
        internal static Func<IEnumerator, Coroutine> Start = routine => ModHelper.StartCoroutine(SuiteMod.Id, routine);
        internal static Action Stop = () => ModHelper.StopAllCoroutines(SuiteMod.Id);
        internal static void EnsureLoop() => TryStartLoop(true);
        internal static void TryStartLoop(bool reportPending)
        {
            try
            {
                if (!Active || Running || !Settings.On("runtime")) return;
                Running = (object)Start(Loop()) != null;
                if (!Running && reportPending) SuiteMod.Fault("loop pending", new InvalidOperationException("the window key and live marks are not running yet; retrying at the next load hook"));
            }
            catch (Exception error) { SuiteMod.Fault("starting loop", error); }
        }
        internal static void AssetsLoaded(LoadedMod mod)
        {
            try { if (mod?.Manifest?.ID == SuiteMod.Id) EnsureLoop(); }
            catch (Exception error) { SuiteMod.Fault("assets loaded", error); }
        }
        internal static void StopLoop()
        {
            Active = false;
            HandsHome.Reset();
            InspectionScreen.Reset();
            TabletScreen.Reset();
            try { Stop(); }
            catch (Exception error) { SuiteMod.Fault("stopping loop", error); }
            finally { Running = false; }
        }
        internal static bool Active;
        internal static Func<bool> HasRun = () => PlayerStore.HasInstance;
        internal static Func<int> FrameNumber = () => Time.frameCount;
        internal static Func<float> Clock = () => Time.unscaledTime;
        internal static Func<bool> KeyPressed = () => Input.GetKeyDown(Settings.KeyCode());
        internal static Func<bool> Typing = () => KeyBindHelper.SuppressHotkeys || (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            (EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null || EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null));
        internal static Action<RichTextBuilder, string> AddLine = (builder, text) => builder.AddLine(text, true, RenderHandler.ColorPalette.LightCyan);
        private static float nextPoll;
        private static int polledFrame = -1;
        internal static void SettingsChanged()
        {
            HandsHome.SettingsChanged();
            InspectionScreen.SettingsChanged();
            TabletScreen.SettingsChanged();
            EnsureLoop();
            try
            {
                Ledger.CheckTallySwitch();
                ReputationFeed.Reset();
                if (!Active) return;
                Marks.EndDrag(); Marks.RequestAll(); SuiteWindow.Dirty = true;
                if (!Settings.On("enabled") || !Settings.On("window")) SuiteWindow.Close();
            }
            catch (Exception error) { SuiteMod.Fault("settings change", error); }
        }
        internal static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            Prices.Tooltip(builder, item);
            Counterfeit.Tooltip(builder, item);
            try
            {
                if (!Settings.On("enabled") || item == null || (!Settings.On("tooltip_wanted") && !Settings.On("tooltip_risk") && !Settings.On("tooltip_appraisal"))) return;
                foreach (string line in Rules.Tooltip(Marks.ReadFacts(item, true), Settings.On)) AddLine(builder, line);
            }
            catch (Exception error) { SuiteMod.Fault("tooltip", error); }
        }
        internal static void Loaded()
        {
            HandsHome.Reset();
            InspectionScreen.Reset();
            TabletScreen.Reset();
            try
            {
                ReputationFeed.Reset(); AllStanding.Loaded();
                SuiteWindow.DayState = new PaperDayState();
                Marks.Reset();
                Marks.ShutterClosed = true;
                try
                {
                    if (Settings.On("marks_data") && HasRun())
                    {
                        Marks.ShutterClosed = Marks.StoreClosed();
                        SuiteWindow.DayState.Loaded(SuiteWindow.CurrentDay(), Marks.ShutterClosed);
                    }
                }
                catch (Exception error) { SuiteMod.Fault("reading shutter state", error); }
                nextPoll = 0; polledFrame = -1; Marks.RequestAll(); SuiteWindow.Page = 0; SuiteWindow.Close();
            }
            catch (Exception error) { SuiteMod.Fault("save loaded", error); }
            EnsureLoop();
        }
        internal static void Opened()
        {
            try { Marks.ShutterClosed = false; Poll(); Marks.RequestAll(); }
            catch (Exception error) { SuiteMod.Fault("shutter opened", error); }
        }
        internal static void Closed()
        {
            try { Marks.ShutterClosed = true; Marks.EndDrag(); Marks.ObserveCustomer(null, Marks.OutsideShop); Wanted.Reset(); Marks.RequestAll(); }
            catch (Exception error) { SuiteMod.Fault("clearing closing marks", error); }
            try { SuiteWindow.OpenAtClosing(); }
            catch (Exception error) { SuiteMod.Fault("shutter closed", error); }
        }
        internal static void Arrived()
        {
            try { if (Settings.On("enabled")) { Poll(); Marks.RequestAll(); SuiteWindow.Dirty = true; } }
            catch (Exception error) { SuiteMod.Fault("customer arrived", error); }
        }
        internal static void Sale()
        {
            try
            {
                if (!Settings.On("enabled")) return;
                StoreClient client = Settings.On("customer_data") ? Marks.Current() : null;
                if (Settings.On("wanted_data") && Wanted.Observe(client)) Marks.RequestAll();
                Poll();
                SuiteWindow.Dirty = true;
            }
            catch (Exception error) { SuiteMod.Fault("sale budget", error); }
        }
        internal static void Poll()
        {
            HandsHome.Poll();
            if (!Settings.On("enabled")) return;
            int frame = FrameNumber(); if (frame == polledFrame) return;
            polledFrame = frame;
            ReputationFeed.Poll();
            Prices.Poll();
            StoreClient client = Settings.On("customer_data") ? Marks.Current() : null;
            bool away = !Settings.On("location_data") || Marks.Outside();
            bool changed = Marks.ObserveCustomer(client, away);
            if (Settings.On("wanted_data")) changed |= Wanted.Observe(client);
            if (changed) { Marks.RequestAll(); SuiteWindow.Dirty = true; }
            try { Marks.CheckDrag(); } catch (Exception error) { SuiteMod.Fault("checking drag", error); }
            try { Ledger.Touch(); } catch (Exception error) { SuiteMod.Fault("ledger poll", error); }
        }
        internal static void Frame()
        {
            InspectionScreen.Tick();
            TabletScreen.Tick();
            try
            {
                if (!HasRun()) return;
                if (!Settings.On("enabled")) { Marks.RefreshStep(); return; }
                if (Settings.On("enabled") && Settings.On("window") && Settings.On("key_data") && KeyPressed() && !Typing()) SuiteWindow.Toggle();
                float now = Clock(); if (now >= nextPoll) { nextPoll = now + 1f; Poll(); }
                InspectionSearch.Poll();
                WantedInside.Poll();
                Marks.RefreshStep();
                if (Settings.On("window") && SuiteWindow.IsShown()) SuiteWindow.Refresh();
            }
            catch (Exception error) { SuiteMod.Fault("frame", error); }
        }
        internal static IEnumerator Loop() { while (Active) { Frame(); yield return null; } }
    }
}

// Scavenging Pool: all of a night's scavenging in one trip.
//
// In the game every arrival at the dumping ground gives a fresh set of attempts, so a
// player with several outings a night (a perk, or a mod that changes travelling) gets the
// most out of scavenging by spending every trip there. This mod turns the attempts of
// those trips into one pool for the night:
//   - the most attempts in a night is the attempts of one trip times the night's trips,
//   - the attempts used are remembered for the night, so leaving and coming back does
//     not give them again,
//   - the chance of a wound is worked out as if each trip's worth of attempts were a
//     trip of its own, so the pool is no more dangerous than the separate trips were.
// The other trips are then free for something else. Nothing here uses a trip up.
using System;
using HarmonyLib;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("2.1.2.0")]

namespace ScavengingPool
{
    public class PoolMod : IMod
    {
        internal const string Id = "rnk.scavenging-pool";
        internal const string OldId = "local.scavenging-pool";
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> Rounds;
        internal static ConfigEntry<int> MostTrips;
        internal static ConfigEntry<bool> ReadTransitQuota;
        internal static ConfigEntry<bool> RiskPerRound;
        internal static ConfigEntry<bool> CountTrips;

        public void Init(ModManifest manifest)
        {
            ModConfig config = ModConfig.For(manifest.ID);
            Enabled = config.Define("enabled", true, "Scavenging pool",
                () => "One pool of attempts per night",
                () => "The attempts of all the night's trips can be used in one visit to the dumping ground, and coming back the same night does not give new ones. Off: the game's own rule, a fresh set on every arrival.");
            Rounds = config.Define("rounds", 0, "Scavenging pool",
                () => "Trips counted into the pool (0 = automatic)", 0, Night.MostRounds,
                () => "0: tonight's trips, up to Most trips counted into the pool. Transit Quota's quota is used when that mod runs and its setting below is on; otherwise the game's outings are used. 1 to 10: that many trips' worth of attempts, without the automatic cap.");
            MostTrips = config.Define("most_trips", 4, "Scavenging pool",
                () => "Most trips counted into the pool", 1, Night.MostRounds,
                () => "The cap for automatic counting. A fixed number in the setting above is not capped.");
            ReadTransitQuota = config.Define("read_transit_quota", true, "Scavenging pool",
                () => "Count Transit Quota's trips instead of the game's counter",
                () => "On: when Transit Quota runs, count its nightly quota, which leaves out one-time cards. Off: count the game's outings, including cards added by travel mods. Takes effect at the next night's count. If Transit Quota cannot be read, the game's outings are used. The automatic cap applies to either count.");
            CountTrips = config.Define("count_trips", true, "Scavenging pool",
                () => "Count the night's trips into the pool",
                () => "On: the pool is one trip's attempts times the trips counted by the settings above. Off: the pool is one trip's attempts, whatever your perks, events and other mods give for one trip; it is still kept for the night, so leaving and coming back does not give it again.");
            RiskPerRound = config.Define("risk_per_round", true, "Scavenging pool",
                () => "Wound chance starts over for each trip's worth",
                () => "On: the chance of a wound rises within one trip's worth of attempts and then starts over, as it did on separate trips. Off: it keeps rising through the whole pool.");
            LocalMods.OptionsMenu.Register(manifest.ID, "Off the Books: Scavenging Pool", Pool.Fault);
        }

        public void OnEnable()
        {
            try
            {
                ModHook.OnGameLoadedEarly -= Pool.Forget;
                ModHook.OnGameLoadedEarly += Pool.Forget;
                if (LeavingFits()) SubscribeLeaving();
                else Pool.Report("leaving shape", "the leaving-store hook differs; counting outings with the fallback rule.");
                Pool.Forget();
            }
            catch (Exception error) { Pool.Fault("enabling", error); }
        }

        public void OnDisable()
        {
            try { ModHook.OnGameLoadedEarly -= Pool.Forget; if (LeavingFits()) UnsubscribeLeaving(); }
            catch (Exception error) { Pool.Fault("disabling", error); }
        }
        internal static bool LeavingFits()
        {
            var hook = typeof(ModHook).GetEvent("OnLeavingStoreEarly", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return hook?.EventHandlerType == typeof(Action) && hook.GetAddMethod()?.IsStatic == true && hook.GetRemoveMethod()?.IsStatic == true;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SubscribeLeaving() { ModHook.OnLeavingStoreEarly -= Pool.Leaving; ModHook.OnLeavingStoreEarly += Pool.Leaving; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void UnsubscribeLeaving() => ModHook.OnLeavingStoreEarly -= Pool.Leaving;
    }

    // The rules, without the game around them, so that they can be tested.
    internal static class Night
    {
        // Mod data has no presence query; the counters never use int.MinValue.
        internal static int ReadSaved(int fallback, Func<int, int> current, Func<int, int> old)
        {
            int value = current(int.MinValue);
            if (value != int.MinValue) return value;
            value = old(int.MinValue);
            return value == int.MinValue ? fallback : value;
        }

        internal const int MostRounds = 10;

        // The night's attempts: one trip's worth times the trips, at least one trip.
        internal static int Pool(int perTrip, int rounds)
        {
            if (perTrip <= 0) return perTrip;
            if (rounds < 1) rounds = 1;
            if (rounds > MostRounds) rounds = MostRounds;
            long pool = (long)perTrip * rounds;
            return pool > int.MaxValue ? int.MaxValue : (int)pool;
        }

        // The attempts used within the current trip's worth, which is what the game's
        // wound chances rise with.
        internal static int InRound(int used, int perTrip)
        {
            if (used <= 0) return 0;
            return perTrip <= 0 ? used : used % perTrip;
        }

        // What the counter is set to on an arrival: the night's count on the same day,
        // nothing on another day or when no night is remembered.
        internal static int OnArrival(int today, int rememberedDay, int rememberedUsed)
        {
            return today >= 0 && rememberedDay == today && rememberedUsed > 0 ? rememberedUsed : 0;
        }

        // A fixed setting wins. Automatic uses the selected source up to the cap,
        // and at least one when the source is absent or answers nonsense.
        internal static int RoundsFor(int setting, int tripsTonight, int cap)
        {
            int rounds = setting > 0 ? setting : tripsTonight;
            if (setting <= 0) rounds = Math.Min(rounds, Math.Max(1, Math.Min(MostRounds, cap)));
            if (rounds < 1) return 1;
            return rounds > MostRounds ? MostRounds : rounds;
        }
    }

    internal static class Pool
    {
        // Replaced by the tests; in the game they are the game's own values.
        internal static Action<string> Log = message => Debug.LogWarning(message);
        internal static Func<int> Today = () => StoreStation.GetDayCounter();
        internal static Func<string, int, int> Read = (key, fallback) => Night.ReadSaved(fallback,
            missing => ModHelper.GetModDataInt(PoolMod.Id, key, missing),
            missing => ModHelper.GetModDataInt(PoolMod.OldId, key, missing));
        internal static Action<string, int> Write = (key, value) => ModHelper.SetModDataInt(PoolMod.Id, key, value);
        internal static Func<int> Trips = OutingsTonight;
        // The game's own counter of outings left tonight. The game sets it each night
        // (one, or more with a perk) and lowers it when the player comes home.
        internal static Func<int> Visits = () => PlayerStore.Instance.visitLeftTonight;
        // The game's counter of attempts used, and one trip's worth as the game and
        // every mod give it now (asking for it also runs this mod's own postfix,
        // which notes the number before multiplying it).
        internal static Func<bool> HasStore = () => PlayerStore.HasInstance;
        internal static Func<int> Count = () => PlayerStore.Instance.scavengingAttempts;
        internal static Action<int> SetCount = value => PlayerStore.Instance.scavengingAttempts = value;
        internal static Action AskMost = () => ScavHelper.GetMaxScavAttempts();

        internal const string DayKey = "night_day", UsedKey = "night_used";
        internal const string RoundsDayKey = "rounds_day", RoundsKey = "rounds_most";
        internal const string LeavingDayKey = "leaving_day", LeavingKey = "leaving_most";
        private const string RoundsQuotaKey = "rounds_from_quota", LeavingQuotaKey = "leaving_from_quota";
        private const string RuleKey = "trips_rule", SourceDayKey = "trips_source_day", SourceKey = "trips_read_quota", LogDayKey = "trips_logged_day";
        private const int TripRule = 210;

        // One trip's worth of attempts, as the game and the other mods last gave it,
        // before this mod multiplied it. Zero until it has been asked for.
        internal static int PerTrip;

        private static int knownDay = int.MinValue, knownMost = -1;
        private static int leavingMost = -1;
        private static bool knownQuota, leavingQuota;
        private static bool retryLeavingQuota;
        private static bool useQuota;
        private static int loggedDay = int.MinValue;
        private static readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        internal static bool On => PoolMod.Enabled == null || PoolMod.Enabled.Value;
        internal static bool PerRound => PoolMod.RiskPerRound == null || PoolMod.RiskPerRound.Value;

        internal static void Report(string key, string message)
        {
            try
            {
                if (reported.Add(key)) Log("[Scavenging Pool] " + message);
            }
            catch
            {
            }
        }

        internal static void Fault(string where, Exception error)
        {
            string text;
            try { text = error.GetType().Name + ": " + error.Message; }
            catch { text = "an error that cannot be shown"; }
            Report("fault " + where, "a fault in " + where + "; the game's own rule is used there. " + text);
        }

        // Replaced by the tests; in the game it is the setting.
        internal static Func<bool> Counting = () => PoolMod.CountTrips == null || PoolMod.CountTrips.Value;
        internal static Func<int> FixedRounds = () => PoolMod.Rounds == null ? 0 : PoolMod.Rounds.Value;
        internal static Func<int> Cap = () => PoolMod.MostTrips == null ? 4 : PoolMod.MostTrips.Value;
        internal static Func<bool> ReadTransitQuota = () => PoolMod.ReadTransitQuota == null || PoolMod.ReadTransitQuota.Value;

        internal static int RoundsNow()
        {
            // Switched off: one trip's worth, without asking anybody for trips.
            if (!Counting()) return 1;
            int setting = FixedRounds();
            int trips = 1;
            if (setting <= 0)
            {
                try { trips = Trips(); }
                catch (Exception error) { Fault("counting the night's outings", error); }
            }
            return Night.RoundsFor(setting, trips, setting > 0 ? Night.MostRounds : Cap());
        }

        // Prefer the highest departure count saved for today. While outside a travel
        // mod may include the way home. Before any departure has been noted (older saves
        // or installation mid-outing), retain the previous maximum-seen fallback.
        private static int OutingsTonight()
        {
            if (!HasStore()) return 1;
            int today = Today();
            if (today < 0) return Math.Max(1, ReadTrips(ReadTransitQuota(), out _));
            // Asked on every frame while the panel is open, so the save's table is only
            // read when the day changes or a game has been loaded.
            if (knownDay != today)
            {
                ReadDay(today);
            }
            if (leavingMost >= 0)
            {
                // Retry a fallback once after each departure or load, not every frame.
                if (retryLeavingQuota)
                {
                    retryLeavingQuota = false;
                    if (useQuota && !leavingQuota && TransitQuotaReader.TryRead(out int total))
                        RememberLeaving(today, total, true);
                }
                return Math.Max(1, leavingMost);
            }
            int now = ReadTrips(useQuota, out bool fromQuota);
            if ((fromQuota && !knownQuota) || (fromQuota == knownQuota && now > knownMost))
            {
                // The number before the day, as for the attempts used.
                Write(RoundsKey, now);
                Write(RoundsQuotaKey, fromQuota ? 1 : 0);
                if (Read(RoundsDayKey, -1) != today) Write(RoundsDayKey, today);
                knownMost = now;
                knownQuota = fromQuota;
            }
            LogTonight(today, knownMost, knownQuota);
            return Math.Max(1, knownMost);
        }

        private static void ReadDay(int today)
        {
            // A previous counting rule may have remembered a card-inflated count today.
            // Invalidate both days before marking migration complete. A failed write
            // retries this sequence, never accepting either old maximum as new data.
            if (Read(RuleKey, 0) != TripRule)
            {
                Write(RoundsDayKey, -1);
                Write(LeavingDayKey, -1);
                Write(SourceDayKey, -1);
                Write(LogDayKey, -1);
                Write(RuleKey, TripRule);
            }
            int fallback = Read(RoundsDayKey, -1) == today ? Read(RoundsKey, 0) : -1;
            int leaving = Read(LeavingDayKey, -1) == today ? Read(LeavingKey, 0) : -1;
            bool source;
            if (Read(SourceDayKey, -1) == today) source = Read(SourceKey, 1) != 0;
            else
            {
                source = ReadTransitQuota();
                // Invalidate the day first, so a partial write cannot change the
                // source of a different remembered night. Loads keep today's choice.
                Write(SourceDayKey, -1);
                Write(SourceKey, source ? 1 : 0);
                Write(SourceDayKey, today);
            }
            useQuota = source;
            loggedDay = Read(LogDayKey, -1);
            knownQuota = fallback >= 0 && Read(RoundsQuotaKey, 0) != 0;
            leavingQuota = leaving >= 0 && Read(LeavingQuotaKey, 0) != 0;
            knownMost = fallback; leavingMost = leaving; knownDay = today;
            retryLeavingQuota = true;
        }

        private static int ReadTrips(bool quota, out bool fromQuota)
        {
            if (quota && TransitQuotaReader.TryRead(out int total))
            {
                fromQuota = true;
                return total;
            }
            fromQuota = false;
            return Math.Max(0, Visits());
        }

        private static void LogTonight(int today, int raw, bool fromQuota)
        {
            if (loggedDay == today) return;
            loggedDay = today;
            try
            {
                Write(LogDayKey, today);
                string source = fromQuota ? "Transit Quota's quota" : "the game's outings";
                int cap = Math.Max(1, Math.Min(Night.MostRounds, Cap()));
                Log("[Scavenging Pool] trips tonight: " + Night.RoundsFor(0, raw, cap)
                    + " (" + source + " " + raw + ", cap " + cap + ").");
            }
            catch (Exception error) { Fault("logging tonight's trips", error); }
        }

        internal static void Leaving()
        {
            try
            {
                if (!HasStore()) return;
                if (!Counting() || FixedRounds() > 0) return;
                int today = Today(); if (today < 0) return;
                if (knownDay != today) ReadDay(today);
                int now = ReadTrips(useQuota, out bool fromQuota);
                if (!fromQuota && knownQuota)
                {
                    now = knownMost;
                    fromQuota = true;
                }
                RememberLeaving(today, now, fromQuota);
                retryLeavingQuota = true;
                LogTonight(today, leavingMost, leavingQuota);
            }
            catch (Exception error) { Fault("noting outings when leaving", error); }
        }

        private static void RememberLeaving(int today, int now, bool fromQuota)
        {
            if (leavingQuota && !fromQuota) return;
            if (fromQuota == leavingQuota && now <= leavingMost) return;
            Write(LeavingKey, now);
            Write(LeavingQuotaKey, fromQuota ? 1 : 0);
            if (Read(LeavingDayKey, -1) != today) Write(LeavingDayKey, today);
            leavingMost = now;
            leavingQuota = fromQuota;
        }

        // What is kept in memory belongs to one save; forgotten when a game is loaded.
        internal static void Forget()
        {
            knownDay = int.MinValue;
            knownMost = -1;
            leavingMost = -1;
            knownQuota = leavingQuota = false;
            loggedDay = int.MinValue;
        }

        // The count is written before the day. If the second write fails, the old day
        // stays, and the night is taken for a new one: attempts are given again, never
        // an old night's count carried into this one.
        internal static void Remember(int used)
        {
            int today = Today();
            if (today < 0) return;
            bool newDay = Read(DayKey, -1) != today;
            if (newDay || Read(UsedKey, 0) != used) Write(UsedKey, used);
            if (newDay) Write(DayKey, today);
        }

        // Called after every search, with the game's counter before and after it. The
        // night's total grows by what the search added, also while the pool is switched
        // off, when the game's counter starts from zero on each arrival. Switching the
        // pool on again the same night then finds every attempt of the night counted.
        internal static void AfterSearch(int before, int after)
        {
            long added = (long)after - before;
            if (added <= 0) return;
            long total = Remembered() + added;
            if (On && after > total) total = after;
            Remember(total > int.MaxValue ? int.MaxValue : (int)total);
        }

        // Before a search with the pool switched on: a counter that is behind the
        // night's total is brought up to it. That happens when the pool was switched
        // off for part of the night and on again without a new arrival.
        internal static void CatchUp()
        {
            if (!On) return;
            int total = Remembered();
            if (Count() < total) SetCount(total);
        }

        internal static int Remembered()
        {
            return Night.OnArrival(Today(), Read(DayKey, -1), Read(UsedKey, 0));
        }
    }

    // Last, so that what other mods add to one trip's attempts is multiplied too.
    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMaxScavAttempts))]
    [HarmonyPriority(Priority.Last)]
    internal static class MostAttemptsPatch
    {
        private static void Postfix(ref int __result)
        {
            try
            {
                Pool.PerTrip = __result;
                if (Pool.On && __result > 0) __result = Night.Pool(__result, Pool.RoundsNow());
            }
            catch (Exception error)
            {
                Pool.Fault("working out the night's attempts", error);
            }
        }
    }

    // The game sets the count to zero on every arrival at the dumping ground.
    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.ResetScavenging))]
    internal static class ArrivalPatch
    {
        private static void Postfix()
        {
            try
            {
                if (!Pool.On || !Pool.HasStore()) return;
                int used = Pool.Remembered();
                if (used > 0) Pool.SetCount(used);
            }
            catch (Exception error)
            {
                Pool.Fault("putting back the night's used attempts", error);
            }
        }
    }

    // The finalizer runs however the search ended, also when another mod stopped it.
    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.ScavengeDumpingGrounds))]
    internal static class SearchPatch
    {
        internal const int Unknown = int.MinValue;

        private static void Prefix(out int __state)
        {
            __state = Unknown;
            try
            {
                if (!Pool.HasStore()) return;
                Pool.CatchUp();
                __state = Pool.Count();
            }
            catch (Exception error)
            {
                Pool.Fault("reading the count before a search", error);
            }
        }

        private static void Finalizer(int __state)
        {
            try
            {
                if (__state == Unknown || !Pool.HasStore()) return;
                Pool.AfterSearch(__state, Pool.Count());
            }
            catch (Exception error)
            {
                Pool.Fault("remembering the night's used attempts", error);
            }
        }
    }

    // The game's two wound chances rise with the attempts used. While one of them is
    // worked out, the count is shown as the attempts within the current trip's worth
    // and put back afterwards, whatever happened in between. One trip's worth is asked
    // for anew each time, because an event or a perk can change it during a night.
    // Asking runs other mods' code; if that code asks for a wound chance in turn, the
    // inner question is left to the game, so the asking cannot go round in a circle.
    internal static class Risk
    {
        internal const int Untouched = -1;
        private static bool asking;

        internal static int Lower()
        {
            try
            {
                if (asking || !Pool.On || !Pool.PerRound || !Pool.HasStore()) return Untouched;
                asking = true;
                try
                {
                    Pool.AskMost();
                }
                finally
                {
                    asking = false;
                }
                int used = Pool.Count();
                int shown = Night.InRound(used, Pool.PerTrip);
                if (shown == used) return Untouched;
                Pool.SetCount(shown);
                return used;
            }
            catch (Exception error)
            {
                Pool.Fault("lowering the count for a wound chance", error);
                return Untouched;
            }
        }

        internal static void PutBack(int used)
        {
            if (used == Untouched) return;
            try
            {
                if (Pool.HasStore()) Pool.SetCount(used);
            }
            catch (Exception error)
            {
                Pool.Fault("putting the count back after a wound chance", error);
            }
        }
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMinorWoundChance))]
    [HarmonyPriority(Priority.First)]
    internal static class MinorWoundPatch
    {
        private static void Prefix(out int __state) => __state = Risk.Lower();
        private static void Finalizer(int __state) => Risk.PutBack(__state);
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMajorWoundChance))]
    [HarmonyPriority(Priority.First)]
    internal static class MajorWoundPatch
    {
        private static void Prefix(out int __state) => __state = Risk.Lower();
        private static void Finalizer(int __state) => Risk.PutBack(__state);
    }
}

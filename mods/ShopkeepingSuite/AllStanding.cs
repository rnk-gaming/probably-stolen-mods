using LocalMods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ShopkeepingSuite
{
    // Both the notice and the window read this list. The optional adapters are independent.
    internal static class AllStanding
    {
        internal static Func<List<StandingView>> Game = ReadGame;
        internal static Func<StandingView> Scavengers = () => WithSteps(OptionalStanding.Scavengers.Read(), StandingBonuses.ScavengerSteps);
        internal static Func<StandingView> Dsi = () => WithSteps(OptionalStanding.Dsi.Read(), StandingBonuses.DsiSteps);
        private static readonly Dictionary<string, List<StandingStep>> steps = new Dictionary<string, List<StandingStep>>(StringComparer.Ordinal);
        private static List<StandingStep> Steps(string id, Func<List<StandingStep>> read)
        {
            if (!steps.TryGetValue(id, out var value)) steps[id] = value = read();
            return value;
        }
        private static StandingView WithSteps(StandingView faction, Func<List<StandingStep>> read)
        { if (faction != null) faction.Steps = Steps(faction.Id, read); return faction; }
        private static readonly HashSet<string> failed = new HashSet<string>();
        internal static List<StandingView> Read()
        {
            var result = new List<StandingView>();
            if (!SuiteRuntime.HasRun()) return result;
            if (Settings.On("standing_data") && !failed.Contains("game standing"))
            {
                try { result.AddRange(Game()); }
                catch (Exception error) { failed.Add("game standing"); SuiteMod.Fault("game standing", error); }
            }
            Add(result, "Scavengers", Scavengers); Add(result, "DSI", Dsi);
            return result.Where(f => f != null && !string.IsNullOrEmpty(f.Id) && LedgerRules.Finite(f.Value))
                .GroupBy(f => f.Id, StringComparer.Ordinal).Select(g => g.First()).ToList();
        }
        private static void Add(List<StandingView> result, string name, Func<StandingView> read)
        {
            if (failed.Contains(name)) return;
            try { StandingView value = read(); if (value != null) result.Add(value); }
            catch (Exception error) { failed.Add(name); SuiteMod.Fault(name + " standing", error); }
        }
        private static List<StandingView> ReadGame()
        {
            var result = new List<StandingView>();
            var factions = ReputationFeed.Read();
            if (factions == null) return result;
            foreach (StoreReputation faction in factions)
            {
                if (faction == null) continue;
                result.Add(new StandingView { Id = faction.factionId,
                    Faction = string.IsNullOrWhiteSpace(faction.factionDisplay) ? Rules.Readable(faction.factionId, "FACTION_") : faction.factionDisplay,
                    Value = ReputationFeed.Value(faction), Steps = Steps(faction.factionId, () => StandingBonuses.GameSteps(faction)) });
            }
            return result;
        }
        // Language changes have no established cheap signal: refresh text once per loaded run.
        internal static void Loaded()
        {
            steps.Clear(); OptionalStanding.Scavengers.ClearCache(); OptionalStanding.Dsi.ClearCache();
            Ledger.Loaded(); WindowRules.ClearLabels(); StandingRules.ClearLabels();
        }
    }

    internal sealed class OptionalFaction
    {
        // name is the existing log scope; only nameKey supplies the player-facing name.
        private readonly string id, name, nameKey;
        private readonly float interval;
        private readonly Func<Func<double?>> bind;
        private bool bound, failed;
        private Func<double?> reader;
        private StandingView cached;
        private float nextRead = float.NegativeInfinity;
        internal OptionalFaction(string id, string name, string nameKey, float interval, Func<Func<double?>> bind)
        { this.id = id; this.name = name; this.nameKey = nameKey; this.interval = interval; this.bind = bind; }
        internal void ClearCache() { cached = null; nextRead = float.NegativeInfinity; }
        internal StandingView Read()
        {
            if (failed || !SuiteRuntime.HasRun()) return null;
            string stage = " shape";
            try
            {
                if (!bound) { reader = bind(); bound = true; }
                if (reader == null) return null; // Not installed: no warning.
                stage = " read";
                float now = SuiteRuntime.Clock();
                if (interval > 0 && now < nextRead) return cached;
                nextRead = now + interval;
                double? value = reader();
                if (value.HasValue && !LedgerRules.Finite(value.Value)) throw new InvalidOperationException("non-finite standing");
                cached = value.HasValue ? new StandingView { Id = id, Faction = TextTable.Get(nameKey), Value = value.Value } : null;
                return cached;
            }
            catch (Exception error) { failed = true; cached = null; SuiteMod.Fault(name + " standing" + stage, error); return null; }
        }
    }
    internal static class OptionalStanding
    {
        internal static Func<PropertyInfo> ScavengerExact = () => AccessTools.Property(AccessTools.TypeByName("DumpingGroundMap.Reputation"), "ExactPoints");
        internal static Func<PropertyInfo> ScavengerStore = () => AccessTools.Property(AccessTools.TypeByName("DumpingGroundMap.Reputation"), "HasStore");
        internal static Func<MethodInfo> ForgeData = () => AccessTools.Method(typeof(ModHelper), "GetModData", new[] { typeof(string), typeof(string), typeof(string) });
        internal static Func<string, string, Type> Resolve = ResolveType;
        private static Type ResolveType(string assembly, string name)
        {
            Assembly loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assembly);
            if (loaded == null) return null;
            // Literal names also let the game's scanner see the bounded set of optional types.
            Type type = name switch
            {
                "DumpingGroundMap.Reputation" => loaded.GetType("DumpingGroundMap.Reputation", false),
                "ModuleForge.GameAdapter.ForgeCommissions" => loaded.GetType("ModuleForge.GameAdapter.ForgeCommissions", false),
                "ModuleForge.GameAdapter.ForgeReputation" => loaded.GetType("ModuleForge.GameAdapter.ForgeReputation", false),
                _ => null
            };
            if (type == null) throw new InvalidOperationException("the optional mod's type is missing");
            return type;
        }
        internal static OptionalFaction Scavengers = ScavengerReader(), Dsi = ForgeReader();
        // The harness replaces the resolver, never loads a Workshop assembly.
        internal static void ResetForTests() { Scavengers = ScavengerReader(); Dsi = ForgeReader(); }
        private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("the optional mod is laid out differently"); }
        private static void StaticClass(Type type) => Require(type != null && type.IsClass && type.IsAbstract && type.IsSealed && !type.ContainsGenericParameters);
        private static PropertyInfo Property(Type type, PropertyInfo property, Type returns)
        {
            MethodInfo get = property?.GetGetMethod(true);
            Require(property != null && property.DeclaringType == type && property.PropertyType == returns && property.GetIndexParameters().Length == 0 &&
                get != null && get.IsStatic && !get.ContainsGenericParameters && get.ReturnType == returns && get.GetParameters().Length == 0);
            return property;
        }
        private static OptionalFaction ScavengerReader() => new OptionalFaction("mod:scavengers", "Scavengers", TextKeys.FactionScavengers, 0, () =>
        {
            Type type = Resolve("DumpingGroundMap", "DumpingGroundMap.Reputation");
            if (type == null) return null;
            StaticClass(type); Require(type.FullName == "DumpingGroundMap.Reputation");
            PropertyInfo exact = Property(type, ScavengerExact(), typeof(float)), store = Property(type, ScavengerStore(), typeof(bool));
            return () => (bool)store.GetValue(null) ? (double?)(float)exact.GetValue(null) : null;
        });
        private static OptionalFaction ForgeReader() => new OptionalFaction("mod:dsi", "DSI", TextKeys.FactionDsi, 3, () =>
        {
            Type type = Resolve("ModuleForge", "ModuleForge.GameAdapter.ForgeCommissions");
            if (type == null) return null;
            StaticClass(type); Require(type.FullName == "ModuleForge.GameAdapter.ForgeCommissions");
            MethodInfo read = ForgeData();
            Require(PaperWindow.Method(read, typeof(string), true, typeof(string), typeof(string), typeof(string)));
            ParameterInfo fallback = read.GetParameters()[2];
            Require(fallback.IsOptional && fallback.DefaultValue == null);
            return () =>
            {
                string text = (string)read.Invoke(null, new object[] { "local.moduleforge", "commissions.v1", null });
                return string.IsNullOrEmpty(text) ? (double?)null : CommissionText.Reputation(text);
            };
        });
    }
}

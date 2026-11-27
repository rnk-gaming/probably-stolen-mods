using LocalMods;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace ShopkeepingSuite
{
    internal sealed class StandingStep
    {
        internal int Threshold, Rank;
        internal string Name = "", Description = "";
    }
    internal sealed class StandingChange
    {
        internal string Text, Description, FullDescription;
    }
    // These rules have no engine calls. In particular, penalties use the opposite boundary.
    internal static class StandingRules
    {
        internal static bool Active(double score, int threshold) => threshold < 0 ? score <= threshold : score >= threshold;
        internal static string Clean(string value)
        {
            var result = new StringBuilder(); bool tag = false;
            foreach (char c in value ?? "")
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag) result.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }
            return result.ToString().Trim();
        }
        internal static string Caption(string value)
        {
            string text = Clean(value);
            // Even 32 wide glyphs fit the 266-point paper line at caption size eight.
            return text.Length <= 32 ? text : text.Substring(0, 29) + "...";
        }
        internal static List<StandingChange> Changes(StandingView faction, double before, double after, IReadOnlyDictionary<string, string> labels)
        {
            string Text(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, labels[key], args);
            var result = new List<StandingChange>();
            foreach (StandingStep step in faction.Steps)
            {
                bool was = Active(before, step.Threshold), active = Active(after, step.Threshold);
                if (was == active) continue;
                string name = Clean(step.Name);
                if (faction.Id == "mod:dsi")
                    name = Text(TextKeys.StandingLevel, step.Rank) + (name.Length == 0 ? "" : ": " + name);
                else if (name.Length == 0) name = Text(faction.Id == "mod:scavengers" ? TextKeys.StandingStep : TextKeys.StandingBonus, step.Threshold);
                string key = step.Threshold < 0 ? (active ? TextKeys.StandingEffect : TextKeys.StandingNoEffect) : (active ? TextKeys.StandingGained : TextKeys.StandingLost);
                result.Add(new StandingChange { Text = Text(key, name), Description = Caption(step.Description), FullDescription = Clean(step.Description) });
            }
            return result;
        }
        private static IReadOnlyDictionary<string, string> labels;
        internal static void ClearLabels() => labels = null;
        internal static IReadOnlyDictionary<string, string> Labels() => labels ??= LabelKeys.ToDictionary(k => k, k => TextTable.Get(k, "{0}"));
        internal static readonly string[] LabelKeys = { TextKeys.StandingGained, TextKeys.StandingLost, TextKeys.StandingEffect, TextKeys.StandingNoEffect, TextKeys.StandingBonus, TextKeys.StandingStep, TextKeys.StandingLevel };
    }
    // Text lookups are independent of the score reader: a missing description never hides standing.
    internal static class StandingBonuses
    {
        internal static Func<FieldInfo> PerksField = () => AccessTools.Field(typeof(StoreReputation), "perks");
        internal static Func<FieldInfo> ThresholdField = () => AccessTools.Field(typeof(StoreReputationPerk), "perkThreshold");
        internal static Func<FieldInfo> AlwaysField = () => AccessTools.Field(typeof(StoreReputationPerk), "isAlwaysActive");
        internal static Func<MethodInfo> GameName = () => AccessTools.Method(typeof(StoreReputationPerk), "GetLocalizedPerkName", Type.EmptyTypes);
        internal static Func<MethodInfo> GameDescription = () => AccessTools.Method(typeof(StoreReputationPerk), "GetLocalizedDescription", Type.EmptyTypes);
        internal static Func<StoreReputationPerk, string> PerkName = p => p.GetLocalizedPerkName();
        internal static Func<StoreReputationPerk, string> PerkDescription = p => p.GetLocalizedDescription();
        internal static Func<MethodInfo> ModText = () => AccessTools.Method(typeof(ModHelper), "GetLocalized", new[] { typeof(string), typeof(string), typeof(object[]) });
        internal static Func<Type> ForgeType = () => OptionalStanding.Resolve("ModuleForge", "ModuleForge.GameAdapter.ForgeReputation");
        internal static Func<MethodInfo> ForgeName = () => AccessTools.Method("ModuleForge.GameAdapter.ForgeReputation:Name", new[] { typeof(int) });
        internal static Func<MethodInfo> ForgeBenefit = () => AccessTools.Method("ModuleForge.GameAdapter.ForgeReputation:Benefit", new[] { typeof(int) });
        private static readonly Dictionary<Delegate, object> members = new Dictionary<Delegate, object>();
        private static T Member<T>(Func<T> lookup)
        {
            if (!members.TryGetValue(lookup, out object member)) members[lookup] = member = lookup();
            return (T)member;
        }
        private static void Require(bool valid) { if (!valid) throw new InvalidOperationException("standing bonus member is laid out differently"); }
        private static string Read(string scope, Func<string> read)
        {
            try { return StandingRules.Clean(read()); }
            catch (Exception error) { SuiteMod.Fault("standing " + scope, error); return ""; }
        }
        internal static List<StandingStep> GameSteps(StoreReputation faction)
        {
            var result = new List<StandingStep>();
            try
            {
                FieldInfo perks = Member(PerksField), threshold = Member(ThresholdField), always = Member(AlwaysField);
                Require(perks != null && perks.DeclaringType == typeof(StoreReputation) && perks.IsPublic && !perks.IsStatic && perks.FieldType == typeof(List<StoreReputationPerk>));
                Require(threshold != null && threshold.DeclaringType == typeof(StoreReputationPerk) && threshold.IsPublic && !threshold.IsStatic && threshold.FieldType == typeof(int));
                Require(always != null && always.DeclaringType == typeof(StoreReputationPerk) && always.IsPublic && !always.IsStatic && always.FieldType == typeof(bool));
                var list = (List<StoreReputationPerk>)perks.GetValue(faction);
                if (list == null) return result;
                foreach (StoreReputationPerk perk in list)
                {
                    if (perk == null || (bool)always.GetValue(perk)) continue;
                    string Text(Func<MethodInfo> lookup, Func<StoreReputationPerk, string> read, string scope) => Read(scope, () =>
                    {
                        MethodInfo method = Member(lookup);
                        Require(PaperWindow.Method(method, typeof(string), false) && method.DeclaringType == typeof(StoreReputationPerk));
                        return read(perk);
                    });
                    result.Add(new StandingStep { Threshold = (int)threshold.GetValue(perk),
                        Name = Text(GameName, PerkName, "perk name"), Description = Text(GameDescription, PerkDescription, "perk description") });
                }
            }
            catch (Exception error) { SuiteMod.Fault("standing perks", error); }
            return result;
        }
        private static string ScavengerText(int score, string suffix) => Read("Scavengers " + suffix, () =>
        {
            MethodInfo method = Member(ModText);
            Require(PaperWindow.Method(method, typeof(string), true, typeof(string), typeof(string), typeof(object[])) &&
                method.GetParameters()[2].IsDefined(typeof(ParamArrayAttribute), false));
            string key = "perk_" + score.ToString(CultureInfo.InvariantCulture) + "_" + suffix;
            string value = (string)method.Invoke(null, new object[] { "local.dumping_ground_map", key, Array.Empty<object>() });
            return string.IsNullOrWhiteSpace(value) || value == key ? "" : value;
        });
        internal static List<StandingStep> ScavengerSteps() => new[] { 20, 40, 60, 80, 100 }.Select(score =>
            new StandingStep { Threshold = score, Name = ScavengerText(score, "name"), Description = ScavengerText(score, "description") }).ToList();
        private static string ForgeText(Func<MethodInfo> lookup, int value, string scope) => Read("DSI " + scope, () =>
        {
            Type type = Member(ForgeType);
            if (type == null) return "";
            Require(type.FullName == "ModuleForge.GameAdapter.ForgeReputation" && type.IsClass && type.IsAbstract && type.IsSealed && !type.ContainsGenericParameters);
            MethodInfo method = Member(lookup);
            Require(method != null && method.IsStatic && !method.ContainsGenericParameters && method.ReturnType == typeof(string) &&
                method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int) }) && method.DeclaringType == type);
            return (string)method.Invoke(null, new object[] { value });
        });
        internal static List<StandingStep> DsiSteps() => new[] { 0, 20, 60, 120, 220, 360, 560, 850 }.Select((score, index) =>
            new StandingStep { Threshold = score, Rank = index + 1, Name = ForgeText(ForgeName, score, "name"), Description = ForgeText(ForgeBenefit, index + 1, "benefit") }).ToList();
    }
}

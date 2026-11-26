using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LocalMods
{
    // Values only, independent of the menu and of the settings store.
    internal static class OptionsRules
    {
        private static string Group(string group) => string.IsNullOrEmpty(group) ? "General" : group;
        internal static string SectionId(string modId, string group) => modId + ".section." + Group(group);
        internal static string OptionId(string modId, string key) => modId + ".option." + key;
        // The group is left out when it only repeats the start of the mod's own name
        // ("Off the Books: Perks" with the group "Perks"), so a title does not say it twice.
        internal static string SectionLabel(string modName, string group)
        {
            string name = modName ?? "", part = Group(group);
            int colon = name.LastIndexOf(": ", StringComparison.Ordinal);
            string own = colon < 0 ? name : name.Substring(colon + 2);
            return own.StartsWith(part, StringComparison.OrdinalIgnoreCase) ? name : name + ": " + part;
        }

        internal static int Kind(Type type, bool hasRange, float min, float max)
        {
            if (type == typeof(bool)) return 1;
            if (type != typeof(int) || !hasRange || float.IsNaN(min) || float.IsNaN(max)
                || float.IsInfinity(min) || float.IsInfinity(max)) return 0;
            double low = Math.Max(int.MinValue, Math.Ceiling((double)min));
            double high = Math.Min(int.MaxValue, Math.Floor((double)max));
            return low <= high ? 2 : 0;
        }

        internal static int ToInt(float value, float min, float max)
        {
            double low = Math.Max(int.MinValue, Math.Ceiling((double)min));
            double high = Math.Min(int.MaxValue, Math.Floor((double)max));
            if (float.IsNaN(value)) return (int)low;
            return (int)Math.Max(low, Math.Min(high, Math.Round((double)value, MidpointRounding.AwayFromZero)));
        }

        internal static float ToFloat(int value, float min, float max) => ToInt(value, min, max);
        // Whether the slider's value differs from what the slider showed for the saved one.
        internal static bool Changed(int saved, int wanted, float min, float max) => ToInt(saved, min, max) != wanted;
    }

    internal static class OptionsMenu
    {
        private static readonly HashSet<string> registered = new HashSet<string>();
        private static readonly HashSet<string> reported = new HashSet<string>();
        internal static Func<string, IReadOnlyList<ConfigEntry>> Entries = id => ModConfig.For(id).Entries;
        internal static Action<string, string, ConfigEntry, Action<string, Exception>> RegisterEntry = Add;

        private static void Fault(string modId, string where, Exception error, Action<string, Exception> report)
        {
            try { if (reported.Add(modId + ":" + where)) report?.Invoke("Options " + where, error); }
            catch (Exception) { }
        }

        private static T Read<T>(string modId, string where, Func<T> get, T fallback, Action<string, Exception> report)
        {
            try { return get(); }
            catch (Exception error) { Fault(modId, where, error, report); return fallback; }
        }

        private static void Write(string modId, string key, Action set, Action<string, Exception> report)
        {
            try { set(); }
            catch (Exception error) { Fault(modId, "applying " + key, error, report); }
        }

        internal static void Register(string modId, string modName, Action<string, Exception> report)
        {
            try
            {
                if (!registered.Add(modId)) return;
                RegisterCore(modId, modName, Entries(modId), report);
            }
            catch (Exception error) { Fault(modId, "registration", error, report); }
        }

        // Keep optional registry types and members out of the caller's JIT boundary.
        // An older game without this API must still be able to load the mod.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterCore(string modId, string modName, IReadOnlyList<ConfigEntry> settings, Action<string, Exception> report)
        {
            foreach (ConfigEntry setting in settings)
            {
                try { RegisterEntry(modId, modName, setting, report); }
                catch (Exception error) { Fault(modId, "registration", error, report); }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Add(string modId, string modName, ConfigEntry setting, Action<string, Exception> report)
        {
            int kind = OptionsRules.Kind(setting.ValueType, setting.HasRange, setting.Min, setting.Max);
            if (kind == 0) return;
            string key = setting.Key, section = OptionsRules.SectionId(modId, setting.Section);
            Func<string> label = () => Read(modId, "label " + key, () => setting.Label?.Invoke() ?? key, key, report);
            Func<string> tooltip = () => Read(modId, "tooltip " + key, () => setting.Tooltip?.Invoke(), null, report);
            if (kind == 1)
            {
                var entry = GameOptionRegistry.Toggle(OptionsRules.OptionId(modId, key), label,
                    () => Read(modId, "reading " + key, () => ((ConfigEntry<bool>)setting).Value, false, report),
                    value => Write(modId, key, () => ((ConfigEntry<bool>)setting).Set(value), report), tooltip);
                entry.source = modId;
                // The section is made last, so that a fault above leaves no empty heading.
                GameOptionRegistry.GetOrAddSection(section, () => OptionsRules.SectionLabel(modName, setting.Section), modId);
                GameOptionRegistry.Add(section, entry);
            }
            else
            {
                float min = OptionsRules.ToInt(setting.Min, setting.Min, setting.Max);
                float max = OptionsRules.ToInt(setting.Max, setting.Min, setting.Max);
                var entry = GameOptionRegistry.Slider(OptionsRules.OptionId(modId, key), label,
                    () => Read(modId, "reading " + key, () => OptionsRules.ToFloat(((ConfigEntry<int>)setting).Value, setting.Min, setting.Max), min, report),
                    // Apply calls this for every entry. A slider the player did not move must
                    // not write a clamped value over one saved outside the range.
                    value => Write(modId, key, () =>
                    {
                        var typed = (ConfigEntry<int>)setting;
                        int wanted = OptionsRules.ToInt(value, setting.Min, setting.Max);
                        if (OptionsRules.Changed(typed.Value, wanted, setting.Min, setting.Max)) typed.Set(wanted);
                    }, report),
                    min, max, true, tooltip, value => OptionsRules.ToInt(value, setting.Min, setting.Max).ToString());
                entry.source = modId;
                GameOptionRegistry.GetOrAddSection(section, () => OptionsRules.SectionLabel(modName, setting.Section), modId);
                GameOptionRegistry.Add(section, entry);
            }
        }
    }
}

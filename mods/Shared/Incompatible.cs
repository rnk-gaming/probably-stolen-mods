using System;
using System.Collections.Generic;
using System.Globalization;

namespace LocalMods
{
    internal static class NoteTextKeys
    {
        internal const string IncompatibleTitle = "incompatible_title";
        internal const string IncompatibleBody = "incompatible_body";
        internal const string IncompatibleFinal = "incompatible_final";
        internal const string IncompatibleEntry = "incompatible_entry";
        internal const string NoteSignature = "notes_signature";
    }

    internal sealed class IncompatibleMod
    {
        internal readonly string Id, NameKey, ReasonKey;
        internal IncompatibleMod(string id, string stem)
        { Id = id; NameKey = "incompatible_" + stem + "_name"; ReasonKey = "incompatible_" + stem + "_reason"; }
    }

    internal static class IncompatibleRules
    {
        // Three deliveries per conflict per save, retained when a mod goes away.
        internal static bool Due(NoteRecord record, int day) => record.Count < 3 && record.LastDay != day;
        internal static string RecordId(string modId) => "incompatible/" + modId;
    }

    internal static class Incompatible
    {
        internal static Func<string, bool> Running = id => ModHelper.IsModRunning(id);
        internal static Action<string> Log = line => UnityEngine.Debug.LogWarning(line);
        private static readonly HashSet<string> logged = new HashSet<string>(StringComparer.Ordinal);

        internal static void Register(string modId, string logPrefix, IncompatibleMod[] mods, Action<string, Exception> faults)
        {
            try
            {
                if (!Notes.Start(modId, NoteTextKeys.NoteSignature, faults)) return;
                // Validate the list here so a malformed registration is reported safely.
                foreach (IncompatibleMod mod in mods ?? Array.Empty<IncompatibleMod>())
                    if (mod == null || string.IsNullOrEmpty(mod.Id))
                        throw new ArgumentException("A conflict needs a mod id.");
                Notes.Register(new NoteDefinition("incompatible", NoteTextKeys.IncompatibleTitle, NoteTextKeys.IncompatibleBody, context =>
                {
                    var lines = new List<string>();
                    var ids = new List<string>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (IncompatibleMod mod in mods ?? Array.Empty<IncompatibleMod>())
                    {
                        if (!seen.Add(mod.Id) || !Running(mod.Id)) continue;
                        // The id must reach the log even if translated text is damaged.
                        if (logged.Add(mod.Id)) Log(logPrefix + " incompatible mod " + mod.Id);
                        string line = string.Format(CultureInfo.InvariantCulture, Notes.Word(NoteTextKeys.IncompatibleEntry),
                            Notes.Word(mod.NameKey), Notes.Word(mod.ReasonKey));
                        string id = IncompatibleRules.RecordId(mod.Id);
                        NoteRecord record = context.Record(id);
                        if (!IncompatibleRules.Due(record, context.Day)) continue;
                        if (record.Count == 2) line += " " + Notes.Word(NoteTextKeys.IncompatibleFinal);
                        ids.Add(id); lines.Add(line);
                    }
                    return ids.Count == 0 ? null : new NotePlan(new object[] { string.Join("\n", lines) }, ids.ToArray());
                }));
            }
            catch (Exception error) { Notes.Fault("incompatible", error); }
        }

        internal static void Enable() => Notes.Enable();
        internal static void Disable() => Notes.Disable();
    }
}

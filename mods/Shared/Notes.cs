using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace LocalMods
{
    internal sealed class NoteRecord
    {
        internal readonly int Count, LastDay;
        internal bool Delivered => Count > 0;
        internal NoteRecord(int count = 0, int lastDay = -1) { Count = count; LastDay = lastDay; }
    }

    internal sealed class NoteContext
    {
        internal readonly int Day;
        private readonly Dictionary<string, NoteRecord> records;
        internal NoteContext(int day, Dictionary<string, NoteRecord> records) { Day = day; this.records = records; }
        internal NoteRecord Record(string id) => records.TryGetValue(id, out NoteRecord record) ? record : new NoteRecord();
    }

    internal sealed class NotePlan
    {
        internal readonly object[] BodyArguments;
        internal readonly string[] RecordIds;
        // Additional ids let several conflicts share one physical note.
        internal NotePlan(object[] bodyArguments = null, params string[] recordIds)
        { BodyArguments = bodyArguments ?? Array.Empty<object>(); RecordIds = recordIds ?? Array.Empty<string>(); }
    }

    internal sealed class NoteDefinition
    {
        internal readonly string Id, TitleKey, BodyKey;
        internal readonly Func<NoteContext, NotePlan> Due;
        // News once after an update: put the version in Id, and return a plan only
        // when !context.Record(Id).Delivered. Rules need no game objects.
        internal NoteDefinition(string id, string titleKey, string bodyKey, Func<NoteContext, NotePlan> due)
        { Id = id; TitleKey = titleKey; BodyKey = bodyKey; Due = due; }
    }

    internal static class NoteRecords
    {
        // One line commits all counts on a combined note together. New writes use
        // semicolons; accept the unreleased newline form from development saves.
        internal static Dictionary<string, NoteRecord> Parse(string text)
        {
            var records = new Dictionary<string, NoteRecord>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return records;
            foreach (string row in text.Split(new[] { ';', '\n' }))
            {
                string[] parts = row.Split('|');
                if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                    || count < 1 || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int day))
                    throw new FormatException("Invalid saved note record.");
                string id = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                if (string.IsNullOrEmpty(id)) throw new FormatException("Empty saved note id.");
                records.Add(id, new NoteRecord(count, day));
            }
            return records;
        }

        internal static string Serialise(Dictionary<string, NoteRecord> records)
        {
            var rows = new List<string>();
            foreach (var pair in records)
                rows.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key)) + "|"
                    + pair.Value.Count.ToString(CultureInfo.InvariantCulture) + "|" + pair.Value.LastDay.ToString(CultureInfo.InvariantCulture));
            return string.Join(";", rows);
        }
    }

    // Object seams keep item stand-ins independent of Unity. Production notes use
    // one tutorial page design, its tags, owned placement and the table signature.
    internal static class NoteBuilder
    {
        internal static Func<string, object> Create = id => DirectoryMaster.Item(id);
        internal static Action<object, string, string, string> Initialise = (item, title, body, signature) =>
            IntelHelper.InitTutorialPage((GameItem)item, title, body, signature);
        internal static Action<object, string> Name = (item, title) => ((GameItem)item).name = title;
        internal static Func<object, bool> Place = item => PlayerStore.Instance.TryAddDirectSellingItemToTable((GameItem)item, true);
        // A page made but never handed to the game is destroyed as the game does with unplaced items.
        internal static Action<object> Discard = item => GraphUtils.DestroyIfUnplaced((GameItem)item, "Off the Books note not placed");

        internal static bool Deliver(string title, string body, string signature)
        {
            object item = Create("tutorial_page");
            if (item == null) throw new InvalidOperationException("The tutorial page is unavailable.");
            try
            {
                Initialise(item, title, body, signature);
                Name(item, title);
            }
            catch
            {
                try { Discard(item); } catch (Exception) { }
                throw;
            }
            return Place(item);
        }
    }

    // Registrations belong to this assembly; records belong to the current save.
    // Records are never cached across callbacks, so save switches need no reset.
    internal static class Notes
    {
        internal const string DataKey = "counter_notes_v1";
        internal static Func<bool> CounterReady = () => PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance == null;
        internal static Func<int> Today = () => StoreStation.GetDayCounter();
        internal static Func<string, string> Text = key => TextTable.Get(key);
        internal static Func<string, string, string> Read = (mod, key) => ModHelper.GetModData(mod, key, "");
        internal static Action<string, string, string> Write = (mod, key, value) => ModHelper.SetModData(mod, key, value);
        internal static Action<Action> Subscribe = Add;
        internal static Action<Action> Unsubscribe = Remove;
        private static readonly Dictionary<string, NoteDefinition> definitions = new Dictionary<string, NoteDefinition>(StringComparer.Ordinal);
        private static string modId, signatureKey;
        private static Action<string, Exception> report;
        private static bool active, stopped, delivering;
        private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Start(string owner, string signature, Action<string, Exception> faults)
        {
            try
            {
                report = faults;
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(signature))
                    throw new ArgumentException("Notes need an owner and a signature key.");
                modId = owner; signatureKey = signature;
                return true;
            }
            catch (Exception error) { Fault("start", error); return false; }
        }

        internal static void Register(NoteDefinition note)
        {
            try
            {
                if (note == null || string.IsNullOrEmpty(note.Id) || note.Due == null)
                    throw new ArgumentException("A note needs an id and a due rule.");
                definitions[note.Id] = note;
            }
            catch (Exception error) { Fault(note?.Id ?? "registration", error); }
        }

        internal static void Enable()
        {
            try { active = true; Unsubscribe(Opened); Subscribe(Opened); }
            catch (Exception error) { Fault("subscription", error); }
        }

        internal static void Disable()
        {
            active = false;
            try { Unsubscribe(Opened); }
            catch (Exception error) { Fault("subscription", error); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Add(Action opened) => ModHook.OnShutterOpenedLate += opened;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Remove(Action opened) => ModHook.OnShutterOpenedLate -= opened;

        internal static string Word(string key)
        {
            string text = Text(key);
            if (string.IsNullOrEmpty(text) || text == key) throw new InvalidOperationException("Missing note text: " + key);
            return text;
        }

        private static void Opened()
        {
            if (!active || stopped || delivering) return;
            delivering = true;
            try
            {
                if (!CounterReady()) return;
                int day = Today();
                if (day < 0) return;
                foreach (NoteDefinition note in new List<NoteDefinition>(definitions.Values))
                {
                    try
                    {
                        string previous = Read(modId, DataKey);
                        // A damaged record is left untouched: no notes for this mod in
                        // this save. Read afresh so a different save can still work.
                        Dictionary<string, NoteRecord> records;
                        try { records = NoteRecords.Parse(previous); }
                        catch (Exception error) { Fault(note.Id, error); return; }
                        var context = new NoteContext(day, records);
                        if (context.Record(note.Id).LastDay == day) continue;
                        NotePlan plan = note.Due(context);
                        if (plan == null) continue;
                        var ids = new HashSet<string>(plan.RecordIds, StringComparer.Ordinal) { note.Id };
                        foreach (string id in ids)
                        {
                            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Empty note record id.");
                            records[id] = new NoteRecord(checked(context.Record(id).Count + 1), day);
                        }
                        string saved = NoteRecords.Serialise(records);
                        string title = Word(note.TitleKey);
                        string body = string.Format(CultureInfo.InvariantCulture, Word(note.BodyKey), plan.BodyArguments);
                        string signature = Word(signatureKey);
                        try
                        {
                            // Commit before any placement: an uncertain write or placement
                            // must never allow an unbounded repeat of physical notes.
                            Write(modId, DataKey, saved);
                            if (!NoteBuilder.Deliver(title, body, signature))
                                throw new InvalidOperationException("The counter refused the note.");
                        }
                        catch (Exception error)
                        {
                            stopped = true;
                            try { Write(modId, DataKey, previous); }
                            catch (Exception) { } // Best effort; never retry delivery this session.
                            Fault(note.Id, error);
                            return;
                        }
                    }
                    catch (Exception error) { Fault(note.Id, error); }
                }
            }
            catch (Exception error) { Fault("counter notes", error); }
            finally { delivering = false; }
        }

        internal static void Fault(string id, Exception error)
        {
            try
            {
                if (!reported.Add(id)) return;
                report?.Invoke(id, error);
            }
            catch (Exception) { }
        }
    }
}

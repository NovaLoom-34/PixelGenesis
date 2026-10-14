using System.Collections.Generic;
using System.IO;

namespace PG.Governor
{
    public sealed class DecisionLogEntry
    {
        public long Tick;
        public int Year, Kingdom;
        public string Thought = "";
        public readonly List<string> Actions = new List<string>();
        public readonly List<string> Rejected = new List<string>();
        public readonly List<string> AdvisorNames = new List<string>();   // v2: parallel to AdvisorThoughts
        public readonly List<string> AdvisorThoughts = new List<string>();

        public void AddAdvisor(string name, string thought)
        {
            AdvisorNames.Add(name ?? "?");
            AdvisorThoughts.Add(thought ?? "");
        }
    }

    // In-memory chronicle of governor decisions; persisted as save section 15 so a reloaded
    // world keeps its history (and the Brain panel keeps its last verdict). v2 also carries the
    // AI-control state: which kingdoms the governors run (Governed) and which the player took
    // over (PlayerTaken) — so a loaded world knows who owns each kingdom's strategy.
    public sealed class DecisionLog
    {
        public const int MaxEntries = 500;
        public const byte FormatVersion = 2;
        public readonly List<DecisionLogEntry> Entries = new List<DecisionLogEntry>();
        public readonly List<int> Governed = new List<int>();
        public readonly List<int> PlayerTaken = new List<int>();

        public DecisionLogEntry Add(long tick, int year, int kingdom, string thought, List<string> actions, List<string> rejected)
        {
            var e = new DecisionLogEntry { Tick = tick, Year = year, Kingdom = kingdom, Thought = thought ?? "" };
            if (actions != null) e.Actions.AddRange(actions);
            if (rejected != null) e.Rejected.AddRange(rejected);
            Entries.Add(e);
            if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
            return e;
        }

        public DecisionLogEntry LastForKingdom(int kingdom)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].Kingdom == kingdom) return Entries[i];
            return null;
        }

        public void SetGoverned(int kingdom, bool on)
        {
            if (on) { if (!Governed.Contains(kingdom)) Governed.Add(kingdom); PlayerTaken.Remove(kingdom); }
            else { Governed.Remove(kingdom); }
        }

        public void SetPlayerTaken(int kingdom, bool on)
        {
            if (on) { if (!PlayerTaken.Contains(kingdom)) PlayerTaken.Add(kingdom); Governed.Remove(kingdom); }
            else { PlayerTaken.Remove(kingdom); }
        }

        // Binary form (save section 15).
        public byte[] WriteBytes()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(FormatVersion);
            w.Write(Entries.Count);
            foreach (var e in Entries)
            {
                w.Write(e.Tick);
                w.Write(e.Year);
                w.Write(e.Kingdom);
                w.Write(e.Thought ?? "");
                WriteStrings(w, e.Actions);
                WriteStrings(w, e.Rejected);
                WriteStrings(w, e.AdvisorNames);
                WriteStrings(w, e.AdvisorThoughts);
            }
            WriteInts(w, Governed);
            WriteInts(w, PlayerTaken);
            return ms.ToArray();
        }

        public void ReadBytes(byte[] raw)
        {
            Entries.Clear();
            Governed.Clear();
            PlayerTaken.Clear();
            if (raw == null || raw.Length == 0) return;
            using var ms = new MemoryStream(raw);
            using var r = new BinaryReader(ms);
            byte version = r.ReadByte();
            int n = r.ReadInt32();
            for (int i = 0; i < n && ms.Position < ms.Length; i++)
            {
                var e = new DecisionLogEntry { Tick = r.ReadInt64(), Year = r.ReadInt32(), Kingdom = r.ReadInt32(), Thought = r.ReadString() };
                ReadStrings(r, e.Actions);
                ReadStrings(r, e.Rejected);
                if (version >= 2)
                {
                    ReadStrings(r, e.AdvisorNames);
                    ReadStrings(r, e.AdvisorThoughts);
                }
                Entries.Add(e);
            }
            if (version >= 2)
            {
                ReadInts(r, Governed);
                ReadInts(r, PlayerTaken);
            }
        }

        static void WriteStrings(BinaryWriter w, List<string> list)
        {
            w.Write(list.Count);
            foreach (var s in list) w.Write(s ?? "");
        }

        static void ReadStrings(BinaryReader r, List<string> list)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) list.Add(r.ReadString());
        }

        static void WriteInts(BinaryWriter w, List<int> list)
        {
            w.Write(list.Count);
            foreach (var v in list) w.Write(v);
        }

        static void ReadInts(BinaryReader r, List<int> list)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
        }
    }
}

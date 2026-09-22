using System;
using System.Collections.Generic;
using System.Linq;

namespace InnovationMR.NgoLan.Discovery
{
    internal sealed class LanSessionRegistry
    {
        private readonly Dictionary<string, Entry> entries = new();
        private readonly int capacity;

        private struct Entry
        {
            public LanSessionInfo Session;
            public double LastSeen;
        }

        internal LanSessionRegistry(int capacity) => this.capacity = Math.Max(1, capacity);
        internal int Count => entries.Count;

        internal bool Upsert(LanSessionInfo session, double now)
        {
            string key = MakeKey(session);
            if (entries.TryGetValue(key, out Entry old))
            {
                entries[key] = new Entry { Session = session, LastSeen = now };
                return !old.Session.Equals(session);
            }

            if (entries.Count >= capacity)
                return false;

            entries.Add(key, new Entry { Session = session, LastSeen = now });
            return true;
        }

        internal bool RemoveExpired(double now, double timeoutSeconds)
        {
            string[] stale = entries.Where(pair => now - pair.Value.LastSeen >= timeoutSeconds)
                .Select(pair => pair.Key).ToArray();
            foreach (string key in stale)
                entries.Remove(key);
            return stale.Length > 0;
        }

        internal bool Clear()
        {
            if (entries.Count == 0) return false;
            entries.Clear();
            return true;
        }

        internal IReadOnlyList<LanSessionInfo> Snapshot() => entries.Values
            .Select(entry => entry.Session)
            .OrderBy(session => session.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        private static string MakeKey(LanSessionInfo session) =>
            string.IsNullOrWhiteSpace(session.Id)
                ? $"{session.Address}:{session.Port}"
                : $"{session.Id}|{session.Address}:{session.Port}";
    }
}

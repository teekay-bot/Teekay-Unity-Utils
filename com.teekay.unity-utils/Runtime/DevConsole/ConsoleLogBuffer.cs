using System;
using System.Collections;
using System.Collections.Generic;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Bounded log storage with duplicate collapsing: appending a line whose category and message
    /// match the newest entry increments that entry's <see cref="ConsoleLogEntry.Count"/> (and
    /// refreshes its timestamp) instead of adding a row — so a spamming per-frame log reads as
    /// one line with a ×N badge rather than 500 identical lines.
    /// <para>
    /// Backed by a plain list with indexed access, which the UI's virtualized view requires.
    /// Overflow trims from the front; at the console's scale (hundreds of entries) the shift cost
    /// is irrelevant next to rendering. Pure C# — unit-tested without Unity.
    /// </para>
    /// </summary>
    public sealed class ConsoleLogBuffer : IEnumerable<ConsoleLogEntry>
    {
        readonly List<ConsoleLogEntry> _entries = new();
        long _nextSequence = 1;

        public int Count => _entries.Count;

        public ConsoleLogEntry this[int index] => _entries[index];

        /// <summary>
        /// Append a line, collapsing it into the newest entry when category and message are
        /// identical. Trims oldest entries to stay within <paramref name="maxEntries"/>.
        /// Returns true when a new row was added, false when the line collapsed into the last row.
        /// </summary>
        public bool Append(ConsoleLogEntry entry, int maxEntries)
        {
            if (_entries.Count > 0)
            {
                ConsoleLogEntry last = _entries[^1];
                if (string.Equals(last.Category, entry.Category, StringComparison.Ordinal) &&
                    string.Equals(last.Message, entry.Message, StringComparison.Ordinal))
                {
                    _entries[^1] = new ConsoleLogEntry(last.Category, last.Message, entry.Color,
                        entry.Timestamp, last.Count + 1, last.Sequence);
                    return false;
                }
            }

            _entries.Add(new ConsoleLogEntry(entry.Category, entry.Message, entry.Color,
                entry.Timestamp, Math.Max(1, entry.Count), _nextSequence++));

            int excess = _entries.Count - Math.Max(1, maxEntries);
            if (excess > 0) _entries.RemoveRange(0, excess);
            return true;
        }

        public void Clear() => _entries.Clear();

        public IEnumerator<ConsoleLogEntry> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

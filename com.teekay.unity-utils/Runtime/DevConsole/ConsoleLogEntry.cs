using UnityEngine;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// A single line of console output. Stored in the ring buffer and rendered by the UI.
    /// Color is resolved at log time (from the category) so render-time doesn't need to
    /// re-look up the category every frame.
    /// </summary>
    public readonly struct ConsoleLogEntry
    {
        public readonly string Category;
        public readonly string Message;
        public readonly Color Color;
        public readonly float Timestamp;

        /// <summary>How many consecutive identical lines this entry represents (≥ 1). The buffer
        /// collapses repeats into the previous entry instead of appending a duplicate row.</summary>
        public readonly int Count;

        /// <summary>
        /// Monotonically increasing id assigned by <see cref="ConsoleLogBuffer"/> when the entry is
        /// first appended. Stable across collapses and buffer trims, so the UI can cache per-entry
        /// data (e.g. measured row heights) without being confused by shifting indices.
        /// </summary>
        public readonly long Sequence;

        public ConsoleLogEntry(string category, string message, Color color, float timestamp)
            : this(category, message, color, timestamp, 1, 0) { }

        public ConsoleLogEntry(string category, string message, Color color, float timestamp,
                               int count, long sequence)
        {
            Category = category;
            Message = message;
            Color = color;
            Timestamp = timestamp;
            Count = count;
            Sequence = sequence;
        }
    }
}

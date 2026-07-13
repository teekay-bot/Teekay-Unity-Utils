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

        public ConsoleLogEntry(string category, string message, Color color, float timestamp)
        {
            Category = category;
            Message = message;
            Color = color;
            Timestamp = timestamp;
        }
    }
}

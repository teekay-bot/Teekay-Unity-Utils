using System.Collections.Generic;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Bounded history of submitted command lines. Up arrow walks backward (older), Down
    /// arrow walks forward (newer). Past the newest entry, navigation returns to an empty
    /// line (matches bash / CS2 behavior). Duplicates of the most-recent entry are skipped.
    /// </summary>
    public sealed class ConsoleHistory
    {
        readonly List<string> _entries = new();
        readonly int _maxEntries;
        int _navIndex = -1; // -1 = at the "live" line (not navigating yet)

        public ConsoleHistory(int maxEntries) => _maxEntries = System.Math.Max(1, maxEntries);

        public int Count => _entries.Count;

        public void Add(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            // Skip if same as last submitted
            if (_entries.Count > 0 && _entries[^1] == entry) { _navIndex = -1; return; }

            _entries.Add(entry);
            if (_entries.Count > _maxEntries) _entries.RemoveAt(0);
            _navIndex = -1;
        }

        /// <summary>Step toward older entries. Returns null when there is no history; stops at the oldest entry.</summary>
        public string NavigatePrevious()
        {
            if (_entries.Count == 0) return null;
            if (_navIndex == -1) _navIndex = _entries.Count - 1;
            else if (_navIndex > 0) _navIndex--;
            return _entries[_navIndex];
        }

        /// <summary>Step toward newer entries. Returns "" when stepping past the newest live line.</summary>
        public string NavigateNext()
        {
            if (_entries.Count == 0 || _navIndex == -1) return null;
            if (_navIndex < _entries.Count - 1) { _navIndex++; return _entries[_navIndex]; }
            _navIndex = -1;
            return string.Empty;
        }

        public void ResetNavigation() => _navIndex = -1;
    }
}

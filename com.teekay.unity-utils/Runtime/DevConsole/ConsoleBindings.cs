using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Maps Input System <see cref="Key"/> values to command-line strings. DevConsole polls
    /// this map each frame (when the console is closed) and executes any bound commands whose
    /// key was just pressed.
    ///
    /// Bindings persist across play sessions via a single PlayerPrefs string: each entry is
    /// "KeyName\tCommandLine", entries separated by "\n". Simple, non-fragile, no JSON
    /// dependency. Commands rarely contain tabs/newlines so collision risk is minimal.
    /// </summary>
    public sealed class ConsoleBindings
    {
        const string PREF_KEY = "DevConsole.Bindings";

        readonly Dictionary<Key, string> _bindings = new();

        public IReadOnlyDictionary<Key, string> All => _bindings;

        /// <summary>Try to parse a key name (e.g. "F3", "Backquote") into a Key enum value.</summary>
        public static bool TryParseKey(string name, out Key key) =>
            Enum.TryParse(name, ignoreCase: true, out key) && key != Key.None;

        public bool TryGet(Key key, out string command) => _bindings.TryGetValue(key, out command);

        public void Set(Key key, string command)
        {
            if (key == Key.None || string.IsNullOrWhiteSpace(command)) return;
            _bindings[key] = command;
            Save();
        }

        public bool Remove(Key key)
        {
            bool removed = _bindings.Remove(key);
            if (removed) Save();
            return removed;
        }

        /// <summary>
        /// Poll every binding once per frame. Returns the FIRST command whose key was just
        /// pressed, or null if none. Caller is responsible for executing the command (so the
        /// console can decide whether to run it — e.g. NOT when the console is open).
        /// </summary>
        public string ConsumePressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb == null || _bindings.Count == 0) return null;
            foreach (var pair in _bindings)
                if (kb[pair.Key].wasPressedThisFrame) return pair.Value;
            return null;
        }

        // ─── Persistence ───

        public void Load()
        {
            _bindings.Clear();
            string raw = PlayerPrefs.GetString(PREF_KEY, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrEmpty(line)) continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab >= line.Length - 1) continue;
                string keyName = line.Substring(0, tab);
                string cmd     = line.Substring(tab + 1);
                if (TryParseKey(keyName, out Key key))
                    _bindings[key] = cmd;
            }
        }

        void Save()
        {
            var sb = new StringBuilder();
            foreach (var pair in _bindings)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(pair.Key).Append('\t').Append(pair.Value);
            }
            PlayerPrefs.SetString(PREF_KEY, sb.ToString());
        }
    }
}

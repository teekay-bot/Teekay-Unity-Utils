using System;
using System.Collections.Generic;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Holds the live set of commands, CVars, and log categories. All lookups are
    /// case-insensitive. Names within each kind must be unique — registering the same name
    /// twice replaces the previous entry (with a warning).
    ///
    /// Commands and CVars share a name lookup space — typing `player.moveSpeed` matches either,
    /// since CVars are conceptually a special kind of command (with built-in get/set semantics).
    /// </summary>
    public sealed class ConsoleRegistry
    {
        readonly Dictionary<string, ConsoleCommand>     _commands   = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ConsoleCVar>        _cvars      = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ConsoleLogCategory> _categories = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fires when the set of commands or CVars changes (used by autocomplete to invalidate caches).</summary>
        public event Action OnRegistryChanged;

        public void Register(ConsoleCommand cmd)
        {
            if (cmd == null) return;
            _commands[cmd.Name] = cmd;
            OnRegistryChanged?.Invoke();
        }

        public void Register(ConsoleCVar cvar)
        {
            if (cvar == null) return;
            _cvars[cvar.Name] = cvar;
            OnRegistryChanged?.Invoke();
        }

        public void RegisterCategory(ConsoleLogCategory category)
        {
            if (category == null) return;
            _categories[category.name] = category;
        }

        /// <summary>Removes a command or CVar by name (no error if missing).</summary>
        public void Unregister(string name)
        {
            bool changed = _commands.Remove(name) | _cvars.Remove(name);
            if (changed) OnRegistryChanged?.Invoke();
        }

        public bool TryGetCommand(string name, out ConsoleCommand cmd) => _commands.TryGetValue(name, out cmd);
        public bool TryGetCVar(string name, out ConsoleCVar cvar)     => _cvars.TryGetValue(name, out cvar);
        public bool TryGetCategory(string name, out ConsoleLogCategory cat) => _categories.TryGetValue(name, out cat);

        public IReadOnlyCollection<ConsoleCommand>     AllCommands()    => _commands.Values;
        public IReadOnlyCollection<ConsoleCVar>        AllCVars()       => _cvars.Values;
        public IReadOnlyCollection<ConsoleLogCategory> AllCategories()  => _categories.Values;
    }
}

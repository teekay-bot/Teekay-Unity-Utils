using System;
using System.Collections.Generic;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// A registered console command. The handler is invoked when the user types the command
    /// name and presses Enter; arguments are pre-parsed into a <see cref="ConsoleArgs"/>.
    ///
    /// <see cref="ArgCompletions"/> optionally feeds the autocomplete system: each entry
    /// represents the candidate values for the corresponding argument index. Null entries
    /// (or indices past the end) mean "no completion for that argument". The most common
    /// case is bool-style commands declaring <c>{ new[] { "true", "false" } }</c> for arg 0.
    /// </summary>
    public sealed class ConsoleCommand
    {
        public readonly string Name;
        public readonly string Description;
        public readonly Action<ConsoleArgs> Handler;
        public readonly IReadOnlyList<IReadOnlyList<string>> ArgCompletions;

        public ConsoleCommand(string name, string description, Action<ConsoleArgs> handler,
            IReadOnlyList<IReadOnlyList<string>> argCompletions = null)
        {
            Name = name;
            Description = description ?? string.Empty;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            ArgCompletions = argCompletions;
        }
    }
}

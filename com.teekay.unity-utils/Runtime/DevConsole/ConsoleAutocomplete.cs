using System;
using System.Collections.Generic;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Matches partial console input against registered command/CVar names. Returns one
    /// inline-completion suffix (for the ghost hint) and / or a ranked list of matches (for
    /// the suggestion dropdown). Order: prefix matches first, then substring matches, both
    /// sorted alphabetically inside their tier.
    ///
    /// Argument-value completion: once the input contains a space, we switch from name
    /// completion to value completion using the resolved command's <see cref="ConsoleCommand.ArgCompletions"/>.
    /// </summary>
    public static class ConsoleAutocomplete
    {
        public enum MatchKind { Prefix, Substring }

        public readonly struct MatchResult
        {
            public readonly string Name;
            public readonly string Description;
            public readonly MatchKind Kind;
            public readonly bool IsCVar;
            public MatchResult(string name, string description, MatchKind kind, bool isCVar)
            { Name = name; Description = description; Kind = kind; IsCVar = isCVar; }
        }

        /// <summary>
        /// Inline ghost-hint completion. Returns the SUFFIX (characters Tab would append).
        /// Two modes:
        ///   1. No space in input → complete the command/CVar name (prefix match, alphabetically first).
        ///   2. Has space → complete the current argument value using the resolved command's
        ///      <see cref="ConsoleCommand.ArgCompletions"/>.
        /// </summary>
        public static bool TryGetCompletion(string input, ConsoleRegistry registry, out string suffix)
        {
            suffix = string.Empty;
            if (string.IsNullOrEmpty(input) || registry == null) return false;

            int firstSpace = input.IndexOf(' ');
            if (firstSpace < 0)
                return TryGetNameCompletion(input, registry, out suffix);

            return TryGetArgCompletion(input, firstSpace, registry, out suffix);
        }

        static bool TryGetNameCompletion(string input, ConsoleRegistry registry, out string suffix)
        {
            suffix = string.Empty;
            string best = null;
            foreach (var cmd in registry.AllCommands())
            {
                if (cmd.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase) &&
                    (best == null || string.CompareOrdinal(cmd.Name, best) < 0))
                    best = cmd.Name;
            }
            foreach (var cvar in registry.AllCVars())
            {
                if (cvar.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase) &&
                    (best == null || string.CompareOrdinal(cvar.Name, best) < 0))
                    best = cvar.Name;
            }

            if (best == null || best.Length == input.Length) return false;
            suffix = best.Substring(input.Length);
            return true;
        }

        static bool TryGetArgCompletion(string input, int firstSpace, ConsoleRegistry registry, out string suffix)
        {
            suffix = string.Empty;
            if (!TryResolveArgContext(input, firstSpace, registry,
                out var candidates, out string partial, out _))
                return false;

            string best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i];
                if (c == null) continue;
                if (c.StartsWith(partial, StringComparison.OrdinalIgnoreCase) &&
                    (best == null || string.CompareOrdinal(c, best) < 0))
                    best = c;
            }

            if (best == null || best.Length == partial.Length) return false;
            suffix = best.Substring(partial.Length);
            return true;
        }

        /// <summary>
        /// Collect up to <paramref name="maxCount"/> ranked matches for the suggestion
        /// dropdown. Prefix matches always rank above substring matches; ties broken
        /// alphabetically by name. Results are written into <paramref name="output"/>
        /// (cleared first) so the caller can reuse buffers across keystrokes.
        ///
        /// When the input has a space, matches come from the resolved command's argument
        /// completion list instead of the global name registry.
        /// </summary>
        public static void GetMatches(string input, ConsoleRegistry registry, int maxCount, List<MatchResult> output)
        {
            output.Clear();
            if (string.IsNullOrEmpty(input) || registry == null || maxCount <= 0) return;

            int firstSpace = input.IndexOf(' ');
            if (firstSpace < 0)
            {
                CollectNameMatches(input, registry, maxCount, output);
                return;
            }

            CollectArgMatches(input, firstSpace, registry, maxCount, output);
        }

        static void CollectNameMatches(string input, ConsoleRegistry registry, int maxCount, List<MatchResult> output)
        {
            CollectPrefix(input, registry, output);
            int prefixCount = output.Count;
            CollectSubstring(input, registry, output, prefixCount);

            output.Sort(0, prefixCount, MatchComparer.Instance);
            if (output.Count - prefixCount > 0)
                output.Sort(prefixCount, output.Count - prefixCount, MatchComparer.Instance);

            if (output.Count > maxCount) output.RemoveRange(maxCount, output.Count - maxCount);
        }

        static void CollectArgMatches(string input, int firstSpace, ConsoleRegistry registry, int maxCount, List<MatchResult> output)
        {
            if (!TryResolveArgContext(input, firstSpace, registry,
                out var candidates, out string partial, out string commandName))
                return;

            // Two tiers: prefix matches first (alphabetical), then substring (also alphabetical).
            int prefixStart = output.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i];
                if (c == null) continue;
                if (partial.Length == 0 || c.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    output.Add(new MatchResult(c, commandName, MatchKind.Prefix, isCVar: false));
            }
            int prefixCount = output.Count - prefixStart;

            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i];
                if (c == null) continue;
                if (partial.Length > 0 &&
                    c.IndexOf(partial, StringComparison.OrdinalIgnoreCase) > 0 &&
                    !AlreadyListed(output, c, prefixStart + prefixCount))
                    output.Add(new MatchResult(c, commandName, MatchKind.Substring, isCVar: false));
            }

            output.Sort(prefixStart, prefixCount, MatchComparer.Instance);
            int substrCount = output.Count - prefixStart - prefixCount;
            if (substrCount > 0)
                output.Sort(prefixStart + prefixCount, substrCount, MatchComparer.Instance);

            if (output.Count > maxCount) output.RemoveRange(maxCount, output.Count - maxCount);
        }

        /// <summary>
        /// Parse "<commandName> <arg0> <arg1> …" into (candidate list for the *currently typing*
        /// arg, partial text already typed for it, command name). Returns false if no completion
        /// data is available for this position.
        /// </summary>
        static bool TryResolveArgContext(string input, int firstSpace, ConsoleRegistry registry,
            out IReadOnlyList<string> candidates, out string partial, out string commandName)
        {
            candidates = null;
            partial = string.Empty;
            commandName = null;

            commandName = input.Substring(0, firstSpace);
            if (!registry.TryGetCommand(commandName, out var cmd)) return false;
            if (cmd.ArgCompletions == null || cmd.ArgCompletions.Count == 0) return false;

            // Find which argument we're currently typing. Tokens are whitespace-separated; the
            // active arg is the one the caret sits in — i.e., the last non-empty run or the empty
            // run after a trailing space.
            string tail = input.Substring(firstSpace + 1);
            int argIndex = 0;
            int tokenStart = 0;
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] == ' ')
                {
                    argIndex++;
                    tokenStart = i + 1;
                }
            }
            partial = tail.Substring(tokenStart);

            if (argIndex >= cmd.ArgCompletions.Count) return false;
            candidates = cmd.ArgCompletions[argIndex];
            return candidates != null && candidates.Count > 0;
        }

        static void CollectPrefix(string input, ConsoleRegistry registry, List<MatchResult> output)
        {
            foreach (var cmd in registry.AllCommands())
            {
                if (cmd.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    output.Add(new MatchResult(cmd.Name, cmd.Description, MatchKind.Prefix, isCVar: false));
            }
            foreach (var cvar in registry.AllCVars())
            {
                if (cvar.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    output.Add(new MatchResult(cvar.Name, cvar.Description, MatchKind.Prefix, isCVar: true));
            }
        }

        static void CollectSubstring(string input, ConsoleRegistry registry, List<MatchResult> output, int alreadyInPrefix)
        {
            foreach (var cmd in registry.AllCommands())
            {
                if (cmd.Name.IndexOf(input, StringComparison.OrdinalIgnoreCase) > 0 &&
                    !AlreadyListed(output, cmd.Name, alreadyInPrefix))
                    output.Add(new MatchResult(cmd.Name, cmd.Description, MatchKind.Substring, isCVar: false));
            }
            foreach (var cvar in registry.AllCVars())
            {
                if (cvar.Name.IndexOf(input, StringComparison.OrdinalIgnoreCase) > 0 &&
                    !AlreadyListed(output, cvar.Name, alreadyInPrefix))
                    output.Add(new MatchResult(cvar.Name, cvar.Description, MatchKind.Substring, isCVar: true));
            }
        }

        static bool AlreadyListed(List<MatchResult> output, string name, int upTo)
        {
            for (int i = 0; i < upTo; i++)
                if (output[i].Name == name) return true;
            return false;
        }

        sealed class MatchComparer : IComparer<MatchResult>
        {
            public static readonly MatchComparer Instance = new();
            public int Compare(MatchResult a, MatchResult b) => string.CompareOrdinal(a.Name, b.Name);
        }
    }
}

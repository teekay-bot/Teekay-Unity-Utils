using System.Collections.Generic;
using System.Text;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Tokenizes a console input line. Whitespace splits tokens, double-quotes group
    /// multi-word arguments. Unterminated quotes are tolerated (treated as if they ran to
    /// end-of-line) so the user gets sensible feedback rather than a crash.
    ///
    ///   say hello world           → ["say", "hello", "world"]
    ///   say "hello world"         → ["say", "hello world"]
    ///   teleport 1.5 0 -3         → ["teleport", "1.5", "0", "-3"]
    /// </summary>
    public static class ConsoleParser
    {
        public static string[] Tokenize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return System.Array.Empty<string>();

            var tokens = new List<string>();
            var sb = new StringBuilder(32);
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0) tokens.Add(sb.ToString());

            return tokens.ToArray();
        }
    }
}

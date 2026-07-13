using System.Globalization;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Argument list passed to a console command handler. Wraps the raw tokens with typed
    /// accessors. Parsing failures don't throw — they fall back to the provided default,
    /// so command handlers can keep their logic linear without try/catch.
    /// Use <see cref="LastError"/> to detect parse failures if you care.
    /// </summary>
    public sealed class ConsoleArgs
    {
        public static readonly ConsoleArgs Empty = new(System.Array.Empty<string>());

        readonly string[] _tokens;

        /// <summary>Last parse error (or null). Reset on every accessor call.</summary>
        public string LastError { get; private set; }

        public ConsoleArgs(string[] tokens) => _tokens = tokens ?? System.Array.Empty<string>();

        public int Count => _tokens.Length;

        public string this[int index] => index >= 0 && index < _tokens.Length ? _tokens[index] : null;

        /// <summary>Raw token at index (or null if out of range). No parsing.</summary>
        public string Raw(int index) => this[index];

        public float AsFloat(int index, float fallback = 0f)
        {
            LastError = null;
            string s = this[index];
            if (s == null) { LastError = $"missing arg {index}"; return fallback; }
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
            LastError = $"arg {index} '{s}' is not a float";
            return fallback;
        }

        public int AsInt(int index, int fallback = 0)
        {
            LastError = null;
            string s = this[index];
            if (s == null) { LastError = $"missing arg {index}"; return fallback; }
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            LastError = $"arg {index} '{s}' is not an int";
            return fallback;
        }

        public bool AsBool(int index, bool fallback = false)
        {
            LastError = null;
            string s = this[index];
            if (s == null) { LastError = $"missing arg {index}"; return fallback; }
            // Accept 0/1, true/false, on/off, yes/no — case insensitive.
            switch (s.ToLowerInvariant())
            {
                case "1": case "true":  case "on":  case "yes": return true;
                case "0": case "false": case "off": case "no":  return false;
            }
            LastError = $"arg {index} '{s}' is not a bool";
            return fallback;
        }
    }
}

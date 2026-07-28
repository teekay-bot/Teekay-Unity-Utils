using System.Collections.Generic;

namespace TeekayUtils.Tags
{
    /// <summary>
    /// One node in a hierarchical tag vocabulary ("Movement.Sprinting", "Status.Stunned") — the
    /// shared language abilities use to describe themselves and gate each other WITHOUT ever
    /// referencing each other's classes. Interned: <see cref="Get"/> returns the SAME instance for
    /// the same path, so a comparison is reference equality and a tag field costs one pointer.
    /// </summary>
    /// <remarks>
    /// Promotion candidate for TeekayUtils once the ability layer proves the API.
    ///
    /// Paths are case-sensitive and NOT normalized ("A. B" is a different tag than "A.B") — tags
    /// are meant to be declared once as code constants or validated config, not typed per use.
    /// Empty segments (leading/trailing/double dots) are rejected loudly at intern time.
    /// Main-thread only (static registry, no locks) — same assumption as all Unity script state.
    /// </remarks>
    public sealed class GameplayTag
    {
        static readonly Dictionary<string, GameplayTag> Registry = new Dictionary<string, GameplayTag>();

        /// <summary>Full dotted path, e.g. "Movement.Sprinting". Unique per instance.</summary>
        public string Path { get; }

        /// <summary>The tag one level up ("Movement" for "Movement.Sprinting"); null for a root tag.</summary>
        public GameplayTag Parent { get; }

        // Private on purpose: interning only works if Get is the single way an instance is born.
        GameplayTag(string path, GameplayTag parent)
        {
            Path = path;
            Parent = parent;
        }

        /// <summary>
        /// The one door to a tag instance: interns <paramref name="path"/> (and every ancestor —
        /// "A.B.C" materializes "A.B" and "A" too, shared with every other descendant) and returns
        /// the canonical instance. Call at init and cache the reference; the path parse is
        /// intern-time only, queries afterwards never touch strings.
        /// </summary>
        public static GameplayTag Get(string path)
        {
            ValidatePath(path);
            return GetValidated(path);
        }

        /// <summary>
        /// True when this tag IS the query or lives anywhere under it: "Movement.Sprinting"
        /// matches query "Movement" (an ability blocking all of Movement blocks sprint), but
        /// "Movement" does NOT match query "Movement.Sprinting" — a broad fact never satisfies a
        /// narrow question.
        /// </summary>
        public bool Matches(GameplayTag query)
        {
            if (query == null) return false;
            for (GameplayTag t = this; t != null; t = t.Parent)
                if (ReferenceEquals(t, query)) return true;
            return false;
        }

        public override string ToString() => Path;

        /// <summary>
        /// String-level mirror of <see cref="Matches"/> for tooling that works on UNRESOLVED
        /// paths (the ability window's arbitration overview): does <paramref name="path"/> equal
        /// <paramref name="queryPath"/> or live anywhere under it. Kept next to Matches so the
        /// two definitions of "matches" cannot drift apart — tests pin their equivalence.
        /// </summary>
        public static bool PathMatches(string path, string queryPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(queryPath)) return false;
            if (!path.StartsWith(queryPath, System.StringComparison.Ordinal)) return false;

            // "Movement.Sprint" must not match query "Movement.Sprinting" — the boundary after the
            // prefix has to be the end of the path or a dot.
            return path.Length == queryPath.Length || path[queryPath.Length] == '.';
        }

        /// <summary>
        /// Non-throwing validity check for a would-be tag path — the same rules <see cref="Get"/>
        /// enforces, exposed so edit-time tooling (the tag catalog, the Inspector picker) can
        /// judge user input without exception-driven control flow.
        /// </summary>
        public static bool IsValidPath(string path, out string error)
        {
            if (string.IsNullOrEmpty(path))
            {
                error = "Tag path must be non-empty.";
                return false;
            }

            // A leading, trailing or doubled dot all produce an empty segment — one scan finds all.
            int segmentStart = 0;
            for (int i = 0; i <= path.Length; i++)
            {
                if (i < path.Length && path[i] != '.') continue;
                if (i == segmentStart)
                {
                    error = $"Tag path \"{path}\" has an empty segment — check for leading/trailing/double dots.";
                    return false;
                }
                segmentStart = i + 1;
            }

            error = null;
            return true;
        }

        static GameplayTag GetValidated(string path)
        {
            if (Registry.TryGetValue(path, out GameplayTag existing)) return existing;

            int lastDot = path.LastIndexOf('.');
            GameplayTag parent = lastDot < 0 ? null : GetValidated(path.Substring(0, lastDot));

            var tag = new GameplayTag(path, parent);
            Registry.Add(path, tag);
            return tag;
        }

        // Throws, not logs: interning happens at assembly/init time, and a malformed path is a
        // typo to fix, not a runtime condition to survive (same rule as CharacterContext's ctor).
        static void ValidatePath(string path)
        {
            if (!IsValidPath(path, out string error))
                throw new System.ArgumentException(error, nameof(path));
        }
    }
}

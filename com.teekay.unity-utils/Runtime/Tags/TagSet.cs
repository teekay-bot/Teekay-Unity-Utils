using System.Collections.Generic;
using UnityEngine;

namespace TeekayUtils.Tags
{
    /// <summary>
    /// A character's live tag state, REFERENCE-COUNTED: two abilities both granting
    /// "Movement.Blocked" and one ending must not clear the other's grant — a plain HashSet is
    /// exactly that bug (the shared-owner family again). Every <see cref="Add"/> must be paired
    /// with exactly one <see cref="Remove"/>; an unbalanced Remove is reported loudly, because it
    /// means two owners think they hold the same grant.
    /// </summary>
    /// <remarks>
    /// Hierarchy-aware queries are O(1), not O(set): counts propagate to every ancestor on
    /// Add/Remove (holding "Movement.Sprinting" makes <see cref="Has"/>("Movement") true), the
    /// same scheme as Unreal's FGameplayTagCountContainer.
    ///
    /// Deliberately NO change events: the view layer POLLS this (animation reads it per frame like
    /// it reads Motor.Velocity). An event that fires exactly once per change is one-shot sim
    /// output — the thing the netcode disciplines keep out of the sim core — and a poll is enough
    /// until a real consumer proves otherwise.
    /// </remarks>
    public sealed class TagSet
    {
        readonly Dictionary<GameplayTag, int> _explicit = new Dictionary<GameplayTag, int>();
        readonly Dictionary<GameplayTag, int> _withAncestors = new Dictionary<GameplayTag, int>();

        /// <summary>
        /// Explicitly held tags with their counts, for debug HUDs/visualizers — foreach over this
        /// allocates nothing (struct enumerator). Ancestors are NOT listed; query them via
        /// <see cref="Has"/>.
        /// </summary>
        public IReadOnlyDictionary<GameplayTag, int> Explicit => _explicit;

        /// <summary>Grants one count of <paramref name="tag"/> (and, implicitly, of every ancestor).</summary>
        public void Add(GameplayTag tag)
        {
            // Log-and-continue, not throw: grants happen in per-frame ability code, which has a
            // duty to keep running (the constructor-vs-tick rule from CharacterContext).
            if (tag == null)
            {
                Debug.LogError("[TagSet] Add(null) — ignored. A null tag is a broken tag list upstream.");
                return;
            }

            _explicit[tag] = _explicit.GetValueOrDefault(tag) + 1;
            for (GameplayTag t = tag; t != null; t = t.Parent)
                _withAncestors[t] = _withAncestors.GetValueOrDefault(t) + 1;
        }

        /// <summary>Releases one count of <paramref name="tag"/>. Must pair with a prior <see cref="Add"/>.</summary>
        public void Remove(GameplayTag tag)
        {
            if (tag == null)
            {
                Debug.LogError("[TagSet] Remove(null) — ignored. A null tag is a broken tag list upstream.");
                return;
            }

            if (!_explicit.TryGetValue(tag, out int count))
            {
                Debug.LogError($"[TagSet] Remove(\"{tag}\") without a matching Add — unbalanced release, " +
                               "two owners think they hold the same grant. Ignored.");
                return;
            }

            // Counts are never stored at zero — a tag at 1 is removed outright, so Explicit and
            // Has stay truthful without callers filtering dead entries.
            if (count == 1) _explicit.Remove(tag);
            else _explicit[tag] = count - 1;

            for (GameplayTag t = tag; t != null; t = t.Parent)
            {
                int c = _withAncestors[t]; // exists by invariant: every explicit Add counted it
                if (c == 1) _withAncestors.Remove(t);
                else _withAncestors[t] = c - 1;
            }
        }

        /// <summary>
        /// Hierarchy-aware: true when <paramref name="tag"/> or ANY descendant of it is held —
        /// Has("Movement") is true while "Movement.Sprinting" is granted. Null asks for no tag
        /// and is false; a null in a tag LIST is caught loudly by the ability layer at init.
        /// </summary>
        public bool Has(GameplayTag tag) => tag != null && _withAncestors.ContainsKey(tag);

        /// <summary>Exact: true only when <paramref name="tag"/> itself was granted.</summary>
        public bool HasExact(GameplayTag tag) => tag != null && _explicit.ContainsKey(tag);

        /// <summary>True when at least one entry matches (<see cref="Has"/> semantics). Empty list: false.</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
                if (Has(tags[i])) return true;
            return false;
        }

        /// <summary>
        /// True when every entry matches (<see cref="Has"/> semantics). Empty list: true — an
        /// empty requirement list requires nothing, so it never blocks (the same vacuous-truth
        /// convention as GAS's ActivationRequiredTags).
        /// </summary>
        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return true;
            for (int i = 0; i < tags.Count; i++)
                if (!Has(tags[i])) return false;
            return true;
        }
    }
}

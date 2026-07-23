using System.Collections.Generic;
using UnityEngine;

namespace TeekayUtils.Tags
{
    /// <summary>
    /// The project's tag vocabulary as an asset — what the Inspector's tag picker lists, and the
    /// one place a new tag name is coined (the Unreal central-registry idea at package scale).
    /// Purely an EDIT-TIME dictionary: runtime never reads it — tags remain plain strings
    /// resolved through <see cref="GameplayTag.Get"/>, so a project without a catalog still
    /// runs; it just types tags blind.
    /// </summary>
    /// <remarks>
    /// One catalog per project (the picker uses the first it finds). Starts empty — the picker's
    /// "New tag…" prompt grows it as the project coins its vocabulary. Entries are validated and
    /// deduplicated on <see cref="Add"/>, not trusted on read — the list is hand-editable in the
    /// Inspector too.
    /// </remarks>
    [CreateAssetMenu(fileName = "GameplayTagCatalog", menuName = "TeekayUtils/Gameplay Tag Catalog")]
    public class GameplayTagCatalog : ScriptableObject
    {
        [Tooltip("Every tag path this project uses. Hierarchy is implied by dots — listing " +
                 "'Movement.Sprinting' makes 'Movement' matchable without listing it.")]
        [SerializeField]
        List<string> _paths = new List<string>();

        /// <summary>Every registered path, unfiltered — hand-edits included, so validate on use.</summary>
        public IReadOnlyList<string> Paths => _paths;

        public bool Contains(string path) => _paths.Contains(path);

        /// <summary>
        /// Registers a new path. False (with a loud log) on malformed input or a duplicate —
        /// callers are editor tooling, and a silent no-op there reads as "the button is broken".
        /// </summary>
        public bool Add(string path)
        {
            if (!GameplayTag.IsValidPath(path, out string error))
            {
                Debug.LogError($"[GameplayTagCatalog] {error}", this);
                return false;
            }

            if (_paths.Contains(path))
            {
                Debug.LogError($"[GameplayTagCatalog] \"{path}\" is already registered.", this);
                return false;
            }

            _paths.Add(path);
            _paths.Sort(System.StringComparer.Ordinal); // stable, diff-friendly asset ordering
            return true;
        }
    }
}

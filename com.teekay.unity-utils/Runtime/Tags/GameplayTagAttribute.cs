using UnityEngine;

namespace TeekayUtils.Tags
{
    /// <summary>
    /// Marks a serialized string (or string array — Unity applies it per element) as a gameplay
    /// tag path, so the Inspector draws a searchable picker over the project's
    /// <see cref="GameplayTagCatalog"/> instead of a raw text field. Runtime behavior is
    /// untouched — the field is still a plain string resolved through <see cref="GameplayTag.Get"/>;
    /// this attribute exists purely so tags are picked, not typed (a typo'd path is a VALID tag
    /// that matches nothing, the one failure mode raw strings cannot report).
    /// </summary>
    public class GameplayTagAttribute : PropertyAttribute { }
}

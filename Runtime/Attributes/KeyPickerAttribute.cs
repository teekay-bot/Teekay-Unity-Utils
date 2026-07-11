using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Marker attribute that swaps the default (very long, painful) <c>UnityEngine.InputSystem.Key</c>
    /// enum popup for a click-to-listen capture button — click, press a key, done. Esc cancels.
    /// The custom drawer lives in the editor assembly.
    /// </summary>
    public sealed class KeyPickerAttribute : PropertyAttribute { }
}

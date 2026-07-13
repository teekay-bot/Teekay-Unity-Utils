using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// Tap handler for a single autocomplete suggestion row. Lets touch users accept a completion
    /// by tapping the row — the mobile equivalent of pressing Tab on a hardware keyboard (which
    /// <see cref="ConsoleUI"/> only reads from <c>Keyboard.current</c>, absent on most phones).
    ///
    /// Deliberately NOT a <see cref="Selectable"/>: a Selectable would pull EventSystem selection
    /// into navigation state on tap, fighting the input field for focus. As a plain pointer-click
    /// handler it carries no selection of its own; ConsoleUI's per-frame re-focus keeps the input
    /// field selected after the tap.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class ConsoleSuggestionRow : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Index of this row within the visible window (0..MAX_VISIBLE-1), not the match index.</summary>
        public int VisualIndex;

        /// <summary>Invoked with <see cref="VisualIndex"/> when the row is tapped/clicked.</summary>
        public Action<int> Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(VisualIndex);
    }
}

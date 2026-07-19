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
    internal sealed class ConsoleSuggestionRow : MonoBehaviour, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Index of this row within the visible window (0..MAX_VISIBLE-1), not the match index.</summary>
        public int VisualIndex;

        /// <summary>Invoked with <see cref="VisualIndex"/> when the row is tapped/clicked.</summary>
        public Action<int> Clicked;

        /// <summary>Invoked when the pointer enters or leaves the row, so the owner can re-tint.</summary>
        public Action HoverChanged;

        public bool Hovered { get; private set; }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(VisualIndex);

        public void OnPointerEnter(PointerEventData _) { Hovered = true; HoverChanged?.Invoke(); }
        public void OnPointerExit(PointerEventData _) { Hovered = false; HoverChanged?.Invoke(); }

        void OnDisable()
        {
            // Rows are pooled and toggled off while hovered all the time — never let a stale
            // hover survive into the next reuse.
            if (!Hovered) return;
            Hovered = false;
            HoverChanged?.Invoke();
        }
    }
}

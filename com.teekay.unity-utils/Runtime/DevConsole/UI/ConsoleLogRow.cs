using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// Pointer adapter for one pooled log row: reports hover so the view can tint the row, and
    /// forwards clicks (click = copy the line). Dragging still scrolls — uGUI routes drag events
    /// past this component to the parent ScrollRect because it implements none of the drag
    /// interfaces.
    /// </summary>
    sealed class ConsoleLogRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>Pool index, assigned by the view; passed back through <see cref="Clicked"/>.</summary>
        public int PoolIndex;
        public Action<int> Clicked;
        public bool Hovered { get; private set; }

        public void OnPointerEnter(PointerEventData _) => Hovered = true;
        public void OnPointerExit(PointerEventData _) => Hovered = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Only a genuine click, not the tail end of a scroll-drag.
            if (eventData.dragging) return;
            Clicked?.Invoke(PoolIndex);
        }

        void OnDisable() => Hovered = false;
    }
}

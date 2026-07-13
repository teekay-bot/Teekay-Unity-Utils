using UnityEngine;
using UnityEngine.EventSystems;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// Drop on the title bar to make the parent window draggable. Reports drag deltas in
    /// screen pixels back to <see cref="ConsoleUI"/>, which clamps and applies them. Stateless
    /// itself — all window state lives on ConsoleUI so dragging and resizing share one source.
    /// </summary>
    public sealed class ConsoleWindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] internal ConsoleUI _owner;

        public void OnBeginDrag(PointerEventData e) { /* nothing to cache — delta is per-event */ }

        public void OnDrag(PointerEventData e)
        {
            if (_owner == null) return;
            _owner.OnWindowDrag(e.delta);
        }
    }
}

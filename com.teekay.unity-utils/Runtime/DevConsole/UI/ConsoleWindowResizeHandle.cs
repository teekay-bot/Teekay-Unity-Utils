using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// Edge / corner resize handle. The <see cref="_edges"/> flag combines which _edges this
    /// handle drags — single edge for the 4 sides, two flags for the 4 corners. ConsoleUI
    /// applies the delta correctly per edge so opposite _edges stay anchored when sized.
    /// </summary>
    internal sealed class ConsoleWindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] internal ConsoleUI _owner;
        [SerializeField] internal ResizeEdges _edges = ResizeEdges.None;

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (_owner == null || _edges == ResizeEdges.None) return;
            _owner.OnEdgeResize(_edges, e.delta);
        }
    }

    [Flags]
    internal enum ResizeEdges
    {
        None   = 0,
        Top    = 1 << 0,
        Bottom = 1 << 1,
        Left   = 1 << 2,
        Right  = 1 << 3,
    }
}

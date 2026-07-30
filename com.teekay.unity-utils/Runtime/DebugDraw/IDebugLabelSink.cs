using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Collects the text annotations an <see cref="IDebugDrawable"/> emits, so it never has to know
    /// which surface renders them (IMGUI over the Game view, <c>Handles.Label</c> in the Scene view).
    /// </summary>
    /// <remarks>
    /// World-anchored only, on purpose. A screen-space channel (a corner readout) is not here
    /// because nothing needs one yet: an overlay's numbers belong next to the geometry they explain,
    /// and a permanent corner HUD is a project's own UI rather than a system's debug output.
    /// </remarks>
    public interface IDebugLabelSink
    {
        /// <summary>
        /// Text anchored at a world position. Multi-line strings are fine — the renderer sizes the
        /// box from the content.
        /// </summary>
        void Label(Vector3 worldPosition, string text);
    }
}

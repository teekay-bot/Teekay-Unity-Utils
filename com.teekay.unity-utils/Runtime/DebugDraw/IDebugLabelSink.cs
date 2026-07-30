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

        /// <summary>
        /// Text about <paramref name="subjectWorldPosition"/> but placed at
        /// <paramref name="anchorWorldPosition"/>, for a caller that had to move the text clear of
        /// something — geometry of its own drawing, usually.
        /// </summary>
        /// <remarks>
        /// The pair is what makes the move work: a plate centred on its anchor still reaches half its
        /// width back toward whatever the anchor was pushed away from, and the caller cannot correct
        /// for that because the box's width is in pixels while its own offset is in metres. Given both
        /// points the renderer projects them, sees which way the push went ON SCREEN, and keeps the
        /// plate entirely on that side. Deciding the side in world space would get it backwards
        /// whenever the camera looks from the other side.
        /// </remarks>
        void Label(Vector3 subjectWorldPosition, Vector3 anchorWorldPosition, string text);
    }
}

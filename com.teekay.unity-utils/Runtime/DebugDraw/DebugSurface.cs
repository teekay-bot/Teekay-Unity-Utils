using System;

namespace TeekayUtils
{
    /// <summary>
    /// Which views an overlay is allowed to appear in. Declared per drawable, because the answer is
    /// not the same for every overlay: one describing screen-space work (what the player's aim is
    /// picking) belongs in the Game view, while one describing geometry (a capsule, a ground probe,
    /// a contact normal) is far easier to read in the Scene view, where the camera can be moved.
    /// </summary>
    /// <remarks>
    /// A per-drawable choice rather than a single global switch on <see cref="DebugDrawHub"/>: with
    /// one global flag, every component would need its own Inspector checkbox writing to it, which
    /// is one flag with N owners — the arrangement that makes two Inspectors disagree about what is
    /// on. Each drawable answering for itself keeps the checkbox and the behaviour in one place.
    /// </remarks>
    [Flags]
    public enum DebugSurface
    {
        /// <summary>Registered but drawing nowhere — the same result as switching the overlay off.</summary>
        None = 0,

        /// <summary>GL lines and IMGUI labels over the running game (and over development builds).</summary>
        GameView = 1 << 0,

        /// <summary>Gizmos and labels in the editor's Scene view.</summary>
        SceneView = 1 << 1,

        /// <summary>Both — what an overlay gets by saying nothing in particular.</summary>
        All = GameView | SceneView
    }
}

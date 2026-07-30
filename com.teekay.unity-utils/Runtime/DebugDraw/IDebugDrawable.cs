namespace TeekayUtils
{
    /// <summary>
    /// A system that draws its own MEASURED runtime state. Implement this on the class that OWNS
    /// the facts — the motor, the targeting pipeline — and register it with
    /// <see cref="DebugDrawHub"/>; the hub handles every surface (Game view, Scene view, labels),
    /// so an implementation only describes what to draw.
    /// <para>
    /// Why the owner draws instead of a sibling "visualizer" component: a separate component can
    /// only read what the owner makes public, so the overlay's appetite for internals leaks into
    /// the production API — and the interesting facts (per-hit verdicts, intent before it was
    /// solved) often exist for one call and are never stored at all. Drawing from inside keeps
    /// them private.
    /// </para>
    /// <para>
    /// The split with edit-time gizmos: <c>OnDrawGizmosSelected</c> draws CONFIG (the volumes a
    /// query is asked over — visible without entering Play mode), <see cref="DrawDebug"/> draws
    /// what was MEASURED this frame. One fact, one drawing site: drawing the same thing from both
    /// is how you get two arrows on screen and a day lost wondering which one lies.
    /// </para>
    /// </summary>
    public interface IDebugDrawable
    {
        /// <summary>
        /// Whether the hub should call <see cref="DrawDebug"/> at all this frame — the overlay's
        /// own on/off switch, checked before any work is done. Cheap by contract: the hub asks
        /// every registered drawable every frame.
        /// </summary>
        bool DebugEnabled { get; }

        /// <summary>
        /// Draw this frame's measured state. Called EXACTLY ONCE per frame, whatever surfaces are
        /// listening, so measuring in here (an extra raycast to explain a verdict, a string built
        /// for a label) is safe — a call site that must not repeat cannot be repeated.
        /// </summary>
        /// <param name="drawer">Records the shapes; replayed to every active surface afterwards.</param>
        /// <param name="labels">World-anchored text. An unlabeled arrow reads as "some arrow".</param>
        void DrawDebug(IDebugDrawer drawer, IDebugLabelSink labels);
    }
}

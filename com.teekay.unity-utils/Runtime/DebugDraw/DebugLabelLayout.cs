using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Decides where debug labels go in GUI space so they stay readable: one box per label, above the
    /// point it describes, clear of every other box, and never a second copy of a label that is
    /// already on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept apart from <see cref="DebugDrawHub"/> and free of any GUI call, because the part that goes
    /// wrong is arithmetic — which box moved, how far, whether it still points at its own fact — and
    /// that is exactly what looking at a screenshot cannot confirm. The hub projects and draws; this
    /// decides.
    /// </para>
    /// <para>
    /// The rules, in the order they apply. A duplicate (same text, near-identical anchor) is dropped:
    /// a measurement re-taken several times inside one step is one fact, and drawing it repeatedly
    /// only thickens the glyphs into mush. The box then sits ABOVE its anchor, because a label that
    /// covers the geometry it annotates hides the thing it was drawn to explain. Overlaps are resolved
    /// by moving further up — away from the scene rather than across it. Finally a box that could not
    /// stay on its anchor is marked <see cref="Placed.Displaced"/>, so the caller can draw a stem back
    /// to it.
    /// </para>
    /// </remarks>
    public static class DebugLabelLayout
    {
        /// <summary>Pixels kept between a box and its anchor, and between two boxes.</summary>
        public const float DefaultGap = 5f;

        /// <summary>
        /// Labels carrying the SAME text whose anchors are within this many pixels count as one.
        /// Sized for the repeated-measurement case: a value re-evaluated several times inside one
        /// simulation step lands a few pixels apart every time.
        /// </summary>
        public const float DefaultDedupeRadius = 24f;

        // Only bounds the pathological case: each pass moves a box strictly upward (see
        // MoveAboveOverlaps), so this loop cannot spin. An overlay with this many stacked labels is
        // unreadable for reasons no layout can fix.
        const int MaxResolvePasses = 16;

        // GUI space grows downward, so the BOTTOM of the screen is the largest y. Placing those
        // first is what makes "resolve upward" mean "into empty sky".
        static readonly Comparison<Request> BottomFirst = (a, b) => b.Anchor.y.CompareTo(a.Anchor.y);

        /// <summary>One label asking for a box: where it points, how big its text is, what it says.</summary>
        public struct Request
        {
            /// <summary>The label's subject, in GUI space (y grows downward from the top edge).</summary>
            public Vector2 Anchor;

            /// <summary>Measured size of the text, padding included.</summary>
            public Vector2 Size;

            /// <summary>The text to draw. Empty requests are skipped.</summary>
            public string Text;

            /// <summary>
            /// Which side of <see cref="Anchor"/> the box sits on: 0 centres it, +1 puts it entirely to
            /// the right, -1 entirely to the left, and values between slide it across. For an anchor
            /// that was deliberately pushed clear of something, a centred box still reaches half its
            /// width back over it — this is how the caller says which way "clear" was.
            /// </summary>
            public float Side;
        }

        /// <summary>One label after placement: the box to draw in, and whether it left its anchor.</summary>
        public struct Placed
        {
            /// <inheritdoc cref="Request.Anchor"/>
            public Vector2 Anchor;

            /// <summary>Where to draw the text, in GUI space.</summary>
            public Rect Box;

            /// <inheritdoc cref="Request.Text"/>
            public string Text;

            /// <summary>
            /// The box could not sit on its anchor and was moved. Draw a stem from the anchor to it:
            /// a label floating free of what it describes is worse than no label, because it silently
            /// attributes its numbers to whatever it happens to hover over.
            /// </summary>
            public bool Displaced;
        }

        /// <inheritdoc cref="DebugLabelLayout"/>
        /// <param name="requests">
        /// Labels to place. SORTED IN PLACE — the caller rebuilds this list every frame, so copying it
        /// to preserve an order nobody reads would allocate for nothing.
        /// </param>
        /// <param name="results">Cleared, then given one entry per label that survived deduping.</param>
        /// <param name="bounds">The visible area in GUI space; boxes are kept inside it.</param>
        /// <param name="gap"><inheritdoc cref="DefaultGap"/></param>
        /// <param name="dedupeRadius"><inheritdoc cref="DefaultDedupeRadius"/></param>
        public static void Arrange(List<Request> requests, List<Placed> results, Rect bounds,
            float gap = DefaultGap, float dedupeRadius = DefaultDedupeRadius)
        {
            if (results == null) return;

            results.Clear();
            if (requests == null || requests.Count == 0) return;

            requests.Sort(BottomFirst);

            for (int i = 0; i < requests.Count; i++)
            {
                Request request = requests[i];
                if (string.IsNullOrEmpty(request.Text)) continue;
                if (IsDuplicate(results, request, dedupeRadius)) continue;

                float desiredTop = request.Anchor.y - gap - request.Size.y;
                // Side 0 leaves the box centred (half its width either way); ±1 slides it until the
                // anchor is at one edge, so the box lies wholly to that side.
                float side = Mathf.Clamp(request.Side, -1f, 1f);
                float desiredLeft = request.Anchor.x - request.Size.x * 0.5f * (1f - side);
                var box = new Rect(desiredLeft, desiredTop, request.Size.x, request.Size.y);

                box = MoveAboveOverlaps(results, box, gap);
                box = KeepInside(box, bounds);

                results.Add(new Placed
                {
                    Anchor = request.Anchor,
                    Box = box,
                    Text = request.Text,
                    // Measured against where the box ASKED to be, not against the anchor: a caller that
                    // requested a side wanted the box off-centre, and calling that "displaced" would
                    // put a stem on every one of them.
                    // Half a pixel: below that it is a rounding artefact, not a move worth a stem.
                    Displaced = Mathf.Abs(box.y - desiredTop) > 0.5f
                                || Mathf.Abs(box.x - desiredLeft) > 0.5f
                });
            }
        }

        static bool IsDuplicate(List<Placed> placed, in Request request, float radius)
        {
            float radiusSq = radius * radius;

            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Text != request.Text) continue;
                if ((placed[i].Anchor - request.Anchor).sqrMagnitude <= radiusSq) return true;
            }

            return false;
        }

        static Rect MoveAboveOverlaps(List<Placed> placed, Rect box, float gap)
        {
            for (int pass = 0; pass < MaxResolvePasses; pass++)
            {
                int blocker = FindOverlap(placed, box, gap);
                if (blocker < 0) return box;

                // Above the blocker rather than below it: every box wants to be above its own anchor,
                // so resolving upward keeps labels in empty sky, while downward would march them back
                // over the geometry the placement rule exists to leave visible.
                //
                // This terminates by construction. Overlapping means blocker.yMin < box.yMax, i.e.
                // blocker.yMin < box.y + height, so the assignment below lands at less than
                // box.y - gap: every pass moves up by at least one gap.
                box.y = placed[blocker].Box.y - gap - box.height;
            }

            return box;
        }

        static int FindOverlap(List<Placed> placed, Rect box, float gap)
        {
            // Inflated by the gap, so boxes keep breathing room instead of merely not intersecting.
            var padded = new Rect(box.x - gap, box.y - gap, box.width + gap * 2f, box.height + gap * 2f);

            for (int i = 0; i < placed.Count; i++)
                if (padded.Overlaps(placed[i].Box)) return i;

            return -1;
        }

        // Clamped last, so the screen edge wins over the no-overlap rule: a stack tall enough to run
        // off the top ends up overlapping there, which is still readable-ish, whereas a box placed
        // outside the view is simply a fact that silently went missing.
        static Rect KeepInside(Rect box, Rect bounds)
        {
            if (bounds.width <= 0f || bounds.height <= 0f) return box;

            box.x = Mathf.Clamp(box.x, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - box.width));
            box.y = Mathf.Clamp(box.y, bounds.yMin, Mathf.Max(bounds.yMin, bounds.yMax - box.height));
            return box;
        }
    }
}

using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// An <see cref="IDebugDrawer"/> that RECORDS instead of rendering, so one description of a
    /// frame can be replayed to several surfaces (<see cref="GLDebugDrawer"/> for the Game view,
    /// <see cref="GizmosDebugDrawer"/> for the Scene view).
    /// </summary>
    /// <remarks>
    /// This is what lets <see cref="IDebugDrawable.DrawDebug"/> be called exactly once per frame.
    /// Rendering straight to a backend means running the draw routine once per surface — and gizmo
    /// passes, IMGUI passes and the GL hook all fire at different moments and different counts
    /// (<c>OnGUI</c> several times a frame), so any measuring inside the routine runs several times
    /// too. The classic workaround is to split "measure into lists" from "draw from lists" in every
    /// overlay; recording removes the reason to.
    /// <para>
    /// Only two primitives are stored. Everything else in <see cref="IDebugDrawer"/> tessellates
    /// through <see cref="IDebugDrawer.Line"/> in <see cref="DebugDrawShapes"/> — the same path the
    /// GL backend takes — so recording a capsule records its segments. <c>Sphere</c> is kept as its
    /// own primitive because it is the one call the backends render differently on purpose (a solid
    /// gizmo ball versus a GL cross), and flattening it here would erase that.
    /// </para>
    /// </remarks>
    public sealed class DebugDrawBuffer : IDebugDrawer
    {
        // Sized for a busy overlay's first frame; both arrays double on demand and never shrink,
        // so the steady state is the worst frame seen so far and no frame after it allocates.
        const int InitialSegments = 512;
        const int InitialDots = 32;

        // A runaway drawable (a loop over a growing list, a shape fed a huge radius) would
        // otherwise grow these arrays until the editor dies with no clue why. Loud and bounded
        // beats mysterious: the frame is truncated and the reason is said out loud, once.
        const int MaxSegments = 200_000;

        // Same segment count as the other backends so a circle looks identical in every view.
        const int CircleSegments = 24;

        struct Segment
        {
            public Vector3 From, To;
            public Color Color;
        }

        struct Dot
        {
            public Vector3 Center;
            public float Radius;
            public Color Color;
        }

        Segment[] _segments = new Segment[InitialSegments];
        Dot[] _dots = new Dot[InitialDots];

        // Reused: WireCube would otherwise allocate eight vectors per call.
        readonly Vector3[] _cubeCorners = new Vector3[8];

        int _segmentCount;
        int _dotCount;
        bool _warnedOverflow;

        /// <summary>Line segments recorded since the last <see cref="Clear"/>.</summary>
        public int SegmentCount => _segmentCount;

        /// <summary>Solid-sphere markers recorded since the last <see cref="Clear"/>.</summary>
        public int DotCount => _dotCount;

        /// <summary>Drops the recorded frame. Capacity is kept.</summary>
        public void Clear()
        {
            _segmentCount = 0;
            _dotCount = 0;
        }

        /// <summary>
        /// Re-issues everything recorded to a real backend. Cheap enough to call once per surface
        /// per frame — it is a walk over two arrays.
        /// </summary>
        public void Replay(IDebugDrawer target) => Replay(target, 0, _segmentCount, 0, _dotCount);

        /// <summary>
        /// Re-issues one SLICE of what was recorded. Counts only ever grow while a frame is being
        /// collected, so a caller that reads <see cref="SegmentCount"/> and <see cref="DotCount"/>
        /// before and after each contributor ends up holding a range per contributor — which is
        /// what lets ONE shared recording go to different surfaces for different contributors,
        /// without asking any of them to describe itself twice.
        /// </summary>
        /// <remarks>
        /// Indices are clamped rather than rejected: the ranges come from marks taken around a call
        /// that may have been cut short by the segment cap, and a debug overlay is the last place
        /// that should turn a truncated frame into an exception.
        /// </remarks>
        public void Replay(IDebugDrawer target, int fromSegment, int toSegment, int fromDot, int toDot)
        {
            if (target == null) return;

            int segmentFrom = Mathf.Clamp(fromSegment, 0, _segmentCount);
            int segmentTo = Mathf.Clamp(toSegment, segmentFrom, _segmentCount);
            for (int i = segmentFrom; i < segmentTo; i++)
            {
                Segment segment = _segments[i];
                target.Line(segment.From, segment.To, segment.Color);
            }

            int dotFrom = Mathf.Clamp(fromDot, 0, _dotCount);
            int dotTo = Mathf.Clamp(toDot, dotFrom, _dotCount);
            for (int i = dotFrom; i < dotTo; i++)
            {
                Dot dot = _dots[i];
                target.Sphere(dot.Center, dot.Radius, dot.Color);
            }
        }

        // ---------- Recorded primitives ----------

        public void Line(Vector3 from, Vector3 to, Color color)
        {
            if (_segmentCount == _segments.Length && !Grow()) return;

            _segments[_segmentCount++] = new Segment { From = from, To = to, Color = color };
        }

        public void Ray(Vector3 from, Vector3 direction, Color color) => Line(from, from + direction, color);

        public void Sphere(Vector3 center, float radius, Color color)
        {
            if (_dotCount == _dots.Length) System.Array.Resize(ref _dots, _dots.Length * 2);

            _dots[_dotCount++] = new Dot { Center = center, Radius = radius, Color = color };
        }

        // ---------- Tessellated through the shared shape code, exactly as the backends do ----------

        public void WireSphere(Vector3 center, float radius, Color color)
            => WireSphere(center, radius, color, DebugDrawShapes.DefaultSphereRings, DebugDrawShapes.DefaultSphereSlices);

        public void WireSphere(Vector3 center, float radius, Color color, int rings, int slices)
            => WireSphereBand(center, Vector3.up, radius, color, 0f, 180f, rings, slices);

        public void WireSphereBand(Vector3 center, Vector3 up, float radius, Color color,
                                   float fromPolarDegrees, float toPolarDegrees, int rings, int slices)
        {
            DebugDrawShapes.WireSphereBand(this, center, up, radius, color,
                fromPolarDegrees * Mathf.Deg2Rad, toPolarDegrees * Mathf.Deg2Rad, rings, slices);
        }

        public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color)
            => WireCapsule(start, end, radius, color, DebugDrawShapes.DefaultSphereRings, DebugDrawShapes.DefaultSphereSlices);

        public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color, int rings, int slices)
            => DebugDrawShapes.WireCapsule(this, start, end, radius, color, rings, slices);

        public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color)
            => ViewCone(apex, direction, fullAngleDegrees, range, color,
                DebugDrawShapes.DefaultSphereRings, DebugDrawShapes.DefaultSphereSlices);

        public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color,
                             int rings, int slices)
            => DebugDrawShapes.ViewCone(this, apex, direction, fullAngleDegrees, range, color, rings, slices);

        public void Arrow(Vector3 from, Vector3 direction, Color color)
            => DebugDrawShapes.Arrow(this, from, direction, color);

        public void Circle(Vector3 center, Vector3 normal, float radius, Color color)
        {
            DebugDrawGeometry.GetCircleBasis(normal, out Vector3 tangent, out Vector3 bitangent);
            DebugDrawShapes.Arc(this, center, tangent, bitangent, radius, 0f, Mathf.PI * 2f, CircleSegments, color);
        }

        public void WireCube(Vector3 center, Vector3 size, Color color)
        {
            DebugDrawGeometry.GetCubeCorners(center, size, _cubeCorners);
            foreach ((int from, int to) in DebugDrawGeometry.CubeEdges)
                Line(_cubeCorners[from], _cubeCorners[to], color);
        }

        /// <summary>Doubles the segment capacity, or refuses once the cap is reached.</summary>
        bool Grow()
        {
            if (_segments.Length >= MaxSegments)
            {
                if (!_warnedOverflow)
                {
                    _warnedOverflow = true;
                    Debug.LogWarning($"[DebugDrawBuffer] Hit the {MaxSegments} segment cap — this frame " +
                                     "is truncated. Something is drawing far more than a debug overlay should; " +
                                     "look for an unbounded loop or a shape given a huge radius.");
                }

                return false;
            }

            System.Array.Resize(ref _segments, Mathf.Min(_segments.Length * 2, MaxSegments));
            return true;
        }
    }
}

using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("TeekayUtils.Tests.Editor")]

namespace TeekayUtils
{
    /// <summary>
    /// Shared tessellation for the <see cref="IDebugDrawer"/> implementations. Shapes whose only
    /// backend-specific part is how one segment is emitted are built once here in terms of
    /// <see cref="IDebugDrawer.Line"/>, so every backend draws them identically by construction.
    /// </summary>
    static class DebugDrawShapes
    {
        public const int DefaultSphereRings = 6;
        public const int DefaultSphereSlices = 16;

        // Latitude rings at the poles collapse to a point; drawing one emits a cluster of
        // zero-length segments. Relative to the sphere radius so tiny spheres still get their rings.
        const float PoleRingFraction = 1e-3f;

        const int CapsuleSideLines = 4;
        const int ArrowHeadSpokes = 4;
        const float ArrowHeadFraction = 0.15f; // of the total arrow length
        const float ArrowHeadSpread = 0.4f;    // head radius as a fraction of head length (~22°)

        /// <summary>
        /// Draws the latitude/longitude grid of a sphere between two polar angles (radians, measured from
        /// <paramref name="up"/>). See <see cref="IDebugDrawer.WireSphereBand"/> for the angle convention.
        /// </summary>
        public static void WireSphereBand(IDebugDrawer drawer, Vector3 center, Vector3 up, float radius, Color color,
                                          float fromPolarRadians, float toPolarRadians, int rings, int slices)
        {
            // A zero/negative radius would otherwise still emit every meridian as a pile of
            // zero-length segments at the centre — a real case when a range value is left unset.
            if (radius <= 0f) return;

            rings = Mathf.Max(1, rings);
            slices = Mathf.Max(3, slices);

            Vector3 axis = up.normalized;
            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 tangent, out Vector3 bitangent);

            for (int i = 0; i <= rings; i++)
            {
                float polar = Mathf.Lerp(fromPolarRadians, toPolarRadians, (float)i / rings);
                DebugDrawGeometry.GetLatitudeRing(center, axis, radius, polar, out Vector3 ringCenter, out float ringRadius);
                if (ringRadius < radius * PoleRingFraction) continue;

                Arc(drawer, ringCenter, tangent, bitangent, ringRadius, 0f, Mathf.PI * 2f, slices, color);
            }

            for (int j = 0; j < slices; j++)
            {
                float azimuth = (float)j / slices * Mathf.PI * 2f;
                // A meridian is an arc on the circle spanned by the axis and the radial direction at this
                // azimuth, swept over the polar angle — the same parameterization the latitude rings use.
                Vector3 radial = tangent * Mathf.Cos(azimuth) + bitangent * Mathf.Sin(azimuth);
                Arc(drawer, center, axis, radial, radius, fromPolarRadians, toPolarRadians, rings, color);
            }
        }

        /// <summary>
        /// Draws a capsule between two sphere centres. See <see cref="IDebugDrawer.WireCapsule"/>
        /// for the end-point convention.
        /// </summary>
        public static void WireCapsule(IDebugDrawer drawer, Vector3 start, Vector3 end, float radius, Color color,
                                       int rings, int slices)
        {
            if (radius <= 0f) return;

            Vector3 span = end - start;
            if (span.sqrMagnitude < 1e-8f)
            {
                // Coincident end points: a capsule of zero length is just a sphere.
                WireSphereBand(drawer, start, Vector3.up, radius, color, 0f, Mathf.PI, rings, slices);
                return;
            }

            slices = Mathf.Max(3, slices);
            Vector3 axis = span.normalized;

            // Both caps are banded around the SAME axis rather than around opposing axes, so they
            // share a circle basis and their meridians line up instead of being rotated apart.
            WireSphereBand(drawer, end, axis, radius, color, 0f, Mathf.PI * 0.5f, rings, slices);
            WireSphereBand(drawer, start, axis, radius, color, Mathf.PI * 0.5f, Mathf.PI, rings, slices);

            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 tangent, out Vector3 bitangent);
            for (int i = 0; i < CapsuleSideLines; i++)
            {
                // Quarter turns land on cap meridians whenever slices is a multiple of 4.
                float azimuth = i / (float)CapsuleSideLines * Mathf.PI * 2f;
                Vector3 offset = (tangent * Mathf.Cos(azimuth) + bitangent * Mathf.Sin(azimuth)) * radius;
                drawer.Line(start + offset, end + offset, color);
            }
        }

        /// <summary>
        /// Draws the spherical cone that a range-and-angle perception check actually describes.
        /// See <see cref="IDebugDrawer.ViewCone"/> for the angle convention.
        /// </summary>
        public static void ViewCone(IDebugDrawer drawer, Vector3 apex, Vector3 direction, float fullAngleDegrees,
                                    float range, Color color, int rings, int slices)
        {
            if (range <= 0f || direction.sqrMagnitude < 1e-8f) return;

            slices = Mathf.Max(3, slices);
            float halfAngle = Mathf.Clamp(fullAngleDegrees, 0f, 360f) * 0.5f * Mathf.Deg2Rad;
            Vector3 axis = direction.normalized;

            // The reachable set of "within range AND within angle" is a spherical cap, not a flat
            // disc — drawing a disc would overstate the range everywhere except dead centre.
            WireSphereBand(drawer, apex, axis, range, color, 0f, halfAngle, rings, slices);

            // The straight sides, from the apex out to the rim of the cap.
            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 tangent, out Vector3 bitangent);
            DebugDrawGeometry.GetLatitudeRing(apex, axis, range, halfAngle,
                out Vector3 rimCenter, out float rimRadius);
            for (int i = 0; i < slices; i++)
            {
                float azimuth = i / (float)slices * Mathf.PI * 2f;
                drawer.Line(apex,
                    DebugDrawGeometry.PointOnCircle(rimCenter, tangent, bitangent, rimRadius, azimuth), color);
            }
        }

        /// <summary>
        /// Draws a line from <paramref name="from"/> to <c>from + direction</c> with a head at the far
        /// end, sized as a fraction of the length so the direction is readable at any scale.
        /// </summary>
        public static void Arrow(IDebugDrawer drawer, Vector3 from, Vector3 direction, Color color)
        {
            Vector3 tip = from + direction;
            drawer.Line(from, tip, color);

            float length = direction.magnitude;
            if (length < 1e-6f) return;

            Vector3 axis = direction / length;
            float headLength = length * ArrowHeadFraction;
            float headRadius = headLength * ArrowHeadSpread;

            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 tangent, out Vector3 bitangent);
            Vector3 headBase = tip - axis * headLength;
            for (int i = 0; i < ArrowHeadSpokes; i++)
            {
                float azimuth = i / (float)ArrowHeadSpokes * Mathf.PI * 2f;
                Vector3 offset = (tangent * Mathf.Cos(azimuth) + bitangent * Mathf.Sin(azimuth)) * headRadius;
                drawer.Line(tip, headBase + offset, color);
            }
        }

        /// <summary>
        /// Draws an arc of the circle defined by an orthonormal basis (see
        /// <see cref="DebugDrawGeometry.GetCircleBasis"/>). A full circle is 0 to 2π.
        /// </summary>
        public static void Arc(IDebugDrawer drawer, Vector3 center, Vector3 tangent, Vector3 bitangent,
                               float radius, float fromRadians, float toRadians, int segments, Color color)
        {
            Vector3 prev = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, fromRadians);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(fromRadians, toRadians, (float)i / segments);
                Vector3 point = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, angle);
                drawer.Line(prev, point, color);
                prev = point;
            }
        }
    }
}

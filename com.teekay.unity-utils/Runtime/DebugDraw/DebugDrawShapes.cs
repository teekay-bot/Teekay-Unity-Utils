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

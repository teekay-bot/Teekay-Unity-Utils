using System;
using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Pure geometry helpers shared by the <see cref="IDebugDrawer"/> implementations: orthonormal circle
    /// basis, points on a circle, and unit-cube corners/edges. Backend-free and side-effect-free, so the
    /// math can be unit-tested independently of any rendering context.
    /// </summary>
    public static class DebugDrawGeometry
    {
        /// <summary>
        /// The 12 edges of a cube as index pairs into the array filled by <see cref="GetCubeCorners"/>.
        /// Corner index packs the sign of each axis: bit 2 = +X, bit 1 = +Y, bit 0 = +Z (e.g. 7 = +++).
        /// </summary>
        public static readonly (int from, int to)[] CubeEdges =
        {
            // bottom face (−Y)
            (0, 1), (1, 5), (5, 4), (4, 0),
            // top face (+Y)
            (2, 3), (3, 7), (7, 6), (6, 2),
            // vertical edges
            (0, 2), (1, 3), (5, 7), (4, 6),
        };

        /// <summary>
        /// Produces an orthonormal pair spanning the plane perpendicular to <paramref name="normal"/>.
        /// <paramref name="normal"/> is normalized internally, and a fallback axis is used when it is
        /// parallel to world-forward, so both outputs are always unit-length and finite.
        /// </summary>
        public static void GetCircleBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            normal = normal.normalized;
            tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            bitangent = Vector3.Cross(normal, tangent); // unit, since normal ⟂ tangent and both are unit
        }

        /// <summary>Returns the point at <paramref name="angleRadians"/> on a circle defined by an orthonormal basis (see <see cref="GetCircleBasis"/>).</summary>
        public static Vector3 PointOnCircle(Vector3 center, Vector3 tangent, Vector3 bitangent, float radius, float angleRadians)
            => center + (tangent * Mathf.Cos(angleRadians) + bitangent * Mathf.Sin(angleRadians)) * radius;

        /// <summary>
        /// Fills <paramref name="corners"/> (length ≥ 8) with the cube corners for <paramref name="center"/>
        /// and full-extents <paramref name="size"/>, ordered to match <see cref="CubeEdges"/>.
        /// </summary>
        public static void GetCubeCorners(Vector3 center, Vector3 size, Vector3[] corners)
        {
            if (corners == null) throw new ArgumentNullException(nameof(corners));
            if (corners.Length < 8) throw new ArgumentException("corners must have length >= 8", nameof(corners));

            Vector3 h = size * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 4) != 0 ? h.x : -h.x;
                float y = (i & 2) != 0 ? h.y : -h.y;
                float z = (i & 1) != 0 ? h.z : -h.z;
                corners[i] = center + new Vector3(x, y, z);
            }
        }
    }
}

using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// <see cref="IDebugDrawer"/> that forwards to UnityEngine.Gizmos. Only does anything inside
    /// OnDrawGizmos / OnDrawGizmosSelected callbacks (Gizmos is a silent no-op elsewhere) — i.e. Scene-view
    /// debug visuals. Compiles in builds, but Gizmos render only in the editor.
    /// </summary>
    public sealed class GizmosDebugDrawer : IDebugDrawer
    {
        const int DiscSegments = 24;

        // Deliberately not Gizmos.DrawWireSphere: that draws three great circles plus a camera-facing
        // silhouette, which reads as a flat ring rather than a volume. The lat/long grid costs
        // 176 DrawLine calls at the default density — fine for debug, but pass lower rings/slices
        // when a call site draws dozens of spheres at once.
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

        public void Sphere(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(center, radius);
        }

        public void Line(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
        }

        public void Ray(Vector3 from, Vector3 direction, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawRay(from, direction);
        }

        public void Disc(Vector3 center, Vector3 normal, float radius, Color color)
        {
            DebugDrawGeometry.GetCircleBasis(normal, out Vector3 tangent, out Vector3 bitangent);
            DebugDrawShapes.Arc(this, center, tangent, bitangent, radius, 0f, Mathf.PI * 2f, DiscSegments, color);
        }

        public void WireCube(Vector3 center, Vector3 size, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }
    }
}

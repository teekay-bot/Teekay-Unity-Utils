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

        public void WireSphere(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
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
            Gizmos.color = color;
            DebugDrawGeometry.GetCircleBasis(normal, out Vector3 tangent, out Vector3 bitangent);

            Vector3 prev = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, 0f);
            for (int i = 1; i <= DiscSegments; i++)
            {
                float angle = (float)i / DiscSegments * Mathf.PI * 2f;
                Vector3 point = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, angle);
                Gizmos.DrawLine(prev, point);
                prev = point;
            }
        }

        public void WireCube(Vector3 center, Vector3 size, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }
    }
}

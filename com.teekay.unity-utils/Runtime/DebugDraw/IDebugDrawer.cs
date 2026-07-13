using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Backend-agnostic drawing surface for debug visuals. Two implementations exist:
    /// <see cref="GizmosDebugDrawer"/> (Scene view, gizmo callbacks) and
    /// <see cref="GLDebugDrawer"/> (Game view + builds, GL.LINES immediate mode).
    /// Call sites describe what to draw once; the drawer decides how.
    /// </summary>
    public interface IDebugDrawer
    {
        void WireSphere(Vector3 center, float radius, Color color);
        void Sphere(Vector3 center, float radius, Color color);
        void Line(Vector3 from, Vector3 to, Color color);
        /// <summary><paramref name="direction"/> is a delta from <paramref name="from"/> (not normalized); the line ends at <c>from + direction</c>.</summary>
        void Ray(Vector3 from, Vector3 direction, Color color);
        void Disc(Vector3 center, Vector3 normal, float radius, Color color);
        /// <summary><paramref name="size"/> is full extents (like <c>Gizmos.DrawWireCube</c>), not half.</summary>
        void WireCube(Vector3 center, Vector3 size, Color color);
    }
}

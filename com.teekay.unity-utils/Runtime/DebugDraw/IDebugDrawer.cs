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
        /// <summary>Wire sphere as a latitude/longitude grid at the backend's default density.</summary>
        void WireSphere(Vector3 center, float radius, Color color);
        /// <summary>
        /// Wire sphere as a latitude/longitude grid. <paramref name="rings"/> is the number of latitude
        /// bands (and the segment count of each meridian); <paramref name="slices"/> is the number of
        /// meridians (and the segment count of each latitude ring). Both are clamped to at least 1 and 3.
        /// </summary>
        void WireSphere(Vector3 center, float radius, Color color, int rings, int slices);
        /// <summary>
        /// A latitude band of a wire sphere, for domes and other partial ranges. Polar angles are measured
        /// in degrees from <paramref name="up"/>: 0 = the pole along up, 90 = the equator, 180 = the opposite
        /// pole. So (0, 180) is a full sphere and (0, 90) is an upper dome.
        /// </summary>
        void WireSphereBand(Vector3 center, Vector3 up, float radius, Color color,
                            float fromPolarDegrees, float toPolarDegrees, int rings, int slices);
        void Sphere(Vector3 center, float radius, Color color);
        void Line(Vector3 from, Vector3 to, Color color);
        /// <summary><paramref name="direction"/> is a delta from <paramref name="from"/> (not normalized); the line ends at <c>from + direction</c>.</summary>
        void Ray(Vector3 from, Vector3 direction, Color color);
        void Disc(Vector3 center, Vector3 normal, float radius, Color color);
        /// <summary><paramref name="size"/> is full extents (like <c>Gizmos.DrawWireCube</c>), not half.</summary>
        void WireCube(Vector3 center, Vector3 size, Color color);
    }
}

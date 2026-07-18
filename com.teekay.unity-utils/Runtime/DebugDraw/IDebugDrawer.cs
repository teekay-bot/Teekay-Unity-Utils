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

        /// <summary>
        /// Wire capsule between two sphere centres, matching <c>Physics.CheckCapsule</c>'s convention.
        /// Note this is NOT <c>CapsuleCollider.height</c>, which measures the full extent including both
        /// caps: for a collider, the centres sit <c>height / 2 - radius</c> either side of its centre.
        /// </summary>
        void WireCapsule(Vector3 start, Vector3 end, float radius, Color color);
        /// <summary>Wire capsule with an explicit cap density; see <see cref="WireSphere(Vector3, float, Color, int, int)"/>.</summary>
        void WireCapsule(Vector3 start, Vector3 end, float radius, Color color, int rings, int slices);

        /// <summary>
        /// The volume a range-and-angle perception check covers: everything within
        /// <paramref name="range"/> of <paramref name="apex"/> and within
        /// <paramref name="fullAngleDegrees"/> of <paramref name="direction"/>.
        /// <para>
        /// The angle is the FULL cone angle, matching how a view angle is normally configured and then
        /// tested as <c>Vector3.Angle(direction, toTarget) &lt;= viewAngle / 2</c>. The far end is drawn
        /// as a spherical cap rather than a flat disc, because that is the shape a distance check
        /// actually produces.
        /// </para>
        /// </summary>
        void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color);
        /// <summary>View cone with an explicit density; see <see cref="WireSphere(Vector3, float, Color, int, int)"/>.</summary>
        void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color,
                      int rings, int slices);

        void Line(Vector3 from, Vector3 to, Color color);
        /// <summary><paramref name="direction"/> is a delta from <paramref name="from"/> (not normalized); the line ends at <c>from + direction</c>.</summary>
        void Ray(Vector3 from, Vector3 direction, Color color);
        /// <summary>
        /// Like <see cref="Ray"/> but with a head at the far end, so the direction is readable.
        /// The head scales with the length.
        /// </summary>
        void Arrow(Vector3 from, Vector3 direction, Color color);
        /// <summary>Wire circle outline (an outline, not a filled disc) in the plane perpendicular to <paramref name="normal"/>.</summary>
        void Circle(Vector3 center, Vector3 normal, float radius, Color color);
        /// <summary><paramref name="size"/> is full extents (like <c>Gizmos.DrawWireCube</c>), not half.</summary>
        void WireCube(Vector3 center, Vector3 size, Color color);
    }
}

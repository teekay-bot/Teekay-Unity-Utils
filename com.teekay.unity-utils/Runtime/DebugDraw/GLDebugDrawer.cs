using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// <see cref="IDebugDrawer"/> that emits GL.LINES vertices. The caller must wrap usage in
    /// material.SetPass(0); GL.Begin(GL.LINES); … GL.End(); so the lines batch into one draw call.
    /// Works in both editor and standalone builds — used to surface debug visuals on the player camera
    /// in Game view (where Gizmos don't render unless the Gizmos toggle is on).
    /// <para>Holds only a small scratch buffer for cube corners; reuse a single instance per consumer.</para>
    /// </summary>
    public sealed class GLDebugDrawer : IDebugDrawer
    {
        const int WireSphereSegments = 16;
        const int DiscSegments = 24;

        // Reused across WireCube calls to avoid per-frame allocations in the GL hot path.
        readonly Vector3[] _cubeCorners = new Vector3[8];

        // Solid sphere → tiny 3D cross. GL.LINES can't fill, so for the typical "marker dot"
        // use case (radius < 0.1f) a small axis-aligned cross is more visible than a wire ball.
        public void Sphere(Vector3 center, float radius, Color color)
        {
            GL.Color(color);
            GL.Vertex(center + new Vector3(-radius, 0f, 0f)); GL.Vertex(center + new Vector3(radius, 0f, 0f));
            GL.Vertex(center + new Vector3(0f, -radius, 0f)); GL.Vertex(center + new Vector3(0f, radius, 0f));
            GL.Vertex(center + new Vector3(0f, 0f, -radius)); GL.Vertex(center + new Vector3(0f, 0f, radius));
        }

        public void WireSphere(Vector3 center, float radius, Color color)
        {
            GL.Color(color);
            DrawCircle(center, Vector3.up, radius, WireSphereSegments);
            DrawCircle(center, Vector3.right, radius, WireSphereSegments);
            DrawCircle(center, Vector3.forward, radius, WireSphereSegments);
        }

        public void Line(Vector3 from, Vector3 to, Color color)
        {
            GL.Color(color);
            GL.Vertex(from);
            GL.Vertex(to);
        }

        public void Ray(Vector3 from, Vector3 direction, Color color)
        {
            GL.Color(color);
            GL.Vertex(from);
            GL.Vertex(from + direction);
        }

        public void Disc(Vector3 center, Vector3 normal, float radius, Color color)
        {
            GL.Color(color);
            DrawCircle(center, normal, radius, DiscSegments);
        }

        public void WireCube(Vector3 center, Vector3 size, Color color)
        {
            GL.Color(color);
            DebugDrawGeometry.GetCubeCorners(center, size, _cubeCorners);
            foreach ((int from, int to) in DebugDrawGeometry.CubeEdges)
            {
                GL.Vertex(_cubeCorners[from]);
                GL.Vertex(_cubeCorners[to]);
            }
        }

        static void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
        {
            DebugDrawGeometry.GetCircleBasis(normal, out Vector3 tangent, out Vector3 bitangent);

            Vector3 prev = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = DebugDrawGeometry.PointOnCircle(center, tangent, bitangent, radius, angle);
                GL.Vertex(prev);
                GL.Vertex(point);
                prev = point;
            }
        }
    }
}

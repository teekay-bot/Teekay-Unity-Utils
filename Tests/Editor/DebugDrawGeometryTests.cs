using System;
using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    public class DebugDrawGeometryTests
    {
        [Test]
        public void GetCircleBasis_ArbitraryNormal_ReturnsUnitOrthonormalPair()
        {
            var normal = new Vector3(1, 2, 3);
            DebugDrawGeometry.GetCircleBasis(normal, out Vector3 tangent, out Vector3 bitangent);

            Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(bitangent.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Dot(tangent, bitangent), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Dot(tangent, normal.normalized), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Dot(bitangent, normal.normalized), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void GetCircleBasis_NormalParallelToForward_UsesFallbackAxis()
        {
            DebugDrawGeometry.GetCircleBasis(Vector3.forward, out Vector3 tangent, out Vector3 bitangent);

            Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(bitangent.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(float.IsNaN(tangent.x), Is.False);
        }

        [Test]
        public void PointOnCircle_LiesAtRadiusFromCenter()
        {
            var center = new Vector3(1, 2, 3);
            DebugDrawGeometry.GetCircleBasis(Vector3.up, out Vector3 t, out Vector3 b);

            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                Vector3 p = DebugDrawGeometry.PointOnCircle(center, t, b, 2.5f, angle);
                Assert.That(Vector3.Distance(center, p), Is.EqualTo(2.5f).Within(1e-4f));
            }
        }

        [Test]
        public void GetCubeCorners_ProducesHalfExtentOffsets()
        {
            var corners = new Vector3[8];
            var center = new Vector3(10, 20, 30);
            DebugDrawGeometry.GetCubeCorners(center, new Vector3(2, 4, 6), corners);

            foreach (Vector3 c in corners)
            {
                Vector3 d = c - center;
                Assert.That(Mathf.Abs(d.x), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(Mathf.Abs(d.y), Is.EqualTo(2f).Within(1e-5f));
                Assert.That(Mathf.Abs(d.z), Is.EqualTo(3f).Within(1e-5f));
            }
        }

        [Test]
        public void GetCubeCorners_TooSmallArray_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                DebugDrawGeometry.GetCubeCorners(Vector3.zero, Vector3.one, new Vector3[4]));
            Assert.Throws<ArgumentNullException>(() =>
                DebugDrawGeometry.GetCubeCorners(Vector3.zero, Vector3.one, null));
        }

        [Test]
        public void CubeEdges_TwelveEdges_EachConnectsAdjacentCorners()
        {
            Assert.That(DebugDrawGeometry.CubeEdges, Has.Length.EqualTo(12));

            foreach ((int from, int to) in DebugDrawGeometry.CubeEdges)
            {
                // Adjacent corners differ in exactly one axis bit.
                int diff = from ^ to;
                bool oneBit = diff != 0 && (diff & (diff - 1)) == 0;
                Assert.That(oneBit, Is.True, $"edge ({from},{to}) is not an adjacent pair");
            }
        }
    }
}

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
        public void GetLatitudeRing_AtEquator_ReturnsFullRadiusAtSphereCenter()
        {
            var center = new Vector3(1, 2, 3);
            DebugDrawGeometry.GetLatitudeRing(center, Vector3.up, 2.5f, Mathf.PI * 0.5f,
                out Vector3 ringCenter, out float ringRadius);

            Assert.That(Vector3.Distance(ringCenter, center), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(ringRadius, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void GetLatitudeRing_AtPole_ReturnsZeroRadiusAtPoleOffset()
        {
            var center = new Vector3(1, 2, 3);
            DebugDrawGeometry.GetLatitudeRing(center, Vector3.up, 2.5f, 0f,
                out Vector3 ringCenter, out float ringRadius);

            Assert.That(Vector3.Distance(ringCenter, center + Vector3.up * 2.5f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(ringRadius, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void GetLatitudeRing_TiltedUnnormalizedAxis_OffsetsAlongAxis()
        {
            var center = new Vector3(1, 2, 3);
            var axis = new Vector3(0, 3, 3); // deliberately not unit length
            DebugDrawGeometry.GetLatitudeRing(center, axis, 2f, 0f, out Vector3 ringCenter, out _);

            Assert.That(Vector3.Distance(ringCenter, center + axis.normalized * 2f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void MeridianPoints_LieOnSphereSurface()
        {
            var center = new Vector3(1, 2, 3);
            var axis = new Vector3(1, 1, 0).normalized;
            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 t, out Vector3 b);

            // A meridian is an arc on the circle spanned by (axis, radial), swept over the polar angle.
            Vector3 radial = t * Mathf.Cos(1.1f) + b * Mathf.Sin(1.1f);
            for (int i = 0; i <= 8; i++)
            {
                float polar = i / 8f * Mathf.PI;
                Vector3 p = DebugDrawGeometry.PointOnCircle(center, axis, radial, 2.5f, polar);
                Assert.That(Vector3.Distance(center, p), Is.EqualTo(2.5f).Within(1e-4f));
            }
        }

        [Test]
        public void LatitudeRingPoints_MatchMeridianPointsAtSameAngles()
        {
            var center = new Vector3(1, 2, 3);
            var axis = new Vector3(1, 1, 0).normalized;
            const float radius = 2.5f;
            const float azimuth = 1.1f;
            DebugDrawGeometry.GetCircleBasis(axis, out Vector3 t, out Vector3 b);
            Vector3 radial = t * Mathf.Cos(azimuth) + b * Mathf.Sin(azimuth);

            // The two parameterizations must agree, otherwise the grid lines would not intersect.
            for (int i = 1; i < 8; i++)
            {
                float polar = i / 8f * Mathf.PI;
                DebugDrawGeometry.GetLatitudeRing(center, axis, radius, polar,
                    out Vector3 ringCenter, out float ringRadius);

                Vector3 onRing = DebugDrawGeometry.PointOnCircle(ringCenter, t, b, ringRadius, azimuth);
                Vector3 onMeridian = DebugDrawGeometry.PointOnCircle(center, axis, radial, radius, polar);

                Assert.That(Vector3.Distance(onRing, onMeridian), Is.EqualTo(0f).Within(1e-4f));
                Assert.That(Vector3.Distance(center, onRing), Is.EqualTo(radius).Within(1e-4f));
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

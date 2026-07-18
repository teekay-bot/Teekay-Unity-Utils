using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    public class DebugDrawShapesTests
    {
        /// Captures the segments a shape tessellates into, so the geometry can be asserted
        /// without a render context.
        class RecordingDrawer : IDebugDrawer
        {
            public readonly List<(Vector3 from, Vector3 to)> Segments = new List<(Vector3, Vector3)>();

            public void Line(Vector3 from, Vector3 to, Color color) => Segments.Add((from, to));

            // Not exercised by these tests.
            public void WireSphere(Vector3 center, float radius, Color color) { }
            public void WireSphere(Vector3 center, float radius, Color color, int rings, int slices) { }
            public void WireSphereBand(Vector3 center, Vector3 up, float radius, Color color,
                                       float fromPolarDegrees, float toPolarDegrees, int rings, int slices) { }
            public void Sphere(Vector3 center, float radius, Color color) { }
            public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color) { }
            public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color, int rings, int slices) { }
            public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color) { }
            public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color,
                                 int rings, int slices) { }
            public void Ray(Vector3 from, Vector3 direction, Color color) { }
            public void Arrow(Vector3 from, Vector3 direction, Color color) { }
            public void Circle(Vector3 center, Vector3 normal, float radius, Color color) { }
            public void WireCube(Vector3 center, Vector3 size, Color color) { }
        }

        static RecordingDrawer Band(Vector3 center, Vector3 up, float radius,
                                    float fromDegrees, float toDegrees, int rings, int slices)
        {
            var drawer = new RecordingDrawer();
            DebugDrawShapes.WireSphereBand(drawer, center, up, radius, Color.white,
                fromDegrees * Mathf.Deg2Rad, toDegrees * Mathf.Deg2Rad, rings, slices);
            return drawer;
        }

        [Test]
        public void WireSphereBand_FullSphere_EveryVertexLiesOnTheSurface()
        {
            var center = new Vector3(1, 2, 3);
            RecordingDrawer drawer = Band(center, Vector3.up, 2.5f, 0f, 180f, 6, 16);

            Assert.That(drawer.Segments, Is.Not.Empty);
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                Assert.That(Vector3.Distance(center, from), Is.EqualTo(2.5f).Within(1e-3f));
                Assert.That(Vector3.Distance(center, to), Is.EqualTo(2.5f).Within(1e-3f));
            }
        }

        [Test]
        public void WireSphereBand_FullSphere_SkipsTheDegeneratePoleRings()
        {
            RecordingDrawer drawer = Band(Vector3.zero, Vector3.up, 2.5f, 0f, 180f, 6, 16);

            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                Assert.That(Vector3.Distance(from, to), Is.GreaterThan(1e-3f),
                    "a pole ring collapsed to zero-length segments");
            }
        }

        [Test]
        public void WireSphereBand_UpperDome_StaysOnOrAboveTheEquator()
        {
            var center = new Vector3(1, 2, 3);
            var up = new Vector3(1, 1, 0); // tilted and deliberately not unit length
            RecordingDrawer drawer = Band(center, up, 2f, 0f, 90f, 4, 16);

            Assert.That(drawer.Segments, Is.Not.Empty);
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                Assert.That(Vector3.Dot(from - center, up.normalized), Is.GreaterThan(-1e-3f));
                Assert.That(Vector3.Dot(to - center, up.normalized), Is.GreaterThan(-1e-3f));
            }
        }

        [Test]
        public void WireSphereBand_NonPositiveRadius_DrawsNothing()
        {
            // Otherwise every meridian still emits a pile of zero-length segments at the centre —
            // the case where a range value is left unset.
            Assert.That(Band(Vector3.zero, Vector3.up, 0f, 0f, 180f, 6, 16).Segments, Is.Empty);
            Assert.That(Band(Vector3.zero, Vector3.up, -1f, 0f, 180f, 6, 16).Segments, Is.Empty);
        }

        [Test]
        public void WireCapsule_EveryVertexLiesOnTheCapsuleSurface()
        {
            var start = new Vector3(1, 2, 3);
            var end = new Vector3(1, 7, 3);
            const float radius = 1.5f;

            var drawer = new RecordingDrawer();
            DebugDrawShapes.WireCapsule(drawer, start, end, radius, Color.white, 4, 16);

            Assert.That(drawer.Segments, Is.Not.Empty);
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                foreach (Vector3 p in new[] { from, to })
                {
                    // Distance to the capsule's core segment is the radius everywhere on the surface.
                    Vector3 axis = end - start;
                    float t = Mathf.Clamp01(Vector3.Dot(p - start, axis) / axis.sqrMagnitude);
                    Assert.That(Vector3.Distance(p, start + axis * t), Is.EqualTo(radius).Within(1e-3f));
                }
            }
        }

        [Test]
        public void WireCapsule_CoincidentEndPoints_FallsBackToASphere()
        {
            var center = new Vector3(1, 2, 3);
            var drawer = new RecordingDrawer();
            DebugDrawShapes.WireCapsule(drawer, center, center, 2f, Color.white, 4, 16);

            Assert.That(drawer.Segments, Is.Not.Empty);
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                Assert.That(Vector3.Distance(center, from), Is.EqualTo(2f).Within(1e-3f));
            }
        }

        [Test]
        public void ViewCone_StaysWithinRangeAndAngle()
        {
            var apex = new Vector3(1, 2, 3);
            var direction = new Vector3(1, 0.5f, 0); // not unit length
            const float fullAngle = 70f;
            const float range = 6f;

            var drawer = new RecordingDrawer();
            DebugDrawShapes.ViewCone(drawer, apex, direction, fullAngle, range, Color.white, 4, 16);

            Assert.That(drawer.Segments, Is.Not.Empty);
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                foreach (Vector3 p in new[] { from, to })
                {
                    Vector3 offset = p - apex;
                    Assert.That(offset.magnitude, Is.LessThanOrEqualTo(range + 1e-3f));
                    if (offset.sqrMagnitude > 1e-6f)
                    {
                        Assert.That(Vector3.Angle(direction, offset),
                            Is.LessThanOrEqualTo(fullAngle * 0.5f + 1e-2f));
                    }
                }
            }
        }

        [Test]
        public void ViewCone_RimSitsAtFullRange_NotOnAFlatDisc()
        {
            var apex = Vector3.zero;
            const float range = 5f;
            var drawer = new RecordingDrawer();
            DebugDrawShapes.ViewCone(drawer, apex, Vector3.forward, 90f, range, Color.white, 4, 16);

            // A flat-disc cone would place its rim short of the range; a spherical cap reaches it.
            float farthest = 0f;
            foreach ((Vector3 from, Vector3 to) in drawer.Segments)
            {
                farthest = Mathf.Max(farthest, from.magnitude);
                farthest = Mathf.Max(farthest, to.magnitude);
            }
            Assert.That(farthest, Is.EqualTo(range).Within(1e-3f));
        }

        [Test]
        public void Arrow_DrawsShaftToTipAndAHeadBehindIt()
        {
            var from = new Vector3(1, 2, 3);
            var direction = new Vector3(0, 4, 0);
            Vector3 tip = from + direction;

            var drawer = new RecordingDrawer();
            DebugDrawShapes.Arrow(drawer, from, direction, Color.white);

            Assert.That(drawer.Segments[0].from, Is.EqualTo(from));
            Assert.That(Vector3.Distance(drawer.Segments[0].to, tip), Is.LessThan(1e-4f));

            // Every head segment starts at the tip and points back down the shaft.
            for (int i = 1; i < drawer.Segments.Count; i++)
            {
                (Vector3 segFrom, Vector3 segTo) = drawer.Segments[i];
                Assert.That(Vector3.Distance(segFrom, tip), Is.LessThan(1e-4f));
                Assert.That(Vector3.Dot(segTo - tip, direction.normalized), Is.LessThan(0f));
            }
        }

        [Test]
        public void Arrow_ZeroLength_DrawsNoHead()
        {
            var drawer = new RecordingDrawer();
            DebugDrawShapes.Arrow(drawer, Vector3.one, Vector3.zero, Color.white);

            Assert.That(drawer.Segments, Has.Count.EqualTo(1), "a zero-length arrow should have no head");
        }

        [Test]
        public void WireSphereBand_DegenerateRingsAndSlices_ClampsAndStaysFinite()
        {
            foreach ((int rings, int slices) in new[] { (0, 0), (-5, -5), (1, 1) })
            {
                RecordingDrawer drawer = Band(Vector3.zero, Vector3.up, 2f, 0f, 180f, rings, slices);

                Assert.That(drawer.Segments, Is.Not.Empty, $"rings={rings} slices={slices} produced nothing");
                foreach ((Vector3 from, Vector3 to) in drawer.Segments)
                {
                    Assert.That(float.IsNaN(from.x) || float.IsInfinity(from.x), Is.False);
                    Assert.That(float.IsNaN(to.x) || float.IsInfinity(to.x), Is.False);
                }
            }
        }
    }
}

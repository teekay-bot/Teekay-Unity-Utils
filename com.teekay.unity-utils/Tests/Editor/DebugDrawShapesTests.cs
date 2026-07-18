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
            public void Ray(Vector3 from, Vector3 direction, Color color) { }
            public void Disc(Vector3 center, Vector3 normal, float radius, Color color) { }
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

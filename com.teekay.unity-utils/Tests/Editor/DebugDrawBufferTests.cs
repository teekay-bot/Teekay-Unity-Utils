using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    /// <summary>
    /// The buffer's promise is that recording then replaying is indistinguishable from drawing
    /// straight to a backend — that is what lets an overlay be collected once and shown on several
    /// surfaces. These tests pin the promise rather than the storage.
    /// </summary>
    public class DebugDrawBufferTests
    {
        /// Counts what a backend was actually asked to draw.
        class CountingDrawer : IDebugDrawer
        {
            public readonly List<(Vector3 From, Vector3 To, Color Color)> Segments =
                new List<(Vector3, Vector3, Color)>();
            public readonly List<(Vector3 Center, float Radius, Color Color)> Dots =
                new List<(Vector3, float, Color)>();

            public void Line(Vector3 from, Vector3 to, Color color) => Segments.Add((from, to, color));
            public void Ray(Vector3 from, Vector3 direction, Color color) => Line(from, from + direction, color);
            public void Sphere(Vector3 center, float radius, Color color) => Dots.Add((center, radius, color));

            // Not exercised: replay only ever issues Line and Sphere.
            public void WireSphere(Vector3 center, float radius, Color color) { }
            public void WireSphere(Vector3 center, float radius, Color color, int rings, int slices) { }
            public void WireSphereBand(Vector3 center, Vector3 up, float radius, Color color,
                                       float fromPolarDegrees, float toPolarDegrees, int rings, int slices) { }
            public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color) { }
            public void WireCapsule(Vector3 start, Vector3 end, float radius, Color color, int rings, int slices) { }
            public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color) { }
            public void ViewCone(Vector3 apex, Vector3 direction, float fullAngleDegrees, float range, Color color,
                                 int rings, int slices) { }
            public void Arrow(Vector3 from, Vector3 direction, Color color) { }
            public void Circle(Vector3 center, Vector3 normal, float radius, Color color) { }
            public void WireCube(Vector3 center, Vector3 size, Color color) { }
        }

        [Test]
        public void Line_ReplaysVerbatim()
        {
            var buffer = new DebugDrawBuffer();
            var from = new Vector3(1f, 2f, 3f);
            var to = new Vector3(4f, 5f, 6f);
            buffer.Line(from, to, Color.red);

            var target = new CountingDrawer();
            buffer.Replay(target);

            Assert.That(target.Segments, Has.Count.EqualTo(1));
            Assert.That(target.Segments[0].From, Is.EqualTo(from));
            Assert.That(target.Segments[0].To, Is.EqualTo(to));
            Assert.That(target.Segments[0].Color, Is.EqualTo(Color.red));
        }

        [Test]
        public void Ray_IsRecordedAsItsEndPoints()
        {
            var buffer = new DebugDrawBuffer();
            buffer.Ray(Vector3.one, Vector3.up * 2f, Color.green);

            var target = new CountingDrawer();
            buffer.Replay(target);

            Assert.That(target.Segments, Has.Count.EqualTo(1));
            Assert.That(target.Segments[0].To, Is.EqualTo(Vector3.one + Vector3.up * 2f));
        }

        /// The one call the backends render differently on purpose (solid gizmo ball vs GL cross),
        /// so it must survive as a sphere rather than being flattened into segments.
        [Test]
        public void Sphere_StaysASphere()
        {
            var buffer = new DebugDrawBuffer();
            buffer.Sphere(Vector3.forward, 0.25f, Color.blue);

            var target = new CountingDrawer();
            buffer.Replay(target);

            Assert.That(target.Segments, Is.Empty);
            Assert.That(target.Dots, Has.Count.EqualTo(1));
            Assert.That(target.Dots[0].Radius, Is.EqualTo(0.25f));
            Assert.That(buffer.DotCount, Is.EqualTo(1));
        }

        [Test]
        public void ComplexShapes_TessellateIntoSegments()
        {
            var buffer = new DebugDrawBuffer();
            buffer.WireCapsule(Vector3.zero, Vector3.up * 2f, 0.5f, Color.white);

            Assert.That(buffer.SegmentCount, Is.GreaterThan(0));
            Assert.That(buffer.DotCount, Is.Zero);
        }

        /// A cube has twelve edges however it is drawn — pinning the count catches a backend that
        /// silently stops decomposing (which would show up as nothing rendered, not as an error).
        [Test]
        public void WireCube_RecordsTwelveEdges()
        {
            var buffer = new DebugDrawBuffer();
            buffer.WireCube(Vector3.zero, Vector3.one, Color.cyan);

            Assert.That(buffer.SegmentCount, Is.EqualTo(12));
        }

        [Test]
        public void Replay_IsRepeatable_SoEverySurfaceSeesTheSameFrame()
        {
            var buffer = new DebugDrawBuffer();
            buffer.Line(Vector3.zero, Vector3.right, Color.white);
            buffer.Sphere(Vector3.up, 0.1f, Color.white);

            var first = new CountingDrawer();
            var second = new CountingDrawer();
            buffer.Replay(first);
            buffer.Replay(second);

            Assert.That(second.Segments, Has.Count.EqualTo(first.Segments.Count));
            Assert.That(second.Dots, Has.Count.EqualTo(first.Dots.Count));
        }

        [Test]
        public void Clear_DropsTheFrame()
        {
            var buffer = new DebugDrawBuffer();
            buffer.Line(Vector3.zero, Vector3.right, Color.white);
            buffer.Sphere(Vector3.up, 0.1f, Color.white);

            buffer.Clear();

            var target = new CountingDrawer();
            buffer.Replay(target);
            Assert.That(buffer.SegmentCount, Is.Zero);
            Assert.That(buffer.DotCount, Is.Zero);
            Assert.That(target.Segments, Is.Empty);
            Assert.That(target.Dots, Is.Empty);
        }

        /// Past the initial capacity the arrays have to grow rather than drop the overflow — a
        /// truncated overlay lies about what happened.
        [Test]
        public void Recording_GrowsBeyondInitialCapacity()
        {
            var buffer = new DebugDrawBuffer();
            const int count = 5000;
            for (int i = 0; i < count; i++)
                buffer.Line(Vector3.zero, Vector3.one * i, Color.white);

            Assert.That(buffer.SegmentCount, Is.EqualTo(count));

            var target = new CountingDrawer();
            buffer.Replay(target);
            Assert.That(target.Segments, Has.Count.EqualTo(count));
            Assert.That(target.Segments[count - 1].To, Is.EqualTo(Vector3.one * (count - 1)));
        }

        [Test]
        public void Replay_ToNull_DoesNotThrow()
        {
            var buffer = new DebugDrawBuffer();
            buffer.Line(Vector3.zero, Vector3.one, Color.white);

            Assert.DoesNotThrow(() => buffer.Replay(null));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    /// <summary>
    /// The layout's promises are the readability rules themselves: a box never covers the point it
    /// annotates, two boxes never cover each other, the same fact is never drawn twice, and a box that
    /// had to move says so. None of those can be confirmed by looking at a screenshot — a label that
    /// happens to be legible in one frame proves nothing about the arithmetic — so they are pinned
    /// here instead.
    /// </summary>
    public class DebugLabelLayoutTests
    {
        const float Gap = DebugLabelLayout.DefaultGap;

        static readonly Rect Screen1080 = new Rect(0f, 0f, 1920f, 1080f);

        static List<DebugLabelLayout.Request> Requests(params DebugLabelLayout.Request[] items) =>
            new List<DebugLabelLayout.Request>(items);

        static DebugLabelLayout.Request At(float x, float y, string text, float width = 80f,
            float height = 20f) =>
            new DebugLabelLayout.Request
            {
                Anchor = new Vector2(x, y),
                Size = new Vector2(width, height),
                Text = text
            };

        [Test]
        public void SingleLabel_SitsAboveItsAnchorAndCentredOnIt()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(At(500f, 500f, "45 degrees")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(1));
            // GUI space grows downward, so "above the anchor" is a SMALLER y than the anchor's.
            Assert.That(results[0].Box.yMax, Is.EqualTo(500f - Gap).Within(0.01f));
            Assert.That(results[0].Box.center.x, Is.EqualTo(500f).Within(0.01f));
            Assert.That(results[0].Displaced, Is.False);
        }

        [Test]
        public void SameTextAtNearlyTheSameSpot_IsDrawnOnce()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // The repeated-measurement case this rule exists for: one value re-evaluated several times
            // inside a single simulation step, landing a few pixels apart each time.
            DebugLabelLayout.Arrange(Requests(
                At(500f, 500f, "support 45"),
                At(503f, 498f, "support 45"),
                At(499f, 502f, "support 45")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(1));
        }

        [Test]
        public void SameTextFarApart_IsDrawnTwice()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // Two genuinely different contacts can carry the same number; deduping must not merge them.
            DebugLabelLayout.Arrange(Requests(
                At(300f, 500f, "45 degrees"),
                At(900f, 500f, "45 degrees")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(2));
        }

        [Test]
        public void TwoLabelsOnOneSpot_DoNotOverlap()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(
                At(500f, 500f, "ground 45"),
                At(500f, 500f, "support 45")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].Box.Overlaps(results[1].Box), Is.False);
        }

        [Test]
        public void OverlapIsResolvedUpwards_NeverOverTheGeometry()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(
                At(500f, 500f, "ground 45"),
                At(500f, 500f, "support 45")), results, Screen1080);

            // Both boxes stay above the anchor they describe — the whole point of resolving upward
            // instead of stacking downward into the shapes.
            Assert.That(results[0].Box.yMax, Is.LessThanOrEqualTo(500f - Gap + 0.01f));
            Assert.That(results[1].Box.yMax, Is.LessThanOrEqualTo(500f - Gap + 0.01f));
        }

        [Test]
        public void LowestAnchorKeepsItsPlace_AndTheOneAboveMoves()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // Anchors 10px apart vertically: their boxes (20px tall) cannot both sit where they want.
            DebugLabelLayout.Arrange(Requests(
                At(500f, 490f, "upper"),
                At(500f, 500f, "lower")), results, Screen1080);

            DebugLabelLayout.Placed lower = results.Find(p => p.Text == "lower");
            DebugLabelLayout.Placed upper = results.Find(p => p.Text == "upper");

            // Bottom-first: the lowest anchor is served first and keeps its ideal spot, so labels
            // pile up into empty sky rather than pushing each other down over the scene.
            Assert.That(lower.Box.yMax, Is.EqualTo(500f - Gap).Within(0.01f));
            Assert.That(lower.Displaced, Is.False);
            Assert.That(upper.Box.yMax, Is.LessThan(lower.Box.yMin));
            Assert.That(upper.Displaced, Is.True);
        }

        [Test]
        public void HorizontallySeparatedLabels_AreNotStacked()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // Same height, far apart: nothing overlaps, so nothing should move. A single-frontier
            // layout would wrongly stack these and drag one label away from its subject.
            DebugLabelLayout.Arrange(Requests(
                At(200f, 500f, "left"),
                At(1200f, 500f, "right")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(2));
            foreach (DebugLabelLayout.Placed placed in results)
            {
                Assert.That(placed.Box.yMax, Is.EqualTo(500f - Gap).Within(0.01f));
                Assert.That(placed.Displaced, Is.False);
            }
        }

        [Test]
        public void BoxNearTheTopEdge_StaysInsideTheView()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // The anchor is so close to the top edge that the ideal box would sit off-screen.
            DebugLabelLayout.Arrange(Requests(At(500f, 8f, "clamped")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Box.yMin, Is.GreaterThanOrEqualTo(0f));
            // Clamping IS a move, and the stem is what keeps the label honest about its subject.
            Assert.That(results[0].Displaced, Is.True);
        }

        [Test]
        public void BoxNearTheSideEdge_StaysInsideTheView()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(At(5f, 500f, "clamped", width: 120f)), results, Screen1080);

            Assert.That(results[0].Box.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(results[0].Box.xMax, Is.LessThanOrEqualTo(Screen1080.xMax + 0.01f));
        }

        [Test]
        public void SidePlacement_KeepsTheBoxWhollyOnThatSide()
        {
            var results = new List<DebugLabelLayout.Placed>();

            // The case it exists for: an anchor deliberately pushed clear of something. Centred, the
            // box would reach half its width straight back over whatever it was pushed away from.
            DebugLabelLayout.Request right = At(500f, 500f, "right", width: 80f);
            right.Side = 1f;
            DebugLabelLayout.Request left = At(900f, 500f, "left", width: 80f);
            left.Side = -1f;

            DebugLabelLayout.Arrange(Requests(right, left), results, Screen1080);

            Assert.That(results.Find(p => p.Text == "right").Box.xMin, Is.EqualTo(500f).Within(0.01f));
            Assert.That(results.Find(p => p.Text == "left").Box.xMax, Is.EqualTo(900f).Within(0.01f));
        }

        [Test]
        public void SidePlacement_IsNotReportedAsDisplaced()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Request request = At(500f, 500f, "aside");
            request.Side = 1f;

            DebugLabelLayout.Arrange(Requests(request), results, Screen1080);

            // Off-centre because it was ASKED to be. A stem here would appear on every side-placed
            // label and mean nothing.
            Assert.That(results[0].Displaced, Is.False);
        }

        [Test]
        public void EmptyText_IsSkipped()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(
                At(500f, 500f, ""),
                At(600f, 500f, null),
                At(700f, 500f, "kept")), results, Screen1080);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Text, Is.EqualTo("kept"));
        }

        [Test]
        public void PreviousResults_AreCleared()
        {
            var results = new List<DebugLabelLayout.Placed>();

            DebugLabelLayout.Arrange(Requests(At(500f, 500f, "first")), results, Screen1080);
            DebugLabelLayout.Arrange(Requests(At(500f, 500f, "second")), results, Screen1080);

            // Every frame rebuilds the list; a stale entry would leave a label frozen on screen.
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Text, Is.EqualTo("second"));
        }

        [Test]
        public void NullRequests_AreHandled()
        {
            var results = new List<DebugLabelLayout.Placed> { new DebugLabelLayout.Placed() };

            Assert.DoesNotThrow(() => DebugLabelLayout.Arrange(null, results, Screen1080));
            Assert.That(results, Is.Empty);
        }
    }
}

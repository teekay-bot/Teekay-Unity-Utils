using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    /// EditMode tests for the pure-value extensions (no GameObjects involved).
    public class ValueExtensionsTests
    {
        // --- Vectors ---

        [Test]
        public void Vector3_With_ReplacesOnlyGivenComponents()
        {
            var v = new Vector3(1, 2, 3).With(y: 9);
            Assert.That(v, Is.EqualTo(new Vector3(1, 9, 3)));
        }

        [Test]
        public void Vector3_ComponentDivide_SkipsZeroDivisors()
        {
            var v = new Vector3(10, 10, 10).ComponentDivide(new Vector3(2, 0, 5));
            Assert.That(v, Is.EqualTo(new Vector3(5, 10, 2)));
        }

        [Test]
        public void Vector2_ToVector3XZ_MapsYToZ()
        {
            Assert.That(new Vector2(3, 7).ToVector3XZ(), Is.EqualTo(new Vector3(3, 0, 7)));
        }

        [Test]
        public void Vector3_Quantize_SnapsDownToGrid()
        {
            var v = new Vector3(2.7f, -0.3f, 5.0f).Quantize(Vector3.one);
            Assert.That(v, Is.EqualTo(new Vector3(2, -1, 5)));
        }

        [Test]
        public void Vector3_DirectionTo_IsNormalized()
        {
            var dir = Vector3.zero.DirectionTo(new Vector3(10, 0, 0));
            Assert.That(dir, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void Vector3_SqrDistanceTo_MatchesSquaredDistance()
        {
            float sqr = new Vector3(1, 0, 0).SqrDistanceTo(new Vector3(4, 4, 0));
            Assert.That(sqr, Is.EqualTo(25f).Within(1e-5f));
        }

        [Test]
        public void Vector3_InRangeOf_IsInclusiveAtBoundary()
        {
            Assert.That(Vector3.zero.InRangeOf(new Vector3(5, 0, 0), 5f), Is.True);
            Assert.That(Vector3.zero.InRangeOf(new Vector3(5.001f, 0, 0), 5f), Is.False);
        }

        [Test]
        public void Vector2_Rotate_90Degrees_IsCounterClockwise()
        {
            var rotated = Vector2.right.Rotate(90f);
            Assert.That(rotated.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(rotated.y, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Vector3_RandomPointInAnnulus_StaysWithinRadii()
        {
            var origin = new Vector3(5, 0, 5);
            for (int i = 0; i < 100; i++)
            {
                Vector3 p = origin.RandomPointInAnnulus(2f, 4f);
                float distance = Vector3.Distance(origin, p);
                Assert.That(distance, Is.InRange(2f - 1e-4f, 4f + 1e-4f));
                Assert.That(p.y, Is.EqualTo(0f), "annulus should lie on the XZ plane");
            }
        }

        [Test]
        public void Vector3_ProjectOntoLine_FindsClosestPoint()
        {
            // Point above the X axis projects straight down onto it.
            var projected = new Vector3(3, 5, 0).ProjectOntoLine(Vector3.zero, Vector3.right);
            Assert.That(projected, Is.EqualTo(new Vector3(3, 0, 0)));

            // Non-normalized direction must give the same answer.
            var projected2 = new Vector3(3, 5, 0).ProjectOntoLine(Vector3.zero, new Vector3(10, 0, 0));
            Assert.That(projected2, Is.EqualTo(new Vector3(3, 0, 0)));

            // Degenerate direction falls back to lineStart.
            var degenerate = new Vector3(3, 5, 0).ProjectOntoLine(Vector3.one, Vector3.zero);
            Assert.That(degenerate, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Vector3_RotateOntoPlane_PreservesLengthAndLandsOnPlane()
        {
            // A vector on the ground plane (up = Y) rotated onto a wall (normal = X)
            // must stay unit length and become perpendicular to the wall normal.
            Vector3 rotated = new Vector3(0, 0, 1).RotateOntoPlane(Vector3.right, Vector3.up);

            Assert.That(rotated.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Dot(rotated, Vector3.right), Is.EqualTo(0f).Within(1e-4f));
        }

        // --- Numbers ---

        [Test]
        public void IsOdd_NegativeOddNumber_ReturnsTrue()
        {
            // Upstream bug: i % 2 == 1 is false for negative odds in C#.
            Assert.That((-3).IsOdd(), Is.True);
            Assert.That(3.IsOdd(), Is.True);
            Assert.That(4.IsOdd(), Is.False);
        }

        [Test]
        public void IsEven_HandlesNegativesAndZero()
        {
            Assert.That((-2).IsEven(), Is.True);
            Assert.That(0.IsEven(), Is.True);
            Assert.That(7.IsEven(), Is.False);
        }

        [Test]
        public void AtLeast_AtMost_ClampCorrectly()
        {
            Assert.That(3.AtLeast(5), Is.EqualTo(5));
            Assert.That(3f.AtMost(2f), Is.EqualTo(2f));
        }

        [Test]
        public void Remap_MapsAndClamps()
        {
            Assert.That(5f.Remap(0f, 10f, 0f, 100f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(20f.Remap(0f, 10f, 0f, 100f), Is.EqualTo(100f), "output should clamp");
        }

        // --- Strings ---

        [Test]
        public void Slice_NegativeEndIndex_CountsFromEnd()
        {
            Assert.That("teekay".Slice(1, -1), Is.EqualTo("eeka"));
        }

        [Test]
        public void Shorten_And_OrEmpty_Work()
        {
            Assert.That("abcdef".Shorten(3), Is.EqualTo("abc"));
            Assert.That("ab".Shorten(5), Is.EqualTo("ab"));
            Assert.That(((string)null).OrEmpty(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void RichColor_WrapsInTag()
        {
            Assert.That("hi".RichColor("red"), Is.EqualTo("<color=red>hi</color>"));
        }

        // --- Collections ---

        [Test]
        public void IsNullOrEmpty_CoversAllCases()
        {
            List<int> nullList = null;
            Assert.That(nullList.IsNullOrEmpty(), Is.True);
            Assert.That(new List<int>().IsNullOrEmpty(), Is.True);
            Assert.That(new List<int> { 1 }.IsNullOrEmpty(), Is.False);
        }

        [Test]
        public void Swap_ExchangesElements()
        {
            var list = new List<int> { 1, 2, 3 };
            list.Swap(0, 2);
            Assert.That(list, Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [Test]
        public void Shuffle_PreservesElements()
        {
            var list = Enumerable.Range(0, 50).ToList();
            list.Shuffle();
            Assert.That(list, Is.EquivalentTo(Enumerable.Range(0, 50)));
        }

        [Test]
        public void Random_SingleElement_ReturnsIt()
        {
            Assert.That(new List<int> { 42 }.Random(), Is.EqualTo(42));
            // Lazy sequence path (reservoir sampling)
            Assert.That(Enumerable.Range(7, 1).Random(), Is.EqualTo(7));
        }

        [Test]
        public void ForEach_VisitsEveryElement()
        {
            int sum = 0;
            new[] { 1, 2, 3 }.ForEach(i => sum += i);
            Assert.That(sum, Is.EqualTo(6));
        }

        // --- Colors ---

        [Test]
        public void SetAlpha_OnlyChangesAlpha()
        {
            var c = Color.red.SetAlpha(0.5f);
            Assert.That(c, Is.EqualTo(new Color(1, 0, 0, 0.5f)));
        }

        [Test]
        public void Add_ClampsAtOne()
        {
            var c = new Color(0.8f, 0.8f, 0.8f, 1f).Add(new Color(0.8f, 0.8f, 0.8f, 1f));
            Assert.That(c, Is.EqualTo(new Color(1, 1, 1, 1)));
        }

        [Test]
        public void Invert_KeepsAlpha()
        {
            var c = new Color(1, 0, 0, 0.3f).Invert();
            Assert.That(c, Is.EqualTo(new Color(0, 1, 1, 0.3f)));
        }

        [Test]
        public void HexRoundTrip_PreservesColor()
        {
            Color parsed = ColorExtensions.FromHex(Color.cyan.ToHex());
            Assert.That(parsed.r, Is.EqualTo(Color.cyan.r).Within(1f / 255f));
            Assert.That(parsed.g, Is.EqualTo(Color.cyan.g).Within(1f / 255f));
            Assert.That(parsed.b, Is.EqualTo(Color.cyan.b).Within(1f / 255f));
        }

        // --- LayerMask ---

        [Test]
        public void LayerMask_Contains_ChecksBits()
        {
            LayerMask mask = (1 << 3) | (1 << 8);
            Assert.That(mask.Contains(3), Is.True);
            Assert.That(mask.Contains(8), Is.True);
            Assert.That(mask.Contains(4), Is.False);
        }
    }
}

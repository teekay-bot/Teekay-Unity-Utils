using System;
using NUnit.Framework;
using TeekayUtils.Tags;

namespace TeekayUtils.Tests
{
    /// <summary>
    /// Interning, hierarchy building and match semantics. The registry is static and shared across
    /// tests by design — tags are immutable, so shared instances are a feature (reference
    /// equality), not leakage between tests.
    /// </summary>
    public class GameplayTagTests
    {
        // ---------- interning ----------

        [Test]
        public void Get_SamePath_ReturnsSameInstance()
        {
            Assert.AreSame(GameplayTag.Get("Test.Intern.A"), GameplayTag.Get("Test.Intern.A"));
        }

        [Test]
        public void Get_BuildsParentChain()
        {
            var leaf = GameplayTag.Get("Test.Chain.Leaf");

            Assert.AreEqual("Test.Chain.Leaf", leaf.Path);
            Assert.AreEqual("Test.Chain", leaf.Parent.Path);
            Assert.AreEqual("Test", leaf.Parent.Parent.Path);
            Assert.IsNull(leaf.Parent.Parent.Parent, "a root tag has no parent");
        }

        [Test]
        public void Get_SiblingsShareTheParentInstance()
        {
            var a = GameplayTag.Get("Test.Shared.A");
            var b = GameplayTag.Get("Test.Shared.B");

            Assert.AreSame(a.Parent, b.Parent);
            Assert.AreSame(a.Parent, GameplayTag.Get("Test.Shared"), "ancestors are real interned tags");
        }

        // ---------- validation ----------

        [Test]
        public void Get_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => GameplayTag.Get(null));
            Assert.Throws<ArgumentException>(() => GameplayTag.Get(""));
        }

        [Test]
        public void Get_EmptySegments_Throw()
        {
            Assert.Throws<ArgumentException>(() => GameplayTag.Get("."));
            Assert.Throws<ArgumentException>(() => GameplayTag.Get("A."));
            Assert.Throws<ArgumentException>(() => GameplayTag.Get(".A"));
            Assert.Throws<ArgumentException>(() => GameplayTag.Get("A..B"));
        }

        [Test]
        public void Get_IsCaseSensitive()
        {
            Assert.AreNotSame(GameplayTag.Get("Test.Case"), GameplayTag.Get("Test.CASE"));
        }

        // ---------- Matches ----------

        [Test]
        public void Matches_Self_True()
        {
            var tag = GameplayTag.Get("Test.Match.Self");
            Assert.IsTrue(tag.Matches(tag));
        }

        [Test]
        public void Matches_AncestorQuery_True()
        {
            // An ability blocking all of "Test.Match" blocks everything under it.
            Assert.IsTrue(GameplayTag.Get("Test.Match.Deep.Leaf").Matches(GameplayTag.Get("Test.Match")));
        }

        [Test]
        public void Matches_DescendantQuery_False()
        {
            // A broad fact never satisfies a narrow question.
            Assert.IsFalse(GameplayTag.Get("Test.Match").Matches(GameplayTag.Get("Test.Match.Deep.Leaf")));
        }

        [Test]
        public void Matches_Sibling_False()
        {
            Assert.IsFalse(GameplayTag.Get("Test.Match.A").Matches(GameplayTag.Get("Test.Match.B")));
        }

        [Test]
        public void Matches_Null_False()
        {
            Assert.IsFalse(GameplayTag.Get("Test.Match.Self").Matches(null));
        }

        [Test]
        public void ToString_IsThePath()
        {
            Assert.AreEqual("Test.Print", GameplayTag.Get("Test.Print").ToString());
        }

        // ---------- PathMatches: the string mirror must agree with Matches ----------

        [Test]
        public void PathMatches_AgreesWithMatches()
        {
            // The equivalence pin: every case Matches decides, PathMatches must decide the same way.
            (string path, string query)[] cases =
            {
                ("Test.PM.A", "Test.PM.A"),      // self
                ("Test.PM.A.Deep", "Test.PM.A"), // descendant vs ancestor query
                ("Test.PM.A", "Test.PM.A.Deep"), // ancestor vs descendant query
                ("Test.PM.A", "Test.PM.B"),      // sibling
            };

            foreach ((string path, string query) in cases)
                Assert.AreEqual(
                    GameplayTag.Get(path).Matches(GameplayTag.Get(query)),
                    GameplayTag.PathMatches(path, query),
                    $"disagreement on ({path}, {query})");
        }

        [Test]
        public void PathMatches_SegmentBoundary_NotAPrefixMatch()
        {
            // "Movement.Sprint" is NOT under "Movement.Sprinting" despite being a string prefix.
            Assert.IsFalse(GameplayTag.PathMatches("Test.PM.Sprinting", "Test.PM.Sprint"));
            Assert.IsTrue(GameplayTag.PathMatches("Test.PM.Sprint.Held", "Test.PM.Sprint"));
        }

        [Test]
        public void PathMatches_NullOrEmpty_False()
        {
            Assert.IsFalse(GameplayTag.PathMatches(null, "Test.PM.A"));
            Assert.IsFalse(GameplayTag.PathMatches("Test.PM.A", null));
            Assert.IsFalse(GameplayTag.PathMatches("", ""));
        }
    }
}

using NUnit.Framework;
using TeekayUtils.Tags;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    /// <summary>
    /// Reference counting (the two-owners-one-tag scenario the class exists for), hierarchy
    /// propagation, and the loud unbalanced-release error. Tags come from the shared static
    /// registry; every test builds its own TagSet, so counts never leak between tests.
    /// </summary>
    public class TagSetTests
    {
        // Distinct subtree per suite so GameplayTagTests' registry entries can't collide.
        static readonly GameplayTag Movement = GameplayTag.Get("TS.Movement");
        static readonly GameplayTag Sprinting = GameplayTag.Get("TS.Movement.Sprinting");
        static readonly GameplayTag Dashing = GameplayTag.Get("TS.Movement.Dashing");
        static readonly GameplayTag Stunned = GameplayTag.Get("TS.Status.Stunned");

        // ---------- basics ----------

        [Test]
        public void Add_ThenHasAndHasExact_True()
        {
            var set = new TagSet();
            set.Add(Sprinting);

            Assert.IsTrue(set.Has(Sprinting));
            Assert.IsTrue(set.HasExact(Sprinting));
        }

        [Test]
        public void EmptySet_HasNothing()
        {
            var set = new TagSet();

            Assert.IsFalse(set.Has(Sprinting));
            Assert.IsFalse(set.HasExact(Sprinting));
            Assert.IsFalse(set.Has(null), "null asks for no tag");
        }

        // ---------- hierarchy ----------

        [Test]
        public void Has_AncestorOfHeldTag_True_ButNotExact()
        {
            var set = new TagSet();
            set.Add(Sprinting);

            Assert.IsTrue(set.Has(Movement), "holding a child answers a broad query");
            Assert.IsFalse(set.HasExact(Movement), "but the ancestor was never granted itself");
        }

        [Test]
        public void Has_DescendantOfHeldTag_False()
        {
            var set = new TagSet();
            set.Add(Movement);

            Assert.IsFalse(set.Has(Sprinting), "a broad grant never answers a narrow query");
        }

        // ---------- reference counting: the reason this class exists ----------

        [Test]
        public void RefCount_AddTwice_RemoveOnce_StillHeld()
        {
            // Two abilities grant the same tag; the first ending must not clear the second's grant.
            var set = new TagSet();
            set.Add(Sprinting);
            set.Add(Sprinting);

            set.Remove(Sprinting);

            Assert.IsTrue(set.Has(Sprinting));
            Assert.IsTrue(set.HasExact(Sprinting));
        }

        [Test]
        public void RefCount_RemoveToZero_Gone()
        {
            var set = new TagSet();
            set.Add(Sprinting);
            set.Add(Sprinting);

            set.Remove(Sprinting);
            set.Remove(Sprinting);

            Assert.IsFalse(set.Has(Sprinting));
            Assert.IsFalse(set.Has(Movement), "the implicit ancestor count drains with it");
        }

        [Test]
        public void RefCount_TwoChildren_RemoveOne_AncestorStillHeld()
        {
            // Sprint and Dash both live under Movement; Dash ending must not clear Has(Movement).
            var set = new TagSet();
            set.Add(Sprinting);
            set.Add(Dashing);

            set.Remove(Dashing);

            Assert.IsTrue(set.Has(Movement));
            Assert.IsFalse(set.Has(Dashing));
        }

        // ---------- error paths ----------

        [Test]
        public void Remove_Unbalanced_LogsErrorAndChangesNothing()
        {
            var set = new TagSet();
            set.Add(Sprinting);
            set.Remove(Sprinting);

            LogAssert.Expect(LogType.Error, $"[TagSet] Remove(\"{Sprinting}\") without a matching Add — " +
                                            "unbalanced release, two owners think they hold the same grant. Ignored.");
            set.Remove(Sprinting);

            // State stays sane: a fresh Add works and counts from zero.
            set.Add(Sprinting);
            Assert.IsTrue(set.Has(Sprinting));
            set.Remove(Sprinting);
            Assert.IsFalse(set.Has(Sprinting));
        }

        [Test]
        public void Add_Null_LogsErrorAndIgnores()
        {
            var set = new TagSet();

            LogAssert.Expect(LogType.Error, "[TagSet] Add(null) — ignored. A null tag is a broken tag list upstream.");
            set.Add(null);
        }

        [Test]
        public void Remove_Null_LogsErrorAndIgnores()
        {
            var set = new TagSet();

            LogAssert.Expect(LogType.Error, "[TagSet] Remove(null) — ignored. A null tag is a broken tag list upstream.");
            set.Remove(null);
        }

        // ---------- list queries ----------

        [Test]
        public void HasAny_MatchesThroughHierarchy()
        {
            var set = new TagSet();
            set.Add(Sprinting);

            Assert.IsTrue(set.HasAny(new[] { Stunned, Movement }));
            Assert.IsFalse(set.HasAny(new[] { Stunned, Dashing }));
        }

        [Test]
        public void HasAny_EmptyOrNull_False()
        {
            var set = new TagSet();
            set.Add(Sprinting);

            Assert.IsFalse(set.HasAny(new GameplayTag[0]));
            Assert.IsFalse(set.HasAny(null));
        }

        [Test]
        public void HasAll_AllHeld_True_OneMissing_False()
        {
            var set = new TagSet();
            set.Add(Sprinting);
            set.Add(Stunned);

            Assert.IsTrue(set.HasAll(new[] { Movement, Stunned }));
            Assert.IsFalse(set.HasAll(new[] { Movement, Dashing }));
        }

        [Test]
        public void HasAll_EmptyOrNull_True()
        {
            // Vacuous truth on purpose: an empty requirement list requires nothing (GAS convention).
            var set = new TagSet();

            Assert.IsTrue(set.HasAll(new GameplayTag[0]));
            Assert.IsTrue(set.HasAll(null));
        }

        // ---------- debug view ----------

        [Test]
        public void Explicit_ListsHeldTagsWithCounts_NoAncestors()
        {
            var set = new TagSet();
            set.Add(Sprinting);
            set.Add(Sprinting);
            set.Add(Stunned);

            Assert.AreEqual(2, set.Explicit.Count);
            Assert.AreEqual(2, set.Explicit[Sprinting]);
            Assert.AreEqual(1, set.Explicit[Stunned]);
            Assert.IsFalse(set.Explicit.ContainsKey(Movement), "ancestors are implicit, not listed");
        }
    }
}

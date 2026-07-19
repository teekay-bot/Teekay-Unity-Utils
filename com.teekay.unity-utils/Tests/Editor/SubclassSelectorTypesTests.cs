using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeekayUtils.EditorTools;
using UnityEngine;

namespace TeekayUtils.Tests
{
    public interface ISubclassSelectorFixture { }

    [Serializable] public class SelectableFixture : ISubclassSelectorFixture { }

    [Serializable] public abstract class AbstractFixture : ISubclassSelectorFixture { }

    public class UnmarkedFixture : ISubclassSelectorFixture { }

    [Serializable]
    public class NoParameterlessCtorFixture : ISubclassSelectorFixture
    {
        public NoParameterlessCtorFixture(int _) { }
    }

    [Serializable] public class UnityObjectFixture : ScriptableObject, ISubclassSelectorFixture { }

    [Serializable] public struct StructFixture : ISubclassSelectorFixture { }

    namespace Left { [Serializable] public class CollidingFixture : ISubclassSelectorFixture { } }

    namespace Right { [Serializable] public class CollidingFixture : ISubclassSelectorFixture { } }

    public class SubclassSelectorTypesTests
    {
        // --- ResolveFieldType -------------------------------------------------------------

        [Test]
        public void ResolveFieldType_RoundTripsUnitysFormat()
        {
            Type expected = typeof(SelectableFixture);
            string typename = $"{expected.Assembly.GetName().Name} {expected.FullName}";

            Assert.AreEqual(expected, SubclassSelectorTypes.ResolveFieldType(typename));
        }

        [Test]
        public void ResolveFieldType_ResolvesInterfaces()
        {
            Type expected = typeof(ISubclassSelectorFixture);
            string typename = $"{expected.Assembly.GetName().Name} {expected.FullName}";

            Assert.AreEqual(expected, SubclassSelectorTypes.ResolveFieldType(typename));
        }

        [Test]
        public void ResolveFieldType_ReturnsNullForMalformedInput()
        {
            // Unity leaves this empty for a field that is not a managed reference.
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType(string.Empty));
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType(null));
            // No space: assembly and type are indistinguishable.
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType("NoSpaceHere"));
            // Leading space would name an empty assembly.
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType(" TypeOnly"));
            // Trailing space would name an empty type.
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType("AssemblyOnly "));
        }

        [Test]
        public void ResolveFieldType_ReturnsNullForUnknownType()
        {
            Assert.IsNull(SubclassSelectorTypes.ResolveFieldType("NoSuchAssembly No.Such.Type"));
        }

        // --- IsSelectable -----------------------------------------------------------------

        [Test]
        public void IsSelectable_AcceptsConcreteSerializableClassWithDefaultCtor()
        {
            Assert.IsTrue(SubclassSelectorTypes.IsSelectable(typeof(SelectableFixture)));
        }

        [Test]
        public void IsSelectable_RejectsWhatUnityCannotStore()
        {
            // Each of these would serialize as null, so offering it would be a trap.
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(AbstractFixture)), "abstract");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(ISubclassSelectorFixture)), "interface");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(UnmarkedFixture)), "no [Serializable]");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(NoParameterlessCtorFixture)), "no default ctor");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(UnityObjectFixture)), "UnityEngine.Object");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(StructFixture)), "value type");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(typeof(List<>)), "open generic");
            Assert.IsFalse(SubclassSelectorTypes.IsSelectable(null), "null");
        }

        // --- GetSelectable ----------------------------------------------------------------

        [Test]
        public void GetSelectable_FindsImplementorsAndExcludesTheRest()
        {
            List<Type> types = SubclassSelectorTypes.GetSelectable(typeof(ISubclassSelectorFixture));

            CollectionAssert.Contains(types, typeof(SelectableFixture));
            CollectionAssert.DoesNotContain(types, typeof(AbstractFixture));
            CollectionAssert.DoesNotContain(types, typeof(UnmarkedFixture));
            CollectionAssert.DoesNotContain(types, typeof(UnityObjectFixture));
        }

        [Test]
        public void GetSelectable_IncludesTheQueriedTypeWhenItIsItselfSelectable()
        {
            // TypeCache reports derived types only, so a field declared as a concrete base would
            // otherwise be unable to select that base.
            CollectionAssert.Contains(SubclassSelectorTypes.GetSelectable(typeof(SelectableFixture)),
                typeof(SelectableFixture));
        }

        [Test]
        public void GetSelectable_IsSortedByName()
        {
            List<Type> types = SubclassSelectorTypes.GetSelectable(typeof(ISubclassSelectorFixture));

            for (int i = 1; i < types.Count; i++)
            {
                Assert.LessOrEqual(string.CompareOrdinal(types[i - 1].Name, types[i].Name), 0,
                    "Menu order must not depend on assembly load order.");
            }
        }

        [Test]
        public void GetSelectable_ReturnsEmptyForNull()
        {
            Assert.IsEmpty(SubclassSelectorTypes.GetSelectable(null));
        }

        // --- BuildMenuLabels --------------------------------------------------------------

        [Test]
        public void BuildMenuLabels_UsesBareNameWhenUnique()
        {
            var types = new List<Type> { typeof(SelectableFixture) };

            Assert.AreEqual(new[] { "SelectableFixture" }, SubclassSelectorTypes.BuildMenuLabels(types));
        }

        [Test]
        public void BuildMenuLabels_QualifiesCollidingNames()
        {
            // GenericMenu merges entries with identical labels, so one of these would vanish.
            var types = new List<Type> { typeof(Left.CollidingFixture), typeof(Right.CollidingFixture) };

            string[] labels = SubclassSelectorTypes.BuildMenuLabels(types);

            Assert.AreEqual($"CollidingFixture ({typeof(Left.CollidingFixture).Namespace})", labels[0]);
            Assert.AreEqual($"CollidingFixture ({typeof(Right.CollidingFixture).Namespace})", labels[1]);
            CollectionAssert.AllItemsAreUnique(labels);
        }

        [Test]
        public void BuildMenuLabels_ReturnsEmptyForNull()
        {
            Assert.IsEmpty(SubclassSelectorTypes.BuildMenuLabels(null));
        }
    }
}

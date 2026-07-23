using NUnit.Framework;
using TeekayUtils.Tags;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    /// <summary>
    /// The catalog's Add contract (validate, dedupe, sort) and the non-throwing path validator the
    /// editor picker leans on. The picker UI itself is editor glue and is not unit-tested.
    /// </summary>
    public class GameplayTagCatalogTests
    {
        GameplayTagCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = ScriptableObject.CreateInstance<GameplayTagCatalog>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        [Test]
        public void Add_NewPaths_RegisteredAndSorted()
        {
            Assert.IsTrue(_catalog.Add("Cat.Second"));
            Assert.IsTrue(_catalog.Add("Cat.First"));

            Assert.IsTrue(_catalog.Contains("Cat.First"));
            Assert.AreEqual("Cat.First", _catalog.Paths[0], "kept ordinal-sorted for diff-friendly assets");
            Assert.AreEqual("Cat.Second", _catalog.Paths[1]);
        }

        [Test]
        public void Add_Duplicate_RefusedLoudly()
        {
            _catalog.Add("Cat.Dup");

            LogAssert.Expect(LogType.Error, "[GameplayTagCatalog] \"Cat.Dup\" is already registered.");
            Assert.IsFalse(_catalog.Add("Cat.Dup"));
        }

        [Test]
        public void Add_MalformedPath_RefusedLoudly()
        {
            LogAssert.Expect(LogType.Error, "[GameplayTagCatalog] Tag path \"A..B\" has an empty segment — " +
                                            "check for leading/trailing/double dots.");
            Assert.IsFalse(_catalog.Add("A..B"));
            Assert.IsFalse(_catalog.Contains("A..B"));
        }

        [Test]
        public void IsValidPath_MirrorsGetRules()
        {
            Assert.IsTrue(GameplayTag.IsValidPath("A.B.C", out _));
            Assert.IsFalse(GameplayTag.IsValidPath(null, out string e1));
            Assert.IsFalse(GameplayTag.IsValidPath("", out _));
            Assert.IsFalse(GameplayTag.IsValidPath(".A", out _));
            Assert.IsFalse(GameplayTag.IsValidPath("A.", out _));
            Assert.IsFalse(GameplayTag.IsValidPath("A..B", out string e2));
            Assert.IsNotNull(e1);
            Assert.IsNotNull(e2, "the error string is what the picker shows the user");
        }
    }
}

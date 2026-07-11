using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    public class TestSingleton : Singleton<TestSingleton>
    {
        public static void ResetStatic()
        {
            instance = null;
            isQuitting = false;
        }
    }

    public class TestPersistentSingleton : PersistentSingleton<TestPersistentSingleton>
    {
        public static void ResetStatic()
        {
            instance = null;
            isQuitting = false;
        }
    }

    public class SingletonTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var s in Object.FindObjectsByType<TestSingleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(s.gameObject);
            foreach (var s in Object.FindObjectsByType<TestPersistentSingleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(s.gameObject);
            yield return null;
            TestSingleton.ResetStatic();
            TestPersistentSingleton.ResetStatic();
        }

        [Test]
        public void TryGetInstance_NoInstance_ReturnsNullWithoutCreating()
        {
            Assert.That(TestSingleton.HasInstance, Is.False);
            Assert.That(TestSingleton.TryGetInstance(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<TestSingleton>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator Instance_NoneInScene_AutoCreatesGameObject()
        {
            var s = TestSingleton.Instance;
            yield return null;

            Assert.That(s, Is.Not.Null);
            Assert.That(TestSingleton.HasInstance, Is.True);
            Assert.That(s.gameObject.name, Does.Contain("Auto-Generated"));
        }

        [UnityTest]
        public IEnumerator Instance_InactiveInstanceInScene_IsFoundInsteadOfDuplicated()
        {
            var go = new GameObject("Inactive");
            go.SetActive(false);
            var s = go.AddComponent<TestSingleton>();

            Assert.That(TestSingleton.Instance, Is.EqualTo(s));
            Assert.That(Object.FindObjectsByType<TestSingleton>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Awake_ManuallyAddedComponent_BecomesInstance()
        {
            var go = new GameObject("Manual");
            var s = go.AddComponent<TestSingleton>();
            yield return null;

            Assert.That(TestSingleton.Instance, Is.EqualTo(s));
        }

        [UnityTest]
        public IEnumerator Duplicate_SecondInstance_DestroysItselfAndFirstWins()
        {
            var first = new GameObject("First").AddComponent<TestSingleton>();
            yield return null;

            LogAssert.Expect(LogType.Warning, "[Singleton] Duplicate TestSingleton on 'Second' destroyed — keeping 'First'.");
            var second = new GameObject("Second").AddComponent<TestSingleton>();
            yield return null;

            Assert.That(second == null, Is.True, "duplicate should destroy itself");
            Assert.That(TestSingleton.Instance, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator OnDestroy_ActiveInstance_ClearsStaticReference()
        {
            var s = new GameObject("Doomed").AddComponent<TestSingleton>();
            yield return null;
            Assert.That(TestSingleton.HasInstance, Is.True);

            Object.Destroy(s.gameObject);
            yield return null;

            Assert.That(TestSingleton.HasInstance, Is.False);
        }

        [UnityTest]
        public IEnumerator Persistent_SecondInstance_DestroysItself()
        {
            var first = new GameObject("First").AddComponent<TestPersistentSingleton>();
            yield return null;

            LogAssert.Expect(LogType.Warning, "[Singleton] Duplicate TestPersistentSingleton on 'Second' destroyed — keeping 'First'.");
            var second = new GameObject("Second").AddComponent<TestPersistentSingleton>();
            yield return null;

            Assert.That(second == null, Is.True, "duplicate should destroy itself");
            Assert.That(TestPersistentSingleton.Instance, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator Persistent_AutoUnparentOnAwake_DetachesFromParent()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            var s = child.AddComponent<TestPersistentSingleton>();
            yield return null;

            Assert.That(s.transform.parent, Is.Null);
            Object.Destroy(parent);
        }

        [UnityTest]
        public IEnumerator Persistent_FirstInstance_IsMarkedDontDestroyOnLoad()
        {
            var s = new GameObject("Persistent").AddComponent<TestPersistentSingleton>();
            yield return null;

            // Objects under DontDestroyOnLoad live in a dedicated scene.
            Assert.That(s.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
        }
    }
}

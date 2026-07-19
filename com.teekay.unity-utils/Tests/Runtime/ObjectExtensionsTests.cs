using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    /// PlayMode tests for extensions that operate on GameObjects and components.
    public class ObjectExtensionsTests
    {
        readonly List<GameObject> spawned = new();

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in spawned.Where(go => go != null))
                Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        [Test]
        public void GetOrAdd_AddsThenReuses()
        {
            var go = Spawn("GetOrAdd");
            var added = go.GetOrAdd<BoxCollider>();
            var reused = go.GetOrAdd<BoxCollider>();

            Assert.That(added, Is.Not.Null);
            Assert.That(reused, Is.EqualTo(added));
            Assert.That(go.GetComponents<BoxCollider>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ComponentGetOrAdd_WorksFromComponentReference()
        {
            var go = Spawn("ComponentGetOrAdd");
            var collider = go.AddComponent<BoxCollider>();
            var rb = collider.GetOrAdd<Rigidbody>();

            Assert.That(rb, Is.Not.Null);
            Assert.That(rb.gameObject, Is.EqualTo(go));
        }

        [UnityTest]
        public IEnumerator OrNull_DestroyedObject_ReturnsRealNull()
        {
            var go = Spawn("Doomed");
            Object.Destroy(go);
            yield return null;

            Assert.That(go.OrNull(), Is.Null);
        }

        [UnityTest]
        public IEnumerator DestroyChildren_RemovesAllChildren()
        {
            var parent = Spawn("Parent");
            for (int i = 0; i < 3; i++)
                new GameObject($"Child{i}").transform.SetParent(parent.transform);

            parent.DestroyChildren();
            yield return null;

            Assert.That(parent.transform.childCount, Is.Zero);
        }

        [Test]
        public void EnableDisableChildren_TogglesActiveState()
        {
            var parent = Spawn("Parent");
            for (int i = 0; i < 3; i++)
                new GameObject($"Child{i}").transform.SetParent(parent.transform);

            parent.DisableChildren();
            Assert.That(parent.transform.Children().All(c => !c.gameObject.activeSelf), Is.True);

            parent.EnableChildren();
            Assert.That(parent.transform.Children().All(c => c.gameObject.activeSelf), Is.True);
        }

        [Test]
        public void TransformReset_ResetsLocalValues()
        {
            var parent = Spawn("Parent");
            parent.transform.position = new Vector3(5, 5, 5);

            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = new Vector3(1, 2, 3);
            child.transform.localRotation = Quaternion.Euler(0, 45, 0);
            child.transform.localScale = new Vector3(2, 2, 2);

            child.transform.Reset();

            Assert.That(child.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(child.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(child.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void TransformInRangeOf_RespectsDistanceAndAngle()
        {
            var source = Spawn("Source");
            var target = Spawn("Target");
            target.transform.position = new Vector3(0, 0, 5); // straight ahead

            Assert.That(source.transform.InRangeOf(target.transform, 10f, 90f), Is.True);

            target.transform.position = new Vector3(0, 0, -5); // behind
            Assert.That(source.transform.InRangeOf(target.transform, 10f, 90f), Is.False);

            target.transform.position = new Vector3(0, 0, 50); // too far
            Assert.That(source.transform.InRangeOf(target.transform, 10f, 360f), Is.False);
        }

        [Test]
        public void FullPath_And_ParentPath_BuildHierarchyPaths()
        {
            var root = Spawn("Root");
            var mid = new GameObject("Mid");
            mid.transform.SetParent(root.transform);
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(mid.transform);

            Assert.That(leaf.FullPath(), Is.EqualTo("/Root/Mid/Leaf"));
            Assert.That(leaf.ParentPath(), Is.EqualTo("/Root/Mid"));
            Assert.That(root.ParentPath(), Is.EqualTo("/"));
        }

        [Test]
        public void SetLayersRecursively_And_IsInLayerMask()
        {
            var parent = Spawn("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            parent.SetLayersRecursively(5);

            Assert.That(parent.layer, Is.EqualTo(5));
            Assert.That(child.layer, Is.EqualTo(5));

            LayerMask mask = 1 << 5;
            Assert.That(parent.IsInLayerMask(mask), Is.True);
            Assert.That(parent.IsInLayerMask((LayerMask)(1 << 6)), Is.False);
        }

        [Test]
        public void RigidbodyChangeDirection_PreservesSpeed()
        {
            var go = Spawn("RB");
            var rb = go.AddComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(3, 4, 0); // speed 5

            rb.ChangeDirection(Vector3.forward);

            Assert.That(rb.linearVelocity.magnitude, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(rb.linearVelocity.normalized, Is.EqualTo(Vector3.forward));

            rb.Stop();
            Assert.That(rb.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(rb.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Rigidbody2DChangeDirection_PreservesSpeed()
        {
            var go = Spawn("RB2D");
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(3, 4); // speed 5

            rb.ChangeDirection(Vector2.up);

            Assert.That(rb.linearVelocity.magnitude, Is.EqualTo(5f).Within(1e-4f));

            rb.Stop();
            Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(rb.angularVelocity, Is.Zero);
        }

        [Test]
        public void CanvasGroup_ShowHide_SetAllThreeProperties()
        {
            var go = Spawn("CanvasGroup");
            var group = go.AddComponent<CanvasGroup>();

            group.Hide();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.False);

            group.Show();
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.True);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    /// EditMode tests for ColliderComponentCache — hit/miss caching and stale-entry re-resolve.
    public class ColliderComponentCacheTests
    {
        class CacheTarget : MonoBehaviour { }

        GameObject _root;
        BoxCollider _collider;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("root");
            var child = new GameObject("child");
            child.transform.SetParent(_root.transform);
            _collider = child.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Get_NullCollider_ReturnsNull()
        {
            var cache = new ColliderComponentCache<CacheTarget>();
            Assert.That(cache.Get(null), Is.Null);
        }

        [Test]
        public void Get_ComponentOnParent_IsResolved()
        {
            var target = _root.AddComponent<CacheTarget>();
            var cache = new ColliderComponentCache<CacheTarget>();

            Assert.That(cache.Get(_collider), Is.SameAs(target));
        }

        [Test]
        public void Get_MissIsCached_LaterAddedComponentIsNotSeen()
        {
            var cache = new ColliderComponentCache<CacheTarget>();
            Assert.That(cache.Get(_collider), Is.Null);

            _root.AddComponent<CacheTarget>();

            // Still null — the miss was cached. This is the documented trade-off.
            Assert.That(cache.Get(_collider), Is.Null);
        }

        [Test]
        public void Clear_ForgetsCachedMisses()
        {
            var cache = new ColliderComponentCache<CacheTarget>();
            Assert.That(cache.Get(_collider), Is.Null);

            var target = _root.AddComponent<CacheTarget>();
            cache.Clear();

            Assert.That(cache.Get(_collider), Is.SameAs(target));
        }

        [Test]
        public void Get_DestroyedComponent_IsReresolved()
        {
            var stale = _root.AddComponent<CacheTarget>();
            var cache = new ColliderComponentCache<CacheTarget>();
            Assert.That(cache.Get(_collider), Is.SameAs(stale));

            Object.DestroyImmediate(stale);
            var fresh = _root.AddComponent<CacheTarget>();

            Assert.That(cache.Get(_collider), Is.SameAs(fresh));
        }

        [Test]
        public void Get_WorksWithInterfaceTypeParameter()
        {
            var target = _root.AddComponent<MarkedCacheTarget>();
            var cache = new ColliderComponentCache<ICacheMarker>();

            Assert.That(cache.Get(_collider), Is.SameAs(target));
        }

        interface ICacheMarker { }

        class MarkedCacheTarget : MonoBehaviour, ICacheMarker { }
    }
}

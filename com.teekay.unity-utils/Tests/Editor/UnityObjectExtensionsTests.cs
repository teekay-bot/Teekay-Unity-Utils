using NUnit.Framework;
using UnityEngine;

namespace TeekayUtils.Tests
{
    /// EditMode tests for IsUnityNull — liveness checks through interface/object references.
    public class UnityObjectExtensionsTests
    {
        interface ITestMarker { }

        class MarkerBehaviour : MonoBehaviour, ITestMarker { }

        class PlainMarker : ITestMarker { }

        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ClrNull_IsUnityNull()
        {
            Assert.That(((object)null).IsUnityNull(), Is.True);
        }

        [Test]
        public void PlainCSharpObject_IsAlive()
        {
            ITestMarker marker = new PlainMarker();
            Assert.That(marker.IsUnityNull(), Is.False);
        }

        [Test]
        public void AliveComponent_ThroughInterface_IsAlive()
        {
            _go = new GameObject("alive");
            ITestMarker marker = _go.AddComponent<MarkerBehaviour>();

            Assert.That(marker.IsUnityNull(), Is.False);
        }

        [Test]
        public void DestroyedComponent_ThroughInterface_IsUnityNull()
        {
            _go = new GameObject("doomed");
            ITestMarker marker = _go.AddComponent<MarkerBehaviour>();

            Object.DestroyImmediate(_go);
            _go = null;

            // The CLR reference survives destruction — only the Unity-aware check sees it.
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsUnityNull(), Is.True);
        }
    }
}

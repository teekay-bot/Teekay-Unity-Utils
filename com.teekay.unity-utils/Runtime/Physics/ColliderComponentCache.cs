using System.Collections.Generic;
using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Caches <c>GetComponentInParent&lt;T&gt;()</c> results keyed by Collider so physics-scan
    /// systems (interaction targeting, AI perception, damage queries) don't re-walk the
    /// hierarchy for the same colliders every frame. Misses (colliders with no T above them)
    /// are cached too — scenery is the common case in overlap queries. Entries whose component
    /// was destroyed are re-resolved transparently.
    /// <para>
    /// The owner is responsible for calling <see cref="Clear"/> on scene unload (destroyed
    /// colliders otherwise linger as dead keys until the entry cap evicts everything).
    /// </para>
    /// </summary>
    public sealed class ColliderComponentCache<T> where T : class
    {
        const int DefaultMaxEntries = 256;

        readonly Dictionary<Collider, T> _cache;
        readonly int _maxEntries;

        public ColliderComponentCache(int capacity = 32, int maxEntries = DefaultMaxEntries)
        {
            _cache = new Dictionary<Collider, T>(capacity);
            _maxEntries = maxEntries;
        }

        public T Get(Collider collider)
        {
            if (collider == null) return null;

            if (_cache.TryGetValue(collider, out T cached))
            {
                if (cached == null) return null;          // cached miss — collider has no T
                if (!cached.IsUnityNull()) return cached; // live hit
                _cache.Remove(collider);                  // component destroyed — re-resolve below
            }

            T result = collider.GetComponentInParent<T>();

            // Blunt eviction beats per-entry bookkeeping here: a full wipe costs one re-resolve
            // per live collider and the cap is rarely hit outside collider-churn-heavy scenes.
            if (_cache.Count >= _maxEntries) _cache.Clear();

            _cache[collider] = result;
            return result;
        }

        public void Clear() => _cache.Clear();
    }
}

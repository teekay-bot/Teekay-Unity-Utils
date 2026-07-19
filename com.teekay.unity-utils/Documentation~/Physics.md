# Physics helpers

## `ColliderComponentCache<T>`

Namespace `TeekayUtils`. A dictionary-backed cache for `GetComponentInParent<T>()` lookups keyed by
`Collider`.

Physics scans hand you colliders, but gameplay wants the owning component — so the same
`GetComponentInParent` walk runs for the same collider every frame. That walk climbs the hierarchy and is
not free when it happens for dozens of overlap results per frame.

```csharp
using TeekayUtils;

readonly ColliderComponentCache<IDamageable> targets = new();

void ApplyExplosion(Vector3 center, float radius)
{
    int count = Physics.OverlapSphereNonAlloc(center, radius, buffer);
    for (int i = 0; i < count; i++)
    {
        IDamageable target = targets.Get(buffer[i]);   // cached after the first hit
        target?.TakeDamage(50);
    }
}

void OnDisable() => targets.Clear();
```

---

## API

| Member | Notes |
|---|---|
| `ColliderComponentCache(int capacity = 32, int maxEntries = 256)` | `capacity` presizes the dictionary; `maxEntries` bounds it. |
| `T Get(Collider collider)` | Cached `GetComponentInParent<T>()`. `null` collider returns `null` — no exception. |
| `void Clear()` | Drops every entry. |

`T` is constrained `where T : class`, so it works with interfaces — the common case for a damage or
interaction contract.

---

## Behaviour worth knowing

**Misses are cached too.** A collider with no `T` in its parents is remembered as "no result", so
scenery you scan every frame costs one failed walk, not one per frame. The flip side: **adding a `T`
component to an already-scanned object will not be seen** until the cache is cleared.

**Destroyed components are re-resolved transparently.** A cached component that Unity has destroyed is
detected and looked up again rather than handed back as a fake-null.

**Eviction is a blunt wipe, not LRU.** Past `maxEntries` the whole cache is cleared. Keeping true LRU
order would cost more per lookup than the walk being avoided, and a debug-frequency cache does not earn
that complexity. Size `maxEntries` above your realistic working set.

**Not thread-safe.** It is a plain `Dictionary`. Use it from the main thread, like the physics API that
feeds it.

---

## Lifetime is yours

The cache holds `Collider` keys, so it pins references to objects from scenes that may have unloaded.
**Call `Clear()` when the owner goes away** — `OnDisable`, `OnDestroy`, or a scene-unload callback.
Nothing does it for you.

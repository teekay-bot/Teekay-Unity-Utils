# Teekay Unity Utils

[![Unity 6000.3+](https://img.shields.io/badge/Unity-6000.3%20LTS%2B-000?logo=unity)](https://unity.com/releases/editor/whats-new/6000.3)
[![UPM](https://img.shields.io/badge/UPM-git%20URL-2296F3)](../README.md#installation)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE.md)

Curated Unity utilities: extension methods, singletons, an event bus, debug drawing, and an in-game
developer console. Small, tested, zero prefabs — everything is plain code.

**Requires Unity 6000.3 (Unity 6.3 LTS) or newer.**
**Install:** see [Installation](../README.md#installation) in the repository README.

---

## Modules

| Module | What it is | Docs |
|---|---|---|
| **Extensions** | 84 methods over `Vector2/3`, `Transform`, `GameObject`, `Color`, `LayerMask`, collections, numbers, `Rigidbody(2D)`, `CanvasGroup` and strings. Includes `IsUnityNull()` for destroyed-object checks through interface references. | [Extensions](Documentation~/Extensions.md) |
| **Physics** | `ColliderComponentCache<T>` — cached `GetComponentInParent` lookups keyed by `Collider`, for overlap/raycast hot paths. | [Physics](Documentation~/Physics.md) |
| **Singletons** | `Singleton<T>` and `PersistentSingleton<T>`. First-Awake-wins, duplicates self-destroy, no auto-create in Edit mode or during quit. | [Singleton](Documentation~/Singleton.md) |
| **EventBus** | Type-keyed pub/sub for gameplay intents. Struct events, zero-alloc publish, one throwing listener never stops the rest. | [EventBus](Documentation~/EventBus.md) |
| **DebugDraw** | One drawing API, two backends: Gizmos (Scene view) and GL lines (Game view + builds, Built-in **and** URP/HDRP). Spheres, domes, capsules, perception cones, circles, cubes, rays, arrows. | [DebugDraw](Documentation~/DebugDraw.md) |
| **DevConsole** | Drop-in developer console (F12): commands, typed CVars, autocomplete, history, key bindings, Unity log capture, virtualized log with duplicate collapsing and filtering. Off in release builds by default. | [DevConsole](Documentation~/DevConsole.md) |
| **Attributes** | `[KeyPicker]` — click-to-listen key capture instead of a 100-entry enum dropdown. `[SubclassSelector]` — type dropdown for `[SerializeReference]` fields, which Unity otherwise leaves assignable only from code. | [Attributes](Documentation~/Attributes.md) |

---

## Quick start

**Extensions** — the everyday ones.

```csharp
using TeekayUtils;

transform.position = transform.position.With(y: 0f);   // change one component
Vector3 spot = origin.RandomPointInAnnulus(2f, 5f);     // spawn ring, uniform by area
if (guard.InRangeOf(player, 10f, maxAngle: 90f)) Alert();
gameObject.GetOrAdd<Rigidbody>().ChangeDirection(Vector3.forward);
```

**Singleton** — one long-lived service.

```csharp
public class AudioManager : PersistentSingleton<AudioManager> { }

AudioManager.Instance.PlayShot(clip);
```

**EventBus** — publisher and subscribers never reference each other.

```csharp
using TeekayUtils.Events;

public struct ScoreChanged : IEvent { public int Total; }

void OnEnable()  => EventBus.Subscribe<ScoreChanged>(e => label.text = $"{e.Total}");
void OnDisable() => EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);

EventBus.Publish(new ScoreChanged { Total = score });
```

**DebugDraw** — the same call renders in the Scene view and in a build.

```csharp
readonly GizmosDebugDrawer gizmos = new();

void OnDrawGizmos()
{
    gizmos.WireSphere(transform.position, hearingRange, Color.cyan);
    gizmos.ViewCone(eye.position, eye.forward, viewAngle, viewRange, Color.yellow);
}
```

**DevConsole** — press F12 in Play mode.

```csharp
using TeekayUtils.DevConsole;

DevConsole.RegisterCommand("give.gold", "Give gold. Usage: give.gold [amount]",
    args => Inventory.AddGold(args.AsInt(0, 100)));

DevConsole.RegisterFloat("player.speed", "Movement speed.", () => speed, v => speed = v);
```

---

## Notes

- Package dependencies (`com.unity.inputsystem`, `com.unity.ugui`) install automatically.
- The DevConsole window needs **TMP Essential Resources** — import once per project via
  `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`. Without them the window will not build;
  commands, CVars and log capture keep working.
- Every module has demo scenes in the development project — open `DemoHub` and press Play.
- Full history of changes and fixes: [CHANGELOG](CHANGELOG.md).

## License

[MIT](LICENSE.md) — portions adapted from [adammyhre/Unity-Utils](https://github.com/adammyhre/Unity-Utils)
(see [Third Party Notices](Third%20Party%20Notices.md)).

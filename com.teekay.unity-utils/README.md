# Teekay Unity Utils

Curated Unity utilities: extension methods, singletons, an event bus, debug drawing, and an in-game developer console. Small, tested, zero prefabs — everything is plain code.

**Requires Unity 6000.3 (Unity 6.3 LTS) or newer.**

## Installation

**Package Manager UI** — `Window ▸ Package Manager ▸ + ▸ Install package from git URL…` and paste:

```
https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils#v2.1.0
```

**Or edit `Packages/manifest.json`** directly:

```json
"com.teekay.unity-utils": "https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils#v2.1.0"
```

Drop the `#v2.1.0` suffix to track the latest. The `.git` extension and `?path=` are both required — without `.git` the Package Manager treats the URL as a package name.

## Features

- **Extensions** — ~70 quality-of-life methods for `Vector2/3`, `Transform`, `GameObject`, `Color`, `LayerMask`, collections, numbers, `Rigidbody(2D)`, `CanvasGroup`, and TMP rich-text strings. Includes `IsUnityNull()` for destroyed-object checks through interface references.
- **Physics helpers** — `ColliderComponentCache<T>`: cached `GetComponentInParent` lookups keyed by Collider for overlap/raycast hot paths.
- **Singletons** — `Singleton<T>` (scene-local) and `PersistentSingleton<T>` (survives scene loads). First-Awake-wins, duplicates self-destroy, safe on application quit.
- **EventBus** — type-keyed publish/subscribe for gameplay intents. Struct events, zero-alloc publish, one throwing listener never stops the rest.
- **DebugDraw** — one drawing API, two backends: Gizmos (Scene view) and GL lines (Game view + builds, Built-in pipeline and URP/HDRP alike). Spheres, latitude bands/domes, capsules, perception cones, circles, cubes, rays and arrows — all tessellated once and shared, so both backends draw identically.
- **DevConsole** — drop-in developer console (F12): commands, typed CVars with play-session-only edits, autocomplete, history, key bindings, Unity log capture. Virtualized log with duplicate collapsing, click-to-copy, search + per-category filter chips, and a themeable chrome palette. Disabled in release builds by default.

## Quick start

```csharp
using TeekayUtils;
using TeekayUtils.Events;
using TeekayUtils.DevConsole;

// Extensions
transform.position = transform.position.With(y: 0);
var target = enemies.Random();
gameObject.GetOrAdd<Rigidbody>().ChangeDirection(Vector3.forward);

// Singleton
public class GameManager : PersistentSingleton<GameManager> { }
GameManager.Instance.AddScore(10);

// EventBus — publisher and subscribers never reference each other
public struct ScoreChanged : IEvent { public int Delta; }
EventBus.Subscribe<ScoreChanged>(OnScore);   // OnEnable
EventBus.Unsubscribe<ScoreChanged>(OnScore); // OnDisable
EventBus.Publish(new ScoreChanged { Delta = 10 });

// DevConsole — press F12 in Play mode
DevConsole.RegisterCommand("give.gold", "Give gold. Usage: give.gold [amount]",
    args => Inventory.AddGold(args.AsInt(0, 100)));
DevConsole.RegisterFloat("player.speed", "Movement speed.",
    () => speed, v => speed = v);
```

## Notes

- Dependencies (`com.unity.inputsystem`, `com.unity.ugui`) install automatically.
- The DevConsole window uses TextMeshPro — import **TMP Essential Resources** once per project (`Window ▸ TextMeshPro ▸ Import TMP Essential Resources`).
- Full history of changes and fixes: [CHANGELOG](CHANGELOG.md).

## License

[MIT](LICENSE.md) — portions adapted from [adammyhre/Unity-Utils](https://github.com/adammyhre/Unity-Utils) (see [Third Party Notices](Third%20Party%20Notices.md)).

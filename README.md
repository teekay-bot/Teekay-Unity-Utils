# Teekay Unity Utils

Personal Unity utilities package: runtime extension methods, singletons, debug drawing, and editor tooling. Curated — only things actually used in production make it in.

**Requires Unity 6000.3 (Unity 6.3 LTS) or newer.**

## Installation

Add to your project's `Packages/manifest.json`:

```json
"com.teekay.unity-utils": "https://github.com/teekay-bot/Teekay-Unity-Utils.git"
```

> Private repo: installation works on machines where git is already authenticated to GitHub (Unity shells out to system git).

## What's inside

| Module | Location | Highlights |
|---|---|---|
| **Extensions** | `Runtime/Extensions/` | `Vector2/3.With/Add/DirectionTo/InRangeOf/RandomPointInAnnulus/Quantize`, `Vector2.Rotate`, `Transform.Children/Reset/ForEveryChild`, `GameObject.GetOrAdd/OrNull/PathFull/IsInLayerMask`, `Color.SetAlpha/ToHex`, TMP rich-text `string.Rich*`, `IList.Shuffle/Swap/Random`, `float.Remap/AtLeast/AtMost`, `Rigidbody(2D).ChangeDirection/Stop`, `CanvasGroup.Show/Hide` |
| **Singleton** | `Runtime/Singleton/` | `Singleton<T>` (scene-local) and `PersistentSingleton<T>` (DontDestroyOnLoad). First-Awake-wins, duplicates self-destroy with a warning, quit-safe, no auto-create in Edit mode |
| **DebugDraw** | `Runtime/DebugDraw/` | Backend-agnostic `IDebugDrawer`: `GizmosDebugDrawer` (Scene view) and zero-alloc `GLDebugDrawer` (Game view + builds) |
| **DevConsole** | `Runtime/DevConsole/` | In-game developer console: commands, typed CVars (play-session-only edits via snapshot/restore), autocomplete, history, key bindings, log categories + Unity log capture. Code-built uGUI window (F12), config via ScriptableObject + `Tools ▸ DevConsole ▸ Config` editor window. Gated by `ConsoleAccess` (default: dev builds only) |
| **Editor** | `Editor/` | `PingAndSelect`, `EditorFileUtils.ConfirmOverwrite/BrowseForFolder`, `[KeyPicker]` drawer |

Package dependencies: `com.unity.inputsystem`, `com.unity.ugui`. The DevConsole UI uses TextMeshPro — the consuming project must have **TMP Essential Resources** imported (`Window ▸ TextMeshPro ▸ Import TMP Essential Resources`); without them the console still registers commands/CVars but cannot open its window.

## Quick examples

```csharp
using TeekayUtils;

// Extensions
transform.position = transform.position.With(y: 0);
var target = enemies.Random();
if (transform.InRangeOf(target.transform, maxDistance: 10f, maxAngle: 90f)) { ... }
gameObject.GetOrAdd<Rigidbody>().ChangeDirection(Vector3.forward);

// Singleton
public class GameManager : PersistentSingleton<GameManager> { }
GameManager.Instance.DoThing();          // auto-creates if missing (Play mode)
GameManager.TryGetInstance()?.DoThing(); // never creates

// DebugDraw (inside OnDrawGizmos)
drawer.WireSphere(transform.position, aggroRadius, Color.red);
```

## Development

Git-URL packages are immutable in consuming projects, so all development happens in the embedded host project:

1. Open `DevProject~/` in Unity 6000.3.19f1. It references this package via `file:../..`, making it editable under `Packages/Teekay Unity Utils`.
2. Run tests via **Window ▸ General ▸ Test Runner** (EditMode + PlayMode). The package is listed in the host's `"testables"`.
3. Open the `DemoHub` scene and enter Play mode — a navigator bar switches between per-feature demo scenes (`SingletonDemo`, `ExtensionsDemo`, `DebugDrawDemo`, `DevConsoleDemo`).

Conventions:

- `Runtime/` → assembly `TeekayUtils`, namespace `TeekayUtils`. `Editor/` → `TeekayUtils.Editor`. Tests are stripped from builds via `UNITY_INCLUDE_TESTS`.
- Every asset needs its committed `.meta` (files must end with a trailing newline or Unity silently ignores the asset).
- `DevProject~/` is invisible to consumers — Unity skips `~`-suffixed folders in installed packages.

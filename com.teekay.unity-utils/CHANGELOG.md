# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-14

### Added

- `Runtime/Extensions/UnityObjectExtensions.cs` — `IsUnityNull(this object)`: liveness check for references that reach a UnityEngine.Object through a non-Object static type (interfaces, `object`), where Unity's overloaded null check can't kick in. Complements `OrNull`, whose `where T : Object` constraint rejects interface types. Promoted from Teekay-Unity-Base's FPP interaction system. 4 EditMode tests.
- `Runtime/Physics/ColliderComponentCache.cs` — `ColliderComponentCache<T>`: Dictionary-backed cache for `GetComponentInParent<T>()` keyed by Collider, for physics-scan hot paths (interaction targeting, AI perception). Caches misses too, re-resolves destroyed components transparently, blunt-wipe eviction past a configurable cap; owner clears on scene unload. Promoted from Teekay-Unity-Base. 6 EditMode tests.

## [1.0.0] - 2026-07-13

### Added

- `Runtime/Events/` — `EventBus` + `IEvent`: type-keyed pub/sub for gameplay intents (struct-constrained for zero-alloc publish, snapshot dispatch so handlers may subscribe/unsubscribe mid-publish, per-handler exception isolation, domain-reload-safe reset, `AnyPublished` tooling hook). Brought over from Teekay-Core-Unity with 8 EditMode tests and an `EventBusDemo` scene.

### Fixed

- `DevConsole`: destroying the console mid-session permanently bricked the static API — `OnDestroy` set the internal shutdown flag, and since `Initialize()` itself was gated by that flag, nothing could ever revive the console. An explicit `Initialize()` now clears the flag (unless the application is actually quitting); scene-teardown callbacks still cannot resurrect the singleton by accident.

### Changed

- **BREAKING — repository restructured; install URL changed.** The package now lives in the `com.teekay.unity-utils/` subfolder; consumers install with `https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils`. Reason: the dev host project was `DevProject~`, and a Unity project whose own path contains a `~`-suffixed segment silently breaks MonoScript class binding for registry packages under its `Library/PackageCache` (TMP settings/fonts and `.inputactions` assets import as empty artifacts). The dev project is now tilde-free (`DevProject/`) beside the package. Tags ≤ v0.4.0 keep the old root layout and old URL.

## [0.4.0] - 2026-07-12

### Added

- `Runtime/DevConsole/` + `Editor/DevConsole/` — in-game developer console brought over from Teekay-Core-Unity (fresh GUIDs, namespace `TeekayUtils.DevConsole`): commands, typed CVars with snapshot/restore, autocomplete, history, key bindings, log categories with Unity log capture, code-built uGUI window, config ScriptableObject + editor window, bridge/category code generators. Now uses the package's `PersistentSingleton<T>` (its `OnDestroy` adapted to override the base). 15 EditMode + 4 PlayMode tests.
- `Runtime/Attributes/KeyPickerAttribute` + `Editor/Attributes/KeyPickerDrawer` — click-to-listen `Key` picker used by `DevConsoleConfig`.
- `LICENSE.md` (MIT) and `Third Party Notices.md` (Unity-Utils attribution).
- Package dependencies: `com.unity.inputsystem` 1.19.0 and `com.unity.ugui` 2.0.0 (required by DevConsole).

### Changed

- DevProject demos split from the single Sample scene into per-feature scenes (`DemoHub`, `SingletonDemo`, `ExtensionsDemo`, `DebugDrawDemo`, `DevConsoleDemo`) with a central `DemoBootstrap` (reacts to `sceneLoaded`, not just play start) and a persistent `DemoNavigator` scene-switch bar.

## [0.3.0] - 2026-07-11

### Added

- `Editor/Extensions/EditorExtensions.cs` — `PingAndSelect` extension for `Object`, adapted from [adammyhre/Unity-Utils](https://github.com/adammyhre/Unity-Utils) (MIT), with EditMode tests.
- `Editor/Utils/EditorFileUtils.cs` — `ConfirmOverwrite` / `BrowseForFolder` file-dialog helpers, adapted from the same source but converted from string extensions to plain static methods.
- `Runtime/Singleton/` — `Singleton<T>` and `PersistentSingleton<T>` MonoBehaviour base classes (from the same source; `RegulatorSingleton` intentionally skipped), hardened over the original: no ghost objects on application quit (`isQuitting` guard), first-Awake-wins with duplicate self-destroy + warning for both classes, no auto-create in Edit mode, inactive instances found via `FindObjectsInactive.Include`, CRTP constraint (`where T : Singleton<T>`), static reference cleared in `OnDestroy`. `PersistentSingleton<T>` inherits `Singleton<T>` and only adds auto-unparent + `DontDestroyOnLoad`. With PlayMode tests.

- `Runtime/Extensions/` — 13 extension classes curated from the same source: Vector2/Vector3, Transform, GameObject, Component, LayerMask, Color, String, Collection (List+Enumerable merged), Number, Rigidbody, Rigidbody2D, CanvasGroup. Fixes over upstream: `IsOdd` correct for negatives, `Path`/`PathFull` no longer duplicate the leaf name and work on inactive objects, `Transform.Reset` uses localPosition consistently, `IsNullOrEmpty` allocation-free, `ToVector3XZ` renamed to reveal the y→z mapping. Additions: `DirectionTo`/`SqrDistanceTo`, `Vector2.Rotate`, `GameObject.IsInLayerMask`, `Component.GetOrAdd`, `Rigidbody2D` variants, `CanvasGroup.Show/Hide/SetVisible`. Async/coroutine extensions intentionally skipped (UniTask covers them); UI Toolkit, Reflection and conversion extensions skipped as unused.

- `Runtime/DebugDraw/` — backend-agnostic debug drawing brought over from Teekay-Core-Unity (fresh GUIDs): `IDebugDrawer` + `GizmosDebugDrawer` (Scene view) + `GLDebugDrawer` (Game view/builds, zero-alloc) + testable `DebugDrawGeometry`, now with EditMode tests.
- `Vector3Extensions`: `ProjectOntoLine` and `RotateOntoPlane`, distilled from upstream `VectorMath` — its other five methods duplicate Unity built-ins (`Vector3.SignedAngle`, `Project`, `ProjectOnPlane`, `MoveTowards`) and were not ported.

### Removed

- Smoke tests (`EditorSmokeTests`, `RuntimeSmokeTests`) — the pipeline is now proven by real tests. `TeekayUtils.Tests` (PlayMode) is currently empty, kept for future runtime code.

## [0.2.0] - 2026-07-10

### Added

- `Tests/Editor` (`TeekayUtils.Tests.Editor`) and `Tests/Runtime` (`TeekayUtils.Tests`) assemblies with smoke tests proving the Test Runner pipeline.
- `DevProject~/` — embedded Unity 6000.3.10f1 host project (ignored by consumers) with the package referenced via `file:../..` and registered in `testables`.

## [0.1.0] - 2026-07-10

### Added

- Initial empty package skeleton: `Runtime/` (`TeekayUtils`) and `Editor/` (`TeekayUtils.Editor`) assemblies, no code yet.

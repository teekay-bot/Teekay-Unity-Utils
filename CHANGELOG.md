# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

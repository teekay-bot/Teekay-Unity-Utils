# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.1.0] - 2026-07-19

### Added

- **`[SubclassSelector]`** — a type dropdown for `[SerializeReference]` fields, so which
  implementation a field holds becomes an authoring choice instead of a code one. The picked
  instance's own serialized fields are drawn underneath it, and a newly written implementation
  appears in the dropdown just by existing — there is no registry to keep in sync.

  Unity serializes managed references but ships **no** type picker for them, so without a drawer a
  `[SerializeReference]` field can only ever be assigned from code. Verified against 6000.3.19f1:
  the serialization API is all present (`ManagedReferenceUtility`, `managedReferenceValue`,
  `isPropertyTypeAManagedReference`) but nothing in the Inspector selects a type.

  ```csharp
  [SerializeReference, SubclassSelector] IDamageModifier _modifier = new FlatDamage();
  ```

  The dropdown offers a type only when Unity could actually store it — concrete, not a
  `UnityEngine.Object`, not a value type, `[Serializable]`, public parameterless constructor. Types
  that fail any of those would serialize as null, so offering them would be a trap rather than a
  convenience. Colliding short names are disambiguated by namespace, because `GenericMenu` silently
  merges entries whose labels match.

  Type discovery and naming live in `SubclassSelectorTypes` (editor, public) separately from the
  drawer, so they are unit-testable without IMGUI — 14 EditMode tests. The attribute itself is a
  pure marker in the runtime assembly and costs a build nothing.

## [3.0.0] - 2026-07-19

An API consistency pass. Every change is small, and every one of them is breaking — hence the major
bump rather than a minor. Nothing in the package's own code or demos relied on the old shapes.

### Fixed

- **Static console events leaked across play sessions.** `OnVisibilityChanged` and `OnFocusChanged` were static with nothing ever clearing them, so with *Enter Play Mode without domain reload* enabled, handlers registered by one session survived into the next and fired against destroyed objects — a leak that grew with every press of Play. All console events are now cleared at `SubsystemRegistration`, before any `Awake`.

### Changed

- **BREAKING — `DevConsole.OnLogAppended` and `OnLogCleared` are now static.** They were the only instance events on a class whose entire public surface is static, so reaching for them the obvious way (`DevConsole.OnLogAppended += …`) was a compile error. Drop the `.Instance`.
- **BREAKING — `GameObjectExtensions.Path()` is now `ParentPath()`, and `PathFull()` is now `FullPath()`.** `Path()` returned the *parent's* path, which the name did nothing to suggest; the pair now says which is which and shares a suffix.
- **BREAKING — the DevConsole UI types are `internal`.** `ConsoleUI`, `ConsoleWindowDragHandle`, `ConsoleWindowResizeHandle`, `ConsoleSuggestionRow` and `ResizeEdges` were public but are pure internal wiring. `ConsoleUI.SetOpen`/`Bind` in particular let a consumer drive the window directly, desynchronising it from `DevConsole`'s own open/focus bookkeeping and bypassing the `ConsoleEnabled` gate that keeps the console out of release builds. Use the `DevConsole` static API instead.

## [2.1.0] - 2026-07-19

A DevConsole UX/polish release: the log becomes a real list instead of one giant string,
common actions get buttons, and the window opens with a fade instead of popping.

### Changed

- **DevConsole log view rewritten as pooled, virtualized rows** (`ConsoleLogView`). Only the visible slice of the buffer has live widgets, manually stacked from measured heights — no layout groups. Unlocks per-line features and replaces the previous "rebuild one giant TMP string on every append" cost model.
- **Consecutive duplicate log lines collapse** into one row with an accent ×N badge (`ConsoleLogBuffer`, data-level, unit-tested). A per-frame spammer now reads as one line with a counter instead of filling the buffer.
- **Auto-scroll no longer yanks the view**: the log follows new output only while you're already at the bottom. Scrolled up, new lines increment a floating "N new" pill; clicking it jumps back to the live tail.
- **One theming surface.** The chrome palette (window/elevated/hover surfaces, text tiers, accent, error accent) moved from a private `Theme` class into `DevConsoleSettings` + `DevConsoleConfig` next to the content colors; hover/selection tints derive from the accent. Suggestion-row rich-text colors now derive from the theme instead of hardcoded hex.

### Added

- **Toolbar** on the title bar: `Clear`, `Copy` (copies the filtered log as plain text), `Filter`.
- **Filter row** (toolbar-toggled): search box narrowing the log to matching lines, plus one chip per category — chips drive the same enabled flag as `log_filter`, and hide existing lines as well as future ones. Chips wrap onto extra lines and the row grows to fit, so narrowing the window never clips a filter out of reach.
- **Click a log line to copy it** — the row flashes accent as confirmation. Severity is now also shown as a colored stripe on the row's left edge.
- **Scrollbar** (thin, auto-hiding) — the log finally advertises that it scrolls.
- **Open/close animation**: 120 ms fade + slide, unscaled time (the console pauses the game while focused — a scaled tween would freeze mid-open). Input focus is granted immediately; animation never gates typing.
- **Error feedback**: a failed command (unknown name, bad CVar value, throwing handler) flashes the input card toward the error accent — visible even when the error line scrolls by unnoticed.
- `DevConsoleSettings.FontAsset` / config `fontAsset` — assign a monospace TMP font so columned output (`help`, `binds`) lines up; the package still ships zero assets.
- Demo: `demo.spam [count]` exercises duplicate collapsing and the jump pill.

## [2.0.0] - 2026-07-19

A DebugDraw release. Debug spheres now read as volumes instead of flat rings, the module
gained the shapes gameplay code actually needs (capsules, perception cones, arrows), and
GL drawing no longer silently fails under URP/HDRP.

### Fixed

- **`GLDebugDrawer` drew nothing at all under URP or HDRP.** Its documented usage pattern relied on `OnPostRender`, which only the Built-in render pipeline calls — under a scriptable pipeline the hook simply never fires, with no error and no warning. Added `GLDebugDrawRenderer`, a camera component that owns the line material and subscribes to whichever hook the active pipeline uses (`OnPostRender` for Built-in, `RenderPipelineManager.endCameraRendering` for SRP). `RenderPipelineManager` lives in `UnityEngine.CoreModule`, so supporting SRP adds **no** package dependency on URP or HDRP. Consumers should prefer this component over wiring GL up by hand.

### Changed

- **BREAKING — `IDebugDrawer.Disc` is now `Circle`.** It always drew an outline, never a filled disc; the name was actively misleading. Rename call sites; the behaviour is unchanged.
- **BREAKING for anyone implementing `IDebugDrawer`** (both in-package implementations are updated). Added to the interface: two `WireSphere` overloads, `WireSphereBand`, two `WireCapsule` overloads, two `ViewCone` overloads, and `Arrow`.
- `IDebugDrawer.WireSphere` now draws a latitude/longitude grid instead of three great circles, so a debug sphere reads as a volume rather than a flat ring. `GizmosDebugDrawer` no longer calls `Gizmos.DrawWireSphere` — it loses Unity's camera-facing silhouette circle but gains the grid, and now matches `GLDebugDrawer` exactly. Default density is 6 rings × 16 slices (176 segments per sphere); pass explicit `rings`/`slices` where a call site draws many spheres. Degenerate input is handled: the pole rings are skipped rather than drawn as zero-length segments, `rings`/`slices` are clamped, and a non-positive radius draws nothing instead of emitting every meridian as a pile of zero-length segments at the centre.

### Added

- `IDebugDrawer.WireSphereBand` — a latitude band of a wire sphere, for domes and other partial ranges (e.g. `(0, 90)` around an arbitrary `up` axis for an upward hearing/vision volume). Polar angles are degrees from `up`: 0 = pole along up, 90 = equator, 180 = opposite pole.
- `DebugDrawGeometry.GetLatitudeRing` — centre and radius of a latitude ring on a sphere; degenerate (zero-radius) at the poles so callers can skip it. 5 new EditMode tests, including one asserting the latitude and meridian parameterizations agree so grid lines actually intersect.
- `IDebugDrawer.WireCapsule` — wire capsule between two sphere centres, matching `Physics.CheckCapsule`'s convention (**not** `CapsuleCollider.height`, which includes both caps). Both caps are banded around the same axis so their meridians line up instead of being rotated apart.
- `IDebugDrawer.ViewCone` — the volume a range-and-angle perception check actually covers, for vision cones and directional hearing. The angle is the FULL cone angle, matching how a view angle is normally configured and then tested as `Vector3.Angle(...) <= viewAngle / 2`. The far end is a spherical cap, not a flat disc, because that is the shape a distance check produces — a disc would overstate the range everywhere except dead centre.
- `IDebugDrawer.Arrow` — `Ray` with a head at the far end, scaled to the length, so direction is readable.
- `Runtime/DebugDraw/DebugDrawShapes.cs` (internal, visible to the test assembly) — shared tessellation for arcs, spheres, capsules, cones and arrows, expressed in terms of `IDebugDrawer.Line`, so every backend draws these shapes identically by construction. Replaces the `DrawCircle` loop that was duplicated in both drawers. 11 EditMode tests covering surface accuracy, cap alignment, cone extent, arrow head orientation and degenerate input.

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

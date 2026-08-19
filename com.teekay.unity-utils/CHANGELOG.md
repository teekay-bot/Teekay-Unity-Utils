# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0] - 2026-08-19

### Removed

- ⚠️ **BREAKING — `TeekayUtils.Tags` is gone**: `GameplayTag`, `TagSet`, `GameplayTagCatalog`,
  `[GameplayTag]` + its drawer, `Documentation~/Tags.md`, and the 34 tests over them. Code that
  compiles against v3.6.0 and uses any of those will not compile against 4.0.0.
  - **Why, and it is not "unused code":** the feature was promoted INTO this package in 3.2.0 after
    proving out under Teekay-Unity-Base's character ability layer, and that ability layer was its
    only customer anywhere. On 2026-08-16 the layer stopped using it — the five `string[]` gates per
    ability became two attributes matched by TYPE (`Type.IsInstanceOfType`), which is strictly
    stronger for what they were actually doing: an interface now catches every implementor and a
    base class every subclass, and the compiler checks the relationship the dotted paths could only
    spell. Measured at the time: `activationBlockedTags` had **zero** declarers and the three
    `activationOwnedTags` had **zero** production consumers. Keeping a project-wide vocabulary asset
    and a custom drawer alive to express two relationships was the wrong trade.
  - **Staying on tags is a supported choice** — pin the git URL to `#v3.6.0` and nothing changes.
    Nothing else in 4.0.0 differs from 3.6.0, so that pin costs no other feature.
  - **Migrating off**, if the tags were doing what ours were (gating one behaviour on another): put
    the relationship on the type instead of in a string. Declare it with an attribute naming the
    other type and resolve it with `Type.IsInstanceOfType`. What you lose is a runtime-authorable
    vocabulary; what you gain is that a rename is a compile error rather than a silent mismatch.
  - ⚠️ **A `GameplayTagCatalog.asset` in a consuming project survives this deletion as a broken
    ScriptableObject** — its script reference no longer resolves. Delete the asset; there is no
    runtime reader for it (there never was — the catalog was edit-time vocabulary only).

## [3.6.0] - 2026-08-13

### Changed

- ⚠️ **Minimum editor is now Unity 6000.5 (Unity 6.5); 6000.3 is no longer supported.** Not a
  preference — 6.5 replaced the 32-bit `InstanceID` with the 64-bit `EntityId` struct and obsoleted
  the APIs that assumed instance-ID ordering, and the replacements are not all present in 6.3. The
  one that forces the floor is `FindObjectsByType<T>(FindObjectsInactive)`: the overload without a
  `FindObjectsSortMode` **does not exist in 6000.3** (measured against both editors'
  `UnityEngine.CoreModule.dll`), so supporting both editors would have meant `#if` guards around
  every call. Note this is a REQUIREMENT change, not an API change — nothing this package exposes
  moved, so consuming code needs no edits.
- **`Singleton<T>.Instance` and the DevConsole's EventSystem check use `FindAnyObjectByType`** where
  they used `FindFirstObjectByType`. The old one promised "first by instance-ID order", an ordering
  6.5 no longer has — and neither caller ever wanted an order: a singleton lookup expects exactly one
  match, and the EventSystem check is a null test. `FindAnyObjectByType` is also the faster of the
  two. Behaviour is unchanged; if a scene ever held two of a singleton, *which* one you got was
  already arbitrary in practice.
- **`FindObjectsSortMode.None` arguments dropped** (DevConsole EventSystem cleanup, `SingletonTests`
  teardown). The enum is obsolete in 6.5 and the parameterless overload is the replacement.



### Added

- **`IDebugDrawable.Surfaces`** and the `DebugSurface` flags enum — an overlay now says which views
  it may appear in, and the hub honours it per drawable. Two overlays in one scene can differ, and
  usually should: one describing screen-space work (what the aim is picking, a cull boundary) is
  meaningless from another angle, while one describing geometry (a capsule, a ground probe, a contact
  normal) reads far better in a Scene view you can orbit than painted over the game you are trying to
  play. Back it with two serialized bools and the component's Inspector gets both switches for free.
  Deliberately not one global switch on the hub: a global would need a checkbox on every component
  writing to it — one flag with N owners, which is how two Inspectors end up disagreeing about what
  is on.
- **`DebugDrawBuffer.Replay(target, fromSegment, toSegment, fromDot, toDot)`** — replays one slice of
  a recording. Counts only grow while a frame is being collected, so reading `SegmentCount` and
  `DotCount` around each contributor yields a range per contributor. Indices are clamped rather than
  rejected: the marks come from a call that may have been truncated by the segment cap, and a debug
  overlay is the last place that should turn a bad frame into an exception.

### Changed

- **`DebugDrawHub` records a range per drawable and filters at REPLAY time.** The obvious
  alternative — one buffer per surface — would have meant describing a drawable once per surface,
  breaking the exactly-once-per-frame promise that makes measuring inside `DrawDebug` safe. Labels
  go through the same ranges, so an overlay kept out of a view keeps its TEXT out too; labels
  floating with no shapes to belong to are worse than none, because they read as annotating whatever
  else happens to be underneath.
- A drawable answering `DebugSurface.None` is skipped before `DrawDebug` is called, so switching an
  overlay off by surface saves the measuring as well as the drawing.

### Breaking

- `IDebugDrawable` gained a member. Existing implementations need
  `public DebugSurface Surfaces => DebugSurface.All;` to keep behaving as they did.

## [3.4.2] - 2026-07-30

### Added

- **`IDebugLabelSink.Label(subject, anchor, text)`** — text ABOUT one world point but PLACED at
  another, for an overlay that had to move a label clear of its own geometry. The pair is what makes
  the move work: a plate centred on its anchor still reaches half its width back toward whatever the
  anchor was pushed away from, and the caller cannot correct for that, because the box's width is in
  pixels while its own offset is in metres. Given both points the hub projects them, sees which way
  the push went *on screen*, and keeps the plate wholly on that side — deciding it in world space
  would get it backwards whenever the camera looks from the other side. `DebugLabelLayout.Request`
  gained a matching `Side` (0 centres, ±1 places the box entirely to one side), and a box placed
  aside is not reported as `Displaced`, since it is where it asked to be. Existing one-position
  `Label` calls are unchanged and still centre.

## [3.4.1] - 2026-07-30

### Fixed

- **Scene-view labels are no longer drawn over by gizmos.** They now render from
  `SceneView.duringSceneGui` — the Scene view's own GUI pass, which runs after the view has rendered
  its contents — instead of from `OnDrawGizmos`. A drawable cannot control the order of gizmo
  callbacks, so a plate drawn there could still be crossed by gizmos it does not own: the selected
  object's collider wireframe, or any other component's `OnDrawGizmos`. Drawing one pass later stops
  the ordering from mattering at all. `OnDrawGizmos` keeps the shapes; the labels for the same
  overlay follow in the GUI pass. (Game-view labels were already last — `OnGUI` composites after
  rendering — and are unchanged.)
- **Label plates are near-opaque (alpha 0.82 → 0.94).** The plate's job is to occlude, and debug
  overlays draw saturated green and yellow wireframes: the 18% that bled through was still bright
  enough to read as lines crossing the text.

## [3.4.0] - 2026-07-30

### Changed

- **Debug labels are drawn as boxed plates, by one renderer shared between the Scene view and the
  Game view.** Previously the Game view got bold centred text with a 1px drop shadow while the Scene
  view called `Handles.Label(position, text)`, whose documented behaviour is to use "the label style
  from the current GUISkin" — unstyled, unboxed, left-anchored text. So the same overlay looked like
  two different tools, and on the surface where labels land *on top of the wireframes the hub just
  drew*, it had no contrast affordance at all. Both surfaces now project into GUI space (the Scene
  view via `HandleUtility.WorldToGUIPointWithDepth`, inside a `Handles.BeginGUI` block) and run the
  same plate-and-text pass: a dark rounded plate with a hairline outline, drawn with
  `GUI.DrawTexture`'s `borderWidths`/`borderRadius` overload — the only IMGUI path that rounds
  corners.
- **Labels no longer overlap, cover their subject, or repeat themselves** — placement moved into a
  new pure `DebugLabelLayout.Arrange` (12 tests). A box sits ABOVE its anchor, because a label that
  covers the geometry it annotates hides the thing it was drawn to explain; overlaps resolve by
  moving further up, into empty sky rather than across the scene, processed from the lowest anchor
  first so the pushes go that way; a box that had to move is marked `Displaced` and gets a stem back
  to its anchor, since a label floating free of its subject silently attributes its numbers to
  whatever it hovers over.
  - Labels with the SAME text and near-identical anchors are drawn once. This is the case that made
    annotated overlays unreadable in practice: a value re-evaluated several times inside one
    simulation step (a physics solver judging stability per sweep, say) produced a stack of identical
    strings a few pixels apart, which renders as thickened mush rather than as text.
  - The layout is separate from the hub and free of any GUI call because the part that goes wrong is
    arithmetic — which box moved, how far, whether it still points at its own fact — and that is
    exactly what looking at a screenshot cannot confirm.

## [3.3.0] - 2026-07-30

### Added

- **Built-in debug overlays — `IDebugDrawable` + `DebugDrawHub`.** A system now draws its own
  measured state instead of shipping a companion `Debug*Visualizer` component: implement
  `IDebugDrawable` on the class that owns the facts, call `DebugDrawHub.Register(this)` in
  `OnEnable`, and the hub handles every surface — GL lines over the Game view and builds, gizmos in
  the Scene view (gated so Game-view gizmos can't double-draw), shadowed IMGUI labels via
  `IDebugLabelSink`, and `debugdraw.*` console variables through `RegisterToggle`. Auto-created on
  first registration in Play mode, so a runtime-spawned character needs no scene setup.
  - Motivation: that plumbing was ~100 identical lines per overlay (camera resolve, `Drawing`
    subscribe/unsubscribe, Scene-view gating, label projection, execution order) and it had already
    been copied verbatim between two systems. Worse, a separate visualiser can only read what the
    owner makes public, so overlays pushed debug accessors into production APIs while the most
    useful facts — per-hit verdicts, intent before the solver reshaped it — were never stored at all.
  - `RegisterToggle` binds a console variable to the caller's existing field through get/set
    accessors, and is deliberately NOT persistent: a persistent variable restores its saved value at
    registration and would overwrite what the component serialized, leaving two sources of truth for
    one flag.
  - `Register`, `RegisterToggle` and the console wiring compile out unless `UNITY_EDITOR` or
    `DEVELOPMENT_BUILD` — a release player creates no hub, spawns no console and runs no draw code.
- **`DebugDrawBuffer`** — an `IDebugDrawer` that records instead of rendering, then replays into any
  backend. This is what lets `DrawDebug` be called exactly once per frame however many surfaces are
  listening, so measuring inside it (an extra raycast to explain a verdict, a string for a label) is
  safe — the "measure into lists, draw from lists" split every overlay used to need exists only
  because `OnGUI` runs several times a frame. Stores two primitives: line segments, plus `Sphere`
  kept whole because the backends render it differently on purpose. 9 EditMode tests (suite total
  140 → 149, verified in a consumer's Test Runner).

## [3.2.1] - 2026-07-23

### Docs

- READMEs and module docs caught up with 3.2.0: the Tags module documented
  (`Documentation~/Tags.md` + module tables + quick start), install snippets bumped, and the
  DevConsole guide updated for the removed `Tools` menu (create via *Assets → Create*, edit via
  the asset's Inspector).

## [3.2.0] - 2026-07-23

### Added

- **`TeekayUtils.Tags` — gameplay tag system** (promoted from Teekay-Unity-Base after proving out
  under its Character ability layer): `GameplayTag` (interned hierarchical dotted paths,
  reference-equality comparisons, hierarchy-aware `Matches` + string-level `PathMatches` mirror,
  non-throwing `IsValidPath`), `TagSet` (ref-counted grants with O(1) ancestor-propagated
  queries — two granters and one release must not clear the tag — loud unbalanced-release
  errors, no change events by design: views poll), `GameplayTagCatalog` (edit-time vocabulary
  asset, empty by default, validated/deduped/sorted `Add`), and `[GameplayTag]` whose drawer
  (`Editor/Tags/GameplayTagDrawer.cs`) renders string fields as a searchable dot-hierarchy
  picker with "New tag…" coining and a warning icon on paths missing from the catalog.
  31 EditMode tests.

### Changed

- **DevConsole: removed the `Tools → DevConsole` menu items.** The config asset is created via
  *Assets → Create → DevConsole → Config* and its Inspector still opens the config window —
  one entry point instead of two.

## [3.1.1] - 2026-07-20

### Fixed

- **`[SubclassSelector]`: the type could not be changed once the chosen type had fields of its
  own.** The header's foldout was given the full-width row, and `EditorGUI.Foldout` with
  `toggleOnLabelClick` consumes mouse events across the *entire* rect it is handed — so it
  swallowed every click aimed at the type dropdown sharing that row. Picking a type with no
  serialized fields left the field editable (no children, so no foldout was drawn); picking one
  with fields made the dropdown permanently unresponsive. The foldout is now confined to the label
  column, leaving the value column to the dropdown.
- **`[SubclassSelector]`: menu callbacks held a `SerializedProperty` past the end of `OnGUI`.**
  `GenericMenu` invokes its callbacks after the GUI pass has returned, at which point a retained
  `SerializedProperty` is no longer valid to use. The chosen type is now applied by re-resolving
  the property from its path on the `SerializedObject`.

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

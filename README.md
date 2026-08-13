# Teekay Unity Utils

[![Unity 6000.5+](https://img.shields.io/badge/Unity-6000.5%2B-000?logo=unity)](https://unity.com/releases/editor/whats-new/6000.5)
[![Release](https://img.shields.io/github/v/tag/teekay-bot/Teekay-Unity-Utils?label=release&color=2296F3)](https://github.com/teekay-bot/Teekay-Unity-Utils/tags)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](com.teekay.unity-utils/LICENSE.md)

Curated Unity utilities: extension methods, singletons, an event bus, gameplay tags, debug
drawing, and an in-game developer console. Small, tested, zero prefabs — everything is plain code.

**Requires Unity 6000.5 (Unity 6.5) or newer.**

<table>
<tr>
<td align="center" width="50%">
  <img src="com.teekay.unity-utils/Documentation~/images/devconsole.png" alt="The in-game developer console showing its toolbar, category filter chips, and a log line collapsed into a times-30 badge" width="380"><br>
  <sub><b>DevConsole</b> — commands, CVars, filtering, duplicate collapsing</sub>
</td>
<td align="center" width="50%">
  <img src="com.teekay.unity-utils/Documentation~/images/debugdraw.png" alt="Wire capsule, domes, spheres at several densities, a circle and a view cone drawn in the Unity Scene view" width="210"><br>
  <sub><b>DebugDraw</b> — capsules, domes, perception cones, in Scene view <i>and</i> builds</sub>
</td>
</tr>
</table>

## Installation

**Package Manager UI** — `Window ▸ Package Manager ▸ + ▸ Install package from git URL…` and paste:

```
https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils
```

This tracks the latest commit on `main`. Unity locks the resolved commit in `packages-lock.json`, so
to pull newer work later, paste the same URL again — a fresh request ignores the locked hash. Append
`#vX.Y.Z` (any tag from [Releases](https://github.com/teekay-bot/Teekay-Unity-Utils/releases)) to pin
to a release instead; a pinned URL never moves, and updating means editing the tag by hand.

## Documentation

| Module | |
|---|---|
| [Extensions](com.teekay.unity-utils/Documentation~/Extensions.md) | 84 methods over vectors, transforms, GameObjects, colours, collections, strings and more. |
| [Physics](com.teekay.unity-utils/Documentation~/Physics.md) | Cached `GetComponentInParent` lookups for physics-scan hot paths. |
| [Singleton](com.teekay.unity-utils/Documentation~/Singleton.md) | Scene-local and persistent singleton base classes. |
| [EventBus](com.teekay.unity-utils/Documentation~/EventBus.md) | Type-keyed pub/sub with zero-alloc publish. |
| [Tags](com.teekay.unity-utils/Documentation~/Tags.md) | Hierarchical gameplay tags: interned `GameplayTag`, ref-counted `TagSet`, catalog asset + `[GameplayTag]` Inspector picker. |
| [DebugDraw](com.teekay.unity-utils/Documentation~/DebugDraw.md) | One drawing API, rendering in the Scene view **and** in builds under any pipeline — plus `IDebugDrawable`/`DebugDrawHub`, so a system draws its own measured state with no scene setup. |
| [DevConsole](com.teekay.unity-utils/Documentation~/DevConsole.md) | In-game console: commands, CVars, autocomplete, bindings, log capture. |
| [Attributes](com.teekay.unity-utils/Documentation~/Attributes.md) | `[KeyPicker]` click-to-listen key capture, `[SubclassSelector]` type dropdown for `[SerializeReference]` fields. |

Start with the [package README](com.teekay.unity-utils/README.md) for a quick tour of every module.

## Repository layout

- [`com.teekay.unity-utils/`](com.teekay.unity-utils/) — the UPM package
  ([README](com.teekay.unity-utils/README.md) · [CHANGELOG](com.teekay.unity-utils/CHANGELOG.md) ·
  [LICENSE](com.teekay.unity-utils/LICENSE.md)).
- [`DevProject/`](DevProject/) — Unity 6000.5 host project for development: tests (Test Runner) and
  per-feature demo scenes. Open `DemoHub` and press Play. Not shipped to consumers — the `?path=` install
  URL scopes them to the package folder only.

## License

[MIT](com.teekay.unity-utils/LICENSE.md) — portions adapted from
[adammyhre/Unity-Utils](https://github.com/adammyhre/Unity-Utils).

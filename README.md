# Teekay Unity Utils

Personal Unity utilities package — extension methods, helpers, and editor tooling. Inspired by [adammyhre/Unity-Utils](https://github.com/adammyhre/Unity-Utils), but trimmed to what I actually use.

**Requires Unity 6.3 LTS (`6000.3`) or newer.**

## Install

### Use in a project (git URL, read-only)

Add to `Packages/manifest.json`:

```json
"com.teekay.unity-utils": "https://github.com/teekay-bot/Teekay-Unity-Utils.git"
```

Pin a version once tags exist: append `#v0.1.0`. Update via Package Manager ▸ Update, or bump the tag.

> Private repo: install works on machines where git is already authenticated to GitHub (Unity shells out to system git).

### Develop the package (DevProject~)

Git-URL packages are immutable inside the consuming project — all package development happens in the embedded host project instead:

1. Open `DevProject~/` in Unity 6000.3.19f1 (add it to Unity Hub once). It references this package via `file:../..`, so the package is **mutable** there: edit under `Packages/Teekay Unity Utils`, and Unity writes `.meta` files back into this repo — commit them.
2. Run tests via **Window ▸ General ▸ Test Runner** (EditMode + PlayMode tabs). Package tests are visible because `DevProject~/Packages/manifest.json` lists the package in `"testables"` — any other host project needs that same entry to see them.
3. Commit + push here, tag `vX.Y.Z`, then update consumers.

`DevProject~` is invisible to consumers: Unity never imports folders ending in `~` when the package is installed.

## Layout & conventions

- `Runtime/` — assembly `TeekayUtils`, namespace `TeekayUtils.*`. All runtime code goes here, grouped by folder (`Extensions/`, `Singleton/`, ...).
- `Editor/` — assembly `TeekayUtils.Editor` (Editor-only, references `TeekayUtils`). Property drawers, editor windows, hotkeys.
- `Tests/Editor` + `Tests/Runtime` — test assemblies (`TeekayUtils.Tests.Editor` / `TeekayUtils.Tests`), namespace `TeekayUtils.Tests`. Stripped from builds via `UNITY_INCLUDE_TESTS`.
- `DevProject~/` — dev/test host project, not part of the package payload.
- Every new file needs its committed `.meta` (edit via the `file:` workflow above so Unity generates them, or hand-author).
- Version bump + `CHANGELOG.md` entry + git tag `vX.Y.Z` per release.

## Ported so far

- **EditorExtensions** (`Editor/Extensions/`) — `PingAndSelect` extension for `Object` (from Unity-Utils, MIT).
- **EditorFileUtils** (`Editor/Utils/`) — `ConfirmOverwrite`, `BrowseForFolder` static file-dialog helpers (from Unity-Utils, MIT; converted from string extensions).

## Roadmap (candidates)

- **Extensions** — Transform/Vector/Color/GameObject/List/Number/String/... extension methods (from the existing `TeekayUtils` sets in my projects).
- **Singleton** — `Singleton<T>` / `PersistentSingleton<T>` base classes.
- **Algorithm** — pure-C# `GraphSearch` (BFS/DFS/Reachable/ShortestPath), possibly as a separate `noEngineReferences` assembly.
- **DebugDraw** — Gizmos/GL debug drawers.
- **From Unity-Utils, if ever needed** — static helpers, editor hotkeys, timers.

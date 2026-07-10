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

### Develop the package (file reference, mutable)

Git-URL packages are immutable inside the consuming project. To edit the package, point the manifest at the local clone instead:

```json
"com.teekay.unity-utils": "file:C:/Users/teeka/GitHub/Personal/Teekay-Unity-Utils"
```

Edit under `Packages/Teekay Unity Utils` in the Editor (Unity writes `.meta` files back into this repo — commit them), then commit + push here and switch consumers back to the git URL.

## Layout & conventions

- `Runtime/` — assembly `TeekayUtils`, namespace `TeekayUtils.*`. All runtime code goes here, grouped by folder (`Extensions/`, `Singleton/`, ...).
- `Editor/` — assembly `TeekayUtils.Editor` (Editor-only, references `TeekayUtils`). Property drawers, editor windows, hotkeys.
- Every new file needs its committed `.meta` (edit via the `file:` workflow above so Unity generates them, or hand-author).
- Version bump + `CHANGELOG.md` entry + git tag `vX.Y.Z` per release.

## Roadmap (candidates, nothing ported yet)

- **Extensions** — Transform/Vector/Color/GameObject/List/Number/String/... extension methods (from the existing `TeekayUtils` sets in my projects).
- **Singleton** — `Singleton<T>` / `PersistentSingleton<T>` base classes.
- **Algorithm** — pure-C# `GraphSearch` (BFS/DFS/Reachable/ShortestPath), possibly as a separate `noEngineReferences` assembly.
- **DebugDraw** — Gizmos/GL debug drawers.
- **From Unity-Utils, if ever needed** — static helpers, editor hotkeys, timers.

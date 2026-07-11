# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A UPM package (`com.teekay.unity-utils`) — the repo root IS the package. Consumers install it via git URL, which makes it immutable in their projects; all development happens through the embedded host project `DevProject~/` (invisible to consumers — Unity skips `~`-suffixed folders in installed packages). `DevProject~/Packages/manifest.json` references the package via `file:../..` and lists it in `"testables"` so its tests appear in the Test Runner.

Requires Unity 6000.3 LTS; `DevProject~` is pinned to **6000.3.19f1** (see `DevProject~/ProjectSettings/ProjectVersion.txt`).

## Assembly structure

Four asmdefs, all at fixed locations:

- `Runtime/TeekayUtils.asmdef` — assembly `TeekayUtils`, namespace `TeekayUtils`. Modules: `Extensions/` (13 extension classes), `Singleton/` (`Singleton<T>` + `PersistentSingleton<T>`, hardened: first-Awake-wins, quit guard, no Edit-mode auto-create), `DebugDraw/` (`IDebugDrawer` + Gizmos/GL backends).
- `Editor/TeekayUtils.Editor.asmdef` — Editor-only, references `TeekayUtils`. `Extensions/` + `Utils/`.
- `Tests/Editor/` and `Tests/Runtime/` — EditMode tests for pure-value code, PlayMode tests for anything touching GameObjects/physics. Constrained by `UNITY_INCLUDE_TESTS`.

## Running tests

In-editor: open `DevProject~/` in Unity 6000.3.19f1, then **Window ▸ General ▸ Test Runner** (EditMode + PlayMode tabs).

From the CLI (batchmode; the editor must be CLOSED — check for `DevProject~/Temp/UnityLockfile` and the Unity process first):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -projectPath "DevProject~" -runTests -testPlatform EditMode -testResults "$PWD\test-results-editmode.xml" -logFile "$PWD\unity-test.log"
```

Use `-testPlatform PlayMode` for the PlayMode assembly, `-testFilter "Full.Test.Name"` for a single test. Exit code 0 = all passed.

Quick compile check without Unity (useful while the editor is open): build the `.cs` files with `dotnet` against the modular DLLs in `C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\Managed\UnityEngine\` (CoreModule, PhysicsModule, UIModule, ...) plus `DevProject~/Library/ScriptAssemblies/UnityEngine.TestRunner.dll` and NUnit. Set `<LangVersion>9.0</LangVersion>`. This catches compile errors but is NOT a substitute for running tests in Unity.

## Critical workflow rules

- **Every asset needs a committed `.meta`, and every `.meta` MUST end with a trailing newline.** Unity's YAML parser rejects metas without the final `\n` and then SILENTLY ignores the asset — no recompile, stale Test Runner, even after editor restart. When generating metas by script, never use `-NoNewline`; verify the last byte is LF.
- **When Unity mysteriously ignores new files** (stale test list, no recompile after Ctrl+R/restart): grep `%LOCALAPPDATA%/Unity/Editor/Editor.log` for `could not be parsed` FIRST. Also compare `DevProject~/Library/ScriptAssemblies/*.dll` timestamps against source file timestamps to confirm whether a recompile actually happened.
- **Files written from outside the editor need a manual Ctrl+R** in Unity before they exist as assets (even with Auto Refresh enabled, focus alone is not always enough).
- **Fresh GUIDs when copying assets from other repos** (e.g. Teekay-Core-Unity): never reuse the source project's GUIDs — a project containing both would conflict. After generating metas, check for duplicate GUIDs across the repo.
- **Releases**: bump `version` in `package.json`, rename `[Unreleased]` in `CHANGELOG.md` to the version + date, commit, tag `vX.Y.Z`, push with tags.
- **Do not add async/coroutine helpers** — the user uses UniTask, which covers that space (`.ToUniTask()`, `UniTask.WaitUntil`, `OnInvokeAsync`, `.Forget()`). A UniTask dependency is deliberately deferred until code actually needs it (would use asmdef `versionDefines`, since UPM cannot declare git-URL dependencies).

## Design conventions (established with the user)

- Curate, don't mirror: when porting from adammyhre/Unity-Utils or elsewhere, drop methods that duplicate Unity built-ins, fix upstream bugs (documented in CHANGELOG), and rename misleading APIs (e.g. `ToVector3XZ`).
- No extensions on `string` for non-string-semantic operations (pollutes IntelliSense) — use plain static methods instead (`EditorFileUtils`, `ColorExtensions.FromHex`).
- Allman braces, XML docs on public API, one class per file named after the type.
- Manual usage verification happens via self-spawning demo panels in `DevProject~/Assets/Scripts/` (`*DemoUI.cs`): bootstrap with `[RuntimeInitializeOnLoadMethod]` guarded by `SceneManager.GetActiveScene().name != "Sample"` so PlayMode test runs are never affected. Demo code lives ONLY in DevProject, never in the package.
- The user's workflow per feature: review/plan → agree on scope → port with tests → compile-check → user runs tests in Unity → scene demo → user verifies by hand → commit (package changes and DevProject changes as separate commits).

## Commit style

No Co-Authored-By trailers (disabled globally via `attribution` settings; history was rewritten once to strip them — don't reintroduce). Subject in imperative mood; body explains what changed vs upstream where relevant.

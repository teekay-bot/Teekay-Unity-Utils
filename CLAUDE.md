# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A UPM package repo with two top-level parts:

- `com.teekay.unity-utils/` — THE PACKAGE. Consumers install via git URL scoped to this subfolder: `https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils#vX.Y.Z` (tags ≤ v0.4.0 predate this layout and use the repo root without `?path=`).
- `DevProject/` — the Unity host project for development. References the package via `file:../../com.teekay.unity-utils` (mutable there) and lists it in `"testables"`. Consumers never receive it because `?path=` scopes the install.

**The dev project folder MUST NOT have `~` in its path.** It used to be `DevProject~` and that silently broke MonoScript class binding for every registry package under its `Library/PackageCache` (TMP settings/fonts and `.inputactions` imported as empty artifacts with zero errors; survived Library wipes). Diagnose suspected recurrences with `MonoScript.GetClass()` on a registry-package script — null while the type exists at runtime means path-based hidden-folder poisoning.

Requires Unity 6000.3 LTS; `DevProject` is pinned to **6000.3.19f1** (see `DevProject/ProjectSettings/ProjectVersion.txt`).

## Assembly structure

Four asmdefs inside `com.teekay.unity-utils/`:

- `Runtime/TeekayUtils.asmdef` — assembly `TeekayUtils`, namespace `TeekayUtils` (+ `TeekayUtils.Events`, `TeekayUtils.DevConsole`). Modules: `Extensions/`, `Singleton/`, `Events/` (EventBus), `DebugDraw/`, `DevConsole/`, `Attributes/`. References `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`; package dependencies `com.unity.inputsystem` + `com.unity.ugui` are declared in `package.json`.
- `Editor/TeekayUtils.Editor.asmdef` — Editor-only. NOTE: editor sub-namespaces use `.EditorTools` (not `.Editor`) to avoid the `TeekayUtils.Editor` namespace shadowing the `UnityEditor.Editor` type; fully qualify `UnityEditor.Editor` when inheriting.
- `Tests/Editor/` + `Tests/Runtime/` — EditMode for pure-value code, PlayMode for GameObjects/physics. Constrained by `UNITY_INCLUDE_TESTS`.

## Running tests

In-editor: open `DevProject/` in Unity 6000.3.19f1 → **Window ▸ General ▸ Test Runner**. Always have a REAL scene open (e.g. `DemoHub`) before a PlayMode run — if the editor sits in a stale `InitTestScene`, runs hang at 0 tests.

CLI (editor must be CLOSED — check `DevProject/Temp/UnityLockfile` and the Unity process first):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -projectPath "DevProject" -runTests -testPlatform EditMode -testResults "$PWD\test-results-editmode.xml" -logFile "$PWD\unity-test.log"
```

Use `-testPlatform PlayMode` for the PlayMode assembly, `-testFilter "Full.Test.Name"` for one test. Exit code 0 = all passed.

Quick compile check without Unity (while the editor is open): build the `.cs` files with `dotnet` against the modular DLLs in `C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\Managed\UnityEngine\` (CoreModule, PhysicsModule, UIModule, ... — for editor code use `UnityEditor.CoreModule.dll` from the same folder, never mix the monolithic `UnityEngine.dll` facade with modular DLLs) plus `DevProject/Library/ScriptAssemblies/` DLLs (UnityEngine.TestRunner, Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI) and NUnit. Set `<LangVersion>9.0</LangVersion>`. Catches compile errors only — NOT a substitute for running tests in Unity.

## Critical workflow rules

- **Every asset needs a committed `.meta`, ending with a trailing newline.** Unity's YAML parser rejects metas without the final `\n` and SILENTLY ignores the asset (no recompile, stale Test Runner, survives restarts). Never generate metas with `-NoNewline`; verify the last byte is LF and that GUIDs are unique repo-wide.
- **When Unity mysteriously ignores new files**: grep `%LOCALAPPDATA%/Unity/Editor/Editor.log` for `could not be parsed` FIRST; compare `DevProject/Library/ScriptAssemblies/*.dll` timestamps against source timestamps to confirm whether a recompile happened.
- **Files written from outside the editor need a manual Ctrl+R** in Unity (Auto Refresh alone is not always enough).
- **Never delete/rename the scene currently open in the user's editor** — the ghost copy hangs PlayMode test runs at startup (EditMode still passes). Ask the user to switch scenes first.
- **Never create junctions/links to the dev project INSIDE the repo** — the package importer will descend into them (no `~` protection) and trigger an infinite import loop that rewrites meta GUIDs. If Unity ever mass-regenerates tracked metas, `git checkout -- DevProject/Assets` restores them.
- **Fresh GUIDs when copying assets from other repos** (e.g. Teekay-Core-Unity) — never reuse source GUIDs.
- **Releases**: bump `version` in `com.teekay.unity-utils/package.json`, rename `[Unreleased]` in `com.teekay.unity-utils/CHANGELOG.md` to the version + date, commit, tag `vX.Y.Z`, push with tags.
- **Do not add async/coroutine helpers** — the user uses UniTask (`.ToUniTask()`, `UniTask.WaitUntil`, `OnInvokeAsync`, `.Forget()`). A UniTask dependency is deliberately deferred (would use asmdef `versionDefines`; UPM cannot declare git-URL dependencies).

## Design conventions (established with the user)

- Curate, don't mirror: when porting from adammyhre/Unity-Utils or Teekay-Core-Unity, drop methods that duplicate Unity built-ins, fix upstream bugs (documented in CHANGELOG), rename misleading APIs.
- No extensions on `string` for non-string-semantic operations — plain static methods instead (`EditorFileUtils`, `ColorExtensions.FromHex`).
- Allman braces, XML docs on public API, one class per file named after the type.
- Manual usage verification via per-feature demo scenes in `DevProject/Assets/Scenes/` (`DemoHub` + `SingletonDemo`/`ExtensionsDemo`/`DebugDrawDemo`/`DevConsoleDemo`/`EventBusDemo`). `DemoBootstrap` spawns demos via `SceneManager.sceneLoaded` (NOT per-demo `[RuntimeInitializeOnLoadMethod]` — fires once per play session, breaks scene switches); `DemoNavigator` is the persistent scene-switch bar. New demo = copy a scene file + fresh meta GUID + EditorBuildSettings entry + a case in `DemoBootstrap` + the navigator list. Demo code lives ONLY in DevProject.
- The user's workflow per feature: review/plan → agree on scope → port with tests → compile-check → user runs tests in Unity → scene demo → user verifies by hand → commit (package and DevProject changes as separate commits).

## Branch workflow

Day-to-day work happens on **`dev`** (commit + push there). `main` only receives merges from `dev` — prefer fast-forward; merge when a batch of work is verified or when releasing. Tags (`vX.Y.Z`) are created on `main` after the release merge. Never commit directly to `main`.

GitHub has a ruleset requiring pull requests into `main`, so pushing the release merge prints
`Bypassed rule violations for refs/heads/main`. **This is expected, not a mistake.** The repo is
public and the ruleset is kept deliberately as a guard for any future collaborator; the owner is
sole maintainer and bypasses it with admin rights when releasing. Don't "fix" the warning by
removing the ruleset or by switching the release to a PR flow.

## Commit style

No Co-Authored-By trailers (disabled globally via `attribution` settings; history was rewritten once to strip them — don't reintroduce). Subject in imperative mood; body explains what changed vs upstream where relevant.

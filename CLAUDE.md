# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A UPM package (`com.teekay.unity-utils`) — the repo root IS the package. Consumers install it via git URL, which makes it immutable in their projects; all development happens through the embedded host project `DevProject~/` (invisible to consumers — Unity skips `~`-suffixed folders in installed packages). `DevProject~/Packages/manifest.json` references the package via `file:../..` and lists it in `"testables"` so its tests appear in the Test Runner.

Requires Unity 6000.3 LTS; `DevProject~` is pinned to **6000.3.19f1** (see `DevProject~/ProjectSettings/ProjectVersion.txt`).

## Assembly structure

Four asmdefs, all at fixed locations:

- `Runtime/TeekayUtils.asmdef` — assembly `TeekayUtils`, namespace `TeekayUtils.*`. Runtime code grouped by folder (`Extensions/`, `Singleton/`, ...).
- `Editor/TeekayUtils.Editor.asmdef` — Editor-only, references `TeekayUtils`.
- `Tests/Runtime/TeekayUtils.Tests.asmdef` and `Tests/Editor/TeekayUtils.Tests.Editor.asmdef` — namespace `TeekayUtils.Tests`, constrained by `UNITY_INCLUDE_TESTS` (stripped from builds).

## Running tests

In-editor: open `DevProject~/` in Unity 6000.3.19f1, then **Window ▸ General ▸ Test Runner** (EditMode + PlayMode tabs).

From the CLI (batchmode; close the Unity editor first — the project can only be open in one place):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -projectPath "DevProject~" -runTests -testPlatform EditMode -testResults "$PWD\test-results-editmode.xml" -logFile "$PWD\unity-test.log"
```

Use `-testPlatform PlayMode` for the PlayMode assembly. Filter to a single test with `-testFilter "TeekayUtils.Tests.RuntimeSmokeTests.TestPipeline_PlayMode_IsWired"` (full name, or a regex). Exit code 0 = all passed; results are NUnit XML in the `-testResults` file.

There is no lint/build step outside Unity — compilation errors surface in the Unity log/Editor.

## Critical workflow rules

- **Every asset file needs a committed `.meta`.** When adding a `.cs` file (or folder) to `Runtime/`, `Editor/`, or `Tests/`, either let Unity generate the `.meta` by opening `DevProject~` (it writes back into this repo through the `file:` reference), or hand-author one with a unique GUID. Never commit a new file without its `.meta`.
- **Releases**: bump `version` in `package.json`, add a `CHANGELOG.md` entry (Keep a Changelog format), commit, tag `vX.Y.Z`.
- `DevProject~/Library`, `Temp/`, `UserSettings/`, and IDE files are gitignored; `DevProject~/Packages` and `DevProject~/ProjectSettings` are tracked.

## Current state

The package is a skeleton — `Runtime/` and `Editor/` contain only asmdefs, and `Tests/` holds smoke tests that prove the Test Runner pipeline. The README's Roadmap section lists what's planned (extension methods, singletons, graph search, debug draw), ported selectively from adammyhre/Unity-Utils and the user's existing projects.

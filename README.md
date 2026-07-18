# Teekay Unity Utils

Curated Unity utilities: extension methods, singletons, an event bus, debug drawing, and an in-game developer console.

📦 **Package & documentation:** [`com.teekay.unity-utils/`](com.teekay.unity-utils/)

## Installation

**Package Manager UI** — `Window ▸ Package Manager ▸ + ▸ Install package from git URL…` and paste:

```
https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils#v2.0.0
```

**Or edit `Packages/manifest.json`** directly:

```json
"com.teekay.unity-utils": "https://github.com/teekay-bot/Teekay-Unity-Utils.git?path=/com.teekay.unity-utils#v2.0.0"
```

The two forms are not interchangeable — the Package Manager's git URL field takes the bare
URL only, so pasting the `manifest.json` line into it fails. Full notes in the
[package README](com.teekay.unity-utils/README.md#installation).

## Repository layout

- [`com.teekay.unity-utils/`](com.teekay.unity-utils/) — the UPM package ([README](com.teekay.unity-utils/README.md) · [CHANGELOG](com.teekay.unity-utils/CHANGELOG.md) · [LICENSE](com.teekay.unity-utils/LICENSE.md)).
- [`DevProject/`](DevProject/) — Unity 6000.3 host project for development: tests (Test Runner) and per-feature demo scenes (open `DemoHub` and press Play).

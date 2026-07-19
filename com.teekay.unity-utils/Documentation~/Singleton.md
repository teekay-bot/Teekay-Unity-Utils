# Singletons

Two base classes in namespace `TeekayUtils`:

- `Singleton<T>` — scene-local. Dies with the scene.
- `PersistentSingleton<T>` — survives scene loads via `DontDestroyOnLoad`.

```csharp
using TeekayUtils;

public class AudioManager : PersistentSingleton<AudioManager>
{
    public void PlayShot(AudioClip clip) { /* ... */ }
}

AudioManager.Instance.PlayShot(clip);
```

---

## Lifecycle rules

**First `Awake` wins.** The first instance to run `Awake` claims the slot. Any later duplicate destroys
its own GameObject and logs a warning — so dragging a second manager into a scene fails loudly instead
of silently shadowing the first.

**`Instance` auto-creates only in Play mode.** If nothing has claimed the slot, `Instance` searches the
scene (including inactive objects) and, failing that, creates a GameObject and adds `T` to it. In Edit
mode it returns `null` instead of creating — so an editor script or `OnValidate` can't litter the scene
with manager objects.

**Nothing is resurrected during quit.** Once `OnApplicationQuit` has run, `Instance` logs a warning and
returns `null` rather than creating an object that Unity is in the middle of tearing down.

---

## API

### `Singleton<T> where T : Singleton<T>`

| Member | Notes |
|---|---|
| `static T Instance` | Get-or-find-or-create. Returns `null` in Edit mode and while quitting. |
| `static bool HasInstance` | True if the slot is claimed. Never creates. |
| `static T TryGetInstance()` | The instance, or `null`. Never creates. |
| `protected static T instance` | The backing field. |
| `protected static bool isQuitting` | Set by `OnApplicationQuit`. |
| `protected virtual void Awake()` | Calls `InitializeSingleton()`. |
| `protected virtual void InitializeSingleton()` | Claim-or-destroy logic. No-op outside Play mode. |
| `protected virtual void OnDestroy()` | Clears the slot if this instance owned it. |
| `protected virtual void OnApplicationQuit()` | Sets `isQuitting`. |

### `PersistentSingleton<T> where T : PersistentSingleton<T>`

Everything above, plus:

| Member | Notes |
|---|---|
| `bool AutoUnparentOnAwake` | Default `true`. Detaches from any parent on Awake. |
| `protected override void InitializeSingleton()` | Unparents, claims the slot, then `DontDestroyOnLoad`. |

`AutoUnparentOnAwake` exists because `DontDestroyOnLoad` only applies to root objects — a nested manager
would be destroyed along with its parent on scene load. Leave it on unless you are deliberately managing
the hierarchy yourself.

---

## Overriding lifecycle methods

`Awake`, `OnDestroy` and `OnApplicationQuit` are `virtual`, not sealed. **Overrides must call base** —
skipping it means the instance never claims the slot, never releases it, or never sets the quit guard:

```csharp
public class GameManager : PersistentSingleton<GameManager>
{
    protected override void Awake()
    {
        base.Awake();          // required
        LoadSaveData();
    }
}
```

---

## Enter Play Mode without domain reload

`InitializeSingleton` resets `isQuitting` to `false` on every claim. With domain reload disabled, static
state survives between play sessions, so a stale `isQuitting = true` from the previous session would
otherwise make `Instance` return `null` for the entire next session.

---

## When not to use this

A singleton couples every caller to a concrete type and a global lifetime. It is a reasonable fit for a
genuinely single, long-lived service (audio, save, input routing). It is a poor fit for anything you will
later want two of, or want to swap in a test.

For decoupling gameplay systems from each other, prefer [EventBus](EventBus.md) — publisher and
subscriber never name each other, so neither needs a global handle.

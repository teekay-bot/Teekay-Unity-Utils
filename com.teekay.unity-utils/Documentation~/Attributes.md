# Attributes

## `[KeyPicker]`

Namespace `TeekayUtils`. Replaces the Inspector's `Key` enum dropdown with a click-to-listen capture
button.

```csharp
using TeekayUtils;
using UnityEngine.InputSystem;

public class Rebindable : MonoBehaviour
{
    [KeyPicker] public Key interactKey = Key.E;
}
```

The Input System's `Key` enum has well over a hundred entries. Finding `Backquote` or `Numpad7` in an
alphabetical popup is slower — and more error-prone — than just pressing the key you mean.

**In the Inspector:** click the button, press a key, done. **Esc** cancels and leaves the value unchanged.

The attribute itself is a pure marker with no runtime logic; the behaviour lives in an editor-only
`PropertyDrawer`, so it costs a build nothing.

Used by `DevConsoleConfig.toggleKey` — [DevConsole](DevConsole.md) is where you will most likely meet it
first.

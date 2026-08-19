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

## `[SubclassSelector]`

Namespace `TeekayUtils`. Pairs with `[SerializeReference]` to add a type dropdown, so *which
implementation* a field holds is chosen in the Inspector instead of in code.

```csharp
using TeekayUtils;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeReference, SubclassSelector]
    IDamageModifier _modifier = new FlatDamage();
}
```

Unity serializes managed references but provides no UI for picking their type, so a bare
`[SerializeReference]` field can only ever be assigned from code. This fills that gap.

**In the Inspector:** a dropdown on the field's row lists every assignable type plus `None`. Picking
one constructs an instance; its own serialized fields are drawn underneath, so a modifier's `amount`
or a gesture's `duration` is edited in place. Writing a new implementation is enough to make it
appear — there is no list to register it in.

### What appears in the dropdown

Only types Unity can actually store in a managed reference:

| Requirement | Why |
|---|---|
| Concrete (not abstract, not an interface) | Nothing to instantiate otherwise. |
| Not a `UnityEngine.Object` | Those are stored as object references, not managed references. |
| Not a value type | Managed references hold classes. |
| `[Serializable]` | Without it the value serializes as null. |
| Public parameterless constructor | The drawer constructs the instance. |
| Not declared in a test assembly | It exists in no player build, so picking one stores a reference that reads back null there — and only there. |

The first five are about storage: a type failing one of them serializes as null, so offering it would
be worse than omitting it. The sixth is about REACH — a test double stores perfectly well and then
is missing from the build, which is the same symptom arriving later and somewhere you cannot see it.

Two types sharing a short name are disambiguated by namespace — `GenericMenu` merges entries with
identical labels, so one would otherwise disappear.

### Reusing the type discovery

`SubclassSelectorTypes` (editor-only) is public if you are building your own picker:

| Method | Notes |
|---|---|
| `ResolveFieldType(managedReferenceFieldTypename)` | Turns Unity's `"<assembly> <type>"` format into a `Type`. Null when malformed. |
| `IsSelectable(type)` | The first five rows above, as one predicate. The sixth is `IsFromTestAssembly`. |
| `GetSelectable(fieldType)` | Assignable selectable types, sorted by name. Includes `fieldType` itself when it qualifies. Does NOT apply the test-assembly rule — it answers "what can Unity store", which is a property of the type alone. |
| `GetShippable(fieldType)` | What the dropdown actually offers: `GetSelectable` minus test assemblies. |
| `IsFromTestAssembly(type)` | Whether the declaring assembly references the test runner or NUnit. |
| `BuildMenuLabels(types)` | Menu labels parallel to the list, namespace-qualified where names collide. |

The attribute is a pure marker with no runtime logic; the drawer is editor-only, so it costs a build
nothing.

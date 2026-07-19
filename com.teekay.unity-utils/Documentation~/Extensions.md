# Extensions

84 extension methods across 14 static classes, all in namespace `TeekayUtils`. Nothing here duplicates a
Unity built-in — every method exists because the built-in equivalent is missing, verbose, or wrong in a
way that bites.

```csharp
using TeekayUtils;

transform.position = transform.position.With(y: 0f);          // change one component
Vector3 spot = origin.RandomPointInAnnulus(2f, 5f);            // spawn ring, uniform by area
if (player.InRangeOf(target, 10f, maxAngle: 90f)) Attack();    // distance + FOV cone in one call
var rb = gameObject.GetOrAdd<Rigidbody>().ChangeDirection(dir);
```

---

## Vector2 / Vector3

`Vector2Extensions`, `Vector3Extensions`

| Method | Notes |
|---|---|
| `With(float? x, float? y[, float? z])` | Copy with only the named components replaced. |
| `Add(float x = 0, float y = 0[, float z = 0])` | Component-wise add. **Not clamped** (unlike `Color.Add`). |
| `InRangeOf(target, float range)` | Compares `sqrMagnitude` — no square root. |
| `DirectionTo(to)` | Normalized direction. |
| `SqrDistanceTo(to)` | Squared distance, cheaper than `Vector3.Distance`. |
| `RandomPointInAnnulus(minRadius, maxRadius)` | Uniform **by area**, not by radius — no clustering at the centre. |

**Vector2 only**

| Method | Notes |
|---|---|
| `Rotate(float degrees)` | Counter-clockwise. |

**Vector3 only**

| Method | Notes |
|---|---|
| `ComponentDivide(Vector3 v1)` | Zero components in `v1` leave the numerator untouched instead of producing `Infinity`/`NaN`. |
| `ToVector3XZ()` | Extends **`Vector2`** despite living in `Vector3Extensions`: `(x, y)` → `(x, 0, y)`. |
| `RandomOffset(float range)` | Random offset in `[-range, range]` per component. |
| `ProjectOntoLine(lineStart, lineDirection)` | Closest point on the infinite line. Degenerate direction returns `lineStart` rather than throwing. |
| `RotateOntoPlane(planeNormal, upDirection)` | Rotates by the rotation mapping `upDirection` onto `planeNormal` — slope alignment. |
| `Quantize(Vector3 step)` | **Floors** to the step grid; it does not round to nearest. Zero step components divide by zero. |

`Vector3.RandomPointInAnnulus` flattens onto the XZ plane (Y stays at the origin's height); the Vector2
version works in its own 2D plane.

---

## Transform

`TransformExtensions`

| Method | Notes |
|---|---|
| `InRangeOf(target, maxDistance, maxAngle = 360f)` | Distance **and** FOV cone. Direction is flattened onto XZ — ground-based checks only, Y is ignored. |
| `Children()` | Direct children as `IEnumerable<Transform>` for LINQ. Allocates an iterator. |
| `Reset()` | Resets localPosition / localRotation / localScale. |
| `DestroyChildren()` | Deferred (`Object.Destroy`, end of frame). |
| `DestroyChildrenImmediate()` | Immediate — edit-mode safe. |
| `EnableChildren()` / `DisableChildren()` | `SetActive` on every direct child. |
| `ForEveryChild(Action<Transform>)` | Iterates in **reverse** so the action may destroy or reparent children safely. |

---

## GameObject

`GameObjectExtensions`

| Method | Notes |
|---|---|
| `GetOrAdd<T>()` | Gets the component, adding one if absent. |
| `OrNull<T>()` | Turns Unity's fake-null (destroyed object) into a real null so `?.` and `??` behave. See the gotcha below. |
| `HideInHierarchy()` | Sets `hideFlags` — does not deactivate the object. |
| `DestroyChildren()` / `DestroyChildrenImmediate()` | As above. |
| `EnableChildren()` / `DisableChildren()` | As above. |
| `ResetTransformation()` | Same operation as `Transform.Reset()` — the two names differ for historical reasons. |
| `Path()` | Path of the **parent**, e.g. `/Root/Enemies`. `/` for a root object. |
| `PathFull()` | Path **including** this object, e.g. `/Root/Enemies/Goblin`. Allocates; fine for logging, not for hot paths. |
| `SetLayersRecursively(int layer)` | Sets the layer on this object and every descendant. |
| `IsInLayerMask(LayerMask mask)` | Layer-vs-mask test. |

Both path methods work on inactive objects.

---

## Component

`ComponentExtensions`

| Method | Notes |
|---|---|
| `GetOrAdd<T>()` | Forwards to `GameObjectExtensions.GetOrAdd<T>` so you can call it from a `Component` reference. |

---

## Unity object liveness

`UnityObjectExtensions`

| Method | Notes |
|---|---|
| `IsUnityNull(this object)` | True when the reference is CLR-null **or** a destroyed `UnityEngine.Object`. Non-Unity objects are always "alive". |

### The gotcha this exists for

Unity overloads `==` on `UnityEngine.Object` so a destroyed object compares equal to null. That overload
only fires when the **static type** is `UnityEngine.Object` or a subclass. Reach the object through an
interface or `object` and you get plain reference comparison, so a destroyed component tests as non-null:

```csharp
IInteractable target = hit.GetComponent<IInteractable>();
Destroy((target as Component).gameObject);

if (target != null) target.Interact();   // TRUE — and throws
if (!target.IsUnityNull()) target.Interact();   // correct
```

`OrNull<T>()` solves the same problem but is constrained to `where T : Object`, so it cannot take an
interface-typed reference. Use `OrNull` for concrete types, `IsUnityNull` for interfaces.

---

## Numbers

`NumberExtensions`

| Method | Notes |
|---|---|
| `Approx(float other)` | Fluent `Mathf.Approximately`. |
| `IsOdd()` / `IsEven()` | Correct for negative integers. |
| `AtLeast(min)` / `AtMost(max)` | One-sided clamps. `int` and `float` overloads. |
| `Remap(from1, to1, from2, to2)` | **Clamped** to the output range — it will not extrapolate. |

---

## Collections

`CollectionExtensions`

| Method | Notes |
|---|---|
| `IsNullOrEmpty<T>()` | Null-safe emptiness check on `IList<T>`. |
| `Swap<T>(indexA, indexB)` | Mutates in place. |
| `Shuffle<T>()` | Fisher–Yates **in place**, returns the list for chaining. Uses `UnityEngine.Random`. |
| `ForEach<T>(Action<T>)` | On `IEnumerable<T>`. |
| `Random<T>()` | Uniform pick. O(1) for `IList`, reservoir sampling for lazy sequences. Throws on null or empty. |

---

## Color

`ColorExtensions`

| Method | Notes |
|---|---|
| `SetAlpha(float alpha)` | Copy with a new alpha. |
| `Add(Color)` / `Subtract(Color)` | Component-wise, **clamped to 0–1** — unlike the Vector `Add`. |
| `Invert()` | Inverts RGB, keeps alpha. |
| `ToHex()` | `"#RRGGBBAA"`. |
| `ColorExtensions.FromHex(string)` | **Plain static, not an extension** — deliberately, so `string` doesn't gain a colour method in IntelliSense. Throws `ArgumentException` on invalid input. |

---

## LayerMask

`LayerMaskExtensions`

| Method | Notes |
|---|---|
| `Contains(int layerNumber)` | Takes a raw **layer index** (0–31), not another mask. Pair with `gameObject.layer`. |

---

## Rigidbody / Rigidbody2D

`RigidbodyExtensions`, `Rigidbody2DExtensions`

| Method | Notes |
|---|---|
| `ChangeDirection(direction)` | Redirects velocity along `direction`, **preserving speed**. A zero-length direction is a silent no-op. |
| `Stop()` | Zeroes linear and angular velocity. |

Both return the rigidbody for chaining and operate on `linearVelocity`.

---

## CanvasGroup

`CanvasGroupExtensions`

| Method | Notes |
|---|---|
| `SetVisible(bool)` | Sets `alpha`, `interactable` and `blocksRaycasts` together — the three that must move as one. |
| `Show()` / `Hide()` | Aliases for `SetVisible(true/false)`. |

---

## Strings

`StringExtensions`

| Method | Notes |
|---|---|
| `IsNullOrEmpty()` / `IsNullOrWhiteSpace()` | Fluent aliases. |
| `OrEmpty()` | Null becomes `""`. |
| `Shorten(int maxLength)` | Truncates. Null/whitespace input is returned unchanged. |
| `Slice(int startIndex, int endIndex)` | Python-style: a negative `endIndex` counts from the end. Throws on null input or bad indices. |

**Rich text** — TMP tag wrappers: `RichColor`, `RichSize`, `RichBold`, `RichItalic`, `RichUnderline`,
`RichStrikethrough`, `RichFont`, `RichAlign`, `RichGradient`, `RichRotation`, `RichSpace`. They wrap the
string in the corresponding tag and validate nothing — the renderer must support the tag.

---

## Contract differences worth remembering

These pairs look symmetric and are not:

- `Color.Add` / `Color.Subtract` **clamp** to 0–1; `Vector2.Add` / `Vector3.Add` do not.
- `Vector3.Quantize` **floors**; it never rounds to nearest.
- `Vector3.ComponentDivide` guards against divide-by-zero; `Quantize` does not.
- `GameObject.Path()` is the **parent's** path; `PathFull()` includes the object itself.
- `Remap` clamps to the output range; it is not an extrapolating lerp.

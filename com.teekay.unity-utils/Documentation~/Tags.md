# Tags

Hierarchical gameplay tags (`TeekayUtils.Tags`) — the shared vocabulary systems use to describe
state and gate each other without referencing each other's classes: `"Movement.Sprinting"`,
`"Status.Stunned"`, `"Ability.Movement.Dash"`. Modeled on Unreal's GameplayTags, trimmed to the
subset that kills FSM state explosion: interned tags, a ref-counted tag set, and an Inspector
picker. Promoted from Teekay-Unity-Base after proving out under its Character ability layer.

```csharp
using TeekayUtils.Tags;

// Intern once at init, cache the reference — comparisons are reference equality.
static readonly GameplayTag Sprinting = GameplayTag.Get("Movement.Sprinting");
static readonly GameplayTag Movement  = GameplayTag.Get("Movement");

var tags = new TagSet();
tags.Add(Sprinting);              // grant (ref-counted)
tags.Has(Movement);               // true — hierarchy-aware: a child answers a broad query
tags.HasExact(Movement);          // false — the ancestor itself was never granted
tags.Remove(Sprinting);           // release; MUST pair with the Add
```

## GameplayTag

- **`Get(path)`** interns: the same dotted path always returns the same instance, and every
  ancestor is materialized too (`"A.B.C"` creates `"A.B"` and `"A"`, shared with all descendants).
  Call at init and cache; queries never touch strings.
- **`Matches(query)`** is one-directional: `"Movement.Sprinting"` matches query `"Movement"`
  (blocking all of Movement blocks sprint), but `"Movement"` does **not** match query
  `"Movement.Sprinting"` — a broad fact never satisfies a narrow question.
- **`PathMatches(path, query)`** — the same semantics on raw strings, for editor tooling that
  works on unresolved paths. Tests pin it equivalent to `Matches`.
- **`IsValidPath(path, out error)`** — non-throwing validation (empty segments, stray dots);
  `Get` throws `ArgumentException` on the same rules (interning is init-time, a bad path is a
  typo to fix, not a condition to survive).
- Paths are case-sensitive and never normalized. Declare tags as code constants or pick them via
  the Inspector — don't type them per use.

## TagSet

A live tag state, **reference-counted**: two systems both granting `"Movement.Blocked"` and one
releasing must not clear the other's grant — a plain `HashSet` is exactly that bug.

- `Add`/`Remove` must pair. An unbalanced `Remove` logs an error and is ignored — it means two
  owners think they hold the same grant.
- Hierarchy queries are O(1): counts propagate to ancestors on `Add`/`Remove`, so
  `Has("Movement")` while holding `"Movement.Sprinting"` is a dictionary hit, not a scan.
- `HasAny(list)` / `HasAll(list)` — empty/null lists follow the GAS conventions: `HasAny` = false,
  `HasAll` = true (an empty requirement requires nothing).
- `Explicit` exposes held tags + counts for debug HUDs (struct enumerator, no allocation).
- **No change events by design** — views poll, the way they poll a velocity. This keeps one-shot
  side effects out of simulation code (rollback/resimulation discipline).

## Catalog + Inspector picker

Tags stay plain serialized strings, so a typo'd path is a *valid* tag that matches nothing —
the one failure raw strings cannot report. The editor half fixes that:

- **`GameplayTagCatalog`** (*Assets ▸ Create ▸ TeekayUtils ▸ Gameplay Tag Catalog*) — the
  project's vocabulary as an asset. Edit-time only: runtime never reads it, a project without one
  still runs. One per project; starts empty and grows from the picker.
- **`[GameplayTag]`** on a serialized `string` (or `string[]` — Unity applies it per element)
  draws a searchable dot-hierarchy dropdown over the catalog, with **New tag…** to coin a path in
  place and a ⚠ icon on values missing from the catalog.

```csharp
[SerializeField, GameplayTag] string[] _blockedWhile = { "Status.Stunned" };

void Awake()
{
    _blocked = new GameplayTag[_blockedWhile.Length];
    for (int i = 0; i < _blockedWhile.Length; i++)
        _blocked[i] = GameplayTag.Get(_blockedWhile[i]);   // resolve once, then never per frame
}
```

## Rules that keep it honest

- Intern at init, never per frame — `Get` parses strings; cached references compare by pointer.
- Every `Add` has exactly one owner and exactly one `Remove`; the loud error on imbalance is the
  feature, not a nuisance.
- Main-thread only (static registry, no locks) — the same assumption as all Unity script state.

# EventBus

Type-keyed publish/subscribe for gameplay intents, in namespace `TeekayUtils.Events`. The publisher and
the subscribers never reference each other — the event type is the only shared vocabulary.

```csharp
using TeekayUtils.Events;

public struct ScoreChanged : IEvent { public int Delta; public int Total; }

// Scoring system — knows nothing about the UI
EventBus.Publish(new ScoreChanged { Delta = 10, Total = score });

// HUD — knows nothing about the scoring system
void OnEnable()  => EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
void OnDisable() => EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);

void OnScoreChanged(ScoreChanged e) => label.text = e.Total.ToString();
```

---

## API

| Member | Notes |
|---|---|
| `static void Subscribe<T>(Action<T> handler)` | Null handler is a silent no-op. |
| `static void Unsubscribe<T>(Action<T> handler)` | Null handler is a silent no-op. |
| `static void Publish<T>(T evt)` | Dispatches to every handler. |
| `static bool HasListeners<T>()` | True if at least one handler is registered. |
| `static event Action<IEvent> AnyPublished` | Fires for **every** event, after typed handlers. Tooling only. |

All generic parameters are constrained `where T : struct, IEvent`.

`IEvent` is an empty marker interface. The `struct` constraint is the point: publishing takes no
allocation, and events can't accidentally carry mutable shared references.

---

## Guarantees

**Handlers may subscribe, unsubscribe, or publish during dispatch.** `Publish` snapshots the invocation
list before calling anyone, so a handler that unsubscribes itself — or spawns an object that subscribes —
won't corrupt the iteration or skip a sibling.

**One throwing handler never stops the rest.** Each handler is invoked inside a try/catch; an exception is
logged via `Debug.LogError` and dispatch continues. A broken HUD widget cannot take down the audio system
listening to the same event.

**Handler order is unspecified.** Do not build sequencing on subscription order — if two systems must run
in order, that ordering belongs in one of them, not in the bus.

---

## Lifetime

Subscriptions are static and live until removed. **Always pair `Subscribe` in `OnEnable` with
`Unsubscribe` in `OnDisable`.** A destroyed MonoBehaviour that never unsubscribed leaves a handler
pointing at a dead object, and the next publish throws inside it (logged, not fatal — but it is a leak
and it fires forever).

Subscriptions do **not** survive a play session: the bus clears itself on
`RuntimeInitializeLoadType.SubsystemRegistration`, so disabling domain reload can't carry stale handlers
from the previous run into the next one.

---

## `AnyPublished`

A debugging hook — an event inspector, or a line in the dev console:

```csharp
EventBus.AnyPublished += e => DevConsole.Log("Events", e.GetType().Name);
```

It receives every event as `IEvent`, which **boxes** the struct. That cost only exists while something is
subscribed; the normal typed path stays allocation-free. Leave it unsubscribed in builds.

---

## When a direct call is better

The bus buys decoupling, and decoupling costs traceability: "who handles this?" becomes a search instead
of a click-through. Use it where the answer is genuinely open-ended — one event, unknown number of
listeners, none of which the publisher should know about.

For a system calling into one known collaborator, a direct method call is clearer and cheaper. Not
everything needs to be an event.

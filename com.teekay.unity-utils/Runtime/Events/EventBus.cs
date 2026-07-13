using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeekayUtils.Events
{
    /// <summary>
    /// A type-keyed publish/subscribe hub for decoupled gameplay intents. A publisher raises an event
    /// value (<c>EventBus.Publish(new SpawnCubeRequest())</c>) without knowing who listens; subscribers
    /// register a handler for the event type they care about. Keyed by the event's <see cref="Type"/>,
    /// so it's compile-time type-safe — a wrong event type is a build error, not a runtime surprise.
    ///
    /// <para><b>Scope.</b> Use the bus for <i>gameplay intents</i> that are many-to-many and don't
    /// belong to one system (spawn requests, score changes, item pickups). Keep using per-class static
    /// events (e.g. <c>ViewManager.ViewSwitching</c>, <c>PoolManager.PoolCreated</c>) for
    /// <i>framework-level</i> signals that have one clear owner — those stay more discoverable.</para>
    ///
    /// <para><b>Lifecycle.</b> Always <see cref="Unsubscribe{T}"/> in <c>OnDisable</c>/<c>OnDestroy</c>
    /// what you <see cref="Subscribe{T}"/> in <c>OnEnable</c>; the bus holds delegates, so a forgotten
    /// unsubscribe leaks the listener (and can null-ref after it's destroyed). Static state is reset on
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> so handlers don't survive across
    /// play sessions when domain reload is disabled.</para>
    ///
    /// <para><b>Guarantees.</b> Handler order is unspecified — don't rely on it. Each handler is invoked
    /// inside its own try/catch so one throwing listener can't stop the rest. The invocation list is
    /// snapshotted before dispatch, so a handler may safely subscribe/unsubscribe (or publish) during
    /// a publish without corrupting iteration.</para>
    /// </summary>
    public static class EventBus
    {
        // Type → combined Action<T> for that event type, stored as a base Delegate and cast on use.
        static readonly Dictionary<Type, Delegate> s_handlers = new();

        /// <summary>
        /// Fired for every published event (boxed to <see cref="IEvent"/>), after its typed handlers
        /// run. For tooling only — e.g. the DevConsole bridge logs the whole event stream through this.
        /// Boxing happens only when something is subscribed here, so normal publishing stays alloc-free.
        /// </summary>
        public static event Action<IEvent> AnyPublished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            s_handlers.Clear();
            AnyPublished = null;
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (s_handlers.TryGetValue(key, out Delegate existing))
                s_handlers[key] = (Action<T>)existing + handler;
            else
                s_handlers[key] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (!s_handlers.TryGetValue(key, out Delegate existing))
                return;

            Delegate remaining = (Action<T>)existing - handler;
            if (remaining == null)
                s_handlers.Remove(key);
            else
                s_handlers[key] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct, IEvent
        {
            if (s_handlers.TryGetValue(typeof(T), out Delegate existing) && existing is Action<T> typed)
            {
                // GetInvocationList returns a copy, so a handler that subscribes/unsubscribes (or
                // publishes) mid-dispatch can't corrupt this loop. Each handler is isolated so one
                // throwing listener doesn't cancel the rest.
                Delegate[] invocationList = typed.GetInvocationList();
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        ((Action<T>)invocationList[i]).Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Handler for {typeof(T).Name} threw: {ex}");
                    }
                }
            }

            // Tooling hook — only boxes when a listener (e.g. the DevConsole bridge) is attached.
            AnyPublished?.Invoke(evt);
        }

        /// <summary>True if at least one handler is registered for <typeparamref name="T"/>.</summary>
        public static bool HasListeners<T>() where T : struct, IEvent
        {
            return s_handlers.ContainsKey(typeof(T));
        }
    }
}

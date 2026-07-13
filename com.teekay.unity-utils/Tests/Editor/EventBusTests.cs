using System.Text.RegularExpressions;
using NUnit.Framework;
using TeekayUtils.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    /// EventBus state is static, so every test unsubscribes what it subscribes
    /// (and TearDown double-checks nothing leaked into the next test).
    public class EventBusTests
    {
        struct TestEvent : IEvent
        {
            public int Value;
        }

        struct OtherEvent : IEvent { }

        [Test]
        public void Publish_InvokesSubscribedHandlerWithValue()
        {
            int received = -1;
            void Handler(TestEvent e) => received = e.Value;

            EventBus.Subscribe<TestEvent>(Handler);
            try
            {
                EventBus.Publish(new TestEvent { Value = 42 });
                Assert.That(received, Is.EqualTo(42));
            }
            finally { EventBus.Unsubscribe<TestEvent>(Handler); }
        }

        [Test]
        public void Publish_MultipleHandlers_AllInvoked()
        {
            int a = 0, b = 0;
            void HandlerA(TestEvent e) => a++;
            void HandlerB(TestEvent e) => b++;

            EventBus.Subscribe<TestEvent>(HandlerA);
            EventBus.Subscribe<TestEvent>(HandlerB);
            try
            {
                EventBus.Publish(new TestEvent());
                Assert.That(a, Is.EqualTo(1));
                Assert.That(b, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe<TestEvent>(HandlerA);
                EventBus.Unsubscribe<TestEvent>(HandlerB);
            }
        }

        [Test]
        public void Unsubscribe_RemovedHandlerNotInvoked_AndListenersTracked()
        {
            int calls = 0;
            void Handler(TestEvent e) => calls++;

            EventBus.Subscribe<TestEvent>(Handler);
            Assert.That(EventBus.HasListeners<TestEvent>(), Is.True);

            EventBus.Unsubscribe<TestEvent>(Handler);
            Assert.That(EventBus.HasListeners<TestEvent>(), Is.False);

            EventBus.Publish(new TestEvent());
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Publish_EventTypesAreIsolated()
        {
            int calls = 0;
            void Handler(TestEvent e) => calls++;

            EventBus.Subscribe<TestEvent>(Handler);
            try
            {
                EventBus.Publish(new OtherEvent());
                Assert.That(calls, Is.Zero);
            }
            finally { EventBus.Unsubscribe<TestEvent>(Handler); }
        }

        [Test]
        public void Publish_ThrowingHandler_DoesNotStopOthers()
        {
            int survivorCalls = 0;
            void Thrower(TestEvent e) => throw new System.InvalidOperationException("boom");
            void Survivor(TestEvent e) => survivorCalls++;

            EventBus.Subscribe<TestEvent>(Thrower);
            EventBus.Subscribe<TestEvent>(Survivor);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[EventBus\] Handler for TestEvent threw"));
                EventBus.Publish(new TestEvent());
                Assert.That(survivorCalls, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe<TestEvent>(Thrower);
                EventBus.Unsubscribe<TestEvent>(Survivor);
            }
        }

        [Test]
        public void Publish_HandlerSubscribingDuringDispatch_IsNotInvokedThisPublish()
        {
            int lateCalls = 0;
            bool lateSubscribed = false;
            void LateHandler(TestEvent e) => lateCalls++;
            // One-shot: without the guard this would subscribe LateHandler once per
            // publish and leak a handler into later tests (EventBus state is static).
            void SubscribingHandler(TestEvent e)
            {
                if (lateSubscribed) return;
                lateSubscribed = true;
                EventBus.Subscribe<TestEvent>(LateHandler);
            }

            EventBus.Subscribe<TestEvent>(SubscribingHandler);
            try
            {
                EventBus.Publish(new TestEvent());
                Assert.That(lateCalls, Is.Zero, "snapshot dispatch: late subscriber must wait for the next publish");

                EventBus.Publish(new TestEvent());
                Assert.That(lateCalls, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe<TestEvent>(SubscribingHandler);
                EventBus.Unsubscribe<TestEvent>(LateHandler);
            }

            Assert.That(EventBus.HasListeners<TestEvent>(), Is.False, "test must leave no leaked handlers");
        }

        [Test]
        public void AnyPublished_ReceivesBoxedEvent()
        {
            IEvent seen = null;
            void Hook(IEvent e) => seen = e;

            EventBus.AnyPublished += Hook;
            try
            {
                EventBus.Publish(new TestEvent { Value = 7 });
                Assert.That(seen, Is.InstanceOf<TestEvent>());
                Assert.That(((TestEvent)seen).Value, Is.EqualTo(7));
            }
            finally { EventBus.AnyPublished -= Hook; }
        }

        [Test]
        public void SubscribeNull_IsIgnored()
        {
            EventBus.Subscribe<TestEvent>(null);
            Assert.That(EventBus.HasListeners<TestEvent>(), Is.False);
            EventBus.Unsubscribe<TestEvent>(null); // must not throw
        }
    }
}

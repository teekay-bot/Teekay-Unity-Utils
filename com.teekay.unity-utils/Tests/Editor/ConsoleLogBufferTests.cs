using NUnit.Framework;
using TeekayUtils.DevConsole;
using UnityEngine;

namespace TeekayUtils.Tests
{
    public class ConsoleLogBufferTests
    {
        static ConsoleLogEntry Entry(string category, string message, float timestamp = 0f)
            => new(category, message, Color.white, timestamp);

        [Test]
        public void Append_DistinctLines_AddsRows()
        {
            var buffer = new ConsoleLogBuffer();

            Assert.That(buffer.Append(Entry("A", "one"), 100), Is.True);
            Assert.That(buffer.Append(Entry("A", "two"), 100), Is.True);

            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer[0].Message, Is.EqualTo("one"));
            Assert.That(buffer[1].Message, Is.EqualTo("two"));
        }

        [Test]
        public void Append_ConsecutiveIdenticalLines_CollapsesIntoCount()
        {
            var buffer = new ConsoleLogBuffer();

            buffer.Append(Entry("A", "spam", 1f), 100);
            Assert.That(buffer.Append(Entry("A", "spam", 2f), 100), Is.False);
            Assert.That(buffer.Append(Entry("A", "spam", 3f), 100), Is.False);

            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer[0].Count, Is.EqualTo(3));
            Assert.That(buffer[0].Timestamp, Is.EqualTo(3f), "collapse should refresh the timestamp");
        }

        [Test]
        public void Append_SameMessageDifferentCategory_DoesNotCollapse()
        {
            var buffer = new ConsoleLogBuffer();

            buffer.Append(Entry("A", "same"), 100);
            buffer.Append(Entry("B", "same"), 100);

            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void Append_NonConsecutiveRepeat_DoesNotCollapse()
        {
            var buffer = new ConsoleLogBuffer();

            buffer.Append(Entry("A", "x"), 100);
            buffer.Append(Entry("A", "y"), 100);
            buffer.Append(Entry("A", "x"), 100);

            Assert.That(buffer.Count, Is.EqualTo(3), "only CONSECUTIVE duplicates collapse");
        }

        [Test]
        public void Append_OverCapacity_DropsOldest()
        {
            var buffer = new ConsoleLogBuffer();
            for (int i = 0; i < 10; i++) buffer.Append(Entry("A", $"line{i}"), 5);

            Assert.That(buffer.Count, Is.EqualTo(5));
            Assert.That(buffer[0].Message, Is.EqualTo("line5"));
            Assert.That(buffer[4].Message, Is.EqualTo("line9"));
        }

        [Test]
        public void Sequence_IsStableAcrossCollapseAndTrim()
        {
            var buffer = new ConsoleLogBuffer();
            buffer.Append(Entry("A", "first"), 3);
            buffer.Append(Entry("A", "second"), 3);
            long secondSeq = buffer[1].Sequence;

            buffer.Append(Entry("A", "second"), 3); // collapse — sequence must not change
            Assert.That(buffer[1].Sequence, Is.EqualTo(secondSeq));

            buffer.Append(Entry("A", "third"), 3);
            buffer.Append(Entry("A", "fourth"), 3); // trims "first"
            Assert.That(buffer[0].Sequence, Is.EqualTo(secondSeq),
                "trimming the front must not renumber surviving entries");
        }

        [Test]
        public void Sequences_AreUniqueAndIncreasing()
        {
            var buffer = new ConsoleLogBuffer();
            for (int i = 0; i < 5; i++) buffer.Append(Entry("A", $"m{i}"), 100);

            for (int i = 1; i < buffer.Count; i++)
                Assert.That(buffer[i].Sequence, Is.GreaterThan(buffer[i - 1].Sequence));
        }

        [Test]
        public void Clear_EmptiesBuffer_AndCollapseDoesNotResurrect()
        {
            var buffer = new ConsoleLogBuffer();
            buffer.Append(Entry("A", "spam"), 100);
            buffer.Clear();

            Assert.That(buffer.Count, Is.EqualTo(0));
            // The pre-clear "spam" must not be collapse-matched after the clear.
            Assert.That(buffer.Append(Entry("A", "spam"), 100), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer[0].Count, Is.EqualTo(1));
        }
    }
}

using System.Linq;
using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class RingBufferTests
    {
        [Test]
        public void Create_Rejects_NonPositive_Capacity()
        {
            Assert.Throws<CollectionException>(() => RingBuffer<int>.Create(0));
            Assert.Throws<CollectionException>(() => RingBuffer<int>.Create(-1));
        }

        [Test]
        public void Empty_TryDequeue_And_TryPeek_Return_False()
        {
            var buffer = RingBuffer<int>.Create(3);

            Assert.IsTrue(buffer.IsEmpty);
            Assert.IsFalse(buffer.TryDequeue(out _));
            Assert.IsFalse(buffer.TryPeek(out _));
            Assert.IsFalse(buffer.TryPeekLast(out _));
        }

        [Test]
        public void Enqueue_Then_Dequeue_Is_Fifo()
        {
            var buffer = RingBuffer<int>.Create(3);
            buffer.Enqueue(1);
            buffer.Enqueue(2);
            buffer.Enqueue(3);

            Assert.IsTrue(buffer.IsFull);
            Assert.AreEqual(3, buffer.Count);

            Assert.IsTrue(buffer.TryDequeue(out var a));
            Assert.IsTrue(buffer.TryDequeue(out var b));
            Assert.IsTrue(buffer.TryDequeue(out var c));
            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual(3, c);
        }

        [Test]
        public void Enqueue_When_Full_Overwrites_Oldest()
        {
            var buffer = RingBuffer<int>.Create(3);
            buffer.Enqueue(1);
            buffer.Enqueue(2);
            buffer.Enqueue(3);

            var overwritten = buffer.Enqueue(4, out var old);
            Assert.IsTrue(overwritten);
            Assert.AreEqual(1, old);

            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, buffer.ToArray());
            Assert.IsTrue(buffer.TryPeek(out var oldest));
            Assert.IsTrue(buffer.TryPeekLast(out var newest));
            Assert.AreEqual(2, oldest);
            Assert.AreEqual(4, newest);
        }

        [Test]
        public void Indexer_Reports_LogicalOrder_And_Throws_OutOfRange()
        {
            var buffer = RingBuffer<int>.Create(3);
            buffer.Enqueue(1);
            buffer.Enqueue(2);
            buffer.Enqueue(3);
            buffer.Enqueue(4); // overwrite -> 2,3,4

            Assert.AreEqual(2, buffer[0]);
            Assert.AreEqual(4, buffer[2]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = buffer[3]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }

        [Test]
        public void Clear_Empties_But_Keeps_Capacity()
        {
            var buffer = RingBuffer<int>.Create(3);
            buffer.Enqueue(1);
            buffer.Enqueue(2);

            buffer.Clear();

            Assert.IsTrue(buffer.IsEmpty);
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(3, buffer.Capacity);
        }
    }
}

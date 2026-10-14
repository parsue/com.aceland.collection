using System.Linq;
using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class DequeTests
    {
        [Test]
        public void Create_Fixed_Rejects_NonPositive_Capacity()
        {
            Assert.Throws<CollectionException>(() => Deque<int>.Create(0));
            Assert.Throws<CollectionException>(() => Deque<int>.Create(-5));
        }

        [Test]
        public void Empty_Remove_And_Peek_Return_False()
        {
            var deque = Deque<int>.Create();

            Assert.IsTrue(deque.IsEmpty);
            Assert.IsFalse(deque.TryRemoveFirst(out _));
            Assert.IsFalse(deque.TryRemoveLast(out _));
            Assert.IsFalse(deque.TryPeekFirst(out _));
            Assert.IsFalse(deque.TryPeekLast(out _));
        }

        [Test]
        public void AddFirst_AddLast_Preserve_Order()
        {
            var deque = Deque<int>.Create();
            Assert.IsTrue(deque.TryAddLast(2));
            Assert.IsTrue(deque.TryAddLast(3));
            Assert.IsTrue(deque.TryAddFirst(1));
            Assert.IsTrue(deque.TryAddFirst(0));

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, deque.ToArray());
            Assert.AreEqual(0, deque[0]);
            Assert.AreEqual(3, deque[3]);
        }

        [Test]
        public void Unbounded_AutoGrows_Beyond_DefaultCapacity()
        {
            var deque = Deque<int>.Create();
            for (var i = 0; i < 100; i++)
                Assert.IsTrue(deque.TryAddLast(i));

            Assert.AreEqual(100, deque.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, 100).ToArray(), deque.ToArray());
        }

        [Test]
        public void Fixed_Capacity_Rejects_When_Full()
        {
            var deque = Deque<int>.Create(2);
            Assert.IsTrue(deque.TryAddLast(1));
            Assert.IsTrue(deque.TryAddLast(2));
            Assert.IsTrue(deque.IsFull);

            Assert.IsFalse(deque.TryAddLast(3));
            Assert.IsFalse(deque.TryAddFirst(0));
            Assert.AreEqual(2, deque.Count);
        }

        [Test]
        public void Remove_From_Both_Ends()
        {
            var deque = Deque<int>.Create();
            foreach (var i in new[] { 1, 2, 3, 4 })
                deque.TryAddLast(i);

            Assert.IsTrue(deque.TryRemoveFirst(out var first));
            Assert.IsTrue(deque.TryRemoveLast(out var last));
            Assert.AreEqual(1, first);
            Assert.AreEqual(4, last);
            CollectionAssert.AreEqual(new[] { 2, 3 }, deque.ToArray());
        }

        [Test]
        public void Indexer_Throws_OutOfRange()
        {
            var deque = Deque<int>.Create();
            deque.TryAddLast(1);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = deque[1]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = deque[-1]);
        }
    }
}

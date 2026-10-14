using System.Collections.Generic;
using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class IndexedPriorityQueueTests
    {
        [Test]
        public void Empty_TryPeek_And_TryDequeue_Return_False()
        {
            var pq = IndexedPriorityQueue<string, int>.Create();

            Assert.IsTrue(pq.IsEmpty);
            Assert.AreEqual(0, pq.Count);
            Assert.IsFalse(pq.TryPeek(out _, out _));
            Assert.IsFalse(pq.TryDequeue(out _, out _));
        }

        [Test]
        public void Create_Custom_Comparer_Rejects_Null()
        {
            Assert.Throws<CollectionException>(
                () => IndexedPriorityQueue<string, int>.Create(HeapMode.Min, null));
        }

        [Test]
        public void Min_Dequeues_In_Ascending_Priority()
        {
            var pq = IndexedPriorityQueue<string, int>.Create(HeapMode.Min);
            pq.Enqueue("c", 3);
            pq.Enqueue("a", 1);
            pq.Enqueue("b", 2);

            Assert.IsTrue(pq.TryPeek(out var item, out var priority));
            Assert.AreEqual("a", item);
            Assert.AreEqual(1, priority);

            var order = new List<string>();
            while (pq.TryDequeue(out var it, out _))
                order.Add(it);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, order);
        }

        [Test]
        public void Max_Dequeues_In_Descending_Priority()
        {
            var pq = IndexedPriorityQueue<string, int>.Create(HeapMode.Max);
            pq.Enqueue("a", 1);
            pq.Enqueue("c", 3);
            pq.Enqueue("b", 2);

            var order = new List<string>();
            while (pq.TryDequeue(out var it, out _))
                order.Add(it);
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, order);
        }

        [Test]
        public void Enqueue_Duplicate_Item_Returns_False()
        {
            var pq = IndexedPriorityQueue<string, int>.Create();
            Assert.IsTrue(pq.Enqueue("a", 1));
            Assert.IsFalse(pq.Enqueue("a", 5));
            Assert.AreEqual(1, pq.Count);
        }

        [Test]
        public void UpdatePriority_Reorders_Heap()
        {
            var pq = IndexedPriorityQueue<string, int>.Create(HeapMode.Min);
            pq.Enqueue("a", 1);
            pq.Enqueue("b", 2);
            pq.Enqueue("c", 3);

            // Push 'c' to the front.
            Assert.IsTrue(pq.UpdatePriority("c", 0));
            Assert.IsTrue(pq.TryPeek(out var item, out var priority));
            Assert.AreEqual("c", item);
            Assert.AreEqual(0, priority);

            // Demote 'a' to the back.
            Assert.IsTrue(pq.UpdatePriority("a", 10));
            var order = new List<string>();
            while (pq.TryDequeue(out var it, out _))
                order.Add(it);
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, order);
        }

        [Test]
        public void UpdatePriority_Absent_Returns_False()
        {
            var pq = IndexedPriorityQueue<string, int>.Create();
            Assert.IsFalse(pq.UpdatePriority("x", 1));
        }

        [Test]
        public void EnqueueOrUpdate_Adds_Then_Updates()
        {
            var pq = IndexedPriorityQueue<string, int>.Create(HeapMode.Min);
            Assert.IsTrue(pq.EnqueueOrUpdate("a", 5));
            Assert.IsFalse(pq.EnqueueOrUpdate("a", 1));

            Assert.IsTrue(pq.TryGetPriority("a", out var p));
            Assert.AreEqual(1, p);
        }

        [Test]
        public void Remove_Pulls_Item_From_Middle()
        {
            var pq = IndexedPriorityQueue<string, int>.Create(HeapMode.Min);
            pq.Enqueue("a", 1);
            pq.Enqueue("b", 2);
            pq.Enqueue("c", 3);

            Assert.IsTrue(pq.Remove("b"));
            Assert.IsFalse(pq.Contains("b"));
            Assert.AreEqual(2, pq.Count);

            var order = new List<string>();
            while (pq.TryDequeue(out var it, out _))
                order.Add(it);
            CollectionAssert.AreEqual(new[] { "a", "c" }, order);
        }

        [Test]
        public void Remove_Absent_Returns_False()
        {
            var pq = IndexedPriorityQueue<string, int>.Create();
            pq.Enqueue("a", 1);
            Assert.IsFalse(pq.Remove("z"));
        }

        [Test]
        public void Clear_Empties_Queue()
        {
            var pq = IndexedPriorityQueue<string, int>.Create();
            pq.Enqueue("a", 1);
            pq.Enqueue("b", 2);
            pq.Clear();

            Assert.IsTrue(pq.IsEmpty);
            Assert.IsFalse(pq.Contains("a"));
        }

        [Test]
        public void Heap_Property_Holds_Under_Random_Operations()
        {
            var pq = IndexedPriorityQueue<int, int>.Create(HeapMode.Min);
            var rng = new System.Random(1234);
            var reference = new Dictionary<int, int>();

            for (var i = 0; i < 200; i++)
            {
                var key = rng.Next(0, 50);
                var priority = rng.Next(0, 1000);
                pq.EnqueueOrUpdate(key, priority);
                reference[key] = priority;
            }

            // Drain and confirm ascending priority + matches reference.
            var last = int.MinValue;
            while (pq.TryDequeue(out var item, out var priority))
            {
                Assert.GreaterOrEqual(priority, last);
                last = priority;
                Assert.AreEqual(reference[item], priority);
                reference.Remove(item);
            }

            Assert.AreEqual(0, reference.Count);
        }
    }
}

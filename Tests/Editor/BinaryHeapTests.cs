using System.Collections.Generic;
using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class BinaryHeapTests
    {
        [Test]
        public void Empty_TryPeek_And_TryPop_Return_False()
        {
            var heap = BinaryHeap<int>.Create();

            Assert.IsTrue(heap.IsEmpty);
            Assert.IsFalse(heap.TryPeek(out _));
            Assert.IsFalse(heap.TryPop(out _));
        }

        [Test]
        public void Create_Custom_Comparer_Rejects_Null()
        {
            Assert.Throws<CollectionException>(() => BinaryHeap<int>.Create(HeapMode.Min, null));
        }

        [Test]
        public void MinHeap_Pops_Ascending()
        {
            var heap = BinaryHeap<int>.Create(HeapMode.Min);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                heap.Push(v);

            Assert.AreEqual(HeapMode.Min, heap.Mode);
            Assert.IsTrue(heap.TryPeek(out var top));
            Assert.AreEqual(1, top);

            var result = new List<int>();
            while (heap.TryPop(out var v))
                result.Add(v);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, result);
        }

        [Test]
        public void MaxHeap_Pops_Descending()
        {
            var heap = BinaryHeap<int>.Create(HeapMode.Max);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                heap.Push(v);

            var result = new List<int>();
            while (heap.TryPop(out var v))
                result.Add(v);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, result);
        }

        [Test]
        public void Custom_Comparer_Controls_Order()
        {
            // Reverse comparer with a Min heap => behaves like a Max heap.
            var reverse = Comparer<int>.Create((a, b) => b.CompareTo(a));
            var heap = BinaryHeap<int>.Create(HeapMode.Min, reverse);
            foreach (var v in new[] { 1, 3, 2 })
                heap.Push(v);

            Assert.IsTrue(heap.TryPop(out var top));
            Assert.AreEqual(3, top);
        }

        [Test]
        public void Clear_Empties_Heap()
        {
            var heap = BinaryHeap<int>.Create();
            heap.Push(1);
            heap.Push(2);

            heap.Clear();

            Assert.IsTrue(heap.IsEmpty);
            Assert.AreEqual(0, heap.Count);
        }
    }
}

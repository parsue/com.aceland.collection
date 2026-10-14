using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace AceLand.Collection.Dots.Tests
{
    public sealed class NativeBinaryHeapTests
    {
        // Value-type comparers: ascending => min-heap, descending => max-heap.
        private struct AscendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => a.CompareTo(b);
        }

        private struct DescendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => b.CompareTo(a);
        }

        [Test]
        public void Empty_TryPeek_And_TryPop_Return_False()
        {
            using var heap = new NativeBinaryHeap<int, AscendingInt>(Allocator.Temp, default);

            Assert.IsTrue(heap.IsEmpty);
            Assert.IsFalse(heap.TryPeek(out _));
            Assert.IsFalse(heap.TryPop(out _));
        }

        [Test]
        public void MinHeap_Pops_Ascending()
        {
            using var heap = new NativeBinaryHeap<int, AscendingInt>(Allocator.Temp, default);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                heap.Push(v);

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
            using var heap = new NativeBinaryHeap<int, DescendingInt>(Allocator.Temp, default);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                heap.Push(v);

            var result = new List<int>();
            while (heap.TryPop(out var v))
                result.Add(v);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, result);
        }

        [Test]
        public void Clear_Empties_Heap()
        {
            using var heap = new NativeBinaryHeap<int, AscendingInt>(Allocator.Temp, default);
            heap.Push(1);
            heap.Push(2);

            heap.Clear();

            Assert.IsTrue(heap.IsEmpty);
            Assert.AreEqual(0, heap.Count);
        }

        [Test]
        public void Count_Tracks_Pushes_And_Pops()
        {
            using var heap = new NativeBinaryHeap<int, AscendingInt>(Allocator.Temp, default);
            heap.Push(10);
            heap.Push(20);
            heap.Push(5);
            Assert.AreEqual(3, heap.Count);

            heap.TryPop(out _);
            Assert.AreEqual(2, heap.Count);
        }
    }
}

using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class MinMaxHeapTests
    {
        [Test]
        public void Empty_Peek_And_Pop_Return_False()
        {
            var heap = MinMaxHeap<int>.Create();

            Assert.IsTrue(heap.IsEmpty);
            Assert.IsFalse(heap.TryPeekMin(out _));
            Assert.IsFalse(heap.TryPeekMax(out _));
            Assert.IsFalse(heap.TryPopMin(out _));
            Assert.IsFalse(heap.TryPopMax(out _));
        }

        [Test]
        public void Create_Custom_Comparer_Rejects_Null()
        {
            Assert.Throws<CollectionException>(() => MinMaxHeap<int>.Create(null));
        }

        [Test]
        public void Single_Element_Is_Both_Min_And_Max()
        {
            var heap = MinMaxHeap<int>.Create();
            heap.Push(42);

            Assert.IsTrue(heap.TryPeekMin(out var min));
            Assert.IsTrue(heap.TryPeekMax(out var max));
            Assert.AreEqual(42, min);
            Assert.AreEqual(42, max);
        }

        [Test]
        public void Peek_Reports_Correct_Extremes()
        {
            var heap = MinMaxHeap<int>.Create();
            foreach (var v in new[] { 5, 1, 9, 3, 7, 2, 8 })
                heap.Push(v);

            Assert.IsTrue(heap.TryPeekMin(out var min));
            Assert.IsTrue(heap.TryPeekMax(out var max));
            Assert.AreEqual(1, min);
            Assert.AreEqual(9, max);
        }

        [Test]
        public void PopMin_Yields_Ascending()
        {
            var heap = MinMaxHeap<int>.Create();
            foreach (var v in new[] { 5, 1, 9, 3, 7, 2, 8, 4, 6 })
                heap.Push(v);

            var prev = int.MinValue;
            var popped = 0;
            while (heap.TryPopMin(out var v))
            {
                Assert.GreaterOrEqual(v, prev);
                prev = v;
                popped++;
            }
            Assert.AreEqual(9, popped);
        }

        [Test]
        public void PopMax_Yields_Descending()
        {
            var heap = MinMaxHeap<int>.Create();
            foreach (var v in new[] { 5, 1, 9, 3, 7, 2, 8, 4, 6 })
                heap.Push(v);

            var prev = int.MaxValue;
            var popped = 0;
            while (heap.TryPopMax(out var v))
            {
                Assert.LessOrEqual(v, prev);
                prev = v;
                popped++;
            }
            Assert.AreEqual(9, popped);
        }

        [Test]
        public void Interleaved_PopMin_PopMax_Converges()
        {
            var heap = MinMaxHeap<int>.Create();
            foreach (var v in new[] { 10, 20, 30, 40, 50 })
                heap.Push(v);

            Assert.IsTrue(heap.TryPopMin(out var lo));
            Assert.IsTrue(heap.TryPopMax(out var hi));
            Assert.AreEqual(10, lo);
            Assert.AreEqual(50, hi);
            Assert.AreEqual(3, heap.Count);

            Assert.IsTrue(heap.TryPeekMin(out var min));
            Assert.IsTrue(heap.TryPeekMax(out var max));
            Assert.AreEqual(20, min);
            Assert.AreEqual(40, max);
        }
    }
}

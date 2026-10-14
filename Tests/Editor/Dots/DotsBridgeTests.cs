using System.Collections.Generic;
using AceLand.Collection;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace AceLand.Collection.Dots.Tests
{
    /// <summary>
    /// Round-trip coverage for the P6-3 conversion bridges
    /// (<c>ToNative</c> / <c>ToManaged</c>) between the managed, Unity-serializable
    /// collections and their Burst-friendly native counterparts.
    /// </summary>
    public sealed class DotsBridgeTests
    {
        // Value-type comparer so it can drive both the managed and native heaps.
        private struct AscendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => a.CompareTo(b);
        }

        // ---- BinaryHeap -----------------------------------------------------

        [Test]
        public void BinaryHeap_ToNative_Preserves_Elements_And_Order()
        {
            var managed = BinaryHeap<int>.Create(HeapMode.Min);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                managed.Push(v);

            using var native = managed.ToNative(default(AscendingInt), Allocator.Temp);

            Assert.AreEqual(managed.Count, native.Count);

            var popped = new List<int>();
            while (native.TryPop(out var v))
                popped.Add(v);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, popped);
        }

        [Test]
        public void BinaryHeap_ToManaged_Preserves_Elements_And_Order()
        {
            using var native = new NativeBinaryHeap<int, AscendingInt>(Allocator.Temp, default);
            foreach (var v in new[] { 5, 1, 4, 2, 3 })
                native.Push(v);

            var managed = native.ToManaged(HeapMode.Min);

            Assert.AreEqual(5, managed.Count);

            var popped = new List<int>();
            while (managed.TryPop(out var v))
                popped.Add(v);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, popped);
        }

        [Test]
        public void BinaryHeap_RoundTrip_Is_Stable()
        {
            var managed = BinaryHeap<int>.Create(HeapMode.Min);
            foreach (var v in new[] { 9, 3, 7, 1, 8, 2 })
                managed.Push(v);

            using var native = managed.ToNative(default(AscendingInt), Allocator.Temp);
            var back = native.ToManaged(HeapMode.Min);

            Assert.AreEqual(managed.Count, back.Count);

            var expected = new List<int>();
            while (managed.TryPop(out var v))
                expected.Add(v);
            var actual = new List<int>();
            while (back.TryPop(out var v))
                actual.Add(v);
            CollectionAssert.AreEqual(expected, actual);
        }

        // ---- SpatialHashGrid2D ---------------------------------------------

        [Test]
        public void SpatialHashGrid2D_ToNative_Preserves_Items_And_CellSize()
        {
            var managed = SpatialHashGrid2D<int>.Create(2f);
            managed.Insert(new Vector2(0.5f, 0.5f), 1);
            managed.Insert(new Vector2(3.5f, 1.5f), 2);
            managed.Insert(new Vector2(10f, 10f), 3);

            using var native = managed.ToNative(Allocator.Temp);

            Assert.AreEqual(2f, native.CellSize);
            Assert.AreEqual(managed.Count, native.Count);
            Assert.IsTrue(native.TryGetPosition(1, out var p1));
            Assert.AreEqual(new Unity.Mathematics.float2(0.5f, 0.5f), p1);
            Assert.IsTrue(native.TryGetPosition(2, out _));
            Assert.IsTrue(native.TryGetPosition(3, out _));
        }

        [Test]
        public void SpatialHashGrid2D_RoundTrip_Preserves_Items()
        {
            var managed = SpatialHashGrid2D<int>.Create(1f);
            managed.Insert(new Vector2(0.5f, 0.5f), 1);
            managed.Insert(new Vector2(2.5f, 2.5f), 2);

            using var native = managed.ToNative(Allocator.Temp);
            var back = native.ToManaged();

            Assert.AreEqual(1f, back.CellSize);
            Assert.AreEqual(managed.Count, back.Count);

            back.QueryRect(new Rect(0f, 0f, 3f, 3f), out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        // ---- SpatialHashGrid3D ---------------------------------------------

        [Test]
        public void SpatialHashGrid3D_RoundTrip_Preserves_Items_And_CellSize()
        {
            var managed = SpatialHashGrid3D<int>.Create(2f);
            managed.Insert(new Vector3(0.5f, 0.5f, 0.5f), 1);
            managed.Insert(new Vector3(3.5f, 1.5f, 0.5f), 2);

            using var native = managed.ToNative(Allocator.Temp);
            Assert.AreEqual(2f, native.CellSize);
            Assert.AreEqual(managed.Count, native.Count);

            var back = native.ToManaged();
            Assert.AreEqual(2f, back.CellSize);
            Assert.AreEqual(managed.Count, back.Count);
            Assert.IsTrue(back.Contains(1));
            Assert.IsTrue(back.Contains(2));
        }

        // ---- KdTree2D -------------------------------------------------------

        [Test]
        public void KdTree2D_ToNative_Preserves_Nearest_Query()
        {
            var managed = KdTree2D<int>.Create();
            managed.Insert(new Vector2(0f, 0f), 1);
            managed.Insert(new Vector2(5f, 5f), 2);
            managed.Insert(new Vector2(10f, 0f), 3);

            using var native = managed.ToNative(Allocator.Temp);

            Assert.AreEqual(managed.Count, native.Count);
            Assert.IsTrue(native.TryFindNearest(new Unity.Mathematics.float2(6f, 5f), out var nearest));
            Assert.AreEqual(2, nearest);
        }

        [Test]
        public void KdTree2D_RoundTrip_Preserves_Items()
        {
            var managed = KdTree2D<int>.Create();
            managed.Insert(new Vector2(0f, 0f), 1);
            managed.Insert(new Vector2(1f, 0f), 2);
            managed.Insert(new Vector2(10f, 10f), 3);

            using var native = managed.ToNative(Allocator.Temp);
            var back = native.ToManaged();

            Assert.AreEqual(managed.Count, back.Count);
            Assert.IsTrue(back.Contains(1));
            Assert.IsTrue(back.Contains(2));
            Assert.IsTrue(back.Contains(3));
            Assert.IsTrue(back.TryFindNearest(new Vector2(0.4f, 0f), out var nearest));
            Assert.AreEqual(1, nearest);
        }

        // ---- KdTree3D -------------------------------------------------------

        [Test]
        public void KdTree3D_RoundTrip_Preserves_Items()
        {
            var managed = KdTree3D<int>.Create();
            managed.Insert(new Vector3(0f, 0f, 0f), 1);
            managed.Insert(new Vector3(5f, 5f, 5f), 2);
            managed.Insert(new Vector3(10f, 0f, 0f), 3);

            using var native = managed.ToNative(Allocator.Temp);
            Assert.AreEqual(managed.Count, native.Count);

            var back = native.ToManaged();
            Assert.AreEqual(managed.Count, back.Count);
            Assert.IsTrue(back.Contains(1));
            Assert.IsTrue(back.Contains(2));
            Assert.IsTrue(back.Contains(3));
            Assert.IsTrue(back.TryFindNearest(new Vector3(9f, 0.5f, 0f), out var nearest));
            Assert.AreEqual(3, nearest);
        }

        // ---- Edge cases -----------------------------------------------------

        [Test]
        public void Empty_Sources_RoundTrip_To_Empty()
        {
            var heap = BinaryHeap<int>.Create(HeapMode.Min);
            using var nativeHeap = heap.ToNative(default(AscendingInt), Allocator.Temp);
            Assert.AreEqual(0, nativeHeap.Count);

            var grid = SpatialHashGrid2D<int>.Create(1f);
            using var nativeGrid = grid.ToNative(Allocator.Temp);
            Assert.AreEqual(0, nativeGrid.Count);

            var tree = KdTree2D<int>.Create();
            using var nativeTree = tree.ToNative(Allocator.Temp);
            Assert.AreEqual(0, nativeTree.Count);
        }
    }
}

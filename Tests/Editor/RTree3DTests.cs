using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class RTree3DTests
    {
        private static Bounds Box(Vector3 center, float size) => new(center, new Vector3(size, size, size));

        [Test]
        public void Create_FanOutBelowFour_Throws()
        {
            Assert.Throws<CollectionException>(() => RTree3D<int>.Create(3));
            Assert.Throws<CollectionException>(() => RTree3D<int>.Create(0));
        }

        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() => RTree3D<int>.Create(4, null));
        }

        [Test]
        public void EmptyTree_QueryReturnsNothing()
        {
            var tree = RTree3D<int>.Create();
            tree.Query(Box(Vector3.zero, 100f), out var results);
            CollectionAssert.IsEmpty(results);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var tree = RTree3D<int>.Create();
            Assert.IsTrue(tree.Insert(Box(Vector3.zero, 2f), 1));
            Assert.IsFalse(tree.Insert(Box(new Vector3(5f, 5f, 5f), 2f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void TryGetBounds_ReturnsStoredBounds()
        {
            var tree = RTree3D<int>.Create();
            var bounds = Box(new Vector3(1f, 2f, 3f), 4f);
            tree.Insert(bounds, 1);

            Assert.IsTrue(tree.TryGetBounds(1, out var stored));
            Assert.AreEqual(bounds, stored);
            Assert.IsFalse(tree.TryGetBounds(99, out _));
        }

        [Test]
        public void Query_ReturnsOverlappingEntries()
        {
            var tree = RTree3D<int>.Create();
            tree.Insert(Box(new Vector3(0f, 0f, 0f), 2f), 1);
            tree.Insert(Box(new Vector3(1f, 1f, 1f), 2f), 2);
            tree.Insert(Box(new Vector3(20f, 20f, 20f), 2f), 3);

            tree.Query(Box(new Vector3(0.5f, 0.5f, 0.5f), 1f), out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryPoint_ReturnsContainingEntries()
        {
            var tree = RTree3D<int>.Create();
            tree.Insert(Box(new Vector3(0f, 0f, 0f), 4f), 1);
            tree.Insert(Box(new Vector3(10f, 10f, 10f), 4f), 2);

            tree.QueryPoint(new Vector3(1f, 1f, 1f), out var results);
            CollectionAssert.AreEqual(new[] { 1 }, results);
        }

        [Test]
        public void Insert_ManyEntries_AllQueryable()
        {
            var tree = RTree3D<int>.Create(4);
            for (var i = 0; i < 30; i++)
                Assert.IsTrue(tree.Insert(Box(new Vector3(i, i, i), 1f), i));

            Assert.AreEqual(30, tree.Count);

            tree.Query(Box(new Vector3(15f, 15f, 15f), 60f), out var results);
            Assert.AreEqual(30, results.Count);
        }

        [Test]
        public void Remove_DropsEntry()
        {
            var tree = RTree3D<int>.Create();
            tree.Insert(Box(new Vector3(0f, 0f, 0f), 2f), 1);
            tree.Insert(Box(new Vector3(5f, 5f, 5f), 2f), 2);

            Assert.IsTrue(tree.Remove(1));
            Assert.IsFalse(tree.Contains(1));
            Assert.IsFalse(tree.Remove(1));

            tree.Query(Box(Vector3.zero, 100f), out var results);
            CollectionAssert.AreEqual(new[] { 2 }, results);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var tree = RTree3D<int>.Create();
            tree.Insert(Box(new Vector3(0f, 0f, 0f), 2f), 1);
            tree.Insert(Box(new Vector3(5f, 5f, 5f), 2f), 2);
            tree.Clear();
            Assert.AreEqual(0, tree.Count);
        }
    }
}

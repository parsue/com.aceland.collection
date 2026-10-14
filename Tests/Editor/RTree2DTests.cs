using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class RTree2DTests
    {
        [Test]
        public void Create_FanOutBelowFour_Throws()
        {
            Assert.Throws<CollectionException>(() => RTree2D<int>.Create(3));
            Assert.Throws<CollectionException>(() => RTree2D<int>.Create(0));
        }

        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() => RTree2D<int>.Create(4, null));
        }

        [Test]
        public void EmptyTree_QueryReturnsNothing()
        {
            var tree = RTree2D<int>.Create();
            tree.Query(new Rect(0f, 0f, 100f, 100f), out var results);
            CollectionAssert.IsEmpty(results);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var tree = RTree2D<int>.Create();
            Assert.IsTrue(tree.Insert(new Rect(0f, 0f, 2f, 2f), 1));
            Assert.IsFalse(tree.Insert(new Rect(5f, 5f, 2f, 2f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void TryGetBounds_ReturnsStoredBounds()
        {
            var tree = RTree2D<int>.Create();
            var bounds = new Rect(1f, 2f, 3f, 4f);
            tree.Insert(bounds, 1);

            Assert.IsTrue(tree.TryGetBounds(1, out var stored));
            Assert.AreEqual(bounds, stored);
            Assert.IsFalse(tree.TryGetBounds(99, out _));
        }

        [Test]
        public void Query_ReturnsOverlappingEntries()
        {
            var tree = RTree2D<int>.Create();
            tree.Insert(new Rect(0f, 0f, 2f, 2f), 1);
            tree.Insert(new Rect(1f, 1f, 2f, 2f), 2);
            tree.Insert(new Rect(20f, 20f, 2f, 2f), 3);

            tree.Query(new Rect(0.5f, 0.5f, 1f, 1f), out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryPoint_ReturnsContainingEntries()
        {
            var tree = RTree2D<int>.Create();
            tree.Insert(new Rect(0f, 0f, 4f, 4f), 1);
            tree.Insert(new Rect(10f, 10f, 4f, 4f), 2);

            tree.QueryPoint(new Vector2(1f, 1f), out var results);
            CollectionAssert.AreEqual(new[] { 1 }, results);
        }

        [Test]
        public void Insert_ManyEntries_AllQueryable()
        {
            var tree = RTree2D<int>.Create(4);
            for (var i = 0; i < 30; i++)
                Assert.IsTrue(tree.Insert(new Rect(i, i, 1f, 1f), i));

            Assert.AreEqual(30, tree.Count);

            tree.Query(new Rect(0f, 0f, 30f, 30f), out var results);
            Assert.AreEqual(30, results.Count);
        }

        [Test]
        public void Remove_DropsEntry()
        {
            var tree = RTree2D<int>.Create();
            tree.Insert(new Rect(0f, 0f, 2f, 2f), 1);
            tree.Insert(new Rect(5f, 5f, 2f, 2f), 2);

            Assert.IsTrue(tree.Remove(1));
            Assert.IsFalse(tree.Contains(1));
            Assert.IsFalse(tree.Remove(1));

            tree.Query(new Rect(0f, 0f, 100f, 100f), out var results);
            CollectionAssert.AreEqual(new[] { 2 }, results);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var tree = RTree2D<int>.Create();
            tree.Insert(new Rect(0f, 0f, 2f, 2f), 1);
            tree.Insert(new Rect(5f, 5f, 2f, 2f), 2);
            tree.Clear();
            Assert.AreEqual(0, tree.Count);
        }
    }
}

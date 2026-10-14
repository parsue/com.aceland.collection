using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class QuadTreeTests
    {
        [Test]
        public void Create_NonPositiveBounds_Throws()
        {
            Assert.Throws<CollectionException>(() =>
                QuadTree<int>.Create(new Rect(0f, 0f, 0f, 10f)));
            Assert.Throws<CollectionException>(() =>
                QuadTree<int>.Create(new Rect(0f, 0f, 10f, -1f)));
        }

        [Test]
        public void Create_NonPositiveCapacityOrDepth_Throws()
        {
            var bounds = new Rect(0f, 0f, 10f, 10f);
            Assert.Throws<CollectionException>(() => QuadTree<int>.Create(bounds, 0, 4));
            Assert.Throws<CollectionException>(() => QuadTree<int>.Create(bounds, 4, 0));
        }

        [Test]
        public void Create_NullComparer_FallsBackToDefault()
        {
            var bounds = new Rect(0f, 0f, 10f, 10f);
            var tree = QuadTree<int>.Create(bounds, 4, 4, null);
            Assert.IsTrue(tree.Insert(new Vector2(1f, 1f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void EmptyTree_QueryReturnsNothing()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 10f, 10f));
            tree.QueryRect(new Rect(0f, 0f, 10f, 10f), out var results);
            CollectionAssert.IsEmpty(results);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_OutOfBounds_ReturnsFalse()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 10f, 10f));
            Assert.IsFalse(tree.Insert(new Vector2(20f, 20f), 1));
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 10f, 10f));
            Assert.IsTrue(tree.Insert(new Vector2(1f, 1f), 1));
            Assert.IsFalse(tree.Insert(new Vector2(2f, 2f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void QueryRect_ReturnsItemsInside()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 16f, 16f), 2, 8);
            tree.Insert(new Vector2(1f, 1f), 1);
            tree.Insert(new Vector2(3f, 3f), 2);
            tree.Insert(new Vector2(14f, 14f), 3);
            tree.Insert(new Vector2(15f, 1f), 4);

            tree.QueryRect(new Rect(0f, 0f, 5f, 5f), out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryRadius_ReturnsItemsWithinDistance()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 16f, 16f));
            tree.Insert(new Vector2(0f, 0f), 1);
            tree.Insert(new Vector2(1f, 0f), 2);
            tree.Insert(new Vector2(8f, 8f), 3);

            tree.QueryRadius(new Vector2(0f, 0f), 1.5f, out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void Subdivision_KeepsAllItemsQueryable()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 16f, 16f), 2, 8);
            for (var i = 0; i < 20; i++)
                Assert.IsTrue(tree.Insert(new Vector2(i % 16, (i * 3) % 16), i));

            Assert.AreEqual(20, tree.Count);

            tree.QueryRect(new Rect(0f, 0f, 16f, 16f), out var results);
            Assert.AreEqual(20, results.Count);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var tree = QuadTree<int>.Create(new Rect(0f, 0f, 10f, 10f));
            tree.Insert(new Vector2(1f, 1f), 1);
            tree.Insert(new Vector2(2f, 2f), 2);
            tree.Clear();
            Assert.AreEqual(0, tree.Count);

            tree.QueryRect(new Rect(0f, 0f, 10f, 10f), out var results);
            CollectionAssert.IsEmpty(results);
        }
    }
}

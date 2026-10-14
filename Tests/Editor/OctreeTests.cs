using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class OctreeTests
    {
        private static Bounds World => new(Vector3.zero, new Vector3(16f, 16f, 16f));

        [Test]
        public void Create_NonPositiveBounds_Throws()
        {
            Assert.Throws<CollectionException>(() =>
                Octree<int>.Create(new Bounds(Vector3.zero, new Vector3(0f, 4f, 4f))));
        }

        [Test]
        public void Create_NonPositiveCapacityOrDepth_Throws()
        {
            Assert.Throws<CollectionException>(() => Octree<int>.Create(World, 0, 4));
            Assert.Throws<CollectionException>(() => Octree<int>.Create(World, 4, 0));
        }

        [Test]
        public void Create_NullComparer_FallsBackToDefault()
        {
            var tree = Octree<int>.Create(World, 4, 4, null);
            Assert.IsTrue(tree.Insert(new Vector3(1f, 1f, 1f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void EmptyTree_QueryReturnsNothing()
        {
            var tree = Octree<int>.Create(World);
            tree.QueryBox(World, out var results);
            CollectionAssert.IsEmpty(results);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_OutOfBounds_ReturnsFalse()
        {
            var tree = Octree<int>.Create(World);
            Assert.IsFalse(tree.Insert(new Vector3(100f, 100f, 100f), 1));
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var tree = Octree<int>.Create(World);
            Assert.IsTrue(tree.Insert(new Vector3(1f, 1f, 1f), 1));
            Assert.IsFalse(tree.Insert(new Vector3(2f, 2f, 2f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void QueryBox_ReturnsItemsInside()
        {
            var tree = Octree<int>.Create(World, 2, 8);
            tree.Insert(new Vector3(1f, 1f, 1f), 1);
            tree.Insert(new Vector3(2f, 2f, 2f), 2);
            tree.Insert(new Vector3(-6f, -6f, -6f), 3);

            tree.QueryBox(new Bounds(new Vector3(1.5f, 1.5f, 1.5f), new Vector3(4f, 4f, 4f)), out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryRadius_ReturnsItemsWithinDistance()
        {
            var tree = Octree<int>.Create(World);
            tree.Insert(new Vector3(0f, 0f, 0f), 1);
            tree.Insert(new Vector3(1f, 0f, 0f), 2);
            tree.Insert(new Vector3(6f, 6f, 6f), 3);

            tree.QueryRadius(new Vector3(0f, 0f, 0f), 1.5f, out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void Subdivision_KeepsAllItemsQueryable()
        {
            var tree = Octree<int>.Create(World, 2, 8);
            for (var i = 0; i < 20; i++)
                Assert.IsTrue(tree.Insert(new Vector3(i % 8 - 4, (i * 2) % 8 - 4, (i * 3) % 8 - 4), i));

            Assert.AreEqual(20, tree.Count);

            tree.QueryBox(World, out var results);
            Assert.AreEqual(20, results.Count);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var tree = Octree<int>.Create(World);
            tree.Insert(new Vector3(1f, 1f, 1f), 1);
            tree.Insert(new Vector3(2f, 2f, 2f), 2);
            tree.Clear();
            Assert.AreEqual(0, tree.Count);
        }
    }
}

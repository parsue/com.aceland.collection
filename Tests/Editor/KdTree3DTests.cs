using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class KdTree3DTests
    {
        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() => KdTree3D<int>.Create(null));
        }

        [Test]
        public void EmptyTree_NearestReturnsFalse()
        {
            var tree = KdTree3D<int>.Create();
            Assert.IsFalse(tree.TryFindNearest(Vector3.zero, out _));
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var tree = KdTree3D<int>.Create();
            Assert.IsTrue(tree.Insert(new Vector3(1f, 1f, 1f), 1));
            Assert.IsFalse(tree.Insert(new Vector3(2f, 2f, 2f), 1));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void TryFindNearest_ReturnsClosest()
        {
            var tree = KdTree3D<int>.Create();
            tree.Insert(new Vector3(0f, 0f, 0f), 1);
            tree.Insert(new Vector3(5f, 5f, 5f), 2);
            tree.Insert(new Vector3(10f, 0f, 0f), 3);

            Assert.IsTrue(tree.TryFindNearest(new Vector3(6f, 5f, 5f), out var nearest));
            Assert.AreEqual(2, nearest);
        }

        [Test]
        public void FindKNearest_ReturnsNearestFirst()
        {
            var tree = KdTree3D<int>.Create();
            tree.Insert(new Vector3(0f, 0f, 0f), 1);
            tree.Insert(new Vector3(1f, 0f, 0f), 2);
            tree.Insert(new Vector3(2f, 0f, 0f), 3);
            tree.Insert(new Vector3(10f, 10f, 10f), 4);

            tree.FindKNearest(new Vector3(0f, 0f, 0f), 2, out var results);
            Assert.AreEqual(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryRadius_ReturnsItemsWithinDistance()
        {
            var tree = KdTree3D<int>.Create();
            tree.Insert(new Vector3(0f, 0f, 0f), 1);
            tree.Insert(new Vector3(1f, 0f, 0f), 2);
            tree.Insert(new Vector3(5f, 5f, 5f), 3);

            tree.QueryRadius(new Vector3(0f, 0f, 0f), 1.5f, out var results);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void Remove_DropsItemAndUpdatesNearest()
        {
            var tree = KdTree3D<int>.Create();
            tree.Insert(new Vector3(0f, 0f, 0f), 1);
            tree.Insert(new Vector3(5f, 5f, 5f), 2);

            Assert.IsTrue(tree.Remove(1));
            Assert.IsFalse(tree.Contains(1));
            Assert.IsFalse(tree.Remove(1));

            Assert.IsTrue(tree.TryFindNearest(new Vector3(0f, 0f, 0f), out var nearest));
            Assert.AreEqual(2, nearest);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var tree = KdTree3D<int>.Create();
            tree.Insert(new Vector3(1f, 1f, 1f), 1);
            tree.Insert(new Vector3(2f, 2f, 2f), 2);
            tree.Clear();
            Assert.AreEqual(0, tree.Count);
            Assert.IsFalse(tree.TryFindNearest(Vector3.zero, out _));
        }
    }
}

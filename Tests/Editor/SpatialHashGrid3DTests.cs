using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class SpatialHashGrid3DTests
    {
        [Test]
        public void Create_NonPositiveCellSize_Throws()
        {
            Assert.Throws<CollectionException>(() => SpatialHashGrid3D<int>.Create(0f));
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var grid = SpatialHashGrid3D<int>.Create(1f);
            Assert.IsTrue(grid.Insert(Vector3.zero, 1));
            Assert.IsFalse(grid.Insert(new Vector3(5f, 5f, 5f), 1));
        }

        [Test]
        public void QueryBox_ReturnsItemsInside()
        {
            var grid = SpatialHashGrid3D<int>.Create(1f);
            grid.Insert(new Vector3(0.5f, 0.5f, 0.5f), 1);
            grid.Insert(new Vector3(2.5f, 2.5f, 2.5f), 2);
            grid.Insert(new Vector3(10f, 10f, 10f), 3);

            grid.QueryBox(new Bounds(new Vector3(1.5f, 1.5f, 1.5f), new Vector3(3f, 3f, 3f)), out var results);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryRadius_ReturnsItemsWithinDistance()
        {
            var grid = SpatialHashGrid3D<int>.Create(1f);
            grid.Insert(Vector3.zero, 1);
            grid.Insert(new Vector3(1f, 0f, 0f), 2);
            grid.Insert(new Vector3(5f, 5f, 5f), 3);

            grid.QueryRadius(Vector3.zero, 1.5f, out var results);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void Move_ReBucketsItem()
        {
            var grid = SpatialHashGrid3D<int>.Create(1f);
            grid.Insert(Vector3.zero, 1);
            Assert.IsTrue(grid.Move(1, new Vector3(9f, 9f, 9f)));

            grid.QueryRadius(new Vector3(9f, 9f, 9f), 0.5f, out var results);
            CollectionAssert.AreEqual(new[] { 1 }, results);
        }

        [Test]
        public void Remove_DropsItem()
        {
            var grid = SpatialHashGrid3D<int>.Create(1f);
            grid.Insert(Vector3.zero, 1);
            Assert.IsTrue(grid.Remove(1));
            Assert.IsFalse(grid.Remove(1));
        }
    }
}

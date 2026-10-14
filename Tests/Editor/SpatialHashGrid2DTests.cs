using System.Collections.Generic;
using AceLand.Collection.Core;
using AceLand.Collection.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AceLand.Collection.Tests
{
    public sealed class SpatialHashGrid2DTests
    {
        [Test]
        public void Create_NonPositiveCellSize_Throws()
        {
            Assert.Throws<CollectionException>(() => SpatialHashGrid2D<int>.Create(0f));
            Assert.Throws<CollectionException>(() => SpatialHashGrid2D<int>.Create(-1f));
        }

        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() => SpatialHashGrid2D<int>.Create(1f, null));
        }

        [Test]
        public void Insert_DuplicateItem_ReturnsFalse()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            Assert.IsTrue(grid.Insert(new Vector2(0f, 0f), 1));
            Assert.IsFalse(grid.Insert(new Vector2(5f, 5f), 1));
            Assert.AreEqual(1, grid.Count);
        }

        [Test]
        public void QueryRect_ReturnsItemsInside()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            grid.Insert(new Vector2(0.5f, 0.5f), 1);
            grid.Insert(new Vector2(2.5f, 2.5f), 2);
            grid.Insert(new Vector2(10f, 10f), 3);

            grid.QueryRect(new Rect(0f, 0f, 3f, 3f), out var results);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void QueryRadius_ReturnsItemsWithinDistance()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            grid.Insert(new Vector2(0f, 0f), 1);
            grid.Insert(new Vector2(1f, 0f), 2);
            grid.Insert(new Vector2(5f, 5f), 3);

            grid.QueryRadius(new Vector2(0f, 0f), 1.5f, out var results);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
        }

        [Test]
        public void Move_ReBucketsItem()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            grid.Insert(new Vector2(0f, 0f), 1);
            Assert.IsTrue(grid.Move(1, new Vector2(9f, 9f)));

            grid.QueryRadius(new Vector2(9f, 9f), 0.5f, out var results);
            CollectionAssert.AreEqual(new[] { 1 }, results);

            grid.QueryRadius(new Vector2(0f, 0f), 0.5f, out results);
            CollectionAssert.IsEmpty(results);
        }

        [Test]
        public void Move_AbsentItem_ReturnsFalse()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            Assert.IsFalse(grid.Move(42, new Vector2(1f, 1f)));
        }

        [Test]
        public void Remove_DropsItem()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            grid.Insert(new Vector2(0f, 0f), 1);
            Assert.IsTrue(grid.Remove(1));
            Assert.IsFalse(grid.Contains(1));
            Assert.IsFalse(grid.Remove(1));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var grid = SpatialHashGrid2D<int>.Create(1f);
            grid.Insert(new Vector2(0f, 0f), 1);
            grid.Insert(new Vector2(2f, 2f), 2);
            grid.Clear();
            Assert.AreEqual(0, grid.Count);
        }
    }
}

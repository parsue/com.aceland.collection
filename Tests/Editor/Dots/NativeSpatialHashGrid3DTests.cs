using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace AceLand.Collection.Dots.Tests
{
    public sealed class NativeSpatialHashGrid3DTests
    {
        [Test]
        public void Insert_Then_TryGetPosition_Returns_Stored_Position()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 16, Allocator.Temp);

            Assert.IsTrue(grid.Insert(new float3(2.5f, 3.5f, 4.5f), 42));
            Assert.AreEqual(1, grid.Count);
            Assert.IsTrue(grid.TryGetPosition(42, out var pos));
            Assert.AreEqual(new float3(2.5f, 3.5f, 4.5f), pos);
        }

        [Test]
        public void Insert_Duplicate_Item_Returns_False()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 16, Allocator.Temp);

            Assert.IsTrue(grid.Insert(float3.zero, 7));
            Assert.IsFalse(grid.Insert(new float3(5f, 5f, 5f), 7));
            Assert.AreEqual(1, grid.Count);
        }

        [Test]
        public void Move_Updates_Position_And_Cell()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 16, Allocator.Temp);
            grid.Insert(new float3(0.5f, 0.5f, 0.5f), 1);

            Assert.IsTrue(grid.Move(1, new float3(10.5f, 10.5f, 10.5f)));
            Assert.IsTrue(grid.TryGetPosition(1, out var pos));
            Assert.AreEqual(new float3(10.5f, 10.5f, 10.5f), pos);

            using var results = new NativeList<int>(Allocator.Temp);
            grid.QueryBox(new float3(10f, 10f, 10f), new float3(11f, 11f, 11f), results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(1, results[0]);
        }

        [Test]
        public void Remove_Deletes_Item()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 16, Allocator.Temp);
            grid.Insert(float3.zero, 3);

            Assert.IsTrue(grid.Remove(3));
            Assert.AreEqual(0, grid.Count);
            Assert.IsFalse(grid.TryGetPosition(3, out _));
            Assert.IsFalse(grid.Remove(3));
        }

        [Test]
        public void QueryBox_Returns_Only_Items_Inside_Bounds()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 64, Allocator.Temp);
            grid.Insert(new float3(1f, 1f, 1f), 1);
            grid.Insert(new float3(2f, 2f, 2f), 2);
            grid.Insert(new float3(20f, 20f, 20f), 3);

            using var results = new NativeList<int>(Allocator.Temp);
            grid.QueryBox(new float3(0f, 0f, 0f), new float3(5f, 5f, 5f), results);

            Assert.AreEqual(2, results.Length);
            Assert.IsFalse(results.Contains(3));
        }

        [Test]
        public void QueryRadius_Returns_Items_Within_Radius()
        {
            using var grid = new NativeSpatialHashGrid3D<int>(1f, 64, Allocator.Temp);
            grid.Insert(new float3(0f, 0f, 0f), 1);
            grid.Insert(new float3(1f, 0f, 0f), 2);
            grid.Insert(new float3(10f, 0f, 0f), 3);

            using var results = new NativeList<int>(Allocator.Temp);
            grid.QueryRadius(float3.zero, 2f, results);

            Assert.AreEqual(2, results.Length);
            Assert.IsFalse(results.Contains(3));
        }
    }
}

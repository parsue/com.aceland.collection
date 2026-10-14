using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace AceLand.Collection.Dots.Tests
{
    public sealed class NativeKdTree3DTests
    {
        private static NativeKdTree3D<int> BuildTree(Allocator allocator, params float3[] points)
        {
            var pts = new NativeArray<float3>(points.Length, Allocator.Temp);
            var items = new NativeArray<int>(points.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < points.Length; i++)
                {
                    pts[i] = points[i];
                    items[i] = i;
                }

                return new NativeKdTree3D<int>(pts, items, allocator);
            }
            finally
            {
                pts.Dispose();
                items.Dispose();
            }
        }

        private static void Rebuild(ref NativeKdTree3D<int> tree, float3[] points, int[] ids)
        {
            var pts = new NativeArray<float3>(points.Length, Allocator.Temp);
            var items = new NativeArray<int>(ids.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < points.Length; i++)
                {
                    pts[i] = points[i];
                    items[i] = ids[i];
                }

                tree.Rebuild(pts, items);
            }
            finally
            {
                pts.Dispose();
                items.Dispose();
            }
        }

        [Test]
        public void Empty_Tree_TryFindNearest_Returns_False()
        {
            using var tree = new NativeKdTree3D<int>(Allocator.Temp);

            Assert.IsTrue(tree.IsEmpty);
            Assert.IsFalse(tree.TryFindNearest(float3.zero, out _));
        }

        [Test]
        public void OneShot_Build_Reports_Count()
        {
            using var tree = BuildTree(Allocator.Temp,
                new float3(0f, 0f, 0f), new float3(1f, 1f, 1f), new float3(2f, 2f, 2f));

            Assert.AreEqual(3, tree.Count);
            Assert.IsFalse(tree.IsEmpty);
        }

        [Test]
        public void TryFindNearest_Returns_Closest_Item()
        {
            using var tree = BuildTree(Allocator.Temp,
                new float3(0f, 0f, 0f), new float3(10f, 10f, 10f), new float3(3f, 4f, 0f));

            Assert.IsTrue(tree.TryFindNearest(new float3(3.1f, 3.9f, 0.1f), out var nearest));
            Assert.AreEqual(2, nearest);
        }

        [Test]
        public void QueryRadius_Collects_Items_Within_Radius()
        {
            using var tree = BuildTree(Allocator.Temp,
                new float3(0f, 0f, 0f), new float3(1f, 0f, 0f), new float3(10f, 10f, 10f));

            using var results = new NativeList<int>(Allocator.Temp);
            tree.QueryRadius(float3.zero, 2f, results);

            Assert.AreEqual(2, results.Length);
            Assert.IsFalse(results.Contains(2));
        }

        [Test]
        public void Rebuild_Replaces_Point_Set()
        {
            var tree = new NativeKdTree3D<int>(Allocator.Temp);
            try
            {
                Rebuild(ref tree,
                    new[] { new float3(0f, 0f, 0f), new float3(1f, 1f, 1f) },
                    new[] { 100, 200 });

                Assert.AreEqual(2, tree.Count);
                Assert.IsTrue(tree.TryFindNearest(new float3(0.9f, 0.9f, 0.9f), out var nearest));
                Assert.AreEqual(200, nearest);

                Rebuild(ref tree,
                    new[] { new float3(5f, 5f, 5f) },
                    new[] { 999 });

                Assert.AreEqual(1, tree.Count);
                Assert.IsTrue(tree.TryFindNearest(float3.zero, out var only));
                Assert.AreEqual(999, only);
            }
            finally
            {
                tree.Dispose();
            }
        }
    }
}

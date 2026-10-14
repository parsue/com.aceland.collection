using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AceLand.Collection.Dots.PlayModeTests
{
    /// <summary>
    /// PlayMode tests that exercise every Native* container from inside a
    /// <see cref="BurstCompileAttribute"/> job. They verify:
    /// <list type="bullet">
    /// <item>the containers compile and run correctly under Burst (job produces the expected result);</item>
    /// <item>running the job performs no managed (GC) allocation;</item>
    /// <item>a <c>struct</c> comparer is usable and correct inside a Burst job (inlined, no vtable dispatch).</item>
    /// </list>
    /// Jobs are executed synchronously with <c>Schedule().Complete()</c> so the Burst
    /// backend is used (rather than the managed fallback of <c>Run()</c>).
    /// </summary>
    public sealed class NativeCollectionsBurstJobTests
    {
        // ---- Comparers (value types => Burst inlines Compare) --------------

        private struct AscendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => a.CompareTo(b);
        }

        private struct DescendingInt : IComparer<int>
        {
            public int Compare(int a, int b) => b.CompareTo(a);
        }

        // ---- Jobs -----------------------------------------------------------

        [BurstCompile]
        private struct BinaryHeapMinJob : IJob
        {
            [WriteOnly] public NativeArray<int> Sorted;

            public void Execute()
            {
                var heap = new NativeBinaryHeap<int, AscendingInt>(Sorted.Length, Allocator.Temp, default);
                // Push in a scrambled order.
                for (var i = 0; i < Sorted.Length; i++)
                    heap.Push((Sorted.Length * 7 + i * 13) % Sorted.Length);

                var idx = 0;
                while (heap.TryPop(out var v))
                    Sorted[idx++] = v;

                heap.Dispose();
            }
        }

        [BurstCompile]
        private struct BinaryHeapMaxJob : IJob
        {
            [WriteOnly] public NativeArray<int> Sorted;

            public void Execute()
            {
                var heap = new NativeBinaryHeap<int, DescendingInt>(Sorted.Length, Allocator.Temp, default);
                for (var i = 0; i < Sorted.Length; i++)
                    heap.Push(i);

                var idx = 0;
                while (heap.TryPop(out var v))
                    Sorted[idx++] = v;

                heap.Dispose();
            }
        }

        [BurstCompile]
        private struct HashGrid2DJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Points;
            public float2 QueryCenter;
            public float QueryRadius;
            [WriteOnly] public NativeArray<int> HitCount;

            public void Execute()
            {
                var grid = new NativeSpatialHashGrid2D<int>(1f, Points.Length, Allocator.Temp);
                for (var i = 0; i < Points.Length; i++)
                    grid.Insert(Points[i], i);

                var results = new NativeList<int>(Points.Length, Allocator.Temp);
                grid.QueryRadius(QueryCenter, QueryRadius, results);
                HitCount[0] = results.Length;

                results.Dispose();
                grid.Dispose();
            }
        }

        [BurstCompile]
        private struct HashGrid3DJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Points;
            public float3 BoxMin;
            public float3 BoxMax;
            [WriteOnly] public NativeArray<int> HitCount;

            public void Execute()
            {
                var grid = new NativeSpatialHashGrid3D<int>(1f, Points.Length, Allocator.Temp);
                for (var i = 0; i < Points.Length; i++)
                    grid.Insert(Points[i], i);

                var results = new NativeList<int>(Points.Length, Allocator.Temp);
                grid.QueryBox(BoxMin, BoxMax, results);
                HitCount[0] = results.Length;

                results.Dispose();
                grid.Dispose();
            }
        }

        [BurstCompile]
        private struct KdTree2DJob : IJob
        {
            [ReadOnly] public NativeArray<float2> Points;
            [ReadOnly] public NativeArray<int> Items;
            public float2 Target;
            [WriteOnly] public NativeArray<int> Nearest;

            public void Execute()
            {
                var tree = new NativeKdTree2D<int>(Points, Items, Allocator.Temp);
                Nearest[0] = tree.TryFindNearest(Target, out var nearest) ? nearest : -1;
                tree.Dispose();
            }
        }

        [BurstCompile]
        private struct KdTree3DJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Points;
            [ReadOnly] public NativeArray<int> Items;
            public float3 Target;
            [WriteOnly] public NativeArray<int> Nearest;

            public void Execute()
            {
                var tree = new NativeKdTree3D<int>(Points, Items, Allocator.Temp);
                Nearest[0] = tree.TryFindNearest(Target, out var nearest) ? nearest : -1;
                tree.Dispose();
            }
        }

        // ---- NativeBinaryHeap ----------------------------------------------

        [Test]
        public void BinaryHeap_MinHeap_In_Burst_Job_Sorts_Ascending()
        {
            using var sorted = new NativeArray<int>(16, Allocator.TempJob);
            new BinaryHeapMinJob { Sorted = sorted }.Schedule().Complete();

            for (var i = 0; i < sorted.Length; i++)
                Assert.AreEqual(i, sorted[i], $"element {i} out of order");
        }

        [Test]
        public void BinaryHeap_MaxHeap_In_Burst_Job_Sorts_Descending()
        {
            using var sorted = new NativeArray<int>(16, Allocator.TempJob);
            new BinaryHeapMaxJob { Sorted = sorted }.Schedule().Complete();

            for (var i = 0; i < sorted.Length; i++)
                Assert.AreEqual(sorted.Length - 1 - i, sorted[i], $"element {i} out of order");
        }

        [Test]
        public void BinaryHeap_Job_Does_Not_Allocate_Managed_Memory()
        {
            using var sorted = new NativeArray<int>(16, Allocator.TempJob);
            var job = new BinaryHeapMinJob { Sorted = sorted };

            // Warm-up: the first Schedule().Complete() of a Burst job triggers
            // Burst compilation / safety-handle setup, which allocates managed
            // memory. Run once up-front so that cost is not attributed to the
            // measured delegate below. (NUnit runs tests alphabetically, so this
            // GC test may execute before the correctness tests that would
            // otherwise warm the same job type.)
            job.Schedule().Complete();

            Assert.That(() => job.Schedule().Complete(), Is.Not.AllocatingGCMemory());
        }

        // ---- NativeSpatialHashGrid2D ---------------------------------------

        [Test]
        public void HashGrid2D_QueryRadius_In_Burst_Job_Returns_Expected_Count()
        {
            using var points = new NativeArray<float2>(4, Allocator.TempJob)
            {
                [0] = new float2(0f, 0f),
                [1] = new float2(0.5f, 0.5f),
                [2] = new float2(5f, 5f),
                [3] = new float2(-3f, 2f),
            };
            using var hit = new NativeArray<int>(1, Allocator.TempJob);

            new HashGrid2DJob
            {
                Points = points,
                QueryCenter = new float2(0f, 0f),
                QueryRadius = 1f,
                HitCount = hit,
            }.Schedule().Complete();

            // Points 0 and 1 are within radius 1 of the origin.
            Assert.AreEqual(2, hit[0]);
        }

        [Test]
        public void HashGrid2D_Job_Does_Not_Allocate_Managed_Memory()
        {
            using var points = new NativeArray<float2>(4, Allocator.TempJob)
            {
                [0] = new float2(0f, 0f),
                [1] = new float2(0.5f, 0.5f),
                [2] = new float2(5f, 5f),
                [3] = new float2(-3f, 2f),
            };
            using var hit = new NativeArray<int>(1, Allocator.TempJob);
            var job = new HashGrid2DJob
            {
                Points = points,
                QueryCenter = new float2(0f, 0f),
                QueryRadius = 1f,
                HitCount = hit,
            };

            // Warm-up run to absorb first-run Burst compilation / safety-handle
            // managed allocation before measuring the delegate.
            job.Schedule().Complete();

            Assert.That(() => job.Schedule().Complete(), Is.Not.AllocatingGCMemory());
        }

        // ---- NativeSpatialHashGrid3D ---------------------------------------

        [Test]
        public void HashGrid3D_QueryBox_In_Burst_Job_Returns_Expected_Count()
        {
            using var points = new NativeArray<float3>(4, Allocator.TempJob)
            {
                [0] = new float3(0f, 0f, 0f),
                [1] = new float3(1f, 1f, 1f),
                [2] = new float3(9f, 9f, 9f),
                [3] = new float3(2f, 0f, 0f),
            };
            using var hit = new NativeArray<int>(1, Allocator.TempJob);

            new HashGrid3DJob
            {
                Points = points,
                BoxMin = new float3(-0.1f, -0.1f, -0.1f),
                BoxMax = new float3(1.5f, 1.5f, 1.5f),
                HitCount = hit,
            }.Schedule().Complete();

            // Points 0 and 1 fall inside the box.
            Assert.AreEqual(2, hit[0]);
        }

        [Test]
        public void HashGrid3D_Job_Does_Not_Allocate_Managed_Memory()
        {
            using var points = new NativeArray<float3>(4, Allocator.TempJob)
            {
                [0] = new float3(0f, 0f, 0f),
                [1] = new float3(1f, 1f, 1f),
                [2] = new float3(9f, 9f, 9f),
                [3] = new float3(2f, 0f, 0f),
            };
            using var hit = new NativeArray<int>(1, Allocator.TempJob);
            var job = new HashGrid3DJob
            {
                Points = points,
                BoxMin = new float3(-0.1f, -0.1f, -0.1f),
                BoxMax = new float3(1.5f, 1.5f, 1.5f),
                HitCount = hit,
            };

            // Warm-up run to absorb first-run Burst compilation / safety-handle
            // managed allocation before measuring the delegate.
            job.Schedule().Complete();

            Assert.That(() => job.Schedule().Complete(), Is.Not.AllocatingGCMemory());
        }

        // ---- NativeKdTree2D -------------------------------------------------

        [Test]
        public void KdTree2D_FindNearest_In_Burst_Job_Returns_Closest_Item()
        {
            using var points = new NativeArray<float2>(4, Allocator.TempJob)
            {
                [0] = new float2(0f, 0f),
                [1] = new float2(10f, 10f),
                [2] = new float2(3f, 4f),
                [3] = new float2(-5f, -5f),
            };
            using var items = new NativeArray<int>(4, Allocator.TempJob)
            {
                [0] = 100,
                [1] = 200,
                [2] = 300,
                [3] = 400,
            };
            using var nearest = new NativeArray<int>(1, Allocator.TempJob);

            new KdTree2DJob
            {
                Points = points,
                Items = items,
                Target = new float2(3.1f, 3.9f),
                Nearest = nearest,
            }.Schedule().Complete();

            Assert.AreEqual(300, nearest[0]);
        }

        [Test]
        public void KdTree2D_Job_Does_Not_Allocate_Managed_Memory()
        {
            using var points = new NativeArray<float2>(4, Allocator.TempJob)
            {
                [0] = new float2(0f, 0f),
                [1] = new float2(10f, 10f),
                [2] = new float2(3f, 4f),
                [3] = new float2(-5f, -5f),
            };
            using var items = new NativeArray<int>(4, Allocator.TempJob)
            {
                [0] = 100,
                [1] = 200,
                [2] = 300,
                [3] = 400,
            };
            using var nearest = new NativeArray<int>(1, Allocator.TempJob);
            var job = new KdTree2DJob
            {
                Points = points,
                Items = items,
                Target = new float2(3.1f, 3.9f),
                Nearest = nearest,
            };

            Assert.That(() => job.Schedule().Complete(), Is.Not.AllocatingGCMemory());
        }

        // ---- NativeKdTree3D -------------------------------------------------

        [Test]
        public void KdTree3D_FindNearest_In_Burst_Job_Returns_Closest_Item()
        {
            using var points = new NativeArray<float3>(4, Allocator.TempJob)
            {
                [0] = new float3(0f, 0f, 0f),
                [1] = new float3(10f, 10f, 10f),
                [2] = new float3(3f, 4f, 0f),
                [3] = new float3(-5f, -5f, -5f),
            };
            using var items = new NativeArray<int>(4, Allocator.TempJob)
            {
                [0] = 100,
                [1] = 200,
                [2] = 300,
                [3] = 400,
            };
            using var nearest = new NativeArray<int>(1, Allocator.TempJob);

            new KdTree3DJob
            {
                Points = points,
                Items = items,
                Target = new float3(3.1f, 3.9f, 0.1f),
                Nearest = nearest,
            }.Schedule().Complete();

            Assert.AreEqual(300, nearest[0]);
        }

        [Test]
        public void KdTree3D_Job_Does_Not_Allocate_Managed_Memory()
        {
            using var points = new NativeArray<float3>(4, Allocator.TempJob)
            {
                [0] = new float3(0f, 0f, 0f),
                [1] = new float3(10f, 10f, 10f),
                [2] = new float3(3f, 4f, 0f),
                [3] = new float3(-5f, -5f, -5f),
            };
            using var items = new NativeArray<int>(4, Allocator.TempJob)
            {
                [0] = 100,
                [1] = 200,
                [2] = 300,
                [3] = 400,
            };
            using var nearest = new NativeArray<int>(1, Allocator.TempJob);
            var job = new KdTree3DJob
            {
                Points = points,
                Items = items,
                Target = new float3(3.1f, 3.9f, 0.1f),
                Nearest = nearest,
            };

            Assert.That(() => job.Schedule().Complete(), Is.Not.AllocatingGCMemory());
        }
    }
}

using AceLand.Collection.Spatial;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Conversion bridge between the managed, Unity-serializable k-d trees
    /// (<see cref="KdTree2D{T}"/>, <see cref="KdTree3D{T}"/>) and their
    /// Burst-friendly counterparts
    /// (<see cref="NativeKdTree2D{T}"/>, <see cref="NativeKdTree3D{T}"/>).
    /// </summary>
    /// <remarks>
    /// The native trees store unmanaged item handles, so the bridge constrains
    /// <c>T</c> to <c>unmanaged</c>. A native tree is built one-shot from the
    /// managed point set (balanced O(n log n) build). Ownership of a produced
    /// native tree transfers to the caller, who must dispose it.
    /// </remarks>
    public static class KdTreeDotsBridge
    {
        /// <summary>
        /// Builds a new <see cref="NativeKdTree2D{T}"/> from every positioned item
        /// of <paramref name="source"/>. The caller owns and must dispose the result.
        /// </summary>
        public static NativeKdTree2D<T> ToNative<T>(this KdTree2D<T> source, Allocator allocator)
            where T : unmanaged
        {
            var snapshot = source.SnapshotSerialized();
            var count = snapshot.Count;
            var points = new NativeArray<float2>(count, Allocator.Temp);
            var items = new NativeArray<T>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                var p = snapshot[i].Position;
                points[i] = new float2(p.x, p.y);
                items[i] = snapshot[i].Item;
            }

            var tree = new NativeKdTree2D<T>(points, items, allocator);
            points.Dispose();
            items.Dispose();
            return tree;
        }

        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// managed <see cref="KdTree2D{T}"/>.
        /// </summary>
        public static KdTree2D<T> ToManaged<T>(this NativeKdTree2D<T> source)
            where T : unmanaged
        {
            var tree = KdTree2D<T>.Create();
            var positions = source.BackingPositions;
            var items = source.BackingItems;
            for (var i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                tree.Insert(new Vector2(p.x, p.y), items[i]);
            }

            return tree;
        }

        /// <summary>
        /// Builds a new <see cref="NativeKdTree3D{T}"/> from every positioned item
        /// of <paramref name="source"/>. The caller owns and must dispose the result.
        /// </summary>
        public static NativeKdTree3D<T> ToNative<T>(this KdTree3D<T> source, Allocator allocator)
            where T : unmanaged
        {
            var snapshot = source.SnapshotSerialized();
            var count = snapshot.Count;
            var points = new NativeArray<float3>(count, Allocator.Temp);
            var items = new NativeArray<T>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                var p = snapshot[i].Position;
                points[i] = new float3(p.x, p.y, p.z);
                items[i] = snapshot[i].Item;
            }

            var tree = new NativeKdTree3D<T>(points, items, allocator);
            points.Dispose();
            items.Dispose();
            return tree;
        }

        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// managed <see cref="KdTree3D{T}"/>.
        /// </summary>
        public static KdTree3D<T> ToManaged<T>(this NativeKdTree3D<T> source)
            where T : unmanaged
        {
            var tree = KdTree3D<T>.Create();
            var positions = source.BackingPositions;
            var items = source.BackingItems;
            for (var i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                tree.Insert(new Vector3(p.x, p.y, p.z), items[i]);
            }

            return tree;
        }
    }
}

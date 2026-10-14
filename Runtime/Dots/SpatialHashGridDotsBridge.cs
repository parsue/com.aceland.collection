using System;
using AceLand.Collection.Spatial;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Conversion bridge between the managed, Unity-serializable spatial hash
    /// grids (<see cref="SpatialHashGrid2D{T}"/>, <see cref="SpatialHashGrid3D{T}"/>)
    /// and their Burst-friendly counterparts
    /// (<see cref="NativeSpatialHashGrid2D{T}"/>, <see cref="NativeSpatialHashGrid3D{T}"/>).
    /// </summary>
    /// <remarks>
    /// The native grids store unmanaged, equatable item handles, so the bridge
    /// constrains <c>T</c> to <c>unmanaged, IEquatable&lt;T&gt;</c>. The cell size
    /// is preserved across conversions. Ownership of a produced native grid
    /// transfers to the caller, who must dispose it.
    /// </remarks>
    public static class SpatialHashGridDotsBridge
    {
        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// <see cref="NativeSpatialHashGrid2D{T}"/> with the same cell size. The
        /// caller owns and must dispose the result.
        /// </summary>
        public static NativeSpatialHashGrid2D<T> ToNative<T>(
            this SpatialHashGrid2D<T> source, Allocator allocator)
            where T : unmanaged, IEquatable<T>
        {
            var snapshot = source.SnapshotSerialized();
            var grid = new NativeSpatialHashGrid2D<T>(source.CellSize, snapshot.Count, allocator);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var p = snapshot[i].Position;
                grid.Insert(new float2(p.x, p.y), snapshot[i].Item);
            }

            return grid;
        }

        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// managed <see cref="SpatialHashGrid2D{T}"/> with the same cell size.
        /// </summary>
        public static SpatialHashGrid2D<T> ToManaged<T>(this NativeSpatialHashGrid2D<T> source)
            where T : unmanaged, IEquatable<T>
        {
            var grid = SpatialHashGrid2D<T>.Create(source.CellSize);
            using var pairs = source.BackingPositions.GetKeyValueArrays(Allocator.Temp);
            for (var i = 0; i < pairs.Keys.Length; i++)
            {
                var p = pairs.Values[i];
                grid.Insert(new Vector2(p.x, p.y), pairs.Keys[i]);
            }

            return grid;
        }

        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// <see cref="NativeSpatialHashGrid3D{T}"/> with the same cell size. The
        /// caller owns and must dispose the result.
        /// </summary>
        public static NativeSpatialHashGrid3D<T> ToNative<T>(
            this SpatialHashGrid3D<T> source, Allocator allocator)
            where T : unmanaged, IEquatable<T>
        {
            var snapshot = source.SnapshotSerialized();
            var grid = new NativeSpatialHashGrid3D<T>(source.CellSize, snapshot.Count, allocator);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var p = snapshot[i].Position;
                grid.Insert(new float3(p.x, p.y, p.z), snapshot[i].Item);
            }

            return grid;
        }

        /// <summary>
        /// Copies every positioned item of <paramref name="source"/> into a new
        /// managed <see cref="SpatialHashGrid3D{T}"/> with the same cell size.
        /// </summary>
        public static SpatialHashGrid3D<T> ToManaged<T>(this NativeSpatialHashGrid3D<T> source)
            where T : unmanaged, IEquatable<T>
        {
            var grid = SpatialHashGrid3D<T>.Create(source.CellSize);
            using var pairs = source.BackingPositions.GetKeyValueArrays(Allocator.Temp);
            for (var i = 0; i < pairs.Keys.Length; i++)
            {
                var p = pairs.Values[i];
                grid.Insert(new Vector3(p.x, p.y, p.z), pairs.Keys[i]);
            }

            return grid;
        }
    }
}

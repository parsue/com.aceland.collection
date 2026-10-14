using System;
using Unity.Collections;
using Unity.Mathematics;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Burst-friendly 3D spatial hash grid mapping unmanaged items to positions and
    /// bucketing them into uniform cells for fast broad-phase range queries.
    /// </summary>
    /// <typeparam name="T">
    /// Unmanaged, equatable item type (an entity handle, id, index, etc.).
    /// </typeparam>
    /// <remarks>
    /// Backed by a <see cref="NativeParallelMultiHashMap{TKey,TValue}"/> (cell key → items)
    /// and a <see cref="NativeParallelHashMap{TKey,TValue}"/> (item → position). Owns
    /// native memory: construct with an <see cref="Allocator"/> and call
    /// <see cref="Dispose()"/> when done. Pure data; safe to read inside Burst-compiled jobs.
    /// </remarks>
    public struct NativeSpatialHashGrid3D<T> : IDisposable
        where T : unmanaged, IEquatable<T>
    {
        private NativeParallelMultiHashMap<int3, T> _cells;
        private NativeParallelHashMap<T, float3> _positions;
        private readonly float _cellSize;
        private readonly float _inverseCellSize;

        /// <summary>Creates an empty grid with the given uniform cell size.</summary>
        public NativeSpatialHashGrid3D(float cellSize, int initialCapacity, Allocator allocator)
        {
            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _cellSize = cellSize;
            _inverseCellSize = 1f / cellSize;
            _cells = new NativeParallelMultiHashMap<int3, T>(initialCapacity, allocator);
            _positions = new NativeParallelHashMap<T, float3>(initialCapacity, allocator);
        }

        /// <summary>Number of stored items.</summary>
        public int Count => _positions.Count();

        /// <summary>True when the backing native memory is allocated.</summary>
        public bool IsCreated => _positions.IsCreated;

        /// <summary>Configured uniform cell size.</summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Internal read access to the item→position map used by the DOTS
        /// conversion bridge to project this grid back to a managed one.
        /// </summary>
        internal NativeParallelHashMap<T, float3> BackingPositions => _positions;

        /// <summary>Inserts <paramref name="item"/> at <paramref name="position"/>. Returns false when it already exists.</summary>
        public bool Insert(float3 position, T item)
        {
            if (_positions.ContainsKey(item)) return false;

            _positions.Add(item, position);
            _cells.Add(CellCoord(position), item);
            return true;
        }

        /// <summary>Reads the stored position of <paramref name="item"/>.</summary>
        public bool TryGetPosition(T item, out float3 position) => _positions.TryGetValue(item, out position);

        /// <summary>Moves <paramref name="item"/> to a new position. Returns false when absent.</summary>
        public bool Move(T item, float3 position)
        {
            if (!_positions.TryGetValue(item, out var old)) return false;

            var oldKey = CellCoord(old);
            var newKey = CellCoord(position);
            if (!oldKey.Equals(newKey))
            {
                RemoveFromCell(oldKey, item);
                _cells.Add(newKey, item);
            }

            _positions[item] = position;
            return true;
        }

        /// <summary>Removes <paramref name="item"/>. Returns false when absent.</summary>
        public bool Remove(T item)
        {
            if (!_positions.TryGetValue(item, out var position)) return false;

            RemoveFromCell(CellCoord(position), item);
            _positions.Remove(item);
            return true;
        }

        /// <summary>Removes every item while keeping the allocated capacity.</summary>
        public void Clear()
        {
            _cells.Clear();
            _positions.Clear();
        }

        /// <summary>
        /// Collects items whose position falls inside the axis-aligned box
        /// [<paramref name="min"/>, <paramref name="max"/>] into <paramref name="results"/>
        /// (cleared first).
        /// </summary>
        public void QueryBox(float3 min, float3 max, NativeList<T> results)
        {
            results.Clear();
            var cellMin = CellCoord(min);
            var cellMax = CellCoord(max);

            for (var cx = cellMin.x; cx <= cellMax.x; cx++)
            for (var cy = cellMin.y; cy <= cellMax.y; cy++)
            for (var cz = cellMin.z; cz <= cellMax.z; cz++)
            {
                var key = new int3(cx, cy, cz);
                if (!_cells.TryGetFirstValue(key, out var item, out var it)) continue;
                do
                {
                    if (!_positions.TryGetValue(item, out var p)) continue;
                    if (p.x >= min.x && p.x <= max.x &&
                        p.y >= min.y && p.y <= max.y &&
                        p.z >= min.z && p.z <= max.z)
                        results.Add(item);
                } while (_cells.TryGetNextValue(out item, ref it));
            }
        }

        /// <summary>
        /// Collects items within <paramref name="radius"/> of <paramref name="center"/>
        /// into <paramref name="results"/> (cleared first).
        /// </summary>
        public void QueryRadius(float3 center, float radius, NativeList<T> results)
        {
            results.Clear();
            if (radius < 0f) return;

            var min = center - radius;
            var max = center + radius;
            var cellMin = CellCoord(min);
            var cellMax = CellCoord(max);
            var sqrRadius = radius * radius;

            for (var cx = cellMin.x; cx <= cellMax.x; cx++)
            for (var cy = cellMin.y; cy <= cellMax.y; cy++)
            for (var cz = cellMin.z; cz <= cellMax.z; cz++)
            {
                var key = new int3(cx, cy, cz);
                if (!_cells.TryGetFirstValue(key, out var item, out var it)) continue;
                do
                {
                    if (!_positions.TryGetValue(item, out var p)) continue;
                    if (math.distancesq(p, center) <= sqrRadius)
                        results.Add(item);
                } while (_cells.TryGetNextValue(out item, ref it));
            }
        }

        /// <summary>Releases the native memory.</summary>
        public void Dispose()
        {
            if (_cells.IsCreated) _cells.Dispose();
            if (_positions.IsCreated) _positions.Dispose();
        }

        // ---- Internals ------------------------------------------------------

        private void RemoveFromCell(int3 key, T item)
        {
            if (!_cells.TryGetFirstValue(key, out var value, out var it)) return;
            do
            {
                if (value.Equals(item))
                {
                    _cells.Remove(it);
                    return;
                }
            } while (_cells.TryGetNextValue(out value, ref it));
        }

        private int3 CellCoord(float3 position) => new(
            (int)math.floor(position.x * _inverseCellSize),
            (int)math.floor(position.y * _inverseCellSize),
            (int)math.floor(position.z * _inverseCellSize));
    }
}

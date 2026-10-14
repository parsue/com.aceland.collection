using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Uniform 2D spatial hash grid: world space is partitioned into square cells
    /// of a fixed size and each item is bucketed by the cell that contains its
    /// position. Insert / remove / move and neighbourhood queries are close to
    /// O(1) when the cell size roughly matches the query radius.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Best fit for evenly distributed, highly dynamic objects — the most common
    /// and most cache-friendly broad-phase in games (boids, proximity, triggers).
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(float)"/>. Item equality uses the
    /// supplied comparer (default otherwise); the comparer is not serialized and
    /// can be re-supplied via <see cref="RestoreComparer"/>. The grid is
    /// Unity-serializable: items are flattened to a position/item list and
    /// re-inserted on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SpatialHashGrid2D<T> : SerializableCollection<SpatialItem2D<T>>
    {
        [SerializeField]
        private float _cellSize;

        private Dictionary<long, List<T>> _cells;
        private Dictionary<T, Vector2> _positions;
        private IEqualityComparer<T> _comparer;

        private SpatialHashGrid2D(float cellSize, IEqualityComparer<T> comparer)
        {
            _cellSize = cellSize;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _cells = new Dictionary<long, List<T>>();
            _positions = new Dictionary<T, Vector2>(_comparer);
        }

        /// <summary>Creates a grid with the given cell size (world units per cell).</summary>
        public static SpatialHashGrid2D<T> Create(float cellSize)
        {
            RequirePositiveCellSize(cellSize);
            return new SpatialHashGrid2D<T>(cellSize, null);
        }

        /// <summary>Creates a grid with a custom item comparer.</summary>
        public static SpatialHashGrid2D<T> Create(float cellSize, IEqualityComparer<T> comparer)
        {
            RequirePositiveCellSize(cellSize);
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new SpatialHashGrid2D<T>(cellSize, comparer);
        }

        /// <summary>World units spanned by one cell edge.</summary>
        public float CellSize => _cellSize;

        /// <summary>Number of stored items.</summary>
        public int Count => _positions.Count;

        /// <summary>Returns true when <paramref name="item"/> is stored.</summary>
        public bool Contains(T item) => _positions.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> at <paramref name="position"/>. Returns
        /// false when the item already exists (use <see cref="Move"/> to relocate).
        /// </summary>
        public bool Insert(Vector2 position, T item)
        {
            if (_positions.ContainsKey(item))
                return false;

            AddToCell(CellKey(position), item);
            _positions[item] = position;
            return true;
        }

        /// <summary>Reads the stored position of <paramref name="item"/>.</summary>
        public bool TryGetPosition(T item, out Vector2 position) =>
            _positions.TryGetValue(item, out position);

        /// <summary>
        /// Moves <paramref name="item"/> to <paramref name="position"/>, re-bucketing
        /// it when it crosses a cell boundary. Returns false when the item is absent.
        /// </summary>
        public bool Move(T item, Vector2 position)
        {
            if (!_positions.TryGetValue(item, out var old))
                return false;

            var oldKey = CellKey(old);
            var newKey = CellKey(position);
            if (oldKey != newKey)
            {
                RemoveFromCell(oldKey, item);
                AddToCell(newKey, item);
            }

            _positions[item] = position;
            return true;
        }

        /// <summary>Removes <paramref name="item"/>. Returns false when absent.</summary>
        public bool Remove(T item)
        {
            if (!_positions.TryGetValue(item, out var position))
                return false;

            RemoveFromCell(CellKey(position), item);
            _positions.Remove(item);
            return true;
        }

        /// <summary>
        /// Collects every item whose position lies within the axis-aligned
        /// <paramref name="area"/> into a freshly allocated <paramref name="results"/> list.
        /// Prefer the <see cref="QueryRect(Rect, List{T})"/> overload with a reusable
        /// buffer to avoid a per-call allocation.
        /// </summary>
        public void QueryRect(Rect area, out List<T> results)
        {
            results = new List<T>();
            QueryRect(area, results);
        }

        /// <summary>
        /// Collects every item whose position lies within the axis-aligned
        /// <paramref name="area"/> into the caller-supplied <paramref name="results"/>
        /// buffer. The buffer is cleared first, then refilled; reuse it across calls
        /// to keep the query allocation-free.
        /// </summary>
        public void QueryRect(Rect area, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            var minX = Mathf.FloorToInt(area.xMin / _cellSize);
            var maxX = Mathf.FloorToInt(area.xMax / _cellSize);
            var minY = Mathf.FloorToInt(area.yMin / _cellSize);
            var maxY = Mathf.FloorToInt(area.yMax / _cellSize);

            for (var cx = minX; cx <= maxX; cx++)
            for (var cy = minY; cy <= maxY; cy++)
            {
                if (!_cells.TryGetValue(PackKey(cx, cy), out var bucket)) continue;
                foreach (var item in bucket)
                    if (area.Contains(_positions[item]))
                        results.Add(item);
            }
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of
        /// <paramref name="center"/> into a freshly allocated <paramref name="results"/> list.
        /// Prefer the <see cref="QueryRadius(Vector2, float, List{T})"/> overload with a
        /// reusable buffer to avoid a per-call allocation.
        /// </summary>
        public void QueryRadius(Vector2 center, float radius, out List<T> results)
        {
            results = new List<T>();
            QueryRadius(center, radius, results);
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of
        /// <paramref name="center"/> into the caller-supplied <paramref name="results"/>
        /// buffer. The buffer is cleared first, then refilled; reuse it across calls
        /// to keep the query allocation-free.
        /// </summary>
        public void QueryRadius(Vector2 center, float radius, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            if (radius < 0f) return;

            var minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
            var maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
            var minY = Mathf.FloorToInt((center.y - radius) / _cellSize);
            var maxY = Mathf.FloorToInt((center.y + radius) / _cellSize);
            var sqr = radius * radius;

            for (var cx = minX; cx <= maxX; cx++)
            for (var cy = minY; cy <= maxY; cy++)
            {
                if (!_cells.TryGetValue(PackKey(cx, cy), out var bucket)) continue;
                foreach (var item in bucket)
                    if ((_positions[item] - center).sqrMagnitude <= sqr)
                        results.Add(item);
            }
        }

        /// <summary>Removes all items, keeping the cell size.</summary>
        public void Clear()
        {
            _cells.Clear();
            _positions.Clear();
        }

        /// <summary>
        /// Re-attaches a custom item comparer (e.g. after Unity deserialization)
        /// and rebuilds the index. Passing null falls back to the default.
        /// </summary>
        public void RestoreComparer(IEqualityComparer<T> comparer)
        {
            var next = comparer ?? EqualityComparer<T>.Default;
            if (ReferenceEquals(next, _comparer)) return;

            _comparer = next;
            Rebuild();
        }

        // ---- Internal helpers ----------------------------------------------

        private static void RequirePositiveCellSize(float cellSize)
        {
            if (cellSize <= 0f)
                throw new CollectionException(
                    $"'cellSize' must be a positive value, got {cellSize}.");
        }

        private long CellKey(Vector2 position) =>
            PackKey(Mathf.FloorToInt(position.x / _cellSize),
                Mathf.FloorToInt(position.y / _cellSize));

        private static long PackKey(int cx, int cy) =>
            ((long)cx << 32) ^ (uint)cy;

        private void AddToCell(long key, T item)
        {
            if (!_cells.TryGetValue(key, out var bucket))
            {
                bucket = new List<T>();
                _cells[key] = bucket;
            }

            bucket.Add(item);
        }

        private void RemoveFromCell(long key, T item)
        {
            if (!_cells.TryGetValue(key, out var bucket)) return;

            for (var i = 0; i < bucket.Count; i++)
            {
                if (!_comparer.Equals(bucket[i], item)) continue;
                bucket.RemoveAt(i);
                break;
            }

            if (bucket.Count == 0)
                _cells.Remove(key);
        }

        private void Rebuild()
        {
            var snapshot = _positions;
            _positions = new Dictionary<T, Vector2>(_comparer);
            _cells = new Dictionary<long, List<T>>();
            foreach (var pair in snapshot)
            {
                AddToCell(CellKey(pair.Value), pair.Key);
                _positions[pair.Key] = pair.Value;
            }
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<SpatialItem2D<T>> buffer)
        {
            if (_positions == null) return;
            foreach (var pair in _positions)
                buffer.Add(new SpatialItem2D<T>(pair.Value, pair.Key));
        }

        protected override void ReadSerializedItems(IReadOnlyList<SpatialItem2D<T>> items)
        {
            if (_cellSize <= 0f) _cellSize = 1f;
            _comparer ??= EqualityComparer<T>.Default;
            _cells = new Dictionary<long, List<T>>();
            _positions = new Dictionary<T, Vector2>(_comparer);

            for (var i = 0; i < items.Count; i++)
                Insert(items[i].Position, items[i].Item);
        }
    }
}

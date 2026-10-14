using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Static 3D k-d tree specialised for point queries: nearest-neighbour,
    /// k-nearest and within-radius look-ups over a set of positioned items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree is built (or rebuilt) from the current point set on demand.
    /// Insertions and removals mark the tree dirty and it is rebuilt lazily on the
    /// next query. Best fit for mostly static point clouds that are queried far more
    /// often than they change.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Item equality uses the supplied
    /// comparer (default otherwise); the comparer is not serialized and can be
    /// re-supplied via <see cref="RestoreComparer"/>. The tree is Unity-serializable:
    /// items are flattened to a position/item list and re-inserted on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class KdTree3D<T> : SerializableCollection<SpatialItem3D<T>>
    {
        private struct KdNode
        {
            public int Point;
            public int Left;
            public int Right;
        }

        private List<Vector3> _positions;
        private List<T> _itemList;
        private Dictionary<T, int> _index;
        private IEqualityComparer<T> _comparer;

        private KdNode[] _nodes;
        private int _root;
        private bool _dirty;

        private KdTree3D(IEqualityComparer<T> comparer)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            InitStorage();
        }

        /// <summary>Creates an empty k-d tree.</summary>
        public static KdTree3D<T> Create() => new(null);

        /// <summary>Creates a k-d tree with a custom item comparer.</summary>
        public static KdTree3D<T> Create(IEqualityComparer<T> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new KdTree3D<T>(comparer);
        }

        /// <summary>Number of stored items.</summary>
        public int Count => _index.Count;

        /// <summary>Returns true when <paramref name="item"/> is stored.</summary>
        public bool Contains(T item) => _index.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> at <paramref name="position"/>. Returns
        /// false when the item already exists. Marks the tree for rebuild.
        /// </summary>
        public bool Insert(Vector3 position, T item)
        {
            if (_index.ContainsKey(item)) return false;

            _index[item] = _positions.Count;
            _positions.Add(position);
            _itemList.Add(item);
            _dirty = true;
            return true;
        }

        /// <summary>Reads the stored position of <paramref name="item"/>.</summary>
        public bool TryGetPosition(T item, out Vector3 position)
        {
            if (_index.TryGetValue(item, out var id))
            {
                position = _positions[id];
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>Removes <paramref name="item"/>. Returns false when absent.</summary>
        public bool Remove(T item)
        {
            if (!_index.TryGetValue(item, out var id)) return false;

            var last = _positions.Count - 1;
            if (id != last)
            {
                _positions[id] = _positions[last];
                _itemList[id] = _itemList[last];
                _index[_itemList[id]] = id;
            }

            _positions.RemoveAt(last);
            _itemList.RemoveAt(last);
            _index.Remove(item);
            _dirty = true;
            return true;
        }

        /// <summary>
        /// Finds the item closest to <paramref name="target"/>. Returns false when
        /// the tree is empty.
        /// </summary>
        public bool TryFindNearest(Vector3 target, out T nearest)
        {
            nearest = default;
            if (_index.Count == 0) return false;

            EnsureBuilt();
            var best = -1;
            var bestSqr = float.PositiveInfinity;
            Nearest(_root, target, 0, ref best, ref bestSqr);
            nearest = _itemList[_nodes[best].Point];
            return true;
        }

        /// <summary>
        /// Collects up to <paramref name="count"/> items closest to
        /// <paramref name="target"/> into a freshly allocated <paramref name="results"/> list,
        /// ordered nearest first. Prefer the <see cref="FindKNearest(Vector3, int, List{T})"/>
        /// overload with a reusable buffer to avoid a per-call allocation.
        /// </summary>
        public void FindKNearest(Vector3 target, int count, out List<T> results)
        {
            results = new List<T>();
            FindKNearest(target, count, results);
        }

        /// <summary>
        /// Collects up to <paramref name="count"/> items closest to
        /// <paramref name="target"/> into the caller-supplied <paramref name="results"/>
        /// buffer, ordered nearest first. The buffer is cleared first, then refilled;
        /// reuse it across calls to keep the query allocation-free.
        /// </summary>
        public void FindKNearest(Vector3 target, int count, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            if (count <= 0 || _index.Count == 0) return;

            EnsureBuilt();
            var heap = new List<(float sqr, int point)>(count);
            KNearest(_root, target, 0, count, heap);
            heap.Sort((a, b) => a.sqr.CompareTo(b.sqr));
            foreach (var (_, point) in heap)
                results.Add(_itemList[_nodes[point].Point]);
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of
        /// <paramref name="target"/> into a freshly allocated <paramref name="results"/> list.
        /// Prefer the <see cref="QueryRadius(Vector3, float, List{T})"/> overload with a
        /// reusable buffer to avoid a per-call allocation.
        /// </summary>
        public void QueryRadius(Vector3 target, float radius, out List<T> results)
        {
            results = new List<T>();
            QueryRadius(target, radius, results);
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of
        /// <paramref name="target"/> into the caller-supplied <paramref name="results"/>
        /// buffer. The buffer is cleared first, then refilled; reuse it across calls
        /// to keep the query allocation-free.
        /// </summary>
        public void QueryRadius(Vector3 target, float radius, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            if (radius < 0f || _index.Count == 0) return;

            EnsureBuilt();
            WithinRadius(_root, target, radius * radius, 0, results);
        }

        /// <summary>Removes all items.</summary>
        public void Clear() => InitStorage();

        /// <summary>
        /// Re-attaches a custom item comparer (e.g. after Unity deserialization)
        /// and rebuilds the item index. Passing null falls back to the default.
        /// </summary>
        public void RestoreComparer(IEqualityComparer<T> comparer)
        {
            var next = comparer ?? EqualityComparer<T>.Default;
            if (ReferenceEquals(next, _comparer)) return;

            _comparer = next;
            var rebuilt = new Dictionary<T, int>(_comparer);
            for (var i = 0; i < _itemList.Count; i++)
                rebuilt[_itemList[i]] = i;
            _index = rebuilt;
            _dirty = true;
        }

        // ---- Build ----------------------------------------------------------

        private void InitStorage()
        {
            _positions = new List<Vector3>();
            _itemList = new List<T>();
            _index = new Dictionary<T, int>(_comparer);
            _nodes = Array.Empty<KdNode>();
            _root = -1;
            _dirty = false;
        }

        private void EnsureBuilt()
        {
            if (!_dirty) return;

            var n = _positions.Count;
            _nodes = new KdNode[n];
            var ids = new int[n];
            for (var i = 0; i < n; i++) ids[i] = i;
            _root = Build(ids, 0, n, 0);
            _dirty = false;
        }

        private int Build(int[] ids, int start, int end, int depth)
        {
            if (start >= end) return -1;

            var axis = depth % 3;
            var mid = (start + end) / 2;
            NthElement(ids, start, end, mid, axis);

            var nodeIndex = mid;
            _nodes[nodeIndex] = new KdNode
            {
                Point = ids[mid],
                Left = Build(ids, start, mid, depth + 1),
                Right = Build(ids, mid + 1, end, depth + 1),
            };
            return nodeIndex;
        }

        private void NthElement(int[] ids, int start, int end, int k, int axis)
        {
            var lo = start;
            var hi = end - 1;
            while (lo < hi)
            {
                var pivot = Coord(ids[(lo + hi) / 2], axis);
                var i = lo;
                var j = hi;
                while (i <= j)
                {
                    while (Coord(ids[i], axis) < pivot) i++;
                    while (Coord(ids[j], axis) > pivot) j--;
                    if (i <= j)
                    {
                        (ids[i], ids[j]) = (ids[j], ids[i]);
                        i++;
                        j--;
                    }
                }

                if (k <= j) hi = j;
                else if (k >= i) lo = i;
                else break;
            }
        }

        private float Coord(int pointId, int axis)
        {
            var p = _positions[pointId];
            return axis switch
            {
                0 => p.x,
                1 => p.y,
                _ => p.z,
            };
        }

        private static float Component(Vector3 v, int axis) =>
            axis switch { 0 => v.x, 1 => v.y, _ => v.z };

        // ---- Queries --------------------------------------------------------

        private void Nearest(int node, Vector3 target, int depth, ref int best, ref float bestSqr)
        {
            if (node < 0) return;

            var pointId = _nodes[node].Point;
            var sqr = (_positions[pointId] - target).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }

            var axis = depth % 3;
            var diff = Component(target, axis) - Coord(pointId, axis);
            var near = diff < 0f ? _nodes[node].Left : _nodes[node].Right;
            var far = diff < 0f ? _nodes[node].Right : _nodes[node].Left;

            Nearest(near, target, depth + 1, ref best, ref bestSqr);
            if (diff * diff < bestSqr)
                Nearest(far, target, depth + 1, ref best, ref bestSqr);
        }

        private void KNearest(int node, Vector3 target, int depth, int k,
            List<(float sqr, int point)> heap)
        {
            if (node < 0) return;

            var pointId = _nodes[node].Point;
            var sqr = (_positions[pointId] - target).sqrMagnitude;
            HeapConsider(heap, k, sqr, node);

            var axis = depth % 3;
            var diff = Component(target, axis) - Coord(pointId, axis);
            var near = diff < 0f ? _nodes[node].Left : _nodes[node].Right;
            var far = diff < 0f ? _nodes[node].Right : _nodes[node].Left;

            KNearest(near, target, depth + 1, k, heap);
            if (heap.Count < k || diff * diff < heap[0].sqr)
                KNearest(far, target, depth + 1, k, heap);
        }

        private static void HeapConsider(List<(float sqr, int point)> heap, int k,
            float sqr, int node)
        {
            if (heap.Count < k)
            {
                heap.Add((sqr, node));
                SiftUp(heap, heap.Count - 1);
            }
            else if (sqr < heap[0].sqr)
            {
                heap[0] = (sqr, node);
                SiftDown(heap, 0);
            }
        }

        private static void SiftUp(List<(float sqr, int point)> heap, int i)
        {
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (heap[parent].sqr >= heap[i].sqr) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static void SiftDown(List<(float sqr, int point)> heap, int i)
        {
            var count = heap.Count;
            while (true)
            {
                var largest = i;
                var l = 2 * i + 1;
                var r = 2 * i + 2;
                if (l < count && heap[l].sqr > heap[largest].sqr) largest = l;
                if (r < count && heap[r].sqr > heap[largest].sqr) largest = r;
                if (largest == i) break;
                (heap[largest], heap[i]) = (heap[i], heap[largest]);
                i = largest;
            }
        }

        private void WithinRadius(int node, Vector3 target, float sqrRadius, int depth,
            List<T> results)
        {
            if (node < 0) return;

            var pointId = _nodes[node].Point;
            if ((_positions[pointId] - target).sqrMagnitude <= sqrRadius)
                results.Add(_itemList[pointId]);

            var axis = depth % 3;
            var diff = Component(target, axis) - Coord(pointId, axis);
            var near = diff < 0f ? _nodes[node].Left : _nodes[node].Right;
            var far = diff < 0f ? _nodes[node].Right : _nodes[node].Left;

            WithinRadius(near, target, sqrRadius, depth + 1, results);
            if (diff * diff <= sqrRadius)
                WithinRadius(far, target, sqrRadius, depth + 1, results);
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<SpatialItem3D<T>> buffer)
        {
            if (_itemList == null || _positions == null) return;
            for (var i = 0; i < _itemList.Count; i++)
                buffer.Add(new SpatialItem3D<T>(_positions[i], _itemList[i]));
        }

        protected override void ReadSerializedItems(IReadOnlyList<SpatialItem3D<T>> items)
        {
            _comparer ??= EqualityComparer<T>.Default;
            InitStorage();
            for (var i = 0; i < items.Count; i++)
                Insert(items[i].Position, items[i].Item);
        }
    }
}

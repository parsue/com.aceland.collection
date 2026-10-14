using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Point Octree over a fixed 3D region. Space is recursively split into eight
    /// octants once a node exceeds its capacity, giving logarithmic point queries
    /// for non-uniformly distributed, mostly static data (spatial culling, region
    /// look-ups, volumetric hit testing).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world bounds are fixed at construction; points outside the bounds are
    /// rejected by <see cref="Insert"/>. For evenly distributed and highly dynamic
    /// data prefer <see cref="SpatialHashGrid3D{T}"/>.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(Bounds)"/>. Item equality uses the
    /// supplied comparer (default otherwise); the comparer is not serialized and can
    /// be re-supplied via <see cref="RestoreComparer"/>. The tree is
    /// Unity-serializable: items are flattened to a position/item list and
    /// re-inserted on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class Octree<T> : SerializableCollection<SpatialItem3D<T>>
    {
        private const int DefaultCapacity = 8;
        private const int DefaultMaxDepth = 16;

        private sealed class Node
        {
            public Bounds Bounds;
            public readonly List<int> Items = new();
            public Node[] Children;

            public bool IsLeaf => Children == null;
        }

        [SerializeField] private Bounds _bounds;
        [SerializeField] private int _capacity;
        [SerializeField] private int _maxDepth;

        private Node _root;
        private List<Vector3> _pointPositions;
        private List<T> _pointItems;
        private Dictionary<T, int> _index;
        private IEqualityComparer<T> _comparer;

        private Octree(Bounds bounds, int capacity, int maxDepth, IEqualityComparer<T> comparer)
        {
            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            InitStorage();
        }

        /// <summary>Creates an octree covering <paramref name="bounds"/>.</summary>
        public static Octree<T> Create(Bounds bounds) =>
            Create(bounds, DefaultCapacity, DefaultMaxDepth, null);

        /// <summary>Creates an octree with custom node capacity and depth cap.</summary>
        public static Octree<T> Create(Bounds bounds, int capacity, int maxDepth) =>
            Create(bounds, capacity, maxDepth, null);

        /// <summary>Creates an octree with a custom item comparer.</summary>
        public static Octree<T> Create(Bounds bounds, int capacity, int maxDepth,
            IEqualityComparer<T> comparer)
        {
            RequirePositiveSize(bounds);
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequirePositiveCapacity(maxDepth, nameof(maxDepth));
            return new Octree<T>(bounds, capacity, maxDepth, comparer);
        }

        /// <summary>The fixed region covered by this tree.</summary>
        public Bounds Bounds => _bounds;

        /// <summary>Number of stored items.</summary>
        public int Count => _index.Count;

        /// <summary>Returns true when <paramref name="item"/> is stored.</summary>
        public bool Contains(T item) => _index.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> at <paramref name="position"/>. Returns
        /// false when the item already exists or the position is outside the bounds.
        /// </summary>
        public bool Insert(Vector3 position, T item)
        {
            if (_index.ContainsKey(item)) return false;
            if (!_bounds.Contains(position)) return false;

            var pointId = _pointPositions.Count;
            _pointPositions.Add(position);
            _pointItems.Add(item);
            _index[item] = pointId;
            InsertPoint(_root, pointId, 0);
            return true;
        }

        /// <summary>Reads the stored position of <paramref name="item"/>.</summary>
        public bool TryGetPosition(T item, out Vector3 position)
        {
            if (_index.TryGetValue(item, out var id))
            {
                position = _pointPositions[id];
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Collects every item whose position lies within <paramref name="area"/>
        /// into a freshly allocated <paramref name="results"/> list. Prefer the
        /// <see cref="QueryBox(Bounds, List{T})"/> overload with a reusable buffer to
        /// avoid a per-call allocation.
        /// </summary>
        public void QueryBox(Bounds area, out List<T> results)
        {
            results = new List<T>();
            QueryBox(area, results);
        }

        /// <summary>
        /// Collects every item whose position lies within <paramref name="area"/>
        /// into the caller-supplied <paramref name="results"/> buffer. The buffer is
        /// cleared first, then refilled; reuse it across calls to keep the query
        /// allocation-free.
        /// </summary>
        public void QueryBox(Bounds area, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            QueryBoxNode(_root, area, results);
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of
        /// <paramref name="center"/> into a freshly allocated <paramref name="results"/> list.
        /// Prefer the <see cref="QueryRadius(Vector3, float, List{T})"/> overload with a
        /// reusable buffer to avoid a per-call allocation.
        /// </summary>
        public void QueryRadius(Vector3 center, float radius, out List<T> results)
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
        public void QueryRadius(Vector3 center, float radius, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            if (radius < 0f) return;
            QueryRadiusNode(_root, center, radius * radius, results);
        }

        /// <summary>Removes all items, keeping the bounds and node parameters.</summary>
        public void Clear() => InitStorage();

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

        private static void RequirePositiveSize(Bounds bounds)
        {
            var s = bounds.size;
            if (s.x <= 0f || s.y <= 0f || s.z <= 0f)
                throw new CollectionException(
                    $"Octree bounds must have a positive size, got {s}.");
        }

        private void InitStorage()
        {
            _root = new Node { Bounds = _bounds };
            _pointPositions = new List<Vector3>();
            _pointItems = new List<T>();
            _index = new Dictionary<T, int>(_comparer);
        }

        private void InsertPoint(Node node, int pointId, int depth)
        {
            while (true)
            {
                if (node.IsLeaf)
                {
                    node.Items.Add(pointId);
                    if (node.Items.Count > _capacity && depth < _maxDepth)
                        Subdivide(node, depth);
                    return;
                }

                node = node.Children[OctantOf(node, _pointPositions[pointId])];
                depth++;
            }
        }

        private void Subdivide(Node node, int depth)
        {
            var c = node.Bounds.center;
            var q = node.Bounds.extents * 0.5f; // quarter-size = child half-extents
            var childSize = node.Bounds.size * 0.5f;

            node.Children = new Node[8];
            for (var i = 0; i < 8; i++)
            {
                var offset = new Vector3(
                    (i & 1) == 0 ? -q.x : q.x,
                    (i & 2) == 0 ? -q.y : q.y,
                    (i & 4) == 0 ? -q.z : q.z);
                node.Children[i] = new Node { Bounds = new Bounds(c + offset, childSize) };
            }

            var pending = new List<int>(node.Items);
            node.Items.Clear();
            foreach (var id in pending)
                node.Children[OctantOf(node, _pointPositions[id])].Items.Add(id);

            var childDepth = depth + 1;
            if (childDepth >= _maxDepth) return;
            foreach (var child in node.Children)
                if (child.Items.Count > _capacity)
                    Subdivide(child, childDepth);
        }

        private static int OctantOf(Node node, Vector3 p)
        {
            var c = node.Bounds.center;
            var index = 0;
            if (p.x >= c.x) index |= 1;
            if (p.y >= c.y) index |= 2;
            if (p.z >= c.z) index |= 4;
            return index;
        }

        private void QueryBoxNode(Node node, Bounds area, List<T> results)
        {
            if (!node.Bounds.Intersects(area)) return;

            if (node.IsLeaf)
            {
                foreach (var id in node.Items)
                    if (area.Contains(_pointPositions[id]))
                        results.Add(_pointItems[id]);
                return;
            }

            foreach (var child in node.Children)
                QueryBoxNode(child, area, results);
        }

        private void QueryRadiusNode(Node node, Vector3 center, float sqrRadius, List<T> results)
        {
            if (SqrDistanceToBounds(center, node.Bounds) > sqrRadius) return;

            if (node.IsLeaf)
            {
                foreach (var id in node.Items)
                    if ((_pointPositions[id] - center).sqrMagnitude <= sqrRadius)
                        results.Add(_pointItems[id]);
                return;
            }

            foreach (var child in node.Children)
                QueryRadiusNode(child, center, sqrRadius, results);
        }

        private static float SqrDistanceToBounds(Vector3 point, Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            var dx = Mathf.Max(min.x - point.x, 0f, point.x - max.x);
            var dy = Mathf.Max(min.y - point.y, 0f, point.y - max.y);
            var dz = Mathf.Max(min.z - point.z, 0f, point.z - max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private void Rebuild()
        {
            var positions = _pointPositions;
            var items = _pointItems;
            _root = new Node { Bounds = _bounds };
            _pointPositions = new List<Vector3>(positions.Count);
            _pointItems = new List<T>(items.Count);
            _index = new Dictionary<T, int>(_comparer);
            for (var i = 0; i < items.Count; i++)
                Insert(positions[i], items[i]);
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<SpatialItem3D<T>> buffer)
        {
            if (_pointItems == null || _pointPositions == null) return;
            for (var i = 0; i < _pointItems.Count; i++)
                buffer.Add(new SpatialItem3D<T>(_pointPositions[i], _pointItems[i]));
        }

        protected override void ReadSerializedItems(IReadOnlyList<SpatialItem3D<T>> items)
        {
            if (_capacity <= 0) _capacity = DefaultCapacity;
            if (_maxDepth <= 0) _maxDepth = DefaultMaxDepth;
            var size = _bounds.size;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                _bounds = new Bounds(Vector3.zero, Vector3.one);
            _comparer ??= EqualityComparer<T>.Default;
            InitStorage();

            for (var i = 0; i < items.Count; i++)
                Insert(items[i].Position, items[i].Item);
        }
    }
}

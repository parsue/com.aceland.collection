using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Point QuadTree over a fixed 2D region. Space is recursively split into four
    /// quadrants once a node exceeds its capacity, giving logarithmic point queries
    /// for non-uniformly distributed, mostly static data (spatial culling, region
    /// look-ups, hit testing).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world bounds are fixed at construction; points outside the bounds are
    /// rejected by <see cref="Insert"/>. For evenly distributed and highly dynamic
    /// data prefer <see cref="SpatialHashGrid2D{T}"/>.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(Rect)"/>. Item equality uses the supplied
    /// comparer (default otherwise); the comparer is not serialized and can be
    /// re-supplied via <see cref="RestoreComparer"/>. The tree is Unity-serializable:
    /// items are flattened to a position/item list and re-inserted on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuadTree<T> : SerializableCollection<SpatialItem2D<T>>
    {
        private const int DefaultCapacity = 8;
        private const int DefaultMaxDepth = 16;

        private sealed class Node
        {
            public Rect Bounds;
            public readonly List<int> Items = new();
            public Node[] Children;

            public bool IsLeaf => Children == null;
        }

        [SerializeField] private Rect _bounds;
        [SerializeField] private int _capacity;
        [SerializeField] private int _maxDepth;

        private Node _root;
        private List<Vector2> _pointPositions;
        private List<T> _pointItems;
        private Dictionary<T, int> _index;
        private IEqualityComparer<T> _comparer;

        private QuadTree(Rect bounds, int capacity, int maxDepth, IEqualityComparer<T> comparer)
        {
            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _comparer = comparer ?? EqualityComparer<T>.Default;
            InitStorage();
        }

        /// <summary>Creates a quadtree covering <paramref name="bounds"/>.</summary>
        public static QuadTree<T> Create(Rect bounds) =>
            Create(bounds, DefaultCapacity, DefaultMaxDepth, null);

        /// <summary>Creates a quadtree with custom node capacity and depth cap.</summary>
        public static QuadTree<T> Create(Rect bounds, int capacity, int maxDepth) =>
            Create(bounds, capacity, maxDepth, null);

        /// <summary>Creates a quadtree with a custom item comparer.</summary>
        public static QuadTree<T> Create(Rect bounds, int capacity, int maxDepth,
            IEqualityComparer<T> comparer)
        {
            RequirePositiveSize(bounds);
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequirePositiveCapacity(maxDepth, nameof(maxDepth));
            return new QuadTree<T>(bounds, capacity, maxDepth, comparer);
        }

        /// <summary>The fixed region covered by this tree.</summary>
        public Rect Bounds => _bounds;

        /// <summary>Number of stored items.</summary>
        public int Count => _index.Count;

        /// <summary>Returns true when <paramref name="item"/> is stored.</summary>
        public bool Contains(T item) => _index.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> at <paramref name="position"/>. Returns
        /// false when the item already exists or the position is outside the bounds.
        /// </summary>
        public bool Insert(Vector2 position, T item)
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
        public bool TryGetPosition(T item, out Vector2 position)
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
        /// <see cref="QueryRect(Rect, List{T})"/> overload with a reusable buffer to
        /// avoid a per-call allocation.
        /// </summary>
        public void QueryRect(Rect area, out List<T> results)
        {
            results = new List<T>();
            QueryRect(area, results);
        }

        /// <summary>
        /// Collects every item whose position lies within <paramref name="area"/>
        /// into the caller-supplied <paramref name="results"/> buffer. The buffer is
        /// cleared first, then refilled; reuse it across calls to keep the query
        /// allocation-free.
        /// </summary>
        public void QueryRect(Rect area, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            QueryRectNode(_root, area, results);
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
            QueryRadiusNode(_root, center, radius * radius, radius, results);
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

        private static void RequirePositiveSize(Rect bounds)
        {
            if (bounds.width <= 0f || bounds.height <= 0f)
                throw new CollectionException(
                    $"QuadTree bounds must have a positive size, got {bounds.size}.");
        }

        private void InitStorage()
        {
            _root = new Node { Bounds = _bounds };
            _pointPositions = new List<Vector2>();
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

                node = node.Children[QuadrantOf(node, _pointPositions[pointId])];
                depth++;
            }
        }

        private void Subdivide(Node node, int depth)
        {
            var b = node.Bounds;
            var hw = b.width * 0.5f;
            var hh = b.height * 0.5f;
            var x = b.xMin;
            var y = b.yMin;

            node.Children = new[]
            {
                new Node { Bounds = new Rect(x, y + hh, hw, hh) },      // 0: top-left
                new Node { Bounds = new Rect(x + hw, y + hh, hw, hh) }, // 1: top-right
                new Node { Bounds = new Rect(x, y, hw, hh) },           // 2: bottom-left
                new Node { Bounds = new Rect(x + hw, y, hw, hh) },      // 3: bottom-right
            };

            var pending = new List<int>(node.Items);
            node.Items.Clear();
            var childDepth = depth + 1;
            foreach (var id in pending)
            {
                var child = node.Children[QuadrantOf(node, _pointPositions[id])];
                child.Items.Add(id);
            }

            // Recursively subdivide overfull children.
            if (childDepth >= _maxDepth) return;
            foreach (var child in node.Children)
                if (child.Items.Count > _capacity)
                    Subdivide(child, childDepth);
        }

        private static int QuadrantOf(Node node, Vector2 p)
        {
            var b = node.Bounds;
            var midX = b.xMin + b.width * 0.5f;
            var midY = b.yMin + b.height * 0.5f;
            var right = p.x >= midX;
            var top = p.y >= midY;
            if (top) return right ? 1 : 0;
            return right ? 3 : 2;
        }

        private void QueryRectNode(Node node, Rect area, List<T> results)
        {
            if (!node.Bounds.Overlaps(area)) return;

            if (node.IsLeaf)
            {
                foreach (var id in node.Items)
                    if (area.Contains(_pointPositions[id]))
                        results.Add(_pointItems[id]);
                return;
            }

            foreach (var child in node.Children)
                QueryRectNode(child, area, results);
        }

        private void QueryRadiusNode(Node node, Vector2 center, float sqrRadius,
            float radius, List<T> results)
        {
            if (!CircleOverlapsRect(center, radius, node.Bounds)) return;

            if (node.IsLeaf)
            {
                foreach (var id in node.Items)
                    if ((_pointPositions[id] - center).sqrMagnitude <= sqrRadius)
                        results.Add(_pointItems[id]);
                return;
            }

            foreach (var child in node.Children)
                QueryRadiusNode(child, center, sqrRadius, radius, results);
        }

        private static bool CircleOverlapsRect(Vector2 center, float radius, Rect rect)
        {
            var cx = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            var cy = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            var dx = center.x - cx;
            var dy = center.y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        private void Rebuild()
        {
            var positions = _pointPositions;
            var items = _pointItems;
            _root = new Node { Bounds = _bounds };
            _pointPositions = new List<Vector2>(positions.Count);
            _pointItems = new List<T>(items.Count);
            _index = new Dictionary<T, int>(_comparer);
            for (var i = 0; i < items.Count; i++)
                Insert(positions[i], items[i]);
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<SpatialItem2D<T>> buffer)
        {
            if (_pointItems == null || _pointPositions == null) return;
            for (var i = 0; i < _pointItems.Count; i++)
                buffer.Add(new SpatialItem2D<T>(_pointPositions[i], _pointItems[i]));
        }

        protected override void ReadSerializedItems(IReadOnlyList<SpatialItem2D<T>> items)
        {
            if (_capacity <= 0) _capacity = DefaultCapacity;
            if (_maxDepth <= 0) _maxDepth = DefaultMaxDepth;
            if (_bounds.width <= 0f || _bounds.height <= 0f)
                _bounds = new Rect(0f, 0f, 1f, 1f);
            _comparer ??= EqualityComparer<T>.Default;
            InitStorage();

            for (var i = 0; i < items.Count; i++)
                Insert(items[i].Position, items[i].Item);
        }
    }
}

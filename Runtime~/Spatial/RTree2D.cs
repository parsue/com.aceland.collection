using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Dynamic 2D R-tree indexing axis-aligned rectangles. Entries occupy an area
    /// (rather than a point) and are grouped into minimum bounding rectangles,
    /// giving efficient window / overlap queries for volumetric objects (broad-phase
    /// culling, spatial joins, region selection).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes split with the classic quadratic algorithm once they exceed
    /// <c>maxEntries</c>. Insert / remove and window queries stay close to
    /// logarithmic for typical data. For point-only data prefer
    /// <see cref="KdTree2D{T}"/> or <see cref="QuadTree{T}"/>.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Item equality uses the supplied
    /// comparer (default otherwise); the comparer is not serialized and can be
    /// re-supplied via <see cref="RestoreComparer"/>. The tree is Unity-serializable:
    /// entries are flattened to a bounds/item list and re-inserted on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RTree2D<T> : SerializableCollection<BoundsItem2D<T>>
    {
        private const int DefaultMaxEntries = 8;

        private sealed class Node
        {
            public Rect Bounds;
            public bool Leaf;
            public readonly List<Node> Children = new();
            public T Item;       // leaf-entry payload
            public bool HasItem; // true for leaf-entry nodes
        }

        [SerializeField] private int _maxEntries;
        [SerializeField] private int _minEntries;

        private Node _root;
        private Dictionary<T, Rect> _entries;
        private IEqualityComparer<T> _comparer;

        private RTree2D(int maxEntries, IEqualityComparer<T> comparer)
        {
            _maxEntries = maxEntries;
            _minEntries = Mathf.Max(2, maxEntries / 2);
            _comparer = comparer ?? EqualityComparer<T>.Default;
            InitStorage();
        }

        /// <summary>Creates an R-tree with the default node fan-out.</summary>
        public static RTree2D<T> Create() => new(DefaultMaxEntries, null);

        /// <summary>Creates an R-tree with a custom maximum node fan-out.</summary>
        public static RTree2D<T> Create(int maxEntries)
        {
            RequireFanOut(maxEntries);
            return new RTree2D<T>(maxEntries, null);
        }

        /// <summary>Creates an R-tree with a custom fan-out and item comparer.</summary>
        public static RTree2D<T> Create(int maxEntries, IEqualityComparer<T> comparer)
        {
            RequireFanOut(maxEntries);
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new RTree2D<T>(maxEntries, comparer);
        }

        /// <summary>Number of stored entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Returns true when <paramref name="item"/> is stored.</summary>
        public bool Contains(T item) => _entries.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> with bounding rectangle
        /// <paramref name="bounds"/>. Returns false when the item already exists.
        /// </summary>
        public bool Insert(Rect bounds, T item)
        {
            if (_entries.ContainsKey(item)) return false;

            _entries[item] = bounds;
            var entry = new Node { Bounds = bounds, Leaf = true, Item = item, HasItem = true };
            InsertEntry(entry);
            return true;
        }

        /// <summary>Reads the stored bounds of <paramref name="item"/>.</summary>
        public bool TryGetBounds(T item, out Rect bounds) =>
            _entries.TryGetValue(item, out bounds);

        /// <summary>Removes <paramref name="item"/>. Returns false when absent.</summary>
        public bool Remove(T item)
        {
            if (!_entries.ContainsKey(item)) return false;

            _entries.Remove(item);
            // Simple, robust strategy: rebuild from the remaining entries.
            RebuildFromEntries();
            return true;
        }

        /// <summary>
        /// Collects every item whose bounds overlap <paramref name="area"/> into
        /// a freshly allocated <paramref name="results"/> list. Prefer the
        /// <see cref="Query(Rect, List{T})"/> overload with a reusable buffer to
        /// avoid a per-call allocation.
        /// </summary>
        public void Query(Rect area, out List<T> results)
        {
            results = new List<T>();
            Query(area, results);
        }

        /// <summary>
        /// Collects every item whose bounds overlap <paramref name="area"/> into
        /// the caller-supplied <paramref name="results"/> buffer. The buffer is
        /// cleared first, then refilled; reuse it across calls to keep the query
        /// allocation-free.
        /// </summary>
        public void Query(Rect area, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            QueryNode(_root, area, results);
        }

        /// <summary>
        /// Collects every item whose bounds contain <paramref name="point"/> into
        /// a freshly allocated <paramref name="results"/> list. Prefer the
        /// <see cref="QueryPoint(Vector2, List{T})"/> overload with a reusable buffer
        /// to avoid a per-call allocation.
        /// </summary>
        public void QueryPoint(Vector2 point, out List<T> results)
        {
            results = new List<T>();
            QueryPoint(point, results);
        }

        /// <summary>
        /// Collects every item whose bounds contain <paramref name="point"/> into
        /// the caller-supplied <paramref name="results"/> buffer. The buffer is
        /// cleared first, then refilled; reuse it across calls to keep the query
        /// allocation-free.
        /// </summary>
        public void QueryPoint(Vector2 point, List<T> results)
        {
            CollectionError.RequireNotNull(results, nameof(results));
            results.Clear();
            QueryPointNode(_root, point, results);
        }

        /// <summary>Removes all entries, keeping the fan-out settings.</summary>
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
            var rebuilt = new Dictionary<T, Rect>(_comparer);
            foreach (var pair in _entries)
                rebuilt[pair.Key] = pair.Value;
            _entries = rebuilt;
            RebuildFromEntries();
        }

        // ---- Internal helpers ----------------------------------------------

        private static void RequireFanOut(int maxEntries)
        {
            if (maxEntries < 4)
                throw new CollectionException(
                    $"R-tree 'maxEntries' must be at least 4, got {maxEntries}.");
        }

        private void InitStorage()
        {
            _root = new Node { Leaf = true };
            _entries = new Dictionary<T, Rect>(_comparer);
        }

        private void RebuildFromEntries()
        {
            var snapshot = _entries;
            _root = new Node { Leaf = true };
            foreach (var pair in snapshot)
                InsertEntry(new Node
                {
                    Bounds = pair.Value, Leaf = true, Item = pair.Key, HasItem = true
                });
        }

        private void InsertEntry(Node entry)
        {
            var path = new List<Node>();
            var node = _root;
            path.Add(node);
            while (!node.Leaf)
            {
                node = ChooseSubtree(node, entry.Bounds);
                path.Add(node);
            }

            node.Children.Add(entry);
            AdjustBounds(node);

            // Split upwards while nodes overflow.
            for (var level = path.Count - 1; level >= 0; level--)
            {
                var current = path[level];
                if (current.Children.Count <= _maxEntries)
                {
                    AdjustBounds(current);
                    continue;
                }

                var split = SplitNode(current);
                if (level == 0)
                {
                    var newRoot = new Node { Leaf = false };
                    newRoot.Children.Add(current);
                    newRoot.Children.Add(split);
                    AdjustBounds(current);
                    AdjustBounds(split);
                    AdjustBounds(newRoot);
                    _root = newRoot;
                }
                else
                {
                    var parent = path[level - 1];
                    parent.Children.Add(split);
                    AdjustBounds(current);
                    AdjustBounds(split);
                }
            }

            AdjustBounds(_root);
        }

        private static Node ChooseSubtree(Node node, Rect bounds)
        {
            Node best = null;
            var bestEnlargement = float.PositiveInfinity;
            var bestArea = float.PositiveInfinity;
            foreach (var child in node.Children)
            {
                var area = Area(child.Bounds);
                var enlargement = Area(Union(child.Bounds, bounds)) - area;
                if (enlargement < bestEnlargement ||
                    (enlargement == bestEnlargement && area < bestArea))
                {
                    best = child;
                    bestEnlargement = enlargement;
                    bestArea = area;
                }
            }

            return best;
        }

        private Node SplitNode(Node node)
        {
            var items = node.Children;
            PickSeeds(items, out var iA, out var iB);

            var groupA = node; // reuse node as group A
            var groupB = new Node { Leaf = node.Leaf };

            var seedA = items[iA];
            var seedB = items[iB];
            var remaining = new List<Node>(items);
            // Remove the higher index first to keep indices valid.
            if (iA > iB) { remaining.RemoveAt(iA); remaining.RemoveAt(iB); }
            else { remaining.RemoveAt(iB); remaining.RemoveAt(iA); }

            groupA.Children.Clear();
            groupA.Children.Add(seedA);
            groupA.Bounds = seedA.Bounds;
            groupB.Children.Add(seedB);
            groupB.Bounds = seedB.Bounds;

            for (var r = 0; r < remaining.Count; r++)
            {
                var entry = remaining[r];
                var left = remaining.Count - r; // entries still to assign, incl. this one

                Node target;
                if (groupA.Children.Count + left <= _minEntries) target = groupA;
                else if (groupB.Children.Count + left <= _minEntries) target = groupB;
                else
                {
                    var enlargeA = Area(Union(groupA.Bounds, entry.Bounds)) - Area(groupA.Bounds);
                    var enlargeB = Area(Union(groupB.Bounds, entry.Bounds)) - Area(groupB.Bounds);
                    if (enlargeA < enlargeB) target = groupA;
                    else if (enlargeB < enlargeA) target = groupB;
                    else target = groupA.Children.Count <= groupB.Children.Count ? groupA : groupB;
                }

                target.Children.Add(entry);
                target.Bounds = Union(target.Bounds, entry.Bounds);
            }

            return groupB;
        }

        private static void PickSeeds(List<Node> items, out int iA, out int iB)
        {
            iA = 0;
            iB = 1;
            var worst = float.NegativeInfinity;
            for (var i = 0; i < items.Count; i++)
            for (var j = i + 1; j < items.Count; j++)
            {
                var combined = Union(items[i].Bounds, items[j].Bounds);
                var d = Area(combined) - Area(items[i].Bounds) - Area(items[j].Bounds);
                if (d > worst)
                {
                    worst = d;
                    iA = i;
                    iB = j;
                }
            }
        }

        private static void AdjustBounds(Node node)
        {
            if (node.Children.Count == 0)
            {
                node.Bounds = new Rect(0f, 0f, 0f, 0f);
                return;
            }

            var bounds = node.Children[0].Bounds;
            for (var i = 1; i < node.Children.Count; i++)
                bounds = Union(bounds, node.Children[i].Bounds);
            node.Bounds = bounds;
        }

        private void QueryNode(Node node, Rect area, List<T> results)
        {
            if (node.Children.Count > 0 && !node.Bounds.Overlaps(area)) return;

            foreach (var child in node.Children)
            {
                if (child.HasItem)
                {
                    if (child.Bounds.Overlaps(area))
                        results.Add(child.Item);
                }
                else if (child.Bounds.Overlaps(area))
                {
                    QueryNode(child, area, results);
                }
            }
        }

        private void QueryPointNode(Node node, Vector2 point, List<T> results)
        {
            if (node.Children.Count > 0 && !node.Bounds.Contains(point)) return;

            foreach (var child in node.Children)
            {
                if (child.HasItem)
                {
                    if (child.Bounds.Contains(point))
                        results.Add(child.Item);
                }
                else if (child.Bounds.Contains(point))
                {
                    QueryPointNode(child, point, results);
                }
            }
        }

        private static Rect Union(Rect a, Rect b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static float Area(Rect r) => r.width * r.height;

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<BoundsItem2D<T>> buffer)
        {
            if (_entries == null) return;
            foreach (var pair in _entries)
                buffer.Add(new BoundsItem2D<T>(pair.Value, pair.Key));
        }

        protected override void ReadSerializedItems(IReadOnlyList<BoundsItem2D<T>> items)
        {
            if (_maxEntries < 4) _maxEntries = DefaultMaxEntries;
            _minEntries = Mathf.Max(2, _maxEntries / 2);
            _comparer ??= EqualityComparer<T>.Default;
            InitStorage();
            for (var i = 0; i < items.Count; i++)
                Insert(items[i].Bounds, items[i].Item);
        }
    }
}

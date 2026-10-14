using System;
using Unity.Collections;
using Unity.Mathematics;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Burst-friendly static 3D k-d tree over a set of positioned unmanaged items,
    /// specialised for nearest-neighbour and within-radius queries.
    /// </summary>
    /// <typeparam name="T">Unmanaged payload type (an entity handle, id, index, etc.).</typeparam>
    /// <remarks>
    /// <para>
    /// The tree stores a flat <see cref="NativeList{T}"/> of nodes (int-indexed left/right
    /// children) built via median-split (quickselect), giving a balanced tree in O(n log n).
    /// </para>
    /// <para>
    /// <b>Build modes:</b> the default is a one-shot balanced build from a
    /// <c>NativeArray&lt;point&gt;</c> supplied at construction. For advanced scenarios the
    /// point set can be replaced and the tree rebuilt in place via
    /// <see cref="Rebuild(NativeArray{float3}, NativeArray{T})"/>.
    /// </para>
    /// <para>
    /// Owns native memory: construct with an <see cref="Allocator"/> and call
    /// <see cref="Dispose()"/> when done. Read-only queries are safe inside Burst jobs.
    /// </para>
    /// </remarks>
    public struct NativeKdTree3D<T> : IDisposable
        where T : unmanaged
    {
        private struct KdNode
        {
            public int Point; // index into _positions / _items
            public int Left;
            public int Right;
        }

        private NativeList<float3> _positions;
        private NativeList<T> _items;
        private NativeList<KdNode> _nodes;
        private NativeList<int> _ids; // scratch used during build
        private int _root;
        private readonly Allocator _allocator;

        /// <summary>Creates an empty tree (use <see cref="Rebuild"/> to populate).</summary>
        public NativeKdTree3D(Allocator allocator)
        {
            _allocator = allocator;
            _positions = new NativeList<float3>(allocator);
            _items = new NativeList<T>(allocator);
            _nodes = new NativeList<KdNode>(allocator);
            _ids = new NativeList<int>(allocator);
            _root = -1;
        }

        /// <summary>
        /// Creates a tree and performs a one-shot balanced build from the given points.
        /// <paramref name="points"/> and <paramref name="items"/> must be the same length.
        /// The source arrays are copied; the caller retains ownership of them.
        /// </summary>
        public NativeKdTree3D(NativeArray<float3> points, NativeArray<T> items, Allocator allocator)
            : this(allocator)
        {
            Rebuild(points, items);
        }

        /// <summary>Number of stored points.</summary>
        public int Count => _positions.Length;

        /// <summary>True when the tree holds no points.</summary>
        public bool IsEmpty => _positions.Length == 0;

        /// <summary>True when the backing native memory is allocated.</summary>
        public bool IsCreated => _positions.IsCreated;

        /// <summary>
        /// Internal read access to the stored positions used by the DOTS
        /// conversion bridge to project this tree back to a managed one.
        /// </summary>
        internal NativeList<float3> BackingPositions => _positions;

        /// <summary>
        /// Internal read access to the stored items used by the DOTS conversion
        /// bridge to project this tree back to a managed one.
        /// </summary>
        internal NativeList<T> BackingItems => _items;

        /// <summary>
        /// Replaces the point set and rebuilds the balanced tree in place.
        /// <paramref name="points"/> and <paramref name="items"/> must have equal length.
        /// </summary>
        public void Rebuild(NativeArray<float3> points, NativeArray<T> items)
        {
            if (points.Length != items.Length)
                throw new ArgumentException("points and items must have the same length.");

            _positions.Clear();
            _items.Clear();
            var n = points.Length;
            _positions.ResizeUninitialized(n);
            _items.ResizeUninitialized(n);
            for (var i = 0; i < n; i++)
            {
                _positions[i] = points[i];
                _items[i] = items[i];
            }

            _nodes.Clear();
            _nodes.ResizeUninitialized(n);
            _ids.Clear();
            _ids.ResizeUninitialized(n);
            for (var i = 0; i < n; i++) _ids[i] = i;

            _root = Build(0, n, 0);
        }

        /// <summary>Finds the item closest to <paramref name="target"/>. Returns false when empty.</summary>
        public bool TryFindNearest(float3 target, out T nearest)
        {
            nearest = default;
            if (_positions.Length == 0) return false;

            var best = -1;
            var bestSqr = float.PositiveInfinity;
            Nearest(_root, target, 0, ref best, ref bestSqr);
            nearest = _items[_nodes[best].Point];
            return true;
        }

        /// <summary>
        /// Collects every item within <paramref name="radius"/> of <paramref name="target"/>
        /// into <paramref name="results"/> (cleared first).
        /// </summary>
        public void QueryRadius(float3 target, float radius, NativeList<T> results)
        {
            results.Clear();
            if (radius < 0f || _positions.Length == 0) return;

            WithinRadius(_root, target, radius * radius, 0, results);
        }

        /// <summary>Releases the native memory.</summary>
        public void Dispose()
        {
            if (_positions.IsCreated) _positions.Dispose();
            if (_items.IsCreated) _items.Dispose();
            if (_nodes.IsCreated) _nodes.Dispose();
            if (_ids.IsCreated) _ids.Dispose();
        }

        // ---- Build ----------------------------------------------------------

        private int Build(int start, int end, int depth)
        {
            if (start >= end) return -1;

            var axis = depth % 3;
            var mid = (start + end) / 2;
            NthElement(start, end, mid, axis);

            var nodeIndex = mid;
            _nodes[nodeIndex] = new KdNode
            {
                Point = _ids[mid],
                Left = Build(start, mid, depth + 1),
                Right = Build(mid + 1, end, depth + 1),
            };
            return nodeIndex;
        }

        // Quickselect partition of _ids[start..end) so element at k is in sorted position along axis.
        private void NthElement(int start, int end, int k, int axis)
        {
            var lo = start;
            var hi = end - 1;
            while (lo < hi)
            {
                var pivot = Coord(_ids[(lo + hi) / 2], axis);
                var i = lo;
                var j = hi;
                while (i <= j)
                {
                    while (Coord(_ids[i], axis) < pivot) i++;
                    while (Coord(_ids[j], axis) > pivot) j--;
                    if (i <= j)
                    {
                        (_ids[i], _ids[j]) = (_ids[j], _ids[i]);
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
            return axis == 0 ? p.x : axis == 1 ? p.y : p.z;
        }

        // ---- Queries --------------------------------------------------------

        private void Nearest(int node, float3 target, int depth, ref int best, ref float bestSqr)
        {
            if (node < 0) return;

            var pointId = _nodes[node].Point;
            var sqr = math.distancesq(_positions[pointId], target);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }

            var axis = depth % 3;
            var diff = TargetAxis(target, axis) - Coord(pointId, axis);
            var near = diff <= 0f ? _nodes[node].Left : _nodes[node].Right;
            var far = diff <= 0f ? _nodes[node].Right : _nodes[node].Left;

            Nearest(near, target, depth + 1, ref best, ref bestSqr);
            if (diff * diff < bestSqr)
                Nearest(far, target, depth + 1, ref best, ref bestSqr);
        }

        private void WithinRadius(int node, float3 target, float sqrRadius, int depth, NativeList<T> results)
        {
            if (node < 0) return;

            var pointId = _nodes[node].Point;
            if (math.distancesq(_positions[pointId], target) <= sqrRadius)
                results.Add(_items[pointId]);

            var axis = depth % 3;
            var diff = TargetAxis(target, axis) - Coord(pointId, axis);
            var near = diff <= 0f ? _nodes[node].Left : _nodes[node].Right;
            var far = diff <= 0f ? _nodes[node].Right : _nodes[node].Left;

            WithinRadius(near, target, sqrRadius, depth + 1, results);
            if (diff * diff <= sqrRadius)
                WithinRadius(far, target, sqrRadius, depth + 1, results);
        }

        private static float TargetAxis(float3 target, int axis) =>
            axis == 0 ? target.x : axis == 1 ? target.y : target.z;
    }
}

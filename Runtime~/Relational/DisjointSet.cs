using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Union-Find over arbitrary elements, with path compression and union by
    /// rank for near-O(1) amortised <see cref="Find"/> / <see cref="Union"/>.
    /// </summary>
    /// <remarks>
    /// <para>Use: connectivity checks, map-region merging, group merge, Kruskal MST.</para>
    /// <para>
    /// Elements are added lazily on first reference (each starts in its own set).
    /// Construct through <see cref="Create()"/>. The element comparer is not
    /// serialized; after Unity deserialization the default comparer is used
    /// unless re-supplied via <see cref="RestoreComparer"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DisjointSet<T> : SerializableCollection<DisjointSetElement<T>>
    {
        private List<int> _parent = new();
        private List<int> _rank = new();
        private List<T> _elements = new();
        private Dictionary<T, int> _indexOf;
        private IEqualityComparer<T> _comparer;
        private int _setCount;

        private DisjointSet(IEqualityComparer<T> comparer)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _indexOf = new Dictionary<T, int>(_comparer);
        }

        /// <summary>Creates a disjoint-set using the default element comparer.</summary>
        public static DisjointSet<T> Create() => new(null);

        /// <summary>Creates a disjoint-set using a custom element comparer.</summary>
        public static DisjointSet<T> Create(IEqualityComparer<T> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new DisjointSet<T>(comparer);
        }

        /// <summary>Total number of distinct elements tracked.</summary>
        public int Count => _elements.Count;

        /// <summary>Number of disjoint sets (connected components).</summary>
        public int SetCount => _setCount;

        /// <summary>Returns true when <paramref name="item"/> has been added.</summary>
        public bool Contains(T item) => _indexOf.ContainsKey(item);

        /// <summary>
        /// Ensures <paramref name="item"/> exists as its own singleton set.
        /// Returns true when newly added, false when already present.
        /// </summary>
        public bool MakeSet(T item)
        {
            if (_indexOf.ContainsKey(item))
                return false;

            var index = _elements.Count;
            _indexOf[item] = index;
            _elements.Add(item);
            _parent.Add(index);
            _rank.Add(0);
            _setCount++;
            return true;
        }

        /// <summary>
        /// Merges the sets containing <paramref name="a"/> and <paramref name="b"/>
        /// (adding either if absent). Returns true when a merge happened, false
        /// when they were already in the same set.
        /// </summary>
        public bool Union(T a, T b)
        {
            var rootA = FindRoot(GetOrAdd(a));
            var rootB = FindRoot(GetOrAdd(b));
            if (rootA == rootB)
                return false;

            // Union by rank: attach the shallower tree under the deeper one.
            if (_rank[rootA] < _rank[rootB])
                (rootA, rootB) = (rootB, rootA);

            _parent[rootB] = rootA;
            if (_rank[rootA] == _rank[rootB])
                _rank[rootA]++;

            _setCount--;
            return true;
        }

        /// <summary>
        /// Returns the representative element of <paramref name="item"/>'s set,
        /// adding it if absent. Returns false only when <paramref name="item"/>
        /// cannot be resolved (never, since it is added on demand).
        /// </summary>
        public bool Find(T item, out T representative)
        {
            var root = FindRoot(GetOrAdd(item));
            representative = _elements[root];
            return true;
        }

        /// <summary>
        /// Returns true when <paramref name="a"/> and <paramref name="b"/> belong
        /// to the same set. Missing elements are added on demand.
        /// </summary>
        public bool Connected(T a, T b) =>
            FindRoot(GetOrAdd(a)) == FindRoot(GetOrAdd(b));

        /// <summary>Size of the set containing <paramref name="item"/> (0 when absent).</summary>
        public int SizeOf(T item)
        {
            if (!_indexOf.TryGetValue(item, out var index))
                return 0;

            var root = FindRoot(index);
            var size = 0;
            for (var i = 0; i < _parent.Count; i++)
                if (FindRoot(i) == root)
                    size++;

            return size;
        }

        /// <summary>Removes every element and set.</summary>
        public void Clear()
        {
            _parent.Clear();
            _rank.Clear();
            _elements.Clear();
            _indexOf.Clear();
            _setCount = 0;
        }

        /// <summary>
        /// Re-attaches a custom element comparer (e.g. after Unity
        /// deserialization) and rebuilds the index map. Passing null falls back
        /// to the default comparer.
        /// </summary>
        public void RestoreComparer(IEqualityComparer<T> comparer)
        {
            var next = comparer ?? EqualityComparer<T>.Default;
            if (ReferenceEquals(next, _comparer)) return;

            _comparer = next;
            Reindex();
        }

        // ---- Union-Find mechanics ------------------------------------------

        private int GetOrAdd(T item)
        {
            if (_indexOf.TryGetValue(item, out var index))
                return index;

            MakeSet(item);
            return _indexOf[item];
        }

        // Iterative find with path compression to the root.
        private int FindRoot(int index)
        {
            var root = index;
            while (_parent[root] != root)
                root = _parent[root];

            // Compress: point every node on the path directly at the root.
            while (_parent[index] != root)
            {
                var next = _parent[index];
                _parent[index] = root;
                index = next;
            }

            return root;
        }

        private void Reindex()
        {
            _indexOf = new Dictionary<T, int>(_comparer);
            for (var i = 0; i < _elements.Count; i++)
                _indexOf[_elements[i]] = i;
        }

        // ---- Serialization --------------------------------------------------
        // Members are written first (each restored via MakeSet) followed by the
        // union pairs that reconnect them, mirroring GraphElement's node/edge
        // split. This persists the full partition, not just the flat elements.

        protected override void WriteSerializedItems(List<DisjointSetElement<T>> buffer)
        {
            // Unity may serialize a [SerializeField] set before the field
            // initializer / constructor has run, leaving _elements null.
            if (_elements == null) return;

            // Members first so deserialization recreates them in index order.
            for (var i = 0; i < _elements.Count; i++)
                buffer.Add(DisjointSetElement<T>.ForMember(_elements[i]));

            // Then one union per non-root element, linking it to its set's
            // representative. Replaying these rebuilds the exact partition.
            for (var i = 0; i < _elements.Count; i++)
            {
                var root = FindRoot(i);
                if (root != i)
                    buffer.Add(DisjointSetElement<T>.ForUnion(_elements[i], _elements[root]));
            }
        }

        protected override void ReadSerializedItems(IReadOnlyList<DisjointSetElement<T>> items)
        {
            // Unity may invoke OnAfterDeserialize on an instance built via the
            // serialization path (constructor / field initializers skipped),
            // leaving the backing lists null. Mirror WriteSerializedItems' guard.
            _comparer ??= EqualityComparer<T>.Default;
            _parent ??= new List<int>();
            _rank ??= new List<int>();
            _elements ??= new List<T>();
            _parent.Clear();
            _rank.Clear();
            _elements.Clear();
            _indexOf = new Dictionary<T, int>(_comparer);
            _setCount = 0;

            // Recreate every member before applying unions so representatives
            // resolve against the full element set.
            for (var i = 0; i < items.Count; i++)
                if (!items[i].IsUnion)
                    MakeSet(items[i].From);

            for (var i = 0; i < items.Count; i++)
                if (items[i].IsUnion)
                    Union(items[i].From, items[i].To);
        }
    }
}

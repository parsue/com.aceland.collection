using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Bounded key/value cache with Least-Recently-Used eviction. Get / Set /
    /// Remove are O(1) via a dictionary plus an intrusive doubly-linked list
    /// (most-recent at the front, least-recent at the back).
    /// </summary>
    /// <remarks>
    /// <para>Game use: asset / texture / model caches. Software use: query and
    /// computation-result caches.</para>
    /// <para>
    /// The eviction policy is pluggable at the class level (see
    /// <see cref="EvictionPolicy"/>); only LRU ships today, with LFU reserved as
    /// a future extension point.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(int)"/>. Unity-serializable: entries
    /// persist MRU→LRU and are re-inserted in reverse so recency order survives
    /// a Play/Edit round-trip.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class LruCache<TKey, TValue> :
        SerializableCollection<CacheEntry<TKey, TValue>>,
        IReadOnlyCollection<CacheEntry<TKey, TValue>>
    {
        /// <summary>Eviction strategies. LRU is implemented; LFU is reserved.</summary>
        public enum EvictionPolicy
        {
            /// <summary>Evict the least recently used entry.</summary>
            Lru
        }

        private sealed class Node
        {
            public TKey Key;
            public TValue Value;
            public Node Prev;
            public Node Next;
        }

        [SerializeField]
        private int _capacity;

        private Dictionary<TKey, Node> _map;
        private Node _head; // most recently used
        private Node _tail; // least recently used

        private LruCache(int capacity, IEqualityComparer<TKey> comparer)
        {
            _capacity = capacity;
            // The map never exceeds _capacity entries, so pre-sizing to capacity
            // eliminates the incremental resize garbage the dictionary would
            // otherwise produce as it grows towards the bound.
            _map = new Dictionary<TKey, Node>(capacity, comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Creates an LRU cache with the given capacity.</summary>
        /// <param name="capacity">Maximum entry count; must be positive.</param>
        public static LruCache<TKey, TValue> Create(int capacity)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            return new LruCache<TKey, TValue>(capacity, null);
        }

        /// <summary>Creates an LRU cache with a custom key comparer.</summary>
        public static LruCache<TKey, TValue> Create(int capacity, IEqualityComparer<TKey> comparer)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new LruCache<TKey, TValue>(capacity, comparer);
        }

        /// <summary>Maximum number of entries retained before eviction.</summary>
        public int Capacity => _capacity;

        /// <summary>Current number of cached entries.</summary>
        public int Count => _map.Count;

        /// <summary>True when the cache holds no entries.</summary>
        public bool IsEmpty => _map.Count == 0;

        /// <summary>
        /// Inserts or updates <paramref name="key"/>, marking it most-recently
        /// used. When capacity is exceeded the least-recently-used entry is
        /// evicted and returned via <paramref name="evicted"/>.
        /// </summary>
        /// <returns><c>true</c> when an entry was evicted.</returns>
        public bool Set(TKey key, TValue value, out CacheEntry<TKey, TValue> evicted)
        {
            evicted = default;

            if (_map.TryGetValue(key, out var node))
            {
                node.Value = value;
                MoveToFront(node);
                return false;
            }

            node = new Node { Key = key, Value = value };
            _map[key] = node;
            AddToFront(node);

            if (_map.Count <= _capacity) return false;

            var lru = _tail;
            RemoveNode(lru);
            _map.Remove(lru.Key);
            evicted = new CacheEntry<TKey, TValue>(lru.Key, lru.Value);
            return true;
        }

        /// <summary>Inserts or updates a key, discarding any evicted entry.</summary>
        public void Set(TKey key, TValue value) => Set(key, value, out _);

        /// <summary>
        /// Looks up <paramref name="key"/>; on hit marks it most-recently used.
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                MoveToFront(node);
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Reads a value without affecting recency order.</summary>
        public bool TryPeek(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>True when the key is cached (does not affect recency).</summary>
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>Removes a key if present.</summary>
        public bool Remove(TKey key)
        {
            if (!_map.TryGetValue(key, out var node)) return false;
            RemoveNode(node);
            _map.Remove(key);
            return true;
        }

        /// <summary>Removes all entries, keeping the capacity.</summary>
        public void Clear()
        {
            _map.Clear();
            _head = null;
            _tail = null;
        }

        /// <summary>Enumerates entries from most- to least-recently used.</summary>
        public IEnumerator<CacheEntry<TKey, TValue>> GetEnumerator()
        {
            for (var n = _head; n is not null; n = n.Next)
                yield return new CacheEntry<TKey, TValue>(n.Key, n.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- linked-list plumbing ------------------------------------------

        private void AddToFront(Node node)
        {
            node.Prev = null;
            node.Next = _head;
            if (_head is not null) _head.Prev = node;
            _head = node;
            _tail ??= node;
        }

        private void RemoveNode(Node node)
        {
            if (node.Prev is not null) node.Prev.Next = node.Next;
            else _head = node.Next;

            if (node.Next is not null) node.Next.Prev = node.Prev;
            else _tail = node.Prev;

            node.Prev = null;
            node.Next = null;
        }

        private void MoveToFront(Node node)
        {
            if (ReferenceEquals(node, _head)) return;
            RemoveNode(node);
            AddToFront(node);
        }

        // ---- Serialization (MRU → LRU) -------------------------------------

        protected override void WriteSerializedItems(List<CacheEntry<TKey, TValue>> buffer)
        {
            for (var n = _head; n is not null; n = n.Next)
                buffer.Add(new CacheEntry<TKey, TValue>(n.Key, n.Value));
        }

        protected override void ReadSerializedItems(IReadOnlyList<CacheEntry<TKey, TValue>> items)
        {
            if (_capacity <= 0)
                _capacity = Mathf.Max(1, items.Count);

            // Unity deserializes via GetUninitializedObject, so the constructor
            // may not have run and _map can be null.
            _map ??= new Dictionary<TKey, Node>(_capacity);
            _map.Clear();
            _head = null;
            _tail = null;

            // Items are stored MRU→LRU; re-insert LRU→MRU so recency is restored.
            for (var i = items.Count - 1; i >= 0; i--)
                Set(items[i].Key, items[i].Value);
        }
    }
}

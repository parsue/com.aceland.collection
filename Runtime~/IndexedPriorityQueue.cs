using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Binary-heap priority queue keyed by a unique item, adding the two
    /// operations the BCL <c>PriorityQueue&lt;TElement,TPriority&gt;</c> lacks:
    /// <see cref="UpdatePriority"/> and <see cref="Remove"/>, both in O(log n).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each item may appear at most once and doubles as its own index key, so an
    /// internal position map lets an existing entry be re-prioritised or pulled
    /// out without a linear scan. This is the shape needed by A* open sets,
    /// Dijkstra frontiers and event schedulers that must revise pending costs.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(HeapMode)"/> and friends. Neither the
    /// priority comparer nor the item comparer is serialized; after Unity
    /// deserialization the defaults are used unless re-supplied via
    /// <see cref="RestoreComparers"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class IndexedPriorityQueue<TItem, TPriority> :
        SerializableCollection<IndexedPriorityEntry<TItem, TPriority>>,
        IReadOnlyCollection<IndexedPriorityEntry<TItem, TPriority>>
    {
        [SerializeField]
        private HeapMode _mode;

        private List<TItem> _heap = new();
        private Dictionary<TItem, int> _positions;
        private Dictionary<TItem, TPriority> _priorities;
        private IComparer<TPriority> _comparer;
        private IEqualityComparer<TItem> _itemComparer;

        private IndexedPriorityQueue(
            HeapMode mode,
            IComparer<TPriority> comparer,
            IEqualityComparer<TItem> itemComparer,
            int capacity = 0)
        {
            _mode = mode;
            _comparer = comparer ?? Comparer<TPriority>.Default;
            _itemComparer = itemComparer ?? EqualityComparer<TItem>.Default;
            if (capacity > 0) _heap.Capacity = capacity;
            _positions = new Dictionary<TItem, int>(capacity, _itemComparer);
            _priorities = new Dictionary<TItem, TPriority>(capacity, _itemComparer);
        }

        /// <summary>Creates a queue using the default priority/item comparers.</summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(HeapMode mode = HeapMode.Min) =>
            new(mode, null, null);

        /// <summary>Creates a queue using a custom priority comparer.</summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(
            HeapMode mode, IComparer<TPriority> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new IndexedPriorityQueue<TItem, TPriority>(mode, comparer, null);
        }

        /// <summary>Creates a queue using custom priority and item comparers.</summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(
            HeapMode mode, IComparer<TPriority> comparer, IEqualityComparer<TItem> itemComparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            CollectionError.RequireNotNull(itemComparer, nameof(itemComparer));
            return new IndexedPriorityQueue<TItem, TPriority>(mode, comparer, itemComparer);
        }

        /// <summary>
        /// Creates a queue pre-sized for <paramref name="capacity"/> items using
        /// the default priority/item comparers. Pre-allocation avoids the
        /// incremental resize garbage produced while the internal heap and index
        /// maps grow, which matters when bulk-loading a known-size frontier.
        /// </summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(
            int capacity, HeapMode mode = HeapMode.Min)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            return new IndexedPriorityQueue<TItem, TPriority>(mode, null, null, capacity);
        }

        /// <summary>
        /// Creates a queue pre-sized for <paramref name="capacity"/> items using
        /// a custom priority comparer.
        /// </summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(
            int capacity, HeapMode mode, IComparer<TPriority> comparer)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new IndexedPriorityQueue<TItem, TPriority>(mode, comparer, null, capacity);
        }

        /// <summary>
        /// Creates a queue pre-sized for <paramref name="capacity"/> items using
        /// custom priority and item comparers.
        /// </summary>
        public static IndexedPriorityQueue<TItem, TPriority> Create(
            int capacity, HeapMode mode, IComparer<TPriority> comparer, IEqualityComparer<TItem> itemComparer)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            CollectionError.RequireNotNull(itemComparer, nameof(itemComparer));
            return new IndexedPriorityQueue<TItem, TPriority>(mode, comparer, itemComparer, capacity);
        }

        /// <summary>Current number of stored items.</summary>
        public int Count => _heap.Count;

        /// <summary>True when the queue holds no items.</summary>
        public bool IsEmpty => _heap.Count == 0;

        /// <summary>Whether the root is the minimum- or maximum-priority item.</summary>
        public HeapMode Mode => _mode;

        /// <summary>Returns true when <paramref name="item"/> is queued.</summary>
        public bool Contains(TItem item) => _positions.ContainsKey(item);

        /// <summary>
        /// Inserts <paramref name="item"/> with <paramref name="priority"/>.
        /// Returns false when the item is already queued (use
        /// <see cref="UpdatePriority"/> to change an existing priority).
        /// </summary>
        public bool Enqueue(TItem item, TPriority priority)
        {
            if (_positions.ContainsKey(item))
                return false;

            _heap.Add(item);
            var index = _heap.Count - 1;
            _positions[item] = index;
            _priorities[item] = priority;
            SiftUp(index);
            return true;
        }

        /// <summary>Returns the root item and its priority without removing it.</summary>
        public bool TryPeek(out TItem item, out TPriority priority)
        {
            if (IsEmpty)
            {
                item = default;
                priority = default;
                return false;
            }

            item = _heap[0];
            priority = _priorities[item];
            return true;
        }

        /// <summary>Removes and returns the root item and its priority.</summary>
        public bool TryDequeue(out TItem item, out TPriority priority)
        {
            if (IsEmpty)
            {
                item = default;
                priority = default;
                return false;
            }

            item = _heap[0];
            priority = _priorities[item];
            RemoveAt(0);
            return true;
        }

        /// <summary>Reads the priority currently associated with an item.</summary>
        public bool TryGetPriority(TItem item, out TPriority priority) =>
            _priorities.TryGetValue(item, out priority);

        /// <summary>
        /// Changes the priority of an already-queued <paramref name="item"/> and
        /// restores heap order in O(log n). Returns false when the item is absent.
        /// </summary>
        public bool UpdatePriority(TItem item, TPriority priority)
        {
            if (!_positions.TryGetValue(item, out var index))
                return false;

            _priorities[item] = priority;
            // Reposition in whichever direction the change requires.
            SiftUp(index);
            SiftDown(_positions[item]);
            return true;
        }

        /// <summary>
        /// If present, updates the item's priority; otherwise enqueues it.
        /// Returns true when a new item was added, false when an existing one
        /// was updated.
        /// </summary>
        public bool EnqueueOrUpdate(TItem item, TPriority priority)
        {
            if (_positions.ContainsKey(item))
            {
                UpdatePriority(item, priority);
                return false;
            }

            Enqueue(item, priority);
            return true;
        }

        /// <summary>Removes <paramref name="item"/> from anywhere in the queue.</summary>
        public bool Remove(TItem item)
        {
            if (!_positions.TryGetValue(item, out var index))
                return false;

            RemoveAt(index);
            return true;
        }

        /// <summary>Removes all items.</summary>
        public void Clear()
        {
            _heap.Clear();
            _positions.Clear();
            _priorities.Clear();
        }

        /// <summary>
        /// Re-attaches custom comparers (e.g. after Unity deserialization) and
        /// re-heapifies. Passing null falls back to the default comparer.
        /// </summary>
        public void RestoreComparers(
            IComparer<TPriority> comparer, IEqualityComparer<TItem> itemComparer = null)
        {
            _comparer = comparer ?? Comparer<TPriority>.Default;

            if (itemComparer is not null && !ReferenceEquals(itemComparer, _itemComparer))
            {
                _itemComparer = itemComparer;
                Reindex();
            }

            Heapify();
        }

        public IEnumerator<IndexedPriorityEntry<TItem, TPriority>> GetEnumerator()
        {
            foreach (var item in _heap)
                yield return new IndexedPriorityEntry<TItem, TPriority>(item, _priorities[item]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Heap mechanics -------------------------------------------------

        // Returns true when 'a' should sit ABOVE 'b' (closer to root).
        private bool HasPriority(TItem a, TItem b)
        {
            var cmp = _comparer.Compare(_priorities[a], _priorities[b]);
            return _mode == HeapMode.Min ? cmp < 0 : cmp > 0;
        }

        private void SiftUp(int index)
        {
            var item = _heap[index];
            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                if (!HasPriority(item, _heap[parent])) break;
                MoveInto(index, _heap[parent]);
                index = parent;
            }

            Place(index, item);
        }

        private void SiftDown(int index)
        {
            var count = _heap.Count;
            var item = _heap[index];
            while (true)
            {
                var left = (index << 1) + 1;
                var right = left + 1;
                var best = index;
                var bestItem = item;

                if (left < count && HasPriority(_heap[left], bestItem))
                {
                    best = left;
                    bestItem = _heap[left];
                }

                if (right < count && HasPriority(_heap[right], bestItem))
                {
                    best = right;
                    bestItem = _heap[right];
                }

                if (best == index) break;

                MoveInto(index, _heap[best]);
                index = best;
            }

            Place(index, item);
        }

        private void RemoveAt(int index)
        {
            var removed = _heap[index];
            var last = _heap.Count - 1;

            if (index != last)
            {
                var moved = _heap[last];
                _heap[index] = moved;
                _positions[moved] = index;
            }

            _heap.RemoveAt(last);
            _positions.Remove(removed);
            _priorities.Remove(removed);

            if (index < _heap.Count)
            {
                SiftDown(index);
                SiftUp(_positions[_heap[index]]);
            }
        }

        // Writes 'item' into 'index' of the heap and updates its position map.
        private void Place(int index, TItem item)
        {
            _heap[index] = item;
            _positions[item] = index;
        }

        // Same as Place but used while shifting a hole downward/upward.
        private void MoveInto(int index, TItem item) => Place(index, item);

        private void Heapify()
        {
            for (var i = (_heap.Count >> 1) - 1; i >= 0; i--)
                SiftDown(i);
        }

        // Rebuilds the position/priority maps under a new item comparer.
        private void Reindex()
        {
            var newPositions = new Dictionary<TItem, int>(_itemComparer);
            var newPriorities = new Dictionary<TItem, TPriority>(_itemComparer);
            for (var i = 0; i < _heap.Count; i++)
            {
                var item = _heap[i];
                newPositions[item] = i;
                newPriorities[item] = _priorities[item];
            }

            _positions = newPositions;
            _priorities = newPriorities;
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(
            List<IndexedPriorityEntry<TItem, TPriority>> buffer)
        {
            // Unity may serialize a [SerializeField] queue before the field
            // initializer / constructor has run, leaving the maps null.
            if (_heap == null || _priorities == null) return;
            foreach (var item in _heap)
                buffer.Add(new IndexedPriorityEntry<TItem, TPriority>(item, _priorities[item]));
        }

        protected override void ReadSerializedItems(
            IReadOnlyList<IndexedPriorityEntry<TItem, TPriority>> items)
        {
            // Unity deserializes via GetUninitializedObject, so field
            // initializers / the constructor may not have run: _heap can be null.
            _comparer ??= Comparer<TPriority>.Default;
            _itemComparer ??= EqualityComparer<TItem>.Default;
            _positions = new Dictionary<TItem, int>(_itemComparer);
            _priorities = new Dictionary<TItem, TPriority>(_itemComparer);
            _heap ??= new List<TItem>();
            _heap.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (_positions.ContainsKey(entry.Item))
                    continue;

                _heap.Add(entry.Item);
                _positions[entry.Item] = _heap.Count - 1;
                _priorities[entry.Item] = entry.Priority;
            }

            Heapify();
        }
    }
}

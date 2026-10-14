using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Array-backed binary heap (single-ended priority queue). Either the
    /// minimum or the maximum sits at the root depending on <see cref="HeapMode"/>,
    /// giving O(1) peek and O(log n) push / pop.
    /// </summary>
    /// <remarks>
    /// <para>Use: priority pick-up, event scheduling, Top-K pruning.</para>
    /// <para>
    /// Fills a BCL gap: <c>PriorityQueue&lt;TElement,TPriority&gt;</c> is not
    /// Unity-serializable and has no double-ended variant (see
    /// <see cref="MinMaxHeap{T}"/>).
    /// </para>
    /// <para>
    /// Construct through <see cref="Create(HeapMode)"/> or
    /// <see cref="Create(HeapMode,IComparer{T})"/>. A custom comparer is not
    /// itself serialized; after deserialization the default comparer is used
    /// unless the comparer is re-supplied via
    /// <see cref="RestoreComparer(IComparer{T})"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class BinaryHeap<T> : SerializableCollection<T>, IReadOnlyCollection<T>
    {
        [SerializeField]
        private HeapMode _mode;

        private List<T> _heap;
        private IComparer<T> _comparer;

        private BinaryHeap(HeapMode mode, IComparer<T> comparer, int capacity)
        {
            _mode = mode;
            _comparer = comparer ?? Comparer<T>.Default;
            _heap = capacity > 0 ? new List<T>(capacity) : new List<T>();
        }

        /// <summary>Creates a heap using <see cref="Comparer{T}.Default"/>.</summary>
        public static BinaryHeap<T> Create(HeapMode mode = HeapMode.Min) =>
            new(mode, null, 0);

        /// <summary>
        /// Creates a heap using <see cref="Comparer{T}.Default"/> with the backing
        /// store pre-sized to <paramref name="capacity"/> elements. Pre-sizing when
        /// the final count is known avoids the incremental doubling reallocations of
        /// the internal array, cutting transient GC allocations.
        /// </summary>
        public static BinaryHeap<T> Create(HeapMode mode, int capacity)
        {
            CollectionError.RequireNonNegativeCapacity(capacity, nameof(capacity));
            return new BinaryHeap<T>(mode, null, capacity);
        }

        /// <summary>Creates a heap using a custom comparer.</summary>
        public static BinaryHeap<T> Create(HeapMode mode, IComparer<T> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new BinaryHeap<T>(mode, comparer, 0);
        }

        /// <summary>
        /// Creates a heap using a custom comparer with the backing store pre-sized
        /// to <paramref name="capacity"/> elements (see <see cref="Create(HeapMode,int)"/>).
        /// </summary>
        public static BinaryHeap<T> Create(HeapMode mode, IComparer<T> comparer, int capacity)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            CollectionError.RequireNonNegativeCapacity(capacity, nameof(capacity));
            return new BinaryHeap<T>(mode, comparer, capacity);
        }

        /// <summary>Current number of stored elements.</summary>
        public int Count => _heap.Count;

        /// <summary>True when the heap holds no elements.</summary>
        public bool IsEmpty => _heap.Count == 0;

        /// <summary>
        /// Ensures the backing store can hold at least <paramref name="capacity"/>
        /// elements without reallocating. Pre-sizing before a large burst of
        /// <see cref="Push"/> calls avoids the incremental doubling reallocations
        /// of the internal array, cutting transient GC allocations.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            CollectionError.RequireNonNegativeCapacity(capacity, nameof(capacity));
            if (_heap.Capacity < capacity)
                _heap.Capacity = capacity;
        }

        /// <summary>Whether the root is the minimum or the maximum.</summary>
        public HeapMode Mode => _mode;

        /// <summary>Inserts <paramref name="item"/>, restoring the heap in O(log n).</summary>
        public void Push(T item)
        {
            _heap.Add(item);
            SiftUp(_heap.Count - 1);
        }

        /// <summary>Returns the root (min or max) without removing it.</summary>
        public bool TryPeek(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _heap[0];
            return true;
        }

        /// <summary>Removes and returns the root (min or max).</summary>
        public bool TryPop(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _heap[0];
            var last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0)
                SiftDown(0);
            return true;
        }

        /// <summary>Removes all elements.</summary>
        public void Clear() => _heap.Clear();

        /// <summary>
        /// Re-attaches a custom comparer (e.g. after Unity deserialization) and
        /// re-heapifies. No-op-safe: passing null falls back to the default comparer.
        /// </summary>
        public void RestoreComparer(IComparer<T> comparer)
        {
            _comparer = comparer ?? Comparer<T>.Default;
            Heapify();
        }

        public IEnumerator<T> GetEnumerator() => _heap.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Heap mechanics -------------------------------------------------

        // Hole-based sift-up: the comparer and mode are hoisted into locals so the
        // per-comparison branch (HasPriority) and field loads leave the hot loop.
        // The sifted item is carried in a register and only one array write happens
        // per level (no full three-assignment swap).
        private void SiftUp(int index)
        {
            var heap = _heap;
            var comparer = _comparer;
            var isMin = _mode == HeapMode.Min;
            var item = heap[index];

            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                var parentItem = heap[parent];
                var cmp = comparer.Compare(item, parentItem);
                if (isMin ? cmp >= 0 : cmp <= 0) break;
                heap[index] = parentItem;
                index = parent;
            }

            heap[index] = item;
        }

        // Hole-based sift-down mirroring SiftUp: hoisted mode/comparer locals and a
        // single write per level (the sifted item is written once at its final
        // resting index instead of swapping down step by step).
        private void SiftDown(int index)
        {
            var heap = _heap;
            var count = heap.Count;
            var comparer = _comparer;
            var isMin = _mode == HeapMode.Min;
            var item = heap[index];

            while (true)
            {
                var left = (index << 1) + 1;
                if (left >= count) break;

                var best = left;
                var bestItem = heap[left];

                var right = left + 1;
                if (right < count)
                {
                    var rightItem = heap[right];
                    var c = comparer.Compare(rightItem, bestItem);
                    if (isMin ? c < 0 : c > 0)
                    {
                        best = right;
                        bestItem = rightItem;
                    }
                }

                var cmp = comparer.Compare(bestItem, item);
                if (isMin ? cmp >= 0 : cmp <= 0) break;

                heap[index] = bestItem;
                index = best;
            }

            heap[index] = item;
        }

        private void Heapify()
        {
            for (var i = (_heap.Count >> 1) - 1; i >= 0; i--)
                SiftDown(i);
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<T> buffer)
        {
            // Unity may serialize a [SerializeField] heap before the field
            // initializer / constructor has run, leaving _heap null.
            if (_heap == null) return;
            buffer.AddRange(_heap);
        }

        protected override void ReadSerializedItems(IReadOnlyList<T> items)
        {
            // Unity may deserialize an inline [Serializable] heap via an
            // uninitialized object (no ctor / field initializer run), leaving
            // _heap null. Lazily materialise it before rebuilding.
            _heap ??= new List<T>();
            _comparer ??= Comparer<T>.Default;
            _heap.Clear();
            for (var i = 0; i < items.Count; i++)
                _heap.Add(items[i]);
            Heapify();
        }
    }
}

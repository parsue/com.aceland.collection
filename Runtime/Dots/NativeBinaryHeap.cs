using System;
using System.Collections.Generic;
using Unity.Collections;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Burst-friendly binary heap (priority queue) backed by a <see cref="NativeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <typeparam name="TComparer">
    /// Value-type comparer. The element on top of the heap is the one for which
    /// <see cref="IComparer{T}.Compare"/> reports it as the smallest, i.e.
    /// <c>Compare(top, other) &lt;= 0</c>. Provide a reversed comparer for a max-heap.
    /// Because the comparer is a struct known at compile time, Burst inlines the
    /// comparison and removes interface dispatch.
    /// </typeparam>
    /// <remarks>
    /// The container owns native memory: construct with an <see cref="Allocator"/> and
    /// call <see cref="Dispose()"/> when done. Purely data-oriented; no managed
    /// allocation, no GC.
    /// </remarks>
    public struct NativeBinaryHeap<T, TComparer> : IDisposable
        where T : unmanaged
        where TComparer : struct, IComparer<T>
    {
        private NativeList<T> _items;
        private TComparer _comparer;

        /// <summary>Creates an empty heap.</summary>
        public NativeBinaryHeap(Allocator allocator, TComparer comparer)
            : this(0, allocator, comparer)
        {
        }

        /// <summary>Creates an empty heap with a pre-reserved capacity.</summary>
        public NativeBinaryHeap(int initialCapacity, Allocator allocator, TComparer comparer)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _items = new NativeList<T>(initialCapacity, allocator);
            _comparer = comparer;
        }

        /// <summary>Number of elements currently stored.</summary>
        public int Count => _items.Length;

        /// <summary>True when the heap holds no elements.</summary>
        public bool IsEmpty => _items.Length == 0;

        /// <summary>True when the backing native memory is allocated.</summary>
        public bool IsCreated => _items.IsCreated;

        /// <summary>
        /// Internal read access to the backing storage (heap order) used by the
        /// DOTS conversion bridge to project this heap back to a managed one.
        /// </summary>
        internal NativeList<T> BackingItems => _items;

        /// <summary>Inserts <paramref name="item"/> and restores the heap invariant.</summary>
        public void Push(T item)
        {
            _items.Add(item);
            SiftUp(_items.Length - 1);
        }

        /// <summary>Reads the top element without removing it.</summary>
        public bool TryPeek(out T item)
        {
            if (_items.Length == 0)
            {
                item = default;
                return false;
            }

            item = _items[0];
            return true;
        }

        /// <summary>Removes and returns the top element.</summary>
        public bool TryPop(out T item)
        {
            if (_items.Length == 0)
            {
                item = default;
                return false;
            }

            item = _items[0];
            var last = _items.Length - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            if (_items.Length > 0)
                SiftDown(0);
            return true;
        }

        /// <summary>Removes every element while keeping the allocated capacity.</summary>
        public void Clear() => _items.Clear();

        /// <summary>Releases the native memory.</summary>
        public void Dispose()
        {
            if (_items.IsCreated)
                _items.Dispose();
        }

        // ---- Heap internals -------------------------------------------------

        // a has priority over b when it should sit closer to the top (smaller by comparer).
        private bool HasPriority(in T a, in T b) => _comparer.Compare(a, b) < 0;

        private void Swap(int i, int j)
        {
            (_items[i], _items[j]) = (_items[j], _items[i]);
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                if (!HasPriority(_items[index], _items[parent]))
                    break;

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            var count = _items.Length;
            while (true)
            {
                var left = (index << 1) + 1;
                var right = left + 1;
                var best = index;

                if (left < count && HasPriority(_items[left], _items[best]))
                    best = left;
                if (right < count && HasPriority(_items[right], _items[best]))
                    best = right;
                if (best == index)
                    break;

                Swap(index, best);
                index = best;
            }
        }
    }
}

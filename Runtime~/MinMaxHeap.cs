using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;

namespace AceLand.Collection
{
    /// <summary>
    /// Double-ended priority queue (min-max heap). The minimum lives at the
    /// root and the maximum on the second level, so both extremes are O(1) to
    /// peek and O(log n) to remove.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use: bounded Top-K / bottom-K, range pruning (drop max while keeping
    /// min), sliding statistics where both ends matter.
    /// </para>
    /// <para>
    /// Classic min-max heap invariant: nodes on even levels (0-based, "min
    /// levels") are ≤ all descendants; nodes on odd levels ("max levels") are ≥
    /// all descendants.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/> or
    /// <see cref="Create(IComparer{T})"/>. Comparer is not serialized; re-supply
    /// via <see cref="RestoreComparer(IComparer{T})"/> after deserialization if
    /// a custom ordering is required.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class MinMaxHeap<T> : SerializableCollection<T>, IReadOnlyCollection<T>
    {
        private List<T> _heap = new();
        private IComparer<T> _comparer;

        private MinMaxHeap(IComparer<T> comparer)
        {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        /// <summary>Creates a min-max heap using <see cref="Comparer{T}.Default"/>.</summary>
        public static MinMaxHeap<T> Create() => new(null);

        /// <summary>Creates a min-max heap using a custom comparer.</summary>
        public static MinMaxHeap<T> Create(IComparer<T> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new MinMaxHeap<T>(comparer);
        }

        /// <summary>Current number of stored elements.</summary>
        public int Count => _heap.Count;

        /// <summary>True when the heap holds no elements.</summary>
        public bool IsEmpty => _heap.Count == 0;

        /// <summary>Inserts <paramref name="item"/> in O(log n).</summary>
        public void Push(T item)
        {
            _heap.Add(item);
            SiftUp(_heap.Count - 1);
        }

        /// <summary>Returns the minimum without removing it.</summary>
        public bool TryPeekMin(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _heap[0];
            return true;
        }

        /// <summary>Returns the maximum without removing it.</summary>
        public bool TryPeekMax(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _heap[MaxIndex()];
            return true;
        }

        /// <summary>Removes and returns the minimum.</summary>
        public bool TryPopMin(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _heap[0];
            RemoveAt(0);
            return true;
        }

        /// <summary>Removes and returns the maximum.</summary>
        public bool TryPopMax(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            var maxIndex = MaxIndex();
            item = _heap[maxIndex];
            RemoveAt(maxIndex);
            return true;
        }

        /// <summary>Removes all elements.</summary>
        public void Clear() => _heap.Clear();

        /// <summary>Re-attaches a comparer (e.g. after deserialization) and re-heapifies.</summary>
        public void RestoreComparer(IComparer<T> comparer)
        {
            _comparer = comparer ?? Comparer<T>.Default;
            Heapify();
        }

        public IEnumerator<T> GetEnumerator() => _heap.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Min-max heap mechanics ----------------------------------------

        // Index of the maximum: the larger of the (up to two) second-level nodes.
        private int MaxIndex()
        {
            switch (_heap.Count)
            {
                case 1: return 0;
                case 2: return 1;
                default: return Greater(_heap[1], _heap[2]) ? 1 : 2;
            }
        }

        private bool Less(T a, T b) => _comparer.Compare(a, b) < 0;
        private bool Greater(T a, T b) => _comparer.Compare(a, b) > 0;

        // Even level = min level.
        private static bool IsMinLevel(int index)
        {
            var level = 0;
            var i = index + 1; // 1-based node number
            while (i > 1)
            {
                i >>= 1;
                level++;
            }
            return (level & 1) == 0;
        }

        private void Swap(int i, int j) => (_heap[i], _heap[j]) = (_heap[j], _heap[i]);

        private static int Parent(int i) => (i - 1) >> 1;
        private static bool HasParent(int i) => i > 0;
        private static int Grandparent(int i) => Parent(Parent(i));
        private static bool HasGrandparent(int i) => Parent(i) > 0;

        private void SiftUp(int index)
        {
            if (!HasParent(index))
                return;

            var parent = Parent(index);
            if (IsMinLevel(index))
            {
                if (Greater(_heap[index], _heap[parent]))
                {
                    Swap(index, parent);
                    SiftUpMax(parent);
                }
                else
                {
                    SiftUpMin(index);
                }
            }
            else
            {
                if (Less(_heap[index], _heap[parent]))
                {
                    Swap(index, parent);
                    SiftUpMin(parent);
                }
                else
                {
                    SiftUpMax(index);
                }
            }
        }

        private void SiftUpMin(int index)
        {
            while (HasGrandparent(index))
            {
                var gp = Grandparent(index);
                if (!Less(_heap[index], _heap[gp])) break;
                Swap(index, gp);
                index = gp;
            }
        }

        private void SiftUpMax(int index)
        {
            while (HasGrandparent(index))
            {
                var gp = Grandparent(index);
                if (!Greater(_heap[index], _heap[gp])) break;
                Swap(index, gp);
                index = gp;
            }
        }

        private void RemoveAt(int index)
        {
            var last = _heap.Count - 1;
            if (index == last)
            {
                _heap.RemoveAt(last);
                return;
            }

            _heap[index] = _heap[last];
            _heap.RemoveAt(last);
            if (index < _heap.Count)
                SiftDown(index);
        }

        private void SiftDown(int index)
        {
            if (IsMinLevel(index)) SiftDownMin(index);
            else SiftDownMax(index);
        }

        private void SiftDownMin(int index)
        {
            while (true)
            {
                // Smallest among children and grandchildren.
                var m = SmallestDescendant(index, out var isGrandchild);
                if (m == -1) break;

                if (isGrandchild)
                {
                    if (Less(_heap[m], _heap[index]))
                    {
                        Swap(m, index);
                        var parent = Parent(m);
                        if (Greater(_heap[m], _heap[parent]))
                            Swap(m, parent);
                        index = m;
                        continue;
                    }
                    break;
                }

                if (Less(_heap[m], _heap[index]))
                    Swap(m, index);
                break;
            }
        }

        private void SiftDownMax(int index)
        {
            while (true)
            {
                var m = LargestDescendant(index, out var isGrandchild);
                if (m == -1) break;

                if (isGrandchild)
                {
                    if (Greater(_heap[m], _heap[index]))
                    {
                        Swap(m, index);
                        var parent = Parent(m);
                        if (Less(_heap[m], _heap[parent]))
                            Swap(m, parent);
                        index = m;
                        continue;
                    }
                    break;
                }

                if (Greater(_heap[m], _heap[index]))
                    Swap(m, index);
                break;
            }
        }

        // Returns index of smallest child/grandchild of 'index', -1 if leaf.
        private int SmallestDescendant(int index, out bool isGrandchild)
        {
            var count = _heap.Count;
            var best = -1;
            isGrandchild = false;

            var left = (index << 1) + 1;
            var right = left + 1;

            if (left < count) best = left;
            if (right < count && Less(_heap[right], _heap[best])) best = right;

            // Grandchildren: children of left and right.
            var gStart = (left << 1) + 1;
            for (var g = gStart; g < gStart + 4 && g < count; g++)
            {
                if (best == -1 || Less(_heap[g], _heap[best]))
                {
                    best = g;
                    isGrandchild = true;
                }
            }

            // Fix flag if best turned out to be a direct child.
            if (best == left || best == right)
                isGrandchild = false;

            return best;
        }

        private int LargestDescendant(int index, out bool isGrandchild)
        {
            var count = _heap.Count;
            var best = -1;
            isGrandchild = false;

            var left = (index << 1) + 1;
            var right = left + 1;

            if (left < count) best = left;
            if (right < count && Greater(_heap[right], _heap[best])) best = right;

            var gStart = (left << 1) + 1;
            for (var g = gStart; g < gStart + 4 && g < count; g++)
            {
                if (best == -1 || Greater(_heap[g], _heap[best]))
                {
                    best = g;
                    isGrandchild = true;
                }
            }

            if (best == left || best == right)
                isGrandchild = false;

            return best;
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

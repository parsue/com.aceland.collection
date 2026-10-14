using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Double-ended queue backed by a growable circular array. Insertion and
    /// removal at either end are amortised O(1); the backing array doubles when
    /// full (unless a hard capacity was configured).
    /// </summary>
    /// <remarks>
    /// <para>Use: undo/redo, sliding windows, work-stealing schedulers.</para>
    /// <para>BCL gap: .NET has no generic double-ended queue.</para>
    /// <para>
    /// Construct through <see cref="Create()"/> / <see cref="Create(int)"/>.
    /// Unity-serializable: elements are flattened front→back and restored in
    /// that order.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class Deque<T> : SerializableCollection<T>, IReadOnlyCollection<T>
    {
        private const int DefaultCapacity = 8;

        [SerializeField]
        private int _fixedCapacity; // 0 = unbounded (auto-grow)

        private T[] _buffer;
        private int _head;  // index of the front element
        private int _count;

        private Deque(int initialCapacity, bool fixedCapacity)
        {
            _fixedCapacity = fixedCapacity ? initialCapacity : 0;
            var initial = initialCapacity > 0 ? initialCapacity : DefaultCapacity;
            _buffer = new T[initial];
            _head = 0;
            _count = 0;
        }

        /// <summary>Creates an unbounded, auto-growing deque.</summary>
        public static Deque<T> Create() => new(0, false);

        /// <summary>Creates a deque with a fixed maximum capacity (no auto-grow).</summary>
        /// <param name="capacity">Maximum element count; must be positive.</param>
        public static Deque<T> Create(int capacity)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            return new Deque<T>(capacity, true);
        }

        /// <summary>
        /// Creates an unbounded, auto-growing deque whose backing array is pre-sized to
        /// <paramref name="initialCapacity"/>. The deque still grows past this value, but
        /// pre-sizing avoids the early doubling reallocations (and their GC) when the
        /// expected element count is known up front.
        /// </summary>
        /// <param name="initialCapacity">Initial backing-array size; must be positive.</param>
        public static Deque<T> CreateWithCapacity(int initialCapacity)
        {
            CollectionError.RequirePositiveCapacity(initialCapacity, nameof(initialCapacity));
            return new Deque<T>(initialCapacity, false);
        }

        /// <summary>Current number of stored elements.</summary>
        public int Count => _count;

        /// <summary>True when no elements are stored.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>Configured hard capacity, or 0 when unbounded.</summary>
        public int FixedCapacity => _fixedCapacity;

        /// <summary>True only for a fixed-capacity deque that is at capacity.</summary>
        public bool IsFull => _fixedCapacity > 0 && _count == _fixedCapacity;

        /// <summary>Inserts <paramref name="item"/> at the front.</summary>
        /// <returns><c>false</c> when a fixed-capacity deque is full.</returns>
        public bool TryAddFirst(T item)
        {
            if (!EnsureSpace()) return false;
            _head = Decrement(_head);
            _buffer[_head] = item;
            _count++;
            return true;
        }

        /// <summary>Inserts <paramref name="item"/> at the back.</summary>
        /// <returns><c>false</c> when a fixed-capacity deque is full.</returns>
        public bool TryAddLast(T item)
        {
            if (!EnsureSpace()) return false;
            _buffer[TailIndex()] = item;
            _count++;
            return true;
        }

        /// <summary>Removes and returns the front element.</summary>
        public bool TryRemoveFirst(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _buffer[_head];
            _buffer[_head] = default;
            _head = Increment(_head);
            _count--;
            return true;
        }

        /// <summary>Removes and returns the back element.</summary>
        public bool TryRemoveLast(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            var tail = (_head + _count - 1) % _buffer.Length;
            item = _buffer[tail];
            _buffer[tail] = default;
            _count--;
            return true;
        }

        /// <summary>Returns the front element without removing it.</summary>
        public bool TryPeekFirst(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _buffer[_head];
            return true;
        }

        /// <summary>Returns the back element without removing it.</summary>
        public bool TryPeekLast(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _buffer[(_head + _count - 1) % _buffer.Length];
            return true;
        }

        /// <summary>Removes all elements, keeping capacity.</summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>Indexer in logical order (0 = front).</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_head + index) % _buffer.Length];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
                yield return _buffer[(_head + i) % _buffer.Length];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int TailIndex() => (_head + _count) % _buffer.Length;

        private int Increment(int index) => index + 1 == _buffer.Length ? 0 : index + 1;

        private int Decrement(int index) => index == 0 ? _buffer.Length - 1 : index - 1;

        private bool EnsureSpace()
        {
            if (_count < _buffer.Length) return true;

            // Fixed-capacity deque never grows.
            if (_fixedCapacity > 0) return false;

            Grow(_buffer.Length * 2);
            return true;
        }

        private void Grow(int newCapacity)
        {
            var next = new T[newCapacity];
            for (var i = 0; i < _count; i++)
                next[i] = _buffer[(_head + i) % _buffer.Length];
            _buffer = next;
            _head = 0;
        }

        // ---- Serialization (front → back) -----------------------------------

        protected override void WriteSerializedItems(List<T> buffer)
        {
            for (var i = 0; i < _count; i++)
                buffer.Add(_buffer[(_head + i) % _buffer.Length]);
        }

        protected override void ReadSerializedItems(IReadOnlyList<T> items)
        {
            var initial = _fixedCapacity > 0
                ? _fixedCapacity
                : Mathf.Max(DefaultCapacity, items.Count);

            _buffer = new T[initial];
            _head = 0;
            _count = 0;

            var count = _fixedCapacity > 0
                ? Mathf.Min(items.Count, _fixedCapacity)
                : items.Count;

            for (var i = 0; i < count; i++)
                TryAddLast(items[i]);
        }
    }
}

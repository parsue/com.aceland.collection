using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Fixed-capacity circular buffer. When full, the oldest element is
    /// overwritten by the next enqueue. Head/tail operations are O(1).
    /// </summary>
    /// <remarks>
    /// <para>Game use: frame data, input history, rolling audio buffers.</para>
    /// <para>Software use: log ring, streaming windows, rate metrics.</para>
    /// <para>
    /// Construct through <see cref="Create(int)"/>; the constructor is not
    /// public by design. The buffer is Unity-serializable: on serialize the
    /// live elements are flattened oldest→newest, and restored in that order.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RingBuffer<T> : SerializableCollection<T>, IReadOnlyCollection<T>
    {
        [SerializeField]
        private int _capacity;

        private T[] _buffer;
        private int _head;   // index of the oldest element
        private int _count;

        private RingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        /// <summary>Creates a ring buffer with the given fixed capacity.</summary>
        /// <param name="capacity">Maximum element count; must be positive.</param>
        public static RingBuffer<T> Create(int capacity)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            return new RingBuffer<T>(capacity);
        }

        /// <summary>Maximum number of elements the buffer can hold.</summary>
        public int Capacity => _capacity;

        /// <summary>Current number of stored elements.</summary>
        public int Count => _count;

        /// <summary>True when no elements are stored.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>True when the buffer is at capacity (next enqueue overwrites).</summary>
        public bool IsFull => _count == _capacity;

        /// <summary>
        /// Appends <paramref name="item"/> at the tail. If the buffer is full,
        /// the oldest element is overwritten and returned via
        /// <paramref name="overwritten"/>.
        /// </summary>
        /// <returns><c>true</c> when an element was overwritten.</returns>
        public bool Enqueue(T item, out T overwritten)
        {
            if (IsFull)
            {
                overwritten = _buffer[_head];
                _buffer[_head] = item;
                _head = Increment(_head);
                return true;
            }

            _buffer[TailIndex()] = item;
            _count++;
            overwritten = default;
            return false;
        }

        /// <summary>Appends <paramref name="item"/> at the tail, overwriting the oldest when full.</summary>
        public void Enqueue(T item) => Enqueue(item, out _);

        /// <summary>Removes and returns the oldest element.</summary>
        public bool TryDequeue(out T item)
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

        /// <summary>Returns the oldest element without removing it.</summary>
        public bool TryPeek(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _buffer[_head];
            return true;
        }

        /// <summary>Returns the newest element without removing it.</summary>
        public bool TryPeekLast(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = _buffer[(_head + _count - 1) % _capacity];
            return true;
        }

        /// <summary>Removes all elements, keeping the capacity.</summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>Indexer in logical order (0 = oldest).</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_head + index) % _capacity];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
                yield return _buffer[(_head + i) % _capacity];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int TailIndex() => (_head + _count) % _capacity;

        private int Increment(int index) => index + 1 == _capacity ? 0 : index + 1;

        // ---- Serialization (oldest → newest) --------------------------------

        protected override void WriteSerializedItems(List<T> buffer)
        {
            for (var i = 0; i < _count; i++)
                buffer.Add(_buffer[(_head + i) % _capacity]);
        }

        protected override void ReadSerializedItems(IReadOnlyList<T> items)
        {
            if (_capacity <= 0)
                _capacity = Mathf.Max(1, items.Count);

            _buffer = new T[_capacity];
            _head = 0;
            _count = 0;

            // If more items were persisted than capacity allows, keep the newest.
            var start = Mathf.Max(0, items.Count - _capacity);
            for (var i = start; i < items.Count; i++)
                Enqueue(items[i]);
        }
    }
}

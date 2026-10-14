using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// One-to-many map: a single key holds an ordered list of values, filling the
    /// gap left by the read-only <see cref="ILookup{TKey,TElement}"/>. Values may
    /// repeat unless <see cref="AllowDuplicateValues"/> is disabled at creation.
    /// </summary>
    /// <remarks>
    /// <para>Use: tag→objects, grouping, event subscriber tables.</para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Comparers are not serialized;
    /// after Unity deserialization the defaults are used unless re-supplied via
    /// <see cref="RestoreComparers"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class MultiMap<TKey, TValue> :
        SerializableCollection<PairElement<TKey, TValue>>
    {
        [SerializeField]
        private bool _allowDuplicateValues;

        private Dictionary<TKey, List<TValue>> _map;
        private IEqualityComparer<TKey> _keyComparer;
        private IEqualityComparer<TValue> _valueComparer;
        private int _count;

        private MultiMap(
            bool allowDuplicateValues,
            IEqualityComparer<TKey> keyComparer,
            IEqualityComparer<TValue> valueComparer)
        {
            _allowDuplicateValues = allowDuplicateValues;
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
            _map = new Dictionary<TKey, List<TValue>>(_keyComparer);
        }

        /// <summary>Creates a map that allows duplicate values per key.</summary>
        public static MultiMap<TKey, TValue> Create(bool allowDuplicateValues = true) =>
            new(allowDuplicateValues, null, null);

        /// <summary>Creates a map using custom key/value comparers.</summary>
        public static MultiMap<TKey, TValue> Create(
            IEqualityComparer<TKey> keyComparer,
            IEqualityComparer<TValue> valueComparer,
            bool allowDuplicateValues = true)
        {
            CollectionError.RequireNotNull(keyComparer, nameof(keyComparer));
            CollectionError.RequireNotNull(valueComparer, nameof(valueComparer));
            return new MultiMap<TKey, TValue>(allowDuplicateValues, keyComparer, valueComparer);
        }

        /// <summary>Number of distinct keys.</summary>
        public int KeyCount => _map.Count;

        /// <summary>Total number of stored values across all keys.</summary>
        public int Count => _count;

        /// <summary>Whether the same value may appear more than once under one key.</summary>
        public bool AllowDuplicateValues => _allowDuplicateValues;

        /// <summary>All keys currently holding at least one value.</summary>
        public IEnumerable<TKey> Keys => _map.Keys;

        /// <summary>Returns true when <paramref name="key"/> holds any value.</summary>
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>Returns true when <paramref name="key"/> holds <paramref name="value"/>.</summary>
        public bool Contains(TKey key, TValue value) =>
            _map.TryGetValue(key, out var list) && IndexOf(list, value) >= 0;

        /// <summary>
        /// Appends <paramref name="value"/> under <paramref name="key"/>. When
        /// duplicates are disallowed and the value already exists, nothing is
        /// added and false is returned.
        /// </summary>
        public bool Add(TKey key, TValue value)
        {
            if (!_map.TryGetValue(key, out var list))
            {
                list = new List<TValue>();
                _map[key] = list;
            }

            if (!_allowDuplicateValues && IndexOf(list, value) >= 0)
                return false;

            list.Add(value);
            _count++;
            return true;
        }

        /// <summary>Number of values stored under <paramref name="key"/> (0 when absent).</summary>
        public int CountOf(TKey key) =>
            _map.TryGetValue(key, out var list) ? list.Count : 0;

        /// <summary>
        /// Returns the values under <paramref name="key"/> as a read-only list.
        /// Empty when the key is absent.
        /// </summary>
        public IReadOnlyList<TValue> GetValues(TKey key) =>
            _map.TryGetValue(key, out var list) ? list : Array.Empty<TValue>();

        /// <summary>Attempts to read the values under <paramref name="key"/>.</summary>
        public bool TryGetValues(TKey key, out IReadOnlyList<TValue> values)
        {
            if (_map.TryGetValue(key, out var list))
            {
                values = list;
                return true;
            }

            values = Array.Empty<TValue>();
            return false;
        }

        /// <summary>
        /// Removes a single occurrence of <paramref name="value"/> from
        /// <paramref name="key"/>. Empties keys are dropped. Returns false when
        /// the pair is absent.
        /// </summary>
        public bool Remove(TKey key, TValue value)
        {
            if (!_map.TryGetValue(key, out var list))
                return false;

            var index = IndexOf(list, value);
            if (index < 0)
                return false;

            list.RemoveAt(index);
            _count--;
            if (list.Count == 0)
                _map.Remove(key);
            return true;
        }

        /// <summary>Removes every value under <paramref name="key"/>. Returns false when absent.</summary>
        public bool RemoveKey(TKey key)
        {
            if (!_map.TryGetValue(key, out var list))
                return false;

            _count -= list.Count;
            _map.Remove(key);
            return true;
        }

        /// <summary>Removes all keys and values.</summary>
        public void Clear()
        {
            _map.Clear();
            _count = 0;
        }

        /// <summary>
        /// Re-attaches custom comparers (e.g. after Unity deserialization) and
        /// rebuilds the index. Passing null falls back to the defaults.
        /// </summary>
        public void RestoreComparers(
            IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
        {
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;

            var rebuilt = new Dictionary<TKey, List<TValue>>(_keyComparer);
            foreach (var pair in _map)
                rebuilt[pair.Key] = pair.Value;

            _map = rebuilt;
        }

        // ---- Internal helpers ----------------------------------------------

        private int IndexOf(List<TValue> list, TValue value)
        {
            for (var i = 0; i < list.Count; i++)
                if (_valueComparer.Equals(list[i], value))
                    return i;

            return -1;
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<PairElement<TKey, TValue>> buffer)
        {
            // Unity may serialize a [SerializeField] map before the field
            // initializer / constructor has run, leaving _map null.
            if (_map == null) return;
            foreach (var pair in _map)
            foreach (var value in pair.Value)
                buffer.Add(new PairElement<TKey, TValue>(pair.Key, value));
        }

        protected override void ReadSerializedItems(IReadOnlyList<PairElement<TKey, TValue>> items)
        {
            _keyComparer ??= EqualityComparer<TKey>.Default;
            _valueComparer ??= EqualityComparer<TValue>.Default;
            _map = new Dictionary<TKey, List<TValue>>(_keyComparer);
            _count = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                Add(entry.Key, entry.Value);
            }
        }
    }
}

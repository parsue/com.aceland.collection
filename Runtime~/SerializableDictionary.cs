using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// A key-unique map that persists through Unity serialization. It mirrors the
    /// semantics of the native <see cref="Dictionary{TKey,TValue}"/> while funnelling
    /// its state through a Unity-serializable backing list so Inspector editing and
    /// Play/Edit round-tripping work out of the box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this whenever you need a dictionary that survives Unity serialization and
    /// can be authored in the Inspector. The paired Editor drawer surfaces a
    /// key/value table window on every Unity version — so upgrading the editor never
    /// forces a type change on your serialized data.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Comparers are not serialized; after
    /// Unity deserialization the default comparer is used unless re-supplied via
    /// <see cref="RestoreComparers"/>.
    /// </para>
    /// <para>
    /// When restoring a serialized payload that contains duplicate keys (for example
    /// after hand-editing), the first occurrence wins and later duplicates are
    /// dropped, matching the uniqueness invariant.
    /// </para>
    /// </remarks>
    /// <typeparam name="TKey">The key type; must be Unity-serializable to persist.</typeparam>
    /// <typeparam name="TValue">The value type; must be Unity-serializable to persist.</typeparam>
    [Serializable]
    public sealed class SerializableDictionary<TKey, TValue> :
        SerializableCollection<PairElement<TKey, TValue>>,
        IReadOnlyCollection<PairElement<TKey, TValue>>
    {
        private Dictionary<TKey, TValue> _map;
        private IEqualityComparer<TKey> _keyComparer;

        private SerializableDictionary(int capacity, IEqualityComparer<TKey> keyComparer)
        {
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _map = new Dictionary<TKey, TValue>(capacity, _keyComparer);
        }

        /// <summary>Creates an empty dictionary using the default key comparer.</summary>
        public static SerializableDictionary<TKey, TValue> Create() =>
            new(0, null);

        /// <summary>Creates an empty dictionary pre-sized for <paramref name="capacity"/> entries.</summary>
        public static SerializableDictionary<TKey, TValue> Create(int capacity)
        {
            CollectionError.RequireNonNegativeCapacity(capacity, nameof(capacity));
            return new SerializableDictionary<TKey, TValue>(capacity, null);
        }

        /// <summary>Creates an empty dictionary using a custom key comparer.</summary>
        public static SerializableDictionary<TKey, TValue> Create(IEqualityComparer<TKey> keyComparer)
        {
            CollectionError.RequireNotNull(keyComparer, nameof(keyComparer));
            return new SerializableDictionary<TKey, TValue>(0, keyComparer);
        }

        /// <summary>Number of key/value pairs currently stored.</summary>
        public int Count => _map.Count;

        /// <summary>All keys currently stored.</summary>
        public IEnumerable<TKey> Keys => _map.Keys;

        /// <summary>All values currently stored.</summary>
        public IEnumerable<TValue> Values => _map.Values;

        /// <summary>
        /// Gets or sets the value associated with <paramref name="key"/>. Reading a
        /// missing key throws <see cref="KeyNotFoundException"/>; writing adds or
        /// overwrites the entry.
        /// </summary>
        public TValue this[TKey key]
        {
            get => _map[key];
            set => _map[key] = value;
        }

        /// <summary>Returns true when <paramref name="key"/> is present.</summary>
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>Attempts to read the value stored under <paramref name="key"/>.</summary>
        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);

        /// <summary>
        /// Adds a new pair. Returns false (without overwriting) when
        /// <paramref name="key"/> already exists.
        /// </summary>
        public bool Add(TKey key, TValue value)
        {
            if (_map.ContainsKey(key))
                return false;

            _map.Add(key, value);
            return true;
        }

        /// <summary>Adds or overwrites the value stored under <paramref name="key"/>.</summary>
        public void Set(TKey key, TValue value) => _map[key] = value;

        /// <summary>Removes the pair under <paramref name="key"/>. Returns false when absent.</summary>
        public bool Remove(TKey key) => _map.Remove(key);

        /// <summary>Removes all pairs.</summary>
        public void Clear() => _map.Clear();

        /// <summary>
        /// Re-attaches a custom key comparer (e.g. after Unity deserialization) and
        /// rebuilds the index. Passing null falls back to the default comparer.
        /// </summary>
        public void RestoreComparers(IEqualityComparer<TKey> keyComparer)
        {
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

            var rebuilt = new Dictionary<TKey, TValue>(_map.Count, _keyComparer);
            foreach (var pair in _map)
                rebuilt[pair.Key] = pair.Value;

            _map = rebuilt;
        }

        /// <summary>Enumerates the stored pairs as <see cref="PairElement{TKey,TValue}"/>.</summary>
        public IEnumerator<PairElement<TKey, TValue>> GetEnumerator()
        {
            foreach (var pair in _map)
                yield return new PairElement<TKey, TValue>(pair.Key, pair.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<PairElement<TKey, TValue>> buffer)
        {
            // Unity may serialize a [SerializeField] map before the field
            // initializer / constructor has run, leaving _map null.
            if (_map == null) return;
            foreach (var pair in _map)
                buffer.Add(new PairElement<TKey, TValue>(pair.Key, pair.Value));
        }

        protected override void ReadSerializedItems(IReadOnlyList<PairElement<TKey, TValue>> items)
        {
            _keyComparer ??= EqualityComparer<TKey>.Default;
            _map = new Dictionary<TKey, TValue>(items.Count, _keyComparer);

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                // First occurrence wins; drop later duplicate keys to preserve the
                // uniqueness invariant after hand-edited payloads.
                _map.TryAdd(entry.Key, entry.Value);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;

namespace AceLand.Collection
{
    /// <summary>
    /// Bidirectional one-to-one map: both key→value and value→key lookups are
    /// O(1). Each key maps to exactly one value and each value back to exactly
    /// one key, so the relation stays a strict bijection.
    /// </summary>
    /// <remarks>
    /// <para>Use: id↔object, enum↔string, any invertible mapping.</para>
    /// <para>
    /// Unlike a naive two-<see cref="Dictionary{TKey,TValue}"/> implementation,
    /// this map stores each <c>(key, value)</c> payload exactly once in a single
    /// entry table and maintains two separate hash-bucket chains over that one
    /// table — a forward chain keyed by the key's hash and a reverse chain keyed
    /// by the value's hash. Both directions still resolve in O(1), but the value
    /// (and key) payload is not duplicated. Entries do not cache the hash codes
    /// either — hashes are recomputed on the rare unlink/resize paths — so a slot
    /// is just two link ints plus the payload, keeping build-time allocation
    /// close to a single dictionary while retaining alloc-free reverse lookup.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Comparers are not serialized;
    /// after Unity deserialization the defaults are used unless re-supplied via
    /// <see cref="RestoreComparers"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class BiDictionary<TKey, TValue> :
        SerializableCollection<PairElement<TKey, TValue>>,
        IReadOnlyCollection<PairElement<TKey, TValue>>
    {
        // One shared payload slot per pair. A live entry carries two singly-linked
        // "next" pointers, one for the key-bucket chain and one for the value-bucket
        // chain (both use -1 to mark the end of the chain). Hash codes are not
        // cached: they are recomputed on unlink/resize, which trades a little
        // remove/grow cost for a smaller slot and lower build-time GC.
        //
        // Free (removed) slots reuse KeyNext to thread the free list, encoded as
        // (StartOfFreeList - nextFreeIndex). Because live chain links are always
        // >= -1 and the free encoding is always <= -2, KeyNext alone distinguishes
        // a live slot (KeyNext >= -1) from a free one (KeyNext < -1).
        private struct Entry
        {
            public int KeyNext;    // key-bucket chain (-1 end) when live; free-list link (< -1) when free
            public int ValueNext;  // value-bucket chain (-1 end)
            public TKey Key;
            public TValue Value;
        }

        // Free-list sentinel, following the BCL Dictionary convention: a free
        // entry's KeyNext == StartOfFreeList - _freeList, which is always <= -2.
        private const int StartOfFreeList = -3;

        // Bucket arrays hold (entryIndex + 1); 0 means empty.
        private int[] _keyBuckets;
        private int[] _valueBuckets;
        private Entry[] _entries;
        private int _count;      // high-water number of used slots (live + freed)
        private int _freeList;   // head of the free-slot chain (-1 when none)
        private int _freeCount;  // number of slots currently on the free list

        private IEqualityComparer<TKey> _keyComparer;
        private IEqualityComparer<TValue> _valueComparer;

        private BiDictionary(
            IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer,
            int capacity = 0)
        {
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
            Initialize(capacity);
        }

        private void Initialize(int capacity)
        {
            var size = HashHelpers.GetPrime(capacity);
            _keyBuckets = new int[size];
            _valueBuckets = new int[size];
            _entries = new Entry[size];
            _count = 0;
            _freeList = -1;
            _freeCount = 0;
        }

        /// <summary>Creates a map using default key/value comparers.</summary>
        public static BiDictionary<TKey, TValue> Create() => new(null, null);

        /// <summary>Creates a map using custom key/value comparers.</summary>
        public static BiDictionary<TKey, TValue> Create(
            IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
        {
            CollectionError.RequireNotNull(keyComparer, nameof(keyComparer));
            CollectionError.RequireNotNull(valueComparer, nameof(valueComparer));
            return new BiDictionary<TKey, TValue>(keyComparer, valueComparer);
        }

        /// <summary>
        /// Creates a map pre-sized for <paramref name="capacity"/> pairs using the
        /// default key/value comparers. Pre-allocation skips the incremental
        /// resize garbage produced while the table grows, which matters when
        /// bulk-loading a known-size relation.
        /// </summary>
        public static BiDictionary<TKey, TValue> Create(int capacity)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            return new BiDictionary<TKey, TValue>(null, null, capacity);
        }

        /// <summary>
        /// Creates a map pre-sized for <paramref name="capacity"/> pairs using
        /// custom key/value comparers.
        /// </summary>
        public static BiDictionary<TKey, TValue> Create(
            int capacity, IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
        {
            CollectionError.RequirePositiveCapacity(capacity, nameof(capacity));
            CollectionError.RequireNotNull(keyComparer, nameof(keyComparer));
            CollectionError.RequireNotNull(valueComparer, nameof(valueComparer));
            return new BiDictionary<TKey, TValue>(keyComparer, valueComparer, capacity);
        }

        /// <summary>Number of key/value pairs.</summary>
        public int Count => _count - _freeCount;

        /// <summary>True when the slot at <paramref name="index"/> holds a live pair.</summary>
        private bool IsLive(int index) => _entries[index].KeyNext >= -1;

        /// <summary>All keys (live entries only).</summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                for (var i = 0; i < _count; i++)
                    if (IsLive(i))
                        yield return _entries[i].Key;
            }
        }

        /// <summary>All values (live entries only).</summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                for (var i = 0; i < _count; i++)
                    if (IsLive(i))
                        yield return _entries[i].Value;
            }
        }

        /// <summary>Returns true when <paramref name="key"/> is present.</summary>
        public bool ContainsKey(TKey key) => FindByKey(key, out _) >= 0;

        /// <summary>Returns true when <paramref name="value"/> is present.</summary>
        public bool ContainsValue(TValue value) => FindByValue(value, out _) >= 0;

        /// <summary>
        /// Inserts or replaces the pair, keeping the bijection intact: any prior
        /// mapping for <paramref name="key"/> or <paramref name="value"/> is
        /// removed first, so a value cannot end up bound to two keys.
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            var keyIndex = FindByKey(key, out var keyHash);
            var valueIndex = FindByValue(value, out var valueHash);

            // The exact pair already exists on the same slot — nothing to do.
            if (keyIndex >= 0 && keyIndex == valueIndex)
                return;

            if (keyIndex >= 0)
                RemoveEntryAt(keyIndex);
            if (valueIndex >= 0)
                RemoveEntryAt(valueIndex);

            AddEntry(key, keyHash, value, valueHash);
        }

        /// <summary>
        /// Inserts the pair only when neither <paramref name="key"/> nor
        /// <paramref name="value"/> already exists. Returns false otherwise.
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            var keyIndex = FindByKey(key, out var keyHash);
            if (keyIndex >= 0)
                return false;
            var valueIndex = FindByValue(value, out var valueHash);
            if (valueIndex >= 0)
                return false;

            AddEntry(key, keyHash, value, valueHash);
            return true;
        }

        /// <summary>Looks up the value bound to <paramref name="key"/>.</summary>
        public bool TryGetByKey(TKey key, out TValue value)
        {
            var index = FindByKey(key, out _);
            if (index >= 0)
            {
                value = _entries[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Looks up the key bound to <paramref name="value"/>.</summary>
        public bool TryGetByValue(TValue value, out TKey key)
        {
            var index = FindByValue(value, out _);
            if (index >= 0)
            {
                key = _entries[index].Key;
                return true;
            }

            key = default;
            return false;
        }

        /// <summary>Removes the pair identified by <paramref name="key"/>.</summary>
        public bool RemoveByKey(TKey key)
        {
            var index = FindByKey(key, out _);
            if (index < 0)
                return false;

            RemoveEntryAt(index);
            return true;
        }

        /// <summary>Removes the pair identified by <paramref name="value"/>.</summary>
        public bool RemoveByValue(TValue value)
        {
            var index = FindByValue(value, out _);
            if (index < 0)
                return false;

            RemoveEntryAt(index);
            return true;
        }

        /// <summary>Removes all pairs.</summary>
        public void Clear()
        {
            if (_count == 0)
                return;

            Array.Clear(_keyBuckets, 0, _keyBuckets.Length);
            Array.Clear(_valueBuckets, 0, _valueBuckets.Length);
            Array.Clear(_entries, 0, _count);
            _count = 0;
            _freeList = -1;
            _freeCount = 0;
        }

        /// <summary>
        /// Re-attaches custom comparers (e.g. after Unity deserialization) and
        /// rebuilds both indexes. Passing null falls back to the defaults.
        /// </summary>
        public void RestoreComparers(
            IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
        {
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;

            // Snapshot the current live pairs, then rebuild the table from scratch
            // so every entry is re-hashed and re-chained under the new comparers.
            var live = new List<PairElement<TKey, TValue>>(Count);
            for (var i = 0; i < _count; i++)
                if (IsLive(i))
                    live.Add(new PairElement<TKey, TValue>(_entries[i].Key, _entries[i].Value));

            Initialize(live.Count);
            for (var i = 0; i < live.Count; i++)
            {
                var pair = live[i];
                var keyHash = _keyComparer.GetHashCode(pair.Key) & int.MaxValue;
                var valueHash = _valueComparer.GetHashCode(pair.Value) & int.MaxValue;
                AddEntry(pair.Key, keyHash, pair.Value, valueHash);
            }
        }

        public IEnumerator<PairElement<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
                if (IsLive(i))
                    yield return new PairElement<TKey, TValue>(_entries[i].Key, _entries[i].Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Core hash-table plumbing --------------------------------------

        private int FindByKey(TKey key, out int hash)
        {
            hash = _keyComparer.GetHashCode(key) & int.MaxValue;
            var i = _keyBuckets[hash % _keyBuckets.Length] - 1;
            while (i >= 0)
            {
                ref var entry = ref _entries[i];
                if (_keyComparer.Equals(entry.Key, key))
                    return i;
                i = entry.KeyNext;
            }

            return -1;
        }

        private int FindByValue(TValue value, out int hash)
        {
            hash = _valueComparer.GetHashCode(value) & int.MaxValue;
            var i = _valueBuckets[hash % _valueBuckets.Length] - 1;
            while (i >= 0)
            {
                ref var entry = ref _entries[i];
                if (_valueComparer.Equals(entry.Value, value))
                    return i;
                i = entry.ValueNext;
            }

            return -1;
        }

        private void AddEntry(TKey key, int keyHash, TValue value, int valueHash)
        {
            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                _freeList = StartOfFreeList - _entries[index].KeyNext;
                _freeCount--;
            }
            else
            {
                if (_count == _entries.Length)
                {
                    Resize(HashHelpers.ExpandPrime(_count));
                    keyHash = _keyComparer.GetHashCode(key) & int.MaxValue;
                    valueHash = _valueComparer.GetHashCode(value) & int.MaxValue;
                }
                index = _count;
                _count++;
            }

            ref var entry = ref _entries[index];
            entry.Key = key;
            entry.Value = value;

            var keyBucket = keyHash % _keyBuckets.Length;
            entry.KeyNext = _keyBuckets[keyBucket] - 1;
            _keyBuckets[keyBucket] = index + 1;

            var valueBucket = valueHash % _valueBuckets.Length;
            entry.ValueNext = _valueBuckets[valueBucket] - 1;
            _valueBuckets[valueBucket] = index + 1;
        }

        private void RemoveEntryAt(int index)
        {
            UnlinkKey(index);
            UnlinkValue(index);

            ref var entry = ref _entries[index];
            entry.Key = default;
            entry.Value = default;
            entry.ValueNext = -1;
            entry.KeyNext = StartOfFreeList - _freeList;  // thread onto free list; marks slot free
            _freeList = index;
            _freeCount++;
        }

        private void UnlinkKey(int index)
        {
            var hash = _keyComparer.GetHashCode(_entries[index].Key) & int.MaxValue;
            var bucket = hash % _keyBuckets.Length;
            var i = _keyBuckets[bucket] - 1;
            var prev = -1;
            while (i >= 0)
            {
                ref var entry = ref _entries[i];
                if (i == index)
                {
                    if (prev < 0)
                        _keyBuckets[bucket] = entry.KeyNext + 1;
                    else
                        _entries[prev].KeyNext = entry.KeyNext;
                    return;
                }

                prev = i;
                i = entry.KeyNext;
            }
        }

        private void UnlinkValue(int index)
        {
            var hash = _valueComparer.GetHashCode(_entries[index].Value) & int.MaxValue;
            var bucket = hash % _valueBuckets.Length;
            var i = _valueBuckets[bucket] - 1;
            var prev = -1;
            while (i >= 0)
            {
                ref var entry = ref _entries[i];
                if (i == index)
                {
                    if (prev < 0)
                        _valueBuckets[bucket] = entry.ValueNext + 1;
                    else
                        _entries[prev].ValueNext = entry.ValueNext;
                    return;
                }

                prev = i;
                i = entry.ValueNext;
            }
        }

        private void Resize(int newSize)
        {
            // Resize only runs when the free list is empty (all slots 0.._count-1
            // are live), so a straight rehash of that range rebuilds both chains.
            var newEntries = new Entry[newSize];
            Array.Copy(_entries, newEntries, _count);
            var newKeyBuckets = new int[newSize];
            var newValueBuckets = new int[newSize];

            for (var i = 0; i < _count; i++)
            {
                var keyHash = _keyComparer.GetHashCode(newEntries[i].Key) & int.MaxValue;
                var keyBucket = keyHash % newSize;
                newEntries[i].KeyNext = newKeyBuckets[keyBucket] - 1;
                newKeyBuckets[keyBucket] = i + 1;

                var valueHash = _valueComparer.GetHashCode(newEntries[i].Value) & int.MaxValue;
                var valueBucket = valueHash % newSize;
                newEntries[i].ValueNext = newValueBuckets[valueBucket] - 1;
                newValueBuckets[valueBucket] = i + 1;
            }

            _entries = newEntries;
            _keyBuckets = newKeyBuckets;
            _valueBuckets = newValueBuckets;
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<PairElement<TKey, TValue>> buffer)
        {
            // Unity may serialize before the constructor / field initializer runs.
            if (_entries == null) return;
            for (var i = 0; i < _count; i++)
                if (IsLive(i))
                    buffer.Add(new PairElement<TKey, TValue>(_entries[i].Key, _entries[i].Value));
        }

        protected override void ReadSerializedItems(IReadOnlyList<PairElement<TKey, TValue>> items)
        {
            _keyComparer ??= EqualityComparer<TKey>.Default;
            _valueComparer ??= EqualityComparer<TValue>.Default;
            Initialize(items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                // Skip entries that would break the bijection.
                if (FindByKey(entry.Key, out var keyHash) >= 0)
                    continue;
                if (FindByValue(entry.Value, out var valueHash) >= 0)
                    continue;

                AddEntry(entry.Key, keyHash, entry.Value, valueHash);
            }
        }
    }
}

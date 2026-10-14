using System;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Serializable key/value pair used as the flat backing element for
    /// <see cref="LruCache{TKey,TValue}"/>.
    /// </summary>
    [Serializable]
    public struct CacheEntry<TKey, TValue>
    {
        [SerializeField] private TKey _key;
        [SerializeField] private TValue _value;

        internal CacheEntry(TKey key, TValue value)
        {
            _key = key;
            _value = value;
        }

        /// <summary>The cache key.</summary>
        public TKey Key => _key;

        /// <summary>The cached value.</summary>
        public TValue Value => _value;
    }
}

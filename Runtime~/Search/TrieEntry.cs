using System;
using UnityEngine;

namespace AceLand.Collection.Search
{
    /// <summary>
    /// Serializable key/value pair used as the flat backing element for
    /// <see cref="Trie{TValue}"/> and <see cref="RadixTree{TValue}"/>.
    /// </summary>
    [Serializable]
    public struct TrieEntry<TValue>
    {
        [SerializeField] private string _key;
        [SerializeField] private TValue _value;

        internal TrieEntry(string key, TValue value)
        {
            _key = key;
            _value = value;
        }

        /// <summary>The full word / key.</summary>
        public string Key => _key;

        /// <summary>The value stored for the key.</summary>
        public TValue Value => _value;
    }
}

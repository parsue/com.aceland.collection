using System;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Serializable key/value pair used as the flat backing element for
    /// <see cref="BiDictionary{TKey,TValue}"/> and
    /// <see cref="MultiMap{TKey,TValue}"/>.
    /// </summary>
    [Serializable]
    public struct PairElement<TKey, TValue>
    {
        [SerializeField] private TKey _key;
        [SerializeField] private TValue _value;

        internal PairElement(TKey key, TValue value)
        {
            _key = key;
            _value = value;
        }

        /// <summary>The pair key.</summary>
        public TKey Key => _key;

        /// <summary>The pair value.</summary>
        public TValue Value => _value;
    }
}

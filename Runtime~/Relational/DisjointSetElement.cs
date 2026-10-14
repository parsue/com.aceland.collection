using System;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Serializable flat element for <see cref="DisjointSet{T}"/>. A single
    /// backing list mixes two kinds of records: standalone members (each a
    /// singleton set) and union pairs that merge two members. The
    /// <see cref="IsUnion"/> flag disambiguates them during deserialization,
    /// mirroring <see cref="GraphElement{TNode,TEdge}"/> but without an edge
    /// payload because unions carry no data.
    /// </summary>
    [Serializable]
    public struct DisjointSetElement<T>
    {
        [SerializeField] private bool _isUnion;
        [SerializeField] private T _from;
        [SerializeField] private T _to;

        private DisjointSetElement(bool isUnion, T from, T to)
        {
            _isUnion = isUnion;
            _from = from;
            _to = to;
        }

        internal static DisjointSetElement<T> ForMember(T member) =>
            new(false, member, default);

        internal static DisjointSetElement<T> ForUnion(T from, T to) =>
            new(true, from, to);

        /// <summary>True when this record describes a union; false for a member.</summary>
        public bool IsUnion => _isUnion;

        /// <summary>The member (when <see cref="IsUnion"/> is false) or union's first element.</summary>
        public T From => _from;

        /// <summary>The union's second element (only meaningful when <see cref="IsUnion"/>).</summary>
        public T To => _to;
    }
}

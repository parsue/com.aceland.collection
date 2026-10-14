using System;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Serializable flat element for <see cref="Graph{TNode,TEdge}"/>. A single
    /// backing list mixes two kinds of records: isolated nodes and edges. The
    /// <see cref="IsEdge"/> flag disambiguates them during deserialization.
    /// </summary>
    [Serializable]
    public struct GraphElement<TNode, TEdge>
    {
        [SerializeField] private bool _isEdge;
        [SerializeField] private TNode _from;
        [SerializeField] private TNode _to;
        [SerializeField] private TEdge _edge;

        private GraphElement(bool isEdge, TNode from, TNode to, TEdge edge)
        {
            _isEdge = isEdge;
            _from = from;
            _to = to;
            _edge = edge;
        }

        internal static GraphElement<TNode, TEdge> ForNode(TNode node) =>
            new(false, node, default, default);

        internal static GraphElement<TNode, TEdge> ForEdge(TNode from, TNode to, TEdge edge) =>
            new(true, from, to, edge);

        /// <summary>True when this record describes an edge; false for a node.</summary>
        public bool IsEdge => _isEdge;

        /// <summary>The node (when <see cref="IsEdge"/> is false) or edge source.</summary>
        public TNode From => _from;

        /// <summary>The edge target (only meaningful when <see cref="IsEdge"/>).</summary>
        public TNode To => _to;

        /// <summary>The edge payload (only meaningful when <see cref="IsEdge"/>).</summary>
        public TEdge Edge => _edge;
    }
}

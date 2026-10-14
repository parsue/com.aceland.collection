using System;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Serializable flat element for <see cref="HierarchyTree{T}"/>. Each record
    /// captures one node and, when <see cref="HasParent"/> is true, the parent it
    /// hangs under; roots carry no parent.
    /// </summary>
    [Serializable]
    public struct HierarchyElement<T>
    {
        [SerializeField] private T _node;
        [SerializeField] private bool _hasParent;
        [SerializeField] private T _parent;

        internal HierarchyElement(T node, bool hasParent, T parent)
        {
            _node = node;
            _hasParent = hasParent;
            _parent = parent;
        }

        /// <summary>The node this record describes.</summary>
        public T Node => _node;

        /// <summary>True when the node has a parent; false for a root.</summary>
        public bool HasParent => _hasParent;

        /// <summary>The parent node (only meaningful when <see cref="HasParent"/>).</summary>
        public T Parent => _parent;
    }
}

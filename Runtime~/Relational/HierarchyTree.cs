using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Single-parent hierarchy container (rose tree): every node has at most one
    /// parent and any number of children. Supports multiple roots (a forest),
    /// DFS / BFS traversal, ancestor / descendant queries and depth lookup.
    /// </summary>
    /// <remarks>
    /// <para>Use: skill trees, UI trees, scene-like hierarchies, file trees.</para>
    /// <para>
    /// Deliberately distinct from <see cref="Graph{TNode,TEdge}"/>: this models a
    /// strict single-parent hierarchy, not a many-to-many relation, and rejects
    /// cycles. Construct through <see cref="Create()"/>. The node comparer is not
    /// serialized; after Unity deserialization the default comparer is used
    /// unless re-supplied via <see cref="RestoreComparer"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class HierarchyTree<T> : SerializableCollection<HierarchyElement<T>>
    {
        private sealed class Node
        {
            public T Value;
            public T Parent;
            public bool HasParent;
            public readonly List<T> Children = new();
        }

        private Dictionary<T, Node> _nodes;
        private List<T> _roots = new();
        private IEqualityComparer<T> _comparer;

        private HierarchyTree(IEqualityComparer<T> comparer)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            _nodes = new Dictionary<T, Node>(_comparer);
        }

        // Unity instantiates a [SerializeField] HierarchyTree by allocating an
        // uninitialized object, bypassing the private constructor. Guard the
        // serialization callbacks so _nodes / _comparer are always valid.
        private void EnsureInitialized()
        {
            _comparer ??= EqualityComparer<T>.Default;
            _nodes ??= new Dictionary<T, Node>(_comparer);
            _roots ??= new List<T>();
        }

        /// <summary>Creates a tree using the default node comparer.</summary>
        public static HierarchyTree<T> Create() => new(null);

        /// <summary>Creates a tree using a custom node comparer.</summary>
        public static HierarchyTree<T> Create(IEqualityComparer<T> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new HierarchyTree<T>(comparer);
        }

        /// <summary>Total number of nodes.</summary>
        public int Count => _nodes.Count;

        /// <summary>The root nodes (nodes without a parent).</summary>
        public IReadOnlyList<T> Roots => _roots;

        /// <summary>All nodes in the tree.</summary>
        public IEnumerable<T> Nodes => _nodes.Keys;

        /// <summary>Returns true when <paramref name="node"/> exists.</summary>
        public bool Contains(T node) => _nodes.ContainsKey(node);

        /// <summary>
        /// Adds <paramref name="node"/> as a root. Returns false when the node
        /// already exists.
        /// </summary>
        public bool AddRoot(T node)
        {
            if (_nodes.ContainsKey(node))
                return false;

            _nodes[node] = new Node { Value = node, HasParent = false };
            _roots.Add(node);
            return true;
        }

        /// <summary>
        /// Adds <paramref name="child"/> under <paramref name="parent"/>. Fails
        /// (returns false) when the parent is absent or the child already exists.
        /// </summary>
        public bool AddChild(T parent, T child)
        {
            if (!_nodes.TryGetValue(parent, out var parentNode))
                return false;
            if (_nodes.ContainsKey(child))
                return false;

            _nodes[child] = new Node { Value = child, Parent = parent, HasParent = true };
            parentNode.Children.Add(child);
            return true;
        }

        /// <summary>Reads the parent of <paramref name="node"/>. False for roots / absent nodes.</summary>
        public bool TryGetParent(T node, out T parent)
        {
            if (_nodes.TryGetValue(node, out var n) && n.HasParent)
            {
                parent = n.Parent;
                return true;
            }

            parent = default;
            return false;
        }

        /// <summary>Children of <paramref name="node"/> (empty when absent / leaf).</summary>
        public IReadOnlyList<T> GetChildren(T node) =>
            _nodes.TryGetValue(node, out var n) ? n.Children : Array.Empty<T>();

        /// <summary>Returns true when <paramref name="node"/> has no children.</summary>
        public bool IsLeaf(T node) =>
            _nodes.TryGetValue(node, out var n) && n.Children.Count == 0;

        /// <summary>Returns true when <paramref name="node"/> is a root.</summary>
        public bool IsRoot(T node) =>
            _nodes.TryGetValue(node, out var n) && !n.HasParent;

        /// <summary>
        /// Re-parents <paramref name="node"/> under <paramref name="newParent"/>.
        /// Fails when either is absent, when they are the same, or when the move
        /// would create a cycle (new parent is a descendant of the node).
        /// </summary>
        public bool Reparent(T node, T newParent)
        {
            if (_comparer.Equals(node, newParent))
                return false;
            if (!_nodes.TryGetValue(node, out var target))
                return false;
            if (!_nodes.TryGetValue(newParent, out var parentNode))
                return false;
            if (IsDescendant(node, newParent))
                return false;

            DetachFromCurrentParent(node, target);
            target.Parent = newParent;
            target.HasParent = true;
            parentNode.Children.Add(node);
            return true;
        }

        /// <summary>
        /// Removes <paramref name="node"/> and its whole subtree. Returns false
        /// when the node is absent.
        /// </summary>
        public bool Remove(T node)
        {
            if (!_nodes.TryGetValue(node, out var target))
                return false;

            DetachFromCurrentParent(node, target);

            // Remove the subtree bottom-up via an explicit stack.
            var stack = new Stack<T>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!_nodes.TryGetValue(current, out var n)) continue;
                foreach (var child in n.Children)
                    stack.Push(child);
                _nodes.Remove(current);
            }

            return true;
        }

        /// <summary>Depth of <paramref name="node"/> (root = 0). Returns -1 when absent.</summary>
        public int GetDepth(T node)
        {
            if (!_nodes.TryGetValue(node, out var n))
                return -1;

            var depth = 0;
            while (n.HasParent)
            {
                n = _nodes[n.Parent];
                depth++;
            }

            return depth;
        }

        /// <summary>
        /// Ancestors of <paramref name="node"/> from immediate parent up to the
        /// root. Empty when the node is a root or absent.
        /// </summary>
        public IEnumerable<T> Ancestors(T node)
        {
            if (!_nodes.TryGetValue(node, out var n))
                yield break;

            while (n.HasParent)
            {
                yield return n.Parent;
                n = _nodes[n.Parent];
            }
        }

        /// <summary>
        /// Descendants of <paramref name="node"/> in DFS pre-order (excluding the
        /// node itself). Empty when the node is absent or a leaf.
        /// </summary>
        public IEnumerable<T> Descendants(T node)
        {
            if (!_nodes.ContainsKey(node))
                yield break;

            var stack = new Stack<T>();
            PushChildrenReversed(node, stack);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                PushChildrenReversed(current, stack);
            }
        }

        /// <summary>
        /// DFS pre-order over the whole forest (or a single subtree when
        /// <paramref name="from"/> is supplied). Includes the start node.
        /// </summary>
        public IEnumerable<T> DepthFirst(T from = default, bool hasStart = false)
        {
            var starts = hasStart ? SingleStart(from) : (IEnumerable<T>)_roots;

            foreach (var start in starts)
            {
                if (!_nodes.ContainsKey(start)) continue;

                var stack = new Stack<T>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    yield return current;
                    PushChildrenReversed(current, stack);
                }
            }
        }

        /// <summary>
        /// BFS over the whole forest (or a single subtree when
        /// <paramref name="from"/> is supplied). Includes the start node.
        /// </summary>
        public IEnumerable<T> BreadthFirst(T from = default, bool hasStart = false)
        {
            var queue = new Queue<T>();
            if (hasStart)
            {
                if (_nodes.ContainsKey(from)) queue.Enqueue(from);
            }
            else
            {
                foreach (var root in _roots)
                    queue.Enqueue(root);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                yield return current;

                if (_nodes.TryGetValue(current, out var n))
                    foreach (var child in n.Children)
                        queue.Enqueue(child);
            }
        }

        /// <summary>Removes every node.</summary>
        public void Clear()
        {
            _nodes.Clear();
            _roots.Clear();
        }

        /// <summary>
        /// Re-attaches a custom node comparer (e.g. after Unity deserialization)
        /// and rebuilds the index. Passing null falls back to the default.
        /// </summary>
        public void RestoreComparer(IEqualityComparer<T> comparer)
        {
            var next = comparer ?? EqualityComparer<T>.Default;
            if (ReferenceEquals(next, _comparer)) return;

            _comparer = next;
            var rebuilt = new Dictionary<T, Node>(_comparer);
            foreach (var pair in _nodes)
                rebuilt[pair.Key] = pair.Value;
            _nodes = rebuilt;
        }

        // ---- Internal helpers ----------------------------------------------

        private IEnumerable<T> SingleStart(T node)
        {
            yield return node;
        }

        private void PushChildrenReversed(T node, Stack<T> stack)
        {
            if (!_nodes.TryGetValue(node, out var n)) return;
            var children = n.Children;
            for (var i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }

        // True when 'candidate' lies within the subtree rooted at 'node'.
        private bool IsDescendant(T node, T candidate)
        {
            var current = candidate;
            while (_nodes.TryGetValue(current, out var n) && n.HasParent)
            {
                if (_comparer.Equals(n.Parent, node)) return true;
                current = n.Parent;
            }

            return false;
        }

        private void DetachFromCurrentParent(T node, Node target)
        {
            if (target.HasParent)
            {
                if (_nodes.TryGetValue(target.Parent, out var parentNode))
                    RemoveChild(parentNode.Children, node);
            }
            else
            {
                RemoveChild(_roots, node);
            }

            target.HasParent = false;
        }

        private void RemoveChild(List<T> list, T node)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (!_comparer.Equals(list[i], node)) continue;
                list.RemoveAt(i);
                return;
            }
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<HierarchyElement<T>> buffer)
        {
            EnsureInitialized();
            // Emit in BFS order so every parent precedes its children on read.
            foreach (var node in BreadthFirst())
            {
                var n = _nodes[node];
                buffer.Add(new HierarchyElement<T>(node, n.HasParent, n.Parent));
            }
        }

        protected override void ReadSerializedItems(IReadOnlyList<HierarchyElement<T>> items)
        {
            _comparer ??= EqualityComparer<T>.Default;
            _nodes = new Dictionary<T, Node>(_comparer);
            _roots?.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                var element = items[i];
                if (element.HasParent && _nodes.ContainsKey(element.Parent))
                    AddChild(element.Parent, element.Node);
                else
                    AddRoot(element.Node);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using AceLand.Collection.Core;
using UnityEngine;

namespace AceLand.Collection.Relational
{
    /// <summary>
    /// Adjacency-list graph over unique nodes, optionally directed and carrying
    /// an edge payload (weight, cost, label, …). The core deliberately ships
    /// only the structure plus BFS / DFS traversal; heavier algorithms
    /// (Dijkstra, A*, topological sort) live in separate extension modules so
    /// the core stays lean.
    /// </summary>
    /// <remarks>
    /// <para>Use: level connectivity, dialogue trees, dependency graphs, state transitions.</para>
    /// <para>
    /// Construct through <see cref="CreateDirected"/> / <see cref="CreateUndirected"/>.
    /// The node comparer is not serialized; after Unity deserialization the
    /// default comparer is used unless re-supplied via <see cref="RestoreComparer"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class Graph<TNode, TEdge> :
        SerializableCollection<GraphElement<TNode, TEdge>>
    {
        /// <summary>An outgoing edge: its target node and payload.</summary>
        public readonly struct Edge
        {
            internal Edge(TNode target, TEdge value)
            {
                Target = target;
                Value = value;
            }

            /// <summary>The node this edge points to.</summary>
            public TNode Target { get; }

            /// <summary>The edge payload (weight / label / cost).</summary>
            public TEdge Value { get; }
        }

        [SerializeField]
        private bool _directed;

        private Dictionary<TNode, List<Edge>> _adjacency;
        private IEqualityComparer<TNode> _comparer;
        private int _edgeCount;

        // Unity instantiates a [SerializeField] Graph by allocating an
        // uninitialized object, bypassing the private constructor. Guard the
        // serialization callbacks so _adjacency / _comparer are always valid.
        private void EnsureInitialized()
        {
            _comparer ??= EqualityComparer<TNode>.Default;
            _adjacency ??= new Dictionary<TNode, List<Edge>>(_comparer);
        }

        private Graph(bool directed, IEqualityComparer<TNode> comparer)
        {
            _directed = directed;
            _comparer = comparer ?? EqualityComparer<TNode>.Default;
            _adjacency = new Dictionary<TNode, List<Edge>>(_comparer);
        }

        /// <summary>Creates a directed graph using the default node comparer.</summary>
        public static Graph<TNode, TEdge> CreateDirected() => new(true, null);

        /// <summary>Creates a directed graph using a custom node comparer.</summary>
        public static Graph<TNode, TEdge> CreateDirected(IEqualityComparer<TNode> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new Graph<TNode, TEdge>(true, comparer);
        }

        /// <summary>Creates an undirected graph using the default node comparer.</summary>
        public static Graph<TNode, TEdge> CreateUndirected() => new(false, null);

        /// <summary>Creates an undirected graph using a custom node comparer.</summary>
        public static Graph<TNode, TEdge> CreateUndirected(IEqualityComparer<TNode> comparer)
        {
            CollectionError.RequireNotNull(comparer, nameof(comparer));
            return new Graph<TNode, TEdge>(false, comparer);
        }

        /// <summary>Whether edges are one-directional.</summary>
        public bool IsDirected => _directed;

        /// <summary>Number of nodes in the graph.</summary>
        public int NodeCount => _adjacency.Count;

        /// <summary>
        /// Number of edges. For an undirected graph a connection counts once even
        /// though it is stored on both endpoints.
        /// </summary>
        public int EdgeCount => _edgeCount;

        /// <summary>All nodes currently in the graph.</summary>
        public IEnumerable<TNode> Nodes => _adjacency.Keys;

        /// <summary>Returns true when <paramref name="node"/> exists.</summary>
        public bool ContainsNode(TNode node) => _adjacency.ContainsKey(node);

        /// <summary>
        /// Ensures <paramref name="node"/> exists. Returns true when it was newly
        /// added, false when it was already present.
        /// </summary>
        public bool AddNode(TNode node)
        {
            if (_adjacency.ContainsKey(node))
                return false;

            _adjacency[node] = new List<Edge>();
            return true;
        }

        /// <summary>
        /// Removes <paramref name="node"/> together with all edges touching it.
        /// Returns false when the node is absent.
        /// </summary>
        public bool RemoveNode(TNode node)
        {
            if (!_adjacency.TryGetValue(node, out var outgoing))
                return false;

            _edgeCount -= outgoing.Count;
            _adjacency.Remove(node);

            // Purge inbound references from every other node.
            foreach (var pair in _adjacency)
            {
                var list = pair.Value;
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (!_comparer.Equals(list[i].Target, node)) continue;
                    list.RemoveAt(i);
                    // For a directed graph these were not counted in 'outgoing'.
                    if (_directed) _edgeCount--;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds (or replaces) an edge from <paramref name="from"/> to
        /// <paramref name="to"/> carrying <paramref name="edge"/>. Missing
        /// endpoints are created automatically. For an undirected graph the
        /// reciprocal edge is stored as well. Returns true when a new edge was
        /// created, false when an existing edge's payload was updated.
        /// </summary>
        public bool AddEdge(TNode from, TNode to, TEdge edge = default)
        {
            AddNode(from);
            AddNode(to);

            var added = SetEdge(from, to, edge);
            if (!_directed && !_comparer.Equals(from, to))
                SetEdge(to, from, edge);

            if (added) _edgeCount++;
            return added;
        }

        /// <summary>
        /// Removes the edge from <paramref name="from"/> to <paramref name="to"/>
        /// (and its reciprocal when undirected). Returns false when absent.
        /// </summary>
        public bool RemoveEdge(TNode from, TNode to)
        {
            var removed = UnsetEdge(from, to);
            if (!_directed && !_comparer.Equals(from, to))
                UnsetEdge(to, from);

            if (removed) _edgeCount--;
            return removed;
        }

        /// <summary>Returns true when an edge from <paramref name="from"/> to <paramref name="to"/> exists.</summary>
        public bool ContainsEdge(TNode from, TNode to) =>
            TryFindEdge(from, to, out _);

        /// <summary>Reads the payload of the edge from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public bool TryGetEdge(TNode from, TNode to, out TEdge edge)
        {
            if (TryFindEdge(from, to, out var index))
            {
                edge = _adjacency[from][index].Value;
                return true;
            }

            edge = default;
            return false;
        }

        /// <summary>Outgoing edges of <paramref name="node"/> (empty when node absent).</summary>
        public IReadOnlyList<Edge> GetEdges(TNode node) =>
            _adjacency.TryGetValue(node, out var list) ? list : Array.Empty<Edge>();

        /// <summary>Out-degree of <paramref name="node"/> (0 when absent).</summary>
        public int GetDegree(TNode node) =>
            _adjacency.TryGetValue(node, out var list) ? list.Count : 0;

        /// <summary>Removes every node and edge.</summary>
        public void Clear()
        {
            _adjacency.Clear();
            _edgeCount = 0;
        }

        /// <summary>
        /// Re-attaches a custom node comparer (e.g. after Unity deserialization)
        /// and rebuilds the adjacency index. Passing null falls back to default.
        /// </summary>
        public void RestoreComparer(IEqualityComparer<TNode> comparer)
        {
            var next = comparer ?? EqualityComparer<TNode>.Default;
            if (ReferenceEquals(next, _comparer)) return;

            var rebuilt = new Dictionary<TNode, List<Edge>>(next);
            foreach (var pair in _adjacency)
                rebuilt[pair.Key] = pair.Value;

            _comparer = next;
            _adjacency.Clear();
            foreach (var pair in rebuilt)
                _adjacency[pair.Key] = pair.Value;
        }

        // ---- Traversal ------------------------------------------------------

        /// <summary>
        /// Breadth-first traversal from <paramref name="start"/>, yielding each
        /// reachable node once in BFS order. Empty when the start node is absent.
        /// </summary>
        public IEnumerable<TNode> BreadthFirst(TNode start)
        {
            if (!_adjacency.ContainsKey(start))
                yield break;

            var visited = new HashSet<TNode>(_comparer);
            var queue = new Queue<TNode>();
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                yield return node;

                foreach (var edge in _adjacency[node])
                {
                    if (!visited.Add(edge.Target)) continue;
                    queue.Enqueue(edge.Target);
                }
            }
        }

        /// <summary>
        /// Depth-first traversal from <paramref name="start"/>, yielding each
        /// reachable node once in pre-order. Empty when the start node is absent.
        /// </summary>
        public IEnumerable<TNode> DepthFirst(TNode start)
        {
            if (!_adjacency.ContainsKey(start))
                yield break;

            var visited = new HashSet<TNode>(_comparer);
            var stack = new Stack<TNode>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!visited.Add(node)) continue;
                yield return node;

                var edges = _adjacency[node];
                // Push in reverse so the first edge is explored first.
                for (var i = edges.Count - 1; i >= 0; i--)
                    stack.Push(edges[i].Target);
            }
        }

        // ---- Internal helpers ----------------------------------------------

        // Writes an edge on 'from'; returns true when newly added.
        private bool SetEdge(TNode from, TNode to, TEdge edge)
        {
            var list = _adjacency[from];
            for (var i = 0; i < list.Count; i++)
            {
                if (!_comparer.Equals(list[i].Target, to)) continue;
                list[i] = new Edge(to, edge);
                return false;
            }

            list.Add(new Edge(to, edge));
            return true;
        }

        private bool UnsetEdge(TNode from, TNode to)
        {
            if (!_adjacency.TryGetValue(from, out var list))
                return false;

            for (var i = 0; i < list.Count; i++)
            {
                if (!_comparer.Equals(list[i].Target, to)) continue;
                list.RemoveAt(i);
                return true;
            }

            return false;
        }

        private bool TryFindEdge(TNode from, TNode to, out int index)
        {
            if (_adjacency.TryGetValue(from, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (!_comparer.Equals(list[i].Target, to)) continue;
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<GraphElement<TNode, TEdge>> buffer)
        {
            EnsureInitialized();
            foreach (var pair in _adjacency)
            {
                var node = pair.Key;
                var edges = pair.Value;

                if (edges.Count == 0)
                {
                    // Preserve isolated nodes.
                    buffer.Add(GraphElement<TNode, TEdge>.ForNode(node));
                    continue;
                }

                foreach (var edge in edges)
                {
                    // For an undirected graph, only emit each pair once.
                    if (!_directed && ShouldSkipMirror(node, edge.Target)) continue;
                    buffer.Add(GraphElement<TNode, TEdge>.ForEdge(node, edge.Target, edge.Value));
                }
            }
        }

        protected override void ReadSerializedItems(IReadOnlyList<GraphElement<TNode, TEdge>> items)
        {
            EnsureInitialized();
            _adjacency.Clear();
            _edgeCount = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var element = items[i];
                if (element.IsEdge)
                    AddEdge(element.From, element.To, element.Edge);
                else
                    AddNode(element.From);
            }
        }

        // Deterministic tie-break to avoid emitting an undirected edge twice.
        private bool ShouldSkipMirror(TNode from, TNode to)
        {
            if (_comparer.Equals(from, to)) return false;
            // Skip when 'from' has the larger hash (keep the smaller-hash side).
            var fromHash = _comparer.GetHashCode(from);
            var toHash = _comparer.GetHashCode(to);
            if (fromHash != toHash) return fromHash > toHash;
            // Hash collision: fall back to keeping the first-seen orientation by
            // emitting it (both sides will attempt; AddEdge dedups on read).
            return false;
        }
    }
}

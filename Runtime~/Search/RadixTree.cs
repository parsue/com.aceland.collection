using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;

namespace AceLand.Collection.Search
{
    /// <summary>
    /// Compressed trie (radix / Patricia tree). Chains of single-child nodes are
    /// merged into one edge that stores a whole substring, saving memory versus
    /// a plain <see cref="Trie{TValue}"/> for sparse key sets while keeping the
    /// same O(key length) operations and prefix queries.
    /// </summary>
    /// <remarks>
    /// <para>Use: large dictionaries, IP / routing tables, autocomplete at scale.</para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Unity-serializable via the same
    /// flattened <see cref="TrieEntry{TValue}"/> list strategy as
    /// <see cref="Trie{TValue}"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RadixTree<TValue> : SerializableCollection<TrieEntry<TValue>>,
        IReadOnlyCollection<TrieEntry<TValue>>
    {
        private sealed class Node
        {
            // Each child edge is keyed by its first character; Label holds the
            // full edge substring.
            public readonly Dictionary<char, Edge> Edges = new();
            public bool HasValue;
            public TValue Value;
        }

        private sealed class Edge
        {
            public string Label;
            public Node Target;
        }

        private Node _root = new();
        private int _count;

        private RadixTree() { }

        /// <summary>Creates an empty radix tree.</summary>
        public static RadixTree<TValue> Create() => new();

        /// <summary>Number of stored keys.</summary>
        public int Count => _count;

        /// <summary>True when no keys are stored.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>Inserts or updates <paramref name="key"/>.</summary>
        /// <returns><c>false</c> when the key was null/empty (rejected).</returns>
        public bool Set(string key, TValue value)
        {
            if (string.IsNullOrEmpty(key)) return false;
            Insert(_root, key, value);
            return true;
        }

        /// <summary>Looks up the value for an exact key.</summary>
        public bool TryGet(string key, out TValue value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;

            var node = _root;
            var i = 0;
            while (i < key.Length)
            {
                if (!node.Edges.TryGetValue(key[i], out var edge))
                    return false;

                var label = edge.Label;
                if (i + label.Length > key.Length ||
                    string.CompareOrdinal(key, i, label, 0, label.Length) != 0)
                    return false;

                i += label.Length;
                node = edge.Target;
            }

            if (!node.HasValue) return false;
            value = node.Value;
            return true;
        }

        /// <summary>True when the exact key exists.</summary>
        public bool ContainsKey(string key) => TryGet(key, out _);

        /// <summary>True when at least one key starts with <paramref name="prefix"/>.</summary>
        public bool ContainsPrefix(string prefix)
        {
            if (prefix is null) return false;
            return FindPrefixRoot(prefix, out _, out _);
        }

        /// <summary>
        /// Enumerates all keys that start with <paramref name="prefix"/> (empty
        /// prefix = all keys), optionally capped by <paramref name="limit"/>.
        /// </summary>
        public IEnumerable<TrieEntry<TValue>> StartsWith(string prefix, int limit = 0)
        {
            prefix ??= string.Empty;

            if (prefix.Length == 0)
            {
                foreach (var entry in EnumerateCapped(Collect(_root, string.Empty), limit))
                    yield return entry;
                yield break;
            }

            if (!FindPrefixRoot(prefix, out var node, out var matched))
                yield break;

            foreach (var entry in EnumerateCapped(Collect(node, matched), limit))
                yield return entry;
        }

        /// <summary>Removes all keys.</summary>
        public void Clear()
        {
            _root = new Node();
            _count = 0;
        }

        public IEnumerator<TrieEntry<TValue>> GetEnumerator() =>
            Collect(_root, string.Empty).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- insert / split -------------------------------------------------

        private void Insert(Node node, string key, TValue value)
        {
            var i = 0;
            while (i < key.Length)
            {
                var c = key[i];
                if (!node.Edges.TryGetValue(c, out var edge))
                {
                    // No edge: attach the remaining suffix as a new leaf.
                    var leaf = new Node { HasValue = true, Value = value };
                    node.Edges[c] = new Edge { Label = key.Substring(i), Target = leaf };
                    _count++;
                    return;
                }

                var label = edge.Label;
                var common = CommonPrefixLength(label, key, i);

                if (common == label.Length)
                {
                    // Whole edge consumed: descend.
                    i += label.Length;
                    node = edge.Target;
                    continue;
                }

                // Partial match: split the edge at 'common'.
                SplitEdge(edge, common);
                i += common;

                var remaining = key.Substring(i);
                if (remaining.Length == 0)
                {
                    if (!edge.Target.HasValue)
                    {
                        edge.Target.HasValue = true;
                        _count++;
                    }
                    edge.Target.Value = value;
                }
                else
                {
                    var leaf = new Node { HasValue = true, Value = value };
                    edge.Target.Edges[remaining[0]] =
                        new Edge { Label = remaining, Target = leaf };
                    _count++;
                }
                return;
            }

            // Key fully matched an existing node boundary.
            if (!node.HasValue)
            {
                node.HasValue = true;
                _count++;
            }
            node.Value = value;
        }

        // Splits 'edge' so its label becomes the first 'at' chars, inserting an
        // intermediate node that carries the remainder.
        private static void SplitEdge(Edge edge, int at)
        {
            var label = edge.Label;
            var head = label.Substring(0, at);
            var tail = label.Substring(at);

            var middle = new Node();
            var child = new Edge { Label = tail, Target = edge.Target };
            middle.Edges[tail[0]] = child;

            edge.Label = head;
            edge.Target = middle;
        }

        private bool FindPrefixRoot(string prefix, out Node node, out string matched)
        {
            node = _root;
            matched = string.Empty;
            var i = 0;

            while (i < prefix.Length)
            {
                if (!node.Edges.TryGetValue(prefix[i], out var edge))
                    return false;

                var label = edge.Label;
                var common = CommonPrefixLength(label, prefix, i);

                if (i + common == prefix.Length)
                {
                    // Prefix ends inside (or exactly at) this edge.
                    node = edge.Target;
                    matched += label;
                    return true;
                }

                if (common < label.Length)
                    return false; // diverged mid-edge

                i += label.Length;
                matched += label;
                node = edge.Target;
            }

            return true;
        }

        private static int CommonPrefixLength(string label, string key, int keyStart)
        {
            var max = Math.Min(label.Length, key.Length - keyStart);
            var n = 0;
            while (n < max && label[n] == key[keyStart + n]) n++;
            return n;
        }

        private static IEnumerable<TrieEntry<TValue>> Collect(Node node, string prefix)
        {
            if (node.HasValue)
                yield return new TrieEntry<TValue>(prefix, node.Value);

            foreach (var pair in node.Edges)
            foreach (var entry in Collect(pair.Value.Target, prefix + pair.Value.Label))
                yield return entry;
        }

        private static IEnumerable<TrieEntry<TValue>> EnumerateCapped(
            IEnumerable<TrieEntry<TValue>> source, int limit)
        {
            var count = 0;
            foreach (var entry in source)
            {
                yield return entry;
                if (limit > 0 && ++count >= limit) yield break;
            }
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<TrieEntry<TValue>> buffer)
        {
            // Unity deserializes a [SerializeField] RadixTree into an
            // uninitialized object, bypassing the field initializer, so _root
            // may be null when OnBeforeSerialize first runs.
            _root ??= new Node();
            foreach (var entry in Collect(_root, string.Empty))
                buffer.Add(entry);
        }

        protected override void ReadSerializedItems(IReadOnlyList<TrieEntry<TValue>> items)
        {
            _root = new Node();
            _count = 0;
            for (var i = 0; i < items.Count; i++)
                Set(items[i].Key, items[i].Value);
        }
    }
}

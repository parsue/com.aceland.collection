using System;
using System.Collections;
using System.Collections.Generic;
using AceLand.Collection.Core;

namespace AceLand.Collection.Search
{
    /// <summary>
    /// Character-by-character prefix tree mapping <see cref="string"/> keys to
    /// values. Lookup / insert / delete are O(key length), independent of the
    /// number of stored keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use: autocomplete, command palettes (pairs with a command history),
    /// dictionary prefix queries. Fills a BCL gap: .NET has no built-in trie.
    /// </para>
    /// <para>
    /// Construct through <see cref="Create()"/>. Unity-serializable: entries are
    /// flattened to a <see cref="TrieEntry{TValue}"/> list and the tree is
    /// rebuilt on deserialize.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class Trie<TValue> : SerializableCollection<TrieEntry<TValue>>,
        IReadOnlyCollection<TrieEntry<TValue>>
    {
        private sealed class Node
        {
            public readonly Dictionary<char, Node> Children = new();
            public bool HasValue;
            public TValue Value;
        }

        private Node _root = new();
        private int _count;

        private Trie() { }

        /// <summary>Creates an empty trie.</summary>
        public static Trie<TValue> Create() => new();

        /// <summary>Number of stored keys.</summary>
        public int Count => _count;

        /// <summary>True when no keys are stored.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// Inserts or updates <paramref name="key"/> with <paramref name="value"/>.
        /// </summary>
        /// <returns><c>false</c> when the key was empty/null (rejected).</returns>
        public bool Set(string key, TValue value)
        {
            if (string.IsNullOrEmpty(key)) return false;

            var node = _root;
            foreach (var c in key)
            {
                if (!node.Children.TryGetValue(c, out var next))
                {
                    next = new Node();
                    node.Children[c] = next;
                }
                node = next;
            }

            if (!node.HasValue)
            {
                node.HasValue = true;
                _count++;
            }
            node.Value = value;
            return true;
        }

        /// <summary>Looks up the value for an exact key.</summary>
        public bool TryGet(string key, out TValue value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;

            var node = FindNode(key);
            if (node is not { HasValue: true }) return false;

            value = node.Value;
            return true;
        }

        /// <summary>True when the exact key exists.</summary>
        public bool ContainsKey(string key) => TryGet(key, out _);

        /// <summary>True when at least one key starts with <paramref name="prefix"/>.</summary>
        public bool ContainsPrefix(string prefix)
        {
            if (prefix is null) return false;
            return FindNode(prefix) is not null;
        }

        /// <summary>Removes an exact key.</summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return RemoveRecursive(_root, key, 0);
        }

        /// <summary>
        /// Enumerates all keys that start with <paramref name="prefix"/> (an
        /// empty prefix returns every key), optionally capped by
        /// <paramref name="limit"/> (0 = unlimited). Ideal for autocomplete.
        /// </summary>
        public IEnumerable<TrieEntry<TValue>> StartsWith(string prefix, int limit = 0)
        {
            prefix ??= string.Empty;
            var node = FindNode(prefix);
            if (node is null) yield break;

            var count = 0;
            foreach (var entry in Collect(node, prefix))
            {
                yield return entry;
                if (limit > 0 && ++count >= limit) yield break;
            }
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

        // ---- internals ------------------------------------------------------

        private Node FindNode(string path)
        {
            var node = _root;
            foreach (var c in path)
            {
                if (!node.Children.TryGetValue(c, out node))
                    return null;
            }
            return node;
        }

        private static IEnumerable<TrieEntry<TValue>> Collect(Node node, string prefix)
        {
            if (node.HasValue)
                yield return new TrieEntry<TValue>(prefix, node.Value);

            foreach (var pair in node.Children)
            foreach (var entry in Collect(pair.Value, prefix + pair.Key))
                yield return entry;
        }

        private bool RemoveRecursive(Node node, string key, int depth)
        {
            if (depth == key.Length)
            {
                if (!node.HasValue) return false;
                node.HasValue = false;
                node.Value = default;
                _count--;
                return true;
            }

            var c = key[depth];
            if (!node.Children.TryGetValue(c, out var child))
                return false;

            var removed = RemoveRecursive(child, key, depth + 1);
            if (removed && child.Children.Count == 0 && !child.HasValue)
                node.Children.Remove(c);
            return removed;
        }

        // ---- Serialization --------------------------------------------------

        protected override void WriteSerializedItems(List<TrieEntry<TValue>> buffer)
        {
            // Unity deserializes a [SerializeField] Trie into an uninitialized
            // object, bypassing the field initializer, so _root may be null when
            // OnBeforeSerialize first runs.
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

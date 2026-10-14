using System;

namespace AceLand.Collection
{
    /// <summary>
    /// Configures the two column headers shown by the Inspector list editor for the
    /// key/value collection family (<see cref="BiDictionary{TKey,TValue}"/>,
    /// <see cref="MultiMap{TKey,TValue}"/>, <see cref="LruCache{TKey,TValue}"/> and
    /// <see cref="IndexedPriorityQueue{TItem,TPriority}"/>).
    /// </summary>
    /// <remarks>
    /// Runtime-only marker (no serialized state); consumed exclusively by the paid
    /// editor drawer via reflection. Apply to the serialized collection field to rename
    /// its two columns, e.g. <c>[KeyValueTitle("Id", "Name")]</c>. When absent the drawer
    /// falls back to its per-type defaults (<c>key</c> / <c>value</c>).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class KeyValueTitleAttribute : Attribute
    {
        /// <summary>
        /// Creates a column-title override. Blank arguments fall back to
        /// <c>"key"</c> / <c>"value"</c>.
        /// </summary>
        /// <param name="key">Header text for the key (left) column.</param>
        /// <param name="value">Header text for the value (right) column.</param>
        public KeyValueTitleAttribute(string key = "key", string value = "value")
        {
            Key = string.IsNullOrWhiteSpace(key) ? "key" : key;
            Value = string.IsNullOrWhiteSpace(value) ? "value" : value;
        }

        /// <summary>Header text for the key (left) column.</summary>
        public string Key { get; }

        /// <summary>Header text for the value (right) column.</summary>
        public string Value { get; }
    }
}

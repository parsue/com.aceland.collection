using System;
using System.Collections.Generic;
using UnityEngine;

namespace AceLand.Collection.Core
{
    /// <summary>
    /// Base class that gives a managed collection a Unity-serializable backing
    /// store. The derived structure keeps its own runtime representation (ring
    /// index, heap array, cache links, ...) but its persistable state is always
    /// projected down to a flat <see cref="_items"/> list on serialize and
    /// rebuilt from that list on deserialize.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity cannot serialize generic containers, dictionaries or arbitrary
    /// object graphs. By funnelling everything through a single
    /// <c>[SerializeField] List&lt;T&gt;</c> we get Inspector persistence and
    /// Play/Edit round-tripping for free, provided <typeparamref name="T"/> is
    /// itself Unity-serializable.
    /// </para>
    /// <para>
    /// Derived types implement <see cref="WriteSerializedItems"/> (runtime →
    /// flat list) and <see cref="ReadSerializedItems"/> (flat list → runtime).
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The element type; must be Unity-serializable to persist.</typeparam>
    [Serializable]
    public abstract class SerializableCollection<T> : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<T> _items = new();

        /// <summary>
        /// Projects the live runtime state into <paramref name="buffer"/> in the
        /// exact order it must be restored. The buffer is cleared before the call.
        /// </summary>
        protected abstract void WriteSerializedItems(List<T> buffer);

        /// <summary>
        /// Rebuilds the live runtime state from the previously serialized
        /// <paramref name="items"/> (in the same order they were written).
        /// </summary>
        protected abstract void ReadSerializedItems(IReadOnlyList<T> items);

        /// <summary>
        /// Produces a fresh, ordered snapshot of the collection's persistable
        /// state by re-running <see cref="WriteSerializedItems"/> into a new
        /// buffer. This is the single, uniform read path used by the DOTS bridge
        /// (<c>ToNative</c>) so native conversions never need each container to
        /// expose a public enumerator.
        /// </summary>
        internal List<T> SnapshotSerialized()
        {
            var buffer = new List<T>();
            WriteSerializedItems(buffer);
            return buffer;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _items ??= new List<T>();
            _items.Clear();
            WriteSerializedItems(_items);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _items ??= new List<T>();
            ReadSerializedItems(_items);
        }
    }
}

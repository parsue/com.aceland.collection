using System;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Serializable rectangle payload used as the flat backing element for
    /// <see cref="RTree2D{T}"/>: an axis-aligned <see cref="Rect"/> paired with
    /// an arbitrary item. Models objects that occupy an area rather than a point.
    /// </summary>
    [Serializable]
    public struct BoundsItem2D<T>
    {
        [SerializeField] private Rect _bounds;
        [SerializeField] private T _item;

        internal BoundsItem2D(Rect bounds, T item)
        {
            _bounds = bounds;
            _item = item;
        }

        /// <summary>Axis-aligned bounding rectangle of the item.</summary>
        public Rect Bounds => _bounds;

        /// <summary>The stored item.</summary>
        public T Item => _item;
    }
}

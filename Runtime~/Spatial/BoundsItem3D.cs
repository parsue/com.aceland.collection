using System;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Serializable volume payload used as the flat backing element for
    /// <see cref="RTree3D{T}"/>: an axis-aligned <see cref="Bounds"/> paired with
    /// an arbitrary item. Models objects that occupy a volume rather than a point.
    /// </summary>
    [Serializable]
    public struct BoundsItem3D<T>
    {
        [SerializeField] private Bounds _bounds;
        [SerializeField] private T _item;

        internal BoundsItem3D(Bounds bounds, T item)
        {
            _bounds = bounds;
            _item = item;
        }

        /// <summary>Axis-aligned bounding box of the item.</summary>
        public Bounds Bounds => _bounds;

        /// <summary>The stored item.</summary>
        public T Item => _item;
    }
}

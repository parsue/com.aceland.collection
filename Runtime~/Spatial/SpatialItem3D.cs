using System;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Serializable point payload used as the flat backing element for the 3D
    /// spatial indexes (<see cref="SpatialHashGrid3D{T}"/>,
    /// <see cref="Octree{T}"/>, <see cref="KdTree3D{T}"/>): a world position
    /// paired with an arbitrary item.
    /// </summary>
    [Serializable]
    public struct SpatialItem3D<T>
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private T _item;

        internal SpatialItem3D(Vector3 position, T item)
        {
            _position = position;
            _item = item;
        }

        /// <summary>World-space position of the item.</summary>
        public Vector3 Position => _position;

        /// <summary>The stored item.</summary>
        public T Item => _item;
    }
}

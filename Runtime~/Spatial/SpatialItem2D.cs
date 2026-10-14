using System;
using UnityEngine;

namespace AceLand.Collection.Spatial
{
    /// <summary>
    /// Serializable point payload used as the flat backing element for the 2D
    /// spatial indexes (<see cref="SpatialHashGrid2D{T}"/>,
    /// <see cref="QuadTree{T}"/>, <see cref="KdTree2D{T}"/>): a world position
    /// paired with an arbitrary item.
    /// </summary>
    [Serializable]
    public struct SpatialItem2D<T>
    {
        [SerializeField] private Vector2 _position;
        [SerializeField] private T _item;

        internal SpatialItem2D(Vector2 position, T item)
        {
            _position = position;
            _item = item;
        }

        /// <summary>World-space position of the item.</summary>
        public Vector2 Position => _position;

        /// <summary>The stored item.</summary>
        public T Item => _item;
    }
}

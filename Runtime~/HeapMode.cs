namespace AceLand.Collection
{
    /// <summary>Orientation of a <see cref="BinaryHeap{T}"/>.</summary>
    public enum HeapMode
    {
        /// <summary>Smallest element sits at the root (O(1) peek min).</summary>
        Min,

        /// <summary>Largest element sits at the root (O(1) peek max).</summary>
        Max
    }
}

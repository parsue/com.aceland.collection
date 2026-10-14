using System.Collections.Generic;
using Unity.Collections;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Conversion bridge between the managed, Unity-serializable
    /// <see cref="BinaryHeap{T}"/> and the Burst-friendly
    /// <see cref="NativeBinaryHeap{T,TComparer}"/>.
    /// </summary>
    /// <remarks>
    /// The managed heap orders elements through an <see cref="IComparer{T}"/>
    /// plus a <see cref="HeapMode"/>, while the native heap needs a value-type
    /// comparer known at compile time so Burst can inline it. Because those two
    /// worlds cannot be mapped automatically, <see cref="ToNative{T,TComparer}"/>
    /// takes the target struct comparer explicitly. Ownership of the returned
    /// native container transfers to the caller, who must
    /// <see cref="NativeBinaryHeap{T,TComparer}.Dispose"/> it.
    /// </remarks>
    public static class BinaryHeapDotsBridge
    {
        /// <summary>
        /// Copies every element of <paramref name="source"/> into a new
        /// <see cref="NativeBinaryHeap{T,TComparer}"/> ordered by
        /// <paramref name="comparer"/>. The caller owns and must dispose the result.
        /// </summary>
        public static NativeBinaryHeap<T, TComparer> ToNative<T, TComparer>(
            this BinaryHeap<T> source, TComparer comparer, Allocator allocator)
            where T : unmanaged
            where TComparer : struct, IComparer<T>
        {
            var snapshot = source.SnapshotSerialized();
            var heap = new NativeBinaryHeap<T, TComparer>(snapshot.Count, allocator, comparer);
            for (var i = 0; i < snapshot.Count; i++)
                heap.Push(snapshot[i]);
            return heap;
        }

        /// <summary>
        /// Copies every element of <paramref name="source"/> into a new managed
        /// <see cref="BinaryHeap{T}"/> using the given <paramref name="mode"/>.
        /// </summary>
        public static BinaryHeap<T> ToManaged<T, TComparer>(
            this NativeBinaryHeap<T, TComparer> source, HeapMode mode = HeapMode.Min)
            where T : unmanaged
            where TComparer : struct, IComparer<T>
        {
            var heap = BinaryHeap<T>.Create(mode);
            var items = source.BackingItems;
            for (var i = 0; i < items.Length; i++)
                heap.Push(items[i]);
            return heap;
        }
    }
}

using System.Collections.Generic;

namespace AceLand.Collection.Dots
{
    /// <summary>
    /// Burst-friendly comparer contract for the unmanaged collection variants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Native* containers take the comparer as a generic type parameter
    /// constrained to <c>where TComparer : struct, IComparer&lt;T&gt;</c>. Because the
    /// comparer is a value type known at compile time, Burst can fully inline
    /// <see cref="IComparer{T}.Compare"/> and remove interface (vtable) dispatch.
    /// </para>
    /// <para>
    /// Implement <see cref="IComparer{T}"/> directly on a <c>struct</c>; this alias
    /// interface simply documents the intended usage and can be used where a named
    /// contract improves readability. It is optional.
    /// </para>
    /// </remarks>
    public interface INativeComparer<in T> : IComparer<T> where T : unmanaged
    {
    }
}

namespace AceLand.Collection.Core
{
    /// <summary>
    /// Common contract for the builder pattern used across the package.
    /// Concrete collections never expose a public constructor; callers obtain
    /// instances through a static <c>Create</c> factory or a builder that
    /// implements this interface.
    /// </summary>
    /// <typeparam name="T">The collection type produced by the builder.</typeparam>
    public interface ICollectionBuilder<out T>
    {
        /// <summary>Materialises the configured collection instance.</summary>
        T Build();
    }
}

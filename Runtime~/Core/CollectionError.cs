using System;

namespace AceLand.Collection.Core
{
    /// <summary>
    /// Thrown only for programmer errors that cannot be represented by a
    /// <c>TryXxx</c> result (e.g. invalid capacity at construction time).
    /// Runtime hot-path boundary conditions (full / empty / duplicate) are
    /// reported through <c>bool TryXxx(...)</c> members instead of exceptions.
    /// </summary>
    public sealed class CollectionException : Exception
    {
        internal CollectionException(string message) : base(message) { }
    }

    /// <summary>Centralised guard helpers for construction-time validation.</summary>
    internal static class CollectionError
    {
        internal static void RequirePositiveCapacity(int capacity, string paramName)
        {
            if (capacity <= 0)
                throw new CollectionException(
                    $"'{paramName}' must be a positive capacity, got {capacity}.");
        }

        internal static void RequireNonNegativeCapacity(int capacity, string paramName)
        {
            if (capacity < 0)
                throw new CollectionException(
                    $"'{paramName}' must be zero or positive, got {capacity}.");
        }

        internal static void RequireNotNull(object value, string paramName)
        {
            if (value is null)
                throw new CollectionException($"'{paramName}' must not be null.");
        }
    }
}

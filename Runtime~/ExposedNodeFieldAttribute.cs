using System;

namespace AceLand.Collection
{
    /// <summary>
    /// Marks a field of a serializable node type so the Collection Whiteboard editor
    /// shows that field's name + value on the node card body. Only exposed fields are
    /// rendered (word-wrapped, with the card auto-sizing its height); if a node type
    /// has no exposed field, its card body stays compact and shows only the title.
    /// </summary>
    /// <remarks>
    /// Runtime-only marker (no serialized state); consumed exclusively by the editor
    /// tooling via reflection. May be applied to multiple fields, including nested
    /// fields of composite (custom class / struct) members, at any depth.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ExposedNodeFieldAttribute : Attribute
    {
    }
}

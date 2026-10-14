using System;

namespace AceLand.Collection
{
    /// <summary>
    /// Marks a field of a serializable node type so the Collection Whiteboard editor
    /// uses that field's value as the node card's title. When absent, the whiteboard
    /// falls back to a default title (<c>node {n}</c>); a manual title override entered
    /// in the side inspector always takes precedence over both.
    /// </summary>
    /// <remarks>
    /// Runtime-only marker (no serialized state); consumed exclusively by the editor
    /// tooling via reflection. Apply to a single field of the <c>TNode</c> type used by
    /// a <see cref="AceLand.Collection.Relational.Graph{TNode,TEdge}"/> (or any custom
    /// node payload). If several fields carry it, the first one found wins.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class NodeTitleAttribute : Attribute
    {
    }
}

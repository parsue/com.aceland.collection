using System;
using UnityEngine;

namespace AceLand.Collection
{
    /// <summary>
    /// Serializable item/priority pair used as the flat backing element for
    /// <see cref="IndexedPriorityQueue{TItem,TPriority}"/>.
    /// </summary>
    [Serializable]
    public struct IndexedPriorityEntry<TItem, TPriority>
    {
        [SerializeField] private TItem _item;
        [SerializeField] private TPriority _priority;

        internal IndexedPriorityEntry(TItem item, TPriority priority)
        {
            _item = item;
            _priority = priority;
        }

        /// <summary>The queued item (also its unique index key).</summary>
        public TItem Item => _item;

        /// <summary>The priority associated with <see cref="Item"/>.</summary>
        public TPriority Priority => _priority;
    }
}

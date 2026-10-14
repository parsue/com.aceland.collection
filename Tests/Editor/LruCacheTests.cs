using System.Linq;
using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class LruCacheTests
    {
        [Test]
        public void Create_Rejects_NonPositive_Capacity()
        {
            Assert.Throws<CollectionException>(() => LruCache<string, int>.Create(0));
            Assert.Throws<CollectionException>(() => LruCache<string, int>.Create(-1));
        }

        [Test]
        public void Empty_TryGet_And_TryPeek_Return_False()
        {
            var cache = LruCache<string, int>.Create(2);

            Assert.IsTrue(cache.IsEmpty);
            Assert.IsFalse(cache.TryGet("missing", out _));
            Assert.IsFalse(cache.TryPeek("missing", out _));
            Assert.IsFalse(cache.ContainsKey("missing"));
        }

        [Test]
        public void Set_And_Get_Roundtrip()
        {
            var cache = LruCache<string, int>.Create(3);
            cache.Set("a", 1);
            cache.Set("b", 2);

            Assert.IsTrue(cache.TryGet("a", out var a));
            Assert.IsTrue(cache.TryGet("b", out var b));
            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void Set_Duplicate_Key_Updates_Value_Not_Count()
        {
            var cache = LruCache<string, int>.Create(3);
            cache.Set("a", 1);
            cache.Set("a", 99);

            Assert.AreEqual(1, cache.Count);
            Assert.IsTrue(cache.TryPeek("a", out var v));
            Assert.AreEqual(99, v);
        }

        [Test]
        public void Exceeding_Capacity_Evicts_LeastRecentlyUsed()
        {
            var cache = LruCache<string, int>.Create(2);
            cache.Set("a", 1);
            cache.Set("b", 2);

            var evicted = cache.Set("c", 3, out var entry);
            Assert.IsTrue(evicted);
            Assert.AreEqual("a", entry.Key);
            Assert.AreEqual(1, entry.Value);
            Assert.IsFalse(cache.ContainsKey("a"));
            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void Get_Refreshes_Recency_Changing_Eviction_Victim()
        {
            var cache = LruCache<string, int>.Create(2);
            cache.Set("a", 1);
            cache.Set("b", 2);

            // Touch "a" so "b" becomes the least-recently used.
            Assert.IsTrue(cache.TryGet("a", out _));

            var evicted = cache.Set("c", 3, out var entry);
            Assert.IsTrue(evicted);
            Assert.AreEqual("b", entry.Key);
            Assert.IsTrue(cache.ContainsKey("a"));
            Assert.IsTrue(cache.ContainsKey("c"));
        }

        [Test]
        public void Peek_Does_Not_Change_Recency()
        {
            var cache = LruCache<string, int>.Create(2);
            cache.Set("a", 1);
            cache.Set("b", 2);

            // Peek "a" must NOT protect it from eviction.
            Assert.IsTrue(cache.TryPeek("a", out _));

            cache.Set("c", 3, out var entry);
            Assert.AreEqual("a", entry.Key);
        }

        [Test]
        public void Enumeration_Is_MostRecent_To_LeastRecent()
        {
            var cache = LruCache<string, int>.Create(3);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("c", 3);

            var keys = cache.Select(e => e.Key).ToArray();
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, keys);
        }

        [Test]
        public void Remove_And_Clear()
        {
            var cache = LruCache<string, int>.Create(3);
            cache.Set("a", 1);
            cache.Set("b", 2);

            Assert.IsTrue(cache.Remove("a"));
            Assert.IsFalse(cache.Remove("a"));
            Assert.AreEqual(1, cache.Count);

            cache.Clear();
            Assert.IsTrue(cache.IsEmpty);
        }
    }
}

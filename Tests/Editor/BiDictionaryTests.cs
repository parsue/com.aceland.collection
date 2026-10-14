using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class BiDictionaryTests
    {
        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() =>
                BiDictionary<int, string>.Create(null, null));
        }

        [Test]
        public void EmptyMap_HasNoEntries()
        {
            var map = BiDictionary<int, string>.Create();

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
            Assert.IsFalse(map.ContainsValue("a"));
        }

        [Test]
        public void Set_AddsBothDirections()
        {
            var map = BiDictionary<int, string>.Create();

            map.Set(1, "one");

            Assert.AreEqual(1, map.Count);
            Assert.IsTrue(map.TryGetByKey(1, out var value));
            Assert.AreEqual("one", value);
            Assert.IsTrue(map.TryGetByValue("one", out var key));
            Assert.AreEqual(1, key);
        }

        [Test]
        public void Set_ReplacingKey_KeepsBijection()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "one");

            map.Set(1, "uno");

            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.ContainsValue("one"));
            Assert.IsTrue(map.TryGetByKey(1, out var value));
            Assert.AreEqual("uno", value);
        }

        [Test]
        public void Set_ReusingValue_RemovesPriorKey()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "shared");

            map.Set(2, "shared");

            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
            Assert.IsTrue(map.TryGetByValue("shared", out var key));
            Assert.AreEqual(2, key);
        }

        [Test]
        public void TryAdd_ExistingKeyOrValue_ReturnsFalse()
        {
            var map = BiDictionary<int, string>.Create();
            Assert.IsTrue(map.TryAdd(1, "one"));

            Assert.IsFalse(map.TryAdd(1, "uno"));
            Assert.IsFalse(map.TryAdd(2, "one"));
            Assert.AreEqual(1, map.Count);
        }

        [Test]
        public void RemoveByKey_RemovesBothDirections()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "one");

            Assert.IsTrue(map.RemoveByKey(1));
            Assert.IsFalse(map.ContainsValue("one"));
            Assert.AreEqual(0, map.Count);
        }

        [Test]
        public void RemoveByValue_RemovesBothDirections()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "one");

            Assert.IsTrue(map.RemoveByValue("one"));
            Assert.IsFalse(map.ContainsKey(1));
            Assert.AreEqual(0, map.Count);
        }

        [Test]
        public void RemoveByKey_Absent_ReturnsFalse()
        {
            var map = BiDictionary<int, string>.Create();

            Assert.IsFalse(map.RemoveByKey(99));
        }

        [Test]
        public void Clear_EmptiesMap()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "one");
            map.Set(2, "two");

            map.Clear();

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
            Assert.IsFalse(map.ContainsValue("two"));
        }

        [Test]
        public void Set_SamePairTwice_IsIdempotent()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "one");

            map.Set(1, "one");

            Assert.AreEqual(1, map.Count);
            Assert.IsTrue(map.TryGetByKey(1, out var value));
            Assert.AreEqual("one", value);
            Assert.IsTrue(map.TryGetByValue("one", out var key));
            Assert.AreEqual(1, key);
        }

        [Test]
        public void BulkInsert_GrowsBeyondInitialCapacity_KeepsBothDirections()
        {
            // Start tiny so the table has to resize several times.
            var map = BiDictionary<int, int>.Create();
            const int n = 5000;

            for (var i = 0; i < n; i++)
                map.Set(i, i * 2);

            Assert.AreEqual(n, map.Count);
            for (var i = 0; i < n; i++)
            {
                Assert.IsTrue(map.TryGetByKey(i, out var v), $"key {i} missing");
                Assert.AreEqual(i * 2, v);
                Assert.IsTrue(map.TryGetByValue(i * 2, out var k), $"value {i * 2} missing");
                Assert.AreEqual(i, k);
            }
        }

        [Test]
        public void RemoveThenReAdd_ReusesFreeSlots_StaysConsistent()
        {
            var map = BiDictionary<int, int>.Create();
            const int n = 2000;
            for (var i = 0; i < n; i++)
                map.Set(i, i + 100000);

            // Remove every even key.
            for (var i = 0; i < n; i += 2)
                Assert.IsTrue(map.RemoveByKey(i));
            Assert.AreEqual(n / 2, map.Count);

            // Re-add fresh pairs; these should recycle the freed slots.
            for (var i = 0; i < n; i += 2)
                map.Set(i + 1000000, i + 2000000);

            Assert.AreEqual(n, map.Count);

            // Odd originals survive; even originals gone; new pairs present.
            for (var i = 1; i < n; i += 2)
            {
                Assert.IsTrue(map.TryGetByKey(i, out var v));
                Assert.AreEqual(i + 100000, v);
            }
            for (var i = 0; i < n; i += 2)
            {
                Assert.IsFalse(map.ContainsKey(i));
                Assert.IsFalse(map.ContainsValue(i + 100000));
                Assert.IsTrue(map.TryGetByValue(i + 2000000, out var k));
                Assert.AreEqual(i + 1000000, k);
            }
        }

        [Test]
        public void Set_ReusingValue_UnlinksOldValueChain()
        {
            var map = BiDictionary<int, string>.Create();
            map.Set(1, "a");
            map.Set(2, "b");

            // Rebind "b" to key 1; key 2 must drop out entirely.
            map.Set(1, "b");

            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.ContainsKey(2));
            Assert.IsFalse(map.ContainsValue("a"));
            Assert.IsTrue(map.TryGetByValue("b", out var key));
            Assert.AreEqual(1, key);
        }

        [Test]
        public void Enumerator_YieldsOnlyLiveEntries()
        {
            var map = BiDictionary<int, int>.Create();
            for (var i = 0; i < 10; i++)
                map.Set(i, i * 10);
            for (var i = 0; i < 10; i += 2)
                map.RemoveByKey(i);

            var seen = 0;
            foreach (var pair in map)
            {
                Assert.AreEqual(pair.Key * 10, pair.Value);
                Assert.AreEqual(1, pair.Key % 2);
                seen++;
            }

            Assert.AreEqual(5, seen);
            Assert.AreEqual(5, map.Count);
        }

        [Test]
        public void RestoreComparers_RebuildsBothIndexes()
        {
            var map = BiDictionary<string, int>.Create();
            map.Set("One", 1);
            map.Set("Two", 2);

            map.RestoreComparers(System.StringComparer.OrdinalIgnoreCase, null);

            // Case-insensitive lookups now resolve.
            Assert.IsTrue(map.TryGetByKey("ONE", out var v));
            Assert.AreEqual(1, v);
            Assert.IsTrue(map.TryGetByValue(2, out var k));
            Assert.AreEqual("Two", k);
            Assert.AreEqual(2, map.Count);
        }
    }
}

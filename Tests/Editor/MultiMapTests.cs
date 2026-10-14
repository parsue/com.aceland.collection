using AceLand.Collection;
using AceLand.Collection.Core;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class MultiMapTests
    {
        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() =>
                MultiMap<int, string>.Create(null, null));
        }

        [Test]
        public void EmptyMap_HasNoEntries()
        {
            var map = MultiMap<int, string>.Create();

            Assert.AreEqual(0, map.KeyCount);
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
            CollectionAssert.IsEmpty(map.GetValues(1));
        }

        [Test]
        public void Add_MultipleValuesUnderKey()
        {
            var map = MultiMap<int, string>.Create();

            map.Add(1, "a");
            map.Add(1, "b");

            Assert.AreEqual(1, map.KeyCount);
            Assert.AreEqual(2, map.Count);
            Assert.AreEqual(2, map.CountOf(1));
            CollectionAssert.AreEqual(new[] { "a", "b" }, map.GetValues(1));
        }

        [Test]
        public void Add_DuplicateAllowed_KeepsBoth()
        {
            var map = MultiMap<int, string>.Create(allowDuplicateValues: true);

            Assert.IsTrue(map.Add(1, "a"));
            Assert.IsTrue(map.Add(1, "a"));
            Assert.AreEqual(2, map.CountOf(1));
        }

        [Test]
        public void Add_DuplicateDisallowed_ReturnsFalse()
        {
            var map = MultiMap<int, string>.Create(allowDuplicateValues: false);

            Assert.IsTrue(map.Add(1, "a"));
            Assert.IsFalse(map.Add(1, "a"));
            Assert.AreEqual(1, map.CountOf(1));
        }

        [Test]
        public void Contains_ReflectsMembership()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");

            Assert.IsTrue(map.Contains(1, "a"));
            Assert.IsFalse(map.Contains(1, "b"));
            Assert.IsFalse(map.Contains(2, "a"));
        }

        [Test]
        public void Remove_SingleOccurrence_DropsEmptyKey()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");

            Assert.IsTrue(map.Remove(1, "a"));
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
        }

        [Test]
        public void Remove_Absent_ReturnsFalse()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");

            Assert.IsFalse(map.Remove(1, "b"));
            Assert.IsFalse(map.Remove(2, "a"));
        }

        [Test]
        public void RemoveKey_DropsAllValues()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");
            map.Add(1, "b");

            Assert.IsTrue(map.RemoveKey(1));
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(1));
        }

        [Test]
        public void TryGetValues_PresentAndAbsent()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");

            Assert.IsTrue(map.TryGetValues(1, out var values));
            CollectionAssert.AreEqual(new[] { "a" }, values);
            Assert.IsFalse(map.TryGetValues(2, out var missing));
            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void Clear_EmptiesMap()
        {
            var map = MultiMap<int, string>.Create();
            map.Add(1, "a");
            map.Add(2, "b");

            map.Clear();

            Assert.AreEqual(0, map.KeyCount);
            Assert.AreEqual(0, map.Count);
        }
    }
}

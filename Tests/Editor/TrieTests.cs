using System.Linq;
using AceLand.Collection.Search;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class TrieTests
    {
        [Test]
        public void Set_Rejects_Null_Or_Empty_Key()
        {
            var trie = Trie<int>.Create();

            Assert.IsFalse(trie.Set(null, 1));
            Assert.IsFalse(trie.Set(string.Empty, 1));
            Assert.AreEqual(0, trie.Count);
        }

        [Test]
        public void Set_And_Get_Roundtrip()
        {
            var trie = Trie<int>.Create();
            Assert.IsTrue(trie.Set("cat", 1));
            Assert.IsTrue(trie.Set("car", 2));

            Assert.IsTrue(trie.TryGet("cat", out var cat));
            Assert.IsTrue(trie.TryGet("car", out var car));
            Assert.AreEqual(1, cat);
            Assert.AreEqual(2, car);
            Assert.IsFalse(trie.TryGet("ca", out _));
            Assert.AreEqual(2, trie.Count);
        }

        [Test]
        public void Set_Duplicate_Key_Updates_Value_Without_Growing_Count()
        {
            var trie = Trie<int>.Create();
            trie.Set("dog", 1);
            trie.Set("dog", 99);

            Assert.AreEqual(1, trie.Count);
            Assert.IsTrue(trie.TryGet("dog", out var v));
            Assert.AreEqual(99, v);
        }

        [Test]
        public void ContainsPrefix_Detects_Partial_Paths()
        {
            var trie = Trie<int>.Create();
            trie.Set("hello", 1);

            Assert.IsTrue(trie.ContainsPrefix("hel"));
            Assert.IsTrue(trie.ContainsPrefix("hello"));
            Assert.IsFalse(trie.ContainsPrefix("world"));
            Assert.IsFalse(trie.ContainsKey("hel"));
        }

        [Test]
        public void StartsWith_Returns_All_Matching_Keys()
        {
            var trie = Trie<int>.Create();
            trie.Set("car", 1);
            trie.Set("cart", 2);
            trie.Set("care", 3);
            trie.Set("dog", 4);

            var keys = trie.StartsWith("car").Select(e => e.Key).OrderBy(k => k).ToArray();
            CollectionAssert.AreEqual(new[] { "car", "care", "cart" }, keys);

            var limited = trie.StartsWith("car", 2).Count();
            Assert.AreEqual(2, limited);
        }

        [Test]
        public void Remove_Deletes_Key_And_Prunes()
        {
            var trie = Trie<int>.Create();
            trie.Set("cat", 1);
            trie.Set("car", 2);

            Assert.IsTrue(trie.Remove("cat"));
            Assert.IsFalse(trie.TryGet("cat", out _));
            Assert.IsTrue(trie.TryGet("car", out _));
            Assert.AreEqual(1, trie.Count);

            Assert.IsFalse(trie.Remove("cat"));
            Assert.IsFalse(trie.Remove("missing"));
        }

        [Test]
        public void Clear_Removes_All()
        {
            var trie = Trie<int>.Create();
            trie.Set("a", 1);
            trie.Set("b", 2);

            trie.Clear();

            Assert.IsTrue(trie.IsEmpty);
            Assert.AreEqual(0, trie.Count);
        }
    }
}

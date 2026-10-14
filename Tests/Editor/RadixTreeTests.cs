using System.Linq;
using AceLand.Collection.Search;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class RadixTreeTests
    {
        [Test]
        public void Set_Rejects_Null_Or_Empty_Key()
        {
            var tree = RadixTree<int>.Create();

            Assert.IsFalse(tree.Set(null, 1));
            Assert.IsFalse(tree.Set(string.Empty, 1));
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Set_And_Get_With_Shared_Prefixes()
        {
            var tree = RadixTree<int>.Create();
            tree.Set("romane", 1);
            tree.Set("romanus", 2);
            tree.Set("romulus", 3);
            tree.Set("rubens", 4);

            Assert.AreEqual(4, tree.Count);
            Assert.IsTrue(tree.TryGet("romane", out var a));
            Assert.IsTrue(tree.TryGet("romanus", out var b));
            Assert.IsTrue(tree.TryGet("romulus", out var c));
            Assert.IsTrue(tree.TryGet("rubens", out var d));
            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual(3, c);
            Assert.AreEqual(4, d);

            Assert.IsFalse(tree.TryGet("roman", out _));
            Assert.IsFalse(tree.TryGet("rom", out _));
        }

        [Test]
        public void Set_Duplicate_Updates_Value_Not_Count()
        {
            var tree = RadixTree<int>.Create();
            tree.Set("test", 1);
            tree.Set("test", 42);

            Assert.AreEqual(1, tree.Count);
            Assert.IsTrue(tree.TryGet("test", out var v));
            Assert.AreEqual(42, v);
        }

        [Test]
        public void Edge_Split_Preserves_Existing_Key()
        {
            var tree = RadixTree<int>.Create();
            tree.Set("tester", 1);
            tree.Set("test", 2); // splits the "tester" edge

            Assert.AreEqual(2, tree.Count);
            Assert.IsTrue(tree.TryGet("tester", out var longer));
            Assert.IsTrue(tree.TryGet("test", out var shorter));
            Assert.AreEqual(1, longer);
            Assert.AreEqual(2, shorter);
        }

        [Test]
        public void ContainsPrefix_And_StartsWith()
        {
            var tree = RadixTree<int>.Create();
            tree.Set("apple", 1);
            tree.Set("application", 2);
            tree.Set("apply", 3);
            tree.Set("banana", 4);

            Assert.IsTrue(tree.ContainsPrefix("app"));
            Assert.IsFalse(tree.ContainsPrefix("xyz"));

            var keys = tree.StartsWith("app").Select(e => e.Key).OrderBy(k => k).ToArray();
            CollectionAssert.AreEqual(new[] { "apple", "application", "apply" }, keys);
        }

        [Test]
        public void Clear_Removes_All()
        {
            var tree = RadixTree<int>.Create();
            tree.Set("x", 1);
            tree.Set("y", 2);

            tree.Clear();

            Assert.IsTrue(tree.IsEmpty);
            Assert.AreEqual(0, tree.Count);
        }
    }
}

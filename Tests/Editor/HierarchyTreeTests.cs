using System.Linq;
using AceLand.Collection.Core;
using AceLand.Collection.Relational;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class HierarchyTreeTests
    {
        [Test]
        public void Create_NullComparer_Throws()
        {
            Assert.Throws<CollectionException>(() =>
                HierarchyTree<string>.Create(null));
        }

        [Test]
        public void EmptyTree_HasNoNodes()
        {
            var tree = HierarchyTree<string>.Create();

            Assert.AreEqual(0, tree.Count);
            CollectionAssert.IsEmpty(tree.Roots);
            Assert.IsFalse(tree.Contains("a"));
        }

        [Test]
        public void AddRoot_Duplicate_ReturnsFalse()
        {
            var tree = HierarchyTree<string>.Create();

            Assert.IsTrue(tree.AddRoot("root"));
            Assert.IsFalse(tree.AddRoot("root"));
            Assert.AreEqual(1, tree.Count);
            Assert.IsTrue(tree.IsRoot("root"));
        }

        [Test]
        public void AddChild_BuildsParentLink()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");

            Assert.IsTrue(tree.AddChild("root", "child"));
            Assert.IsTrue(tree.TryGetParent("child", out var parent));
            Assert.AreEqual("root", parent);
            CollectionAssert.AreEqual(new[] { "child" }, tree.GetChildren("root"));
            Assert.IsFalse(tree.IsRoot("child"));
        }

        [Test]
        public void AddChild_MissingParentOrExistingChild_ReturnsFalse()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "child");

            Assert.IsFalse(tree.AddChild("missing", "x"));
            Assert.IsFalse(tree.AddChild("root", "child"));
        }

        [Test]
        public void GetDepth_ReflectsLevel()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "child");
            tree.AddChild("child", "grandchild");

            Assert.AreEqual(0, tree.GetDepth("root"));
            Assert.AreEqual(1, tree.GetDepth("child"));
            Assert.AreEqual(2, tree.GetDepth("grandchild"));
            Assert.AreEqual(-1, tree.GetDepth("missing"));
        }

        [Test]
        public void Ancestors_FromChildToRoot()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "child");
            tree.AddChild("child", "grandchild");

            CollectionAssert.AreEqual(
                new[] { "child", "root" },
                tree.Ancestors("grandchild").ToArray());
        }

        [Test]
        public void Descendants_PreOrder()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("root", "b");
            tree.AddChild("a", "a1");

            CollectionAssert.AreEqual(
                new[] { "a", "a1", "b" },
                tree.Descendants("root").ToArray());
        }

        [Test]
        public void Reparent_MovesSubtree()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("root", "b");

            Assert.IsTrue(tree.Reparent("a", "b"));
            Assert.IsTrue(tree.TryGetParent("a", out var parent));
            Assert.AreEqual("b", parent);
            CollectionAssert.DoesNotContain(tree.GetChildren("root"), "a");
        }

        [Test]
        public void Reparent_Cycle_ReturnsFalse()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("a", "b");

            // Making 'a' a child of its own descendant 'b' must be rejected.
            Assert.IsFalse(tree.Reparent("a", "b"));
        }

        [Test]
        public void Remove_DropsSubtree()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("a", "a1");

            Assert.IsTrue(tree.Remove("a"));
            Assert.IsFalse(tree.Contains("a"));
            Assert.IsFalse(tree.Contains("a1"));
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void BreadthFirst_VisitsLevelByLevel()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("root", "b");
            tree.AddChild("a", "a1");

            CollectionAssert.AreEqual(
                new[] { "root", "a", "b", "a1" },
                tree.BreadthFirst().ToArray());
        }

        [Test]
        public void DepthFirst_FromSubtree()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");
            tree.AddChild("a", "a1");
            tree.AddChild("root", "b");

            CollectionAssert.AreEqual(
                new[] { "a", "a1" },
                tree.DepthFirst("a", true).ToArray());
        }

        [Test]
        public void Clear_EmptiesTree()
        {
            var tree = HierarchyTree<string>.Create();
            tree.AddRoot("root");
            tree.AddChild("root", "a");

            tree.Clear();

            Assert.AreEqual(0, tree.Count);
            CollectionAssert.IsEmpty(tree.Roots);
        }
    }
}

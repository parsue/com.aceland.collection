using AceLand.Collection.Core;
using AceLand.Collection.Relational;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class DisjointSetTests
    {
        [Test]
        public void Empty_Set_Has_No_Elements()
        {
            var ds = DisjointSet<int>.Create();
            Assert.AreEqual(0, ds.Count);
            Assert.AreEqual(0, ds.SetCount);
            Assert.IsFalse(ds.Contains(1));
        }

        [Test]
        public void Create_Custom_Comparer_Rejects_Null()
        {
            Assert.Throws<CollectionException>(() => DisjointSet<int>.Create(null));
        }

        [Test]
        public void MakeSet_Is_Idempotent()
        {
            var ds = DisjointSet<int>.Create();
            Assert.IsTrue(ds.MakeSet(1));
            Assert.IsFalse(ds.MakeSet(1));
            Assert.AreEqual(1, ds.Count);
            Assert.AreEqual(1, ds.SetCount);
        }

        [Test]
        public void Union_Merges_Sets_And_Reduces_SetCount()
        {
            var ds = DisjointSet<int>.Create();
            ds.MakeSet(1);
            ds.MakeSet(2);
            ds.MakeSet(3);
            Assert.AreEqual(3, ds.SetCount);

            Assert.IsTrue(ds.Union(1, 2));
            Assert.AreEqual(2, ds.SetCount);
            Assert.IsTrue(ds.Connected(1, 2));
            Assert.IsFalse(ds.Connected(1, 3));
        }

        [Test]
        public void Union_Already_Connected_Returns_False()
        {
            var ds = DisjointSet<int>.Create();
            ds.Union(1, 2);
            Assert.IsFalse(ds.Union(2, 1));
            Assert.AreEqual(1, ds.SetCount);
        }

        [Test]
        public void Union_Adds_Absent_Elements_On_Demand()
        {
            var ds = DisjointSet<int>.Create();
            Assert.IsTrue(ds.Union(1, 2));
            Assert.IsTrue(ds.Contains(1));
            Assert.IsTrue(ds.Contains(2));
            Assert.AreEqual(2, ds.Count);
        }

        [Test]
        public void Find_Returns_Same_Representative_For_Connected()
        {
            var ds = DisjointSet<int>.Create();
            ds.Union(1, 2);
            ds.Union(2, 3);

            Assert.IsTrue(ds.Find(1, out var r1));
            Assert.IsTrue(ds.Find(3, out var r3));
            Assert.AreEqual(r1, r3);
        }

        [Test]
        public void Connected_Is_Transitive()
        {
            var ds = DisjointSet<int>.Create();
            ds.Union(1, 2);
            ds.Union(3, 4);
            Assert.IsFalse(ds.Connected(1, 3));

            ds.Union(2, 3);
            Assert.IsTrue(ds.Connected(1, 4));
            Assert.AreEqual(1, ds.SetCount);
        }

        [Test]
        public void SizeOf_Reflects_Component_Size()
        {
            var ds = DisjointSet<int>.Create();
            ds.Union(1, 2);
            ds.Union(2, 3);
            ds.MakeSet(9);

            Assert.AreEqual(3, ds.SizeOf(1));
            Assert.AreEqual(3, ds.SizeOf(3));
            Assert.AreEqual(1, ds.SizeOf(9));
            Assert.AreEqual(0, ds.SizeOf(42));
        }

        [Test]
        public void Clear_Empties_Structure()
        {
            var ds = DisjointSet<int>.Create();
            ds.Union(1, 2);
            ds.Clear();
            Assert.AreEqual(0, ds.Count);
            Assert.AreEqual(0, ds.SetCount);
            Assert.IsFalse(ds.Contains(1));
        }
    }
}

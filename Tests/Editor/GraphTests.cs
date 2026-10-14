using System.Collections.Generic;
using System.Linq;
using AceLand.Collection.Core;
using AceLand.Collection.Relational;
using NUnit.Framework;

namespace AceLand.Collection.Tests
{
    public sealed class GraphTests
    {
        [Test]
        public void Empty_Graph_Has_No_Nodes_Or_Edges()
        {
            var g = Graph<int, int>.CreateDirected();
            Assert.AreEqual(0, g.NodeCount);
            Assert.AreEqual(0, g.EdgeCount);
            Assert.IsFalse(g.ContainsNode(1));
        }

        [Test]
        public void CreateDirected_Custom_Comparer_Rejects_Null()
        {
            Assert.Throws<CollectionException>(() => Graph<int, int>.CreateDirected(null));
        }

        [Test]
        public void AddNode_Is_Idempotent()
        {
            var g = Graph<int, int>.CreateDirected();
            Assert.IsTrue(g.AddNode(1));
            Assert.IsFalse(g.AddNode(1));
            Assert.AreEqual(1, g.NodeCount);
        }

        [Test]
        public void AddEdge_Creates_Missing_Endpoints_Directed()
        {
            var g = Graph<int, int>.CreateDirected();
            Assert.IsTrue(g.AddEdge(1, 2, 10));

            Assert.AreEqual(2, g.NodeCount);
            Assert.AreEqual(1, g.EdgeCount);
            Assert.IsTrue(g.ContainsEdge(1, 2));
            Assert.IsFalse(g.ContainsEdge(2, 1));

            Assert.IsTrue(g.TryGetEdge(1, 2, out var w));
            Assert.AreEqual(10, w);
        }

        [Test]
        public void AddEdge_Undirected_Is_Reciprocal()
        {
            var g = Graph<int, int>.CreateUndirected();
            g.AddEdge(1, 2, 5);

            Assert.IsTrue(g.ContainsEdge(1, 2));
            Assert.IsTrue(g.ContainsEdge(2, 1));
            Assert.AreEqual(1, g.EdgeCount);
            Assert.AreEqual(1, g.GetDegree(1));
            Assert.AreEqual(1, g.GetDegree(2));
        }

        [Test]
        public void AddEdge_Existing_Updates_Payload_Not_Count()
        {
            var g = Graph<int, int>.CreateDirected();
            Assert.IsTrue(g.AddEdge(1, 2, 10));
            Assert.IsFalse(g.AddEdge(1, 2, 20));

            Assert.AreEqual(1, g.EdgeCount);
            Assert.IsTrue(g.TryGetEdge(1, 2, out var w));
            Assert.AreEqual(20, w);
        }

        [Test]
        public void RemoveEdge_Directed_And_Undirected()
        {
            var d = Graph<int, int>.CreateDirected();
            d.AddEdge(1, 2);
            Assert.IsTrue(d.RemoveEdge(1, 2));
            Assert.AreEqual(0, d.EdgeCount);
            Assert.IsFalse(d.RemoveEdge(1, 2));

            var u = Graph<int, int>.CreateUndirected();
            u.AddEdge(1, 2);
            Assert.IsTrue(u.RemoveEdge(2, 1));
            Assert.IsFalse(u.ContainsEdge(1, 2));
            Assert.AreEqual(0, u.EdgeCount);
        }

        [Test]
        public void RemoveNode_Purges_Incident_Edges()
        {
            var g = Graph<int, int>.CreateDirected();
            g.AddEdge(1, 2);
            g.AddEdge(2, 3);
            g.AddEdge(3, 2);

            Assert.IsTrue(g.RemoveNode(2));
            Assert.IsFalse(g.ContainsNode(2));
            Assert.IsFalse(g.ContainsEdge(1, 2));
            Assert.IsFalse(g.ContainsEdge(3, 2));
            Assert.AreEqual(0, g.EdgeCount);
        }

        [Test]
        public void BreadthFirst_Visits_Reachable_In_Bfs_Order()
        {
            var g = Graph<int, int>.CreateDirected();
            g.AddEdge(1, 2);
            g.AddEdge(1, 3);
            g.AddEdge(2, 4);
            g.AddEdge(3, 4);

            var visited = g.BreadthFirst(1).ToList();
            Assert.AreEqual(1, visited[0]);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, visited);
            Assert.AreEqual(4, visited.Count);
        }

        [Test]
        public void DepthFirst_Visits_All_Reachable_Once()
        {
            var g = Graph<int, int>.CreateUndirected();
            g.AddEdge(1, 2);
            g.AddEdge(2, 3);
            g.AddEdge(3, 1); // cycle

            var visited = g.DepthFirst(1).ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, visited);
            Assert.AreEqual(3, visited.Count);
        }

        [Test]
        public void Traversal_From_Absent_Node_Is_Empty()
        {
            var g = Graph<int, int>.CreateDirected();
            g.AddNode(1);
            Assert.IsEmpty(g.BreadthFirst(99).ToList());
            Assert.IsEmpty(g.DepthFirst(99).ToList());
        }

        [Test]
        public void Clear_Empties_Graph()
        {
            var g = Graph<int, int>.CreateDirected();
            g.AddEdge(1, 2);
            g.Clear();
            Assert.AreEqual(0, g.NodeCount);
            Assert.AreEqual(0, g.EdgeCount);
        }
    }
}

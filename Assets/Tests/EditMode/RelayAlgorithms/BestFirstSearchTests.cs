using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Search;

namespace Skybreak.AI.Algorithms.Tests
{
    public class BestFirstSearchTests
    {
        // ------------------------------------------------------------ Lecture 05: Romania, Arad -> Bucharest
        sealed class Romania : ISearchProblem<string>
        {
            static readonly (string a, string b, int km)[] Roads =
            {
                ("Arad", "Zerind", 75), ("Arad", "Sibiu", 140), ("Arad", "Timisoara", 118), ("Zerind", "Oradea", 71),
                ("Oradea", "Sibiu", 151), ("Timisoara", "Lugoj", 111), ("Lugoj", "Mehadia", 70), ("Mehadia", "Drobeta", 75),
                ("Drobeta", "Craiova", 120), ("Craiova", "Rimnicu Vilcea", 146), ("Craiova", "Pitesti", 138),
                ("Sibiu", "Fagaras", 99), ("Sibiu", "Rimnicu Vilcea", 80), ("Rimnicu Vilcea", "Pitesti", 97),
                ("Fagaras", "Bucharest", 211), ("Pitesti", "Bucharest", 101), ("Bucharest", "Giurgiu", 90),
                ("Bucharest", "Urziceni", 85), ("Urziceni", "Hirsova", 98), ("Hirsova", "Eforie", 86),
                ("Urziceni", "Vaslui", 142), ("Vaslui", "Iasi", 92), ("Iasi", "Neamt", 87),
            };
            // h_SLD: straight-line distance to Bucharest (admissible: never more than the road distance)
            static readonly Dictionary<string, int> StraightLineToBucharest = new Dictionary<string, int>
            {
                { "Arad", 366 }, { "Bucharest", 0 }, { "Craiova", 160 }, { "Drobeta", 242 }, { "Eforie", 161 }, { "Fagaras", 176 },
                { "Giurgiu", 77 }, { "Hirsova", 151 }, { "Iasi", 226 }, { "Lugoj", 244 }, { "Mehadia", 241 }, { "Neamt", 234 },
                { "Oradea", 380 }, { "Pitesti", 100 }, { "Rimnicu Vilcea", 193 }, { "Sibiu", 253 }, { "Timisoara", 329 },
                { "Urziceni", 80 }, { "Vaslui", 199 }, { "Zerind", 374 },
            };
            public IEnumerable<(string next, double cost)> Successors(string node)
            {
                foreach (var r in Roads)
                {
                    if (r.a == node) yield return (r.b, r.km);
                    else if (r.b == node) yield return (r.a, r.km);
                }
            }
            public double Heuristic(string node, string goal) => goal == "Bucharest" ? StraightLineToBucharest[node] : 0;
        }

        [Test]
        public void AStar_Romania_FindsOptimal418kmRouteViaRimnicuAndPitesti()
        {
            var r = BestFirstSearch.Run(new Romania(), "Arad", "Bucharest", SearchStrategy.AStar);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(418, r.Cost, 1e-9);
            CollectionAssert.AreEqual(new[] { "Arad", "Sibiu", "Rimnicu Vilcea", "Pitesti", "Bucharest" }, r.Path);
        }

        [Test]
        public void Greedy_Romania_TakesShorterLookingButCostlier450kmRouteViaFagaras()
        {
            var r = BestFirstSearch.Run(new Romania(), "Arad", "Bucharest", SearchStrategy.Greedy);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(450, r.Cost, 1e-9);   // not optimal (Lecture 05: "Optimal? No")
            CollectionAssert.AreEqual(new[] { "Arad", "Sibiu", "Fagaras", "Bucharest" }, r.Path);
        }

        [Test]
        public void UniformCost_Romania_SameOptimalCostButExpandsMoreNodesThanAStar()
        {
            var ucs = BestFirstSearch.Run(new Romania(), "Arad", "Bucharest", SearchStrategy.UniformCost);
            var astar = BestFirstSearch.Run(new Romania(), "Arad", "Bucharest", SearchStrategy.AStar);
            Assert.AreEqual(418, ucs.Cost, 1e-9);
            Assert.Less(astar.Expanded, ucs.Expanded, "the heuristic should focus the search");
            TestContext.WriteLine($"Expanded: A* {astar.Expanded}, UCS {ucs.Expanded}");
        }

        [Test]
        public void Romania_StraightLineHeuristic_IsConsistent()
        {
            // h(n) <= c(n, n') + h(n') for every road, so A* graph search is optimal (Lecture 05)
            var p = new Romania();
            foreach (var city in new[] { "Arad", "Zerind", "Oradea", "Sibiu", "Timisoara", "Lugoj", "Mehadia", "Drobeta", "Craiova",
                                         "Rimnicu Vilcea", "Pitesti", "Fagaras", "Bucharest", "Giurgiu", "Urziceni", "Hirsova", "Eforie", "Vaslui", "Iasi", "Neamt" })
                foreach (var (next, cost) in p.Successors(city))
                    Assert.LessOrEqual(p.Heuristic(city, "Bucharest"), cost + p.Heuristic(next, "Bucharest"), $"{city} -> {next}");
        }

        // ------------------------------------------------------------ Tactical grid: cost = length + lambda * exposure
        sealed class DangerGrid : ISearchProblem<(int x, int y)>
        {
            public readonly int W, H; public readonly bool[,] Wall; public readonly double[,] Exposure; public double Lambda = 3;
            public DangerGrid(int w, int h) { W = w; H = h; Wall = new bool[w, h]; Exposure = new double[w, h]; }
            public IEnumerable<((int x, int y) next, double cost)> Successors((int x, int y) n)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int x = n.x + dx, y = n.y + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H || Wall[x, y]) continue;
                        if (dx != 0 && dy != 0 && (Wall[n.x + dx, n.y] || Wall[n.x, n.y + dy])) continue;   // no corner cutting
                        double length = Math.Sqrt(dx * dx + dy * dy);
                        yield return ((x, y), length + Lambda * Exposure[x, y]);
                    }
            }
            // Straight-line distance: admissible because every step costs at least its length (exposure >= 0)
            public double Heuristic((int x, int y) n, (int x, int y) goal) => Math.Sqrt((n.x - goal.x) * (n.x - goal.x) + (n.y - goal.y) * (n.y - goal.y));
        }

        [Test]
        public void AStar_MatchesUniformCostOptimum_OnRandomDangerGrids()
        {
            var rng = new Random(3062);
            for (int trial = 0; trial < 60; trial++)
            {
                var g = new DangerGrid(18, 14);
                for (int x = 0; x < g.W; x++)
                    for (int y = 0; y < g.H; y++)
                    {
                        g.Wall[x, y] = rng.NextDouble() < .22;
                        g.Exposure[x, y] = rng.NextDouble() < .3 ? rng.NextDouble() : 0;
                    }
                g.Wall[0, 0] = g.Wall[17, 13] = false;
                var astar = BestFirstSearch.Run(g, (0, 0), (17, 13), SearchStrategy.AStar);
                var ucs = BestFirstSearch.Run(g, (0, 0), (17, 13), SearchStrategy.UniformCost);
                Assert.AreEqual(ucs.Found, astar.Found, $"trial {trial}");
                if (astar.Found) { Assert.AreEqual(ucs.Cost, astar.Cost, 1e-9, $"trial {trial}"); Assert.LessOrEqual(astar.Expanded, ucs.Expanded, $"trial {trial}"); }
            }
        }

        [Test]
        public void TacticalCost_DetoursAroundExposedCorridor()
        {
            // A 9x5 room: the straight middle row is watched by a threat, the top row is covered but longer.
            var g = new DangerGrid(9, 5);
            for (int x = 1; x < 8; x++) g.Exposure[x, 2] = 1;          // exposed straight line y = 2
            for (int x = 1; x < 8; x++) { g.Exposure[x, 1] = 1; g.Exposure[x, 3] = 1; }
            var safe = BestFirstSearch.Run(g, (0, 2), (8, 2), SearchStrategy.AStar);
            Assert.IsTrue(safe.Found);
            Assert.IsTrue(safe.Path.All(p => g.Exposure[p.Item1, p.Item2] == 0), "route should stay in cover");
            g.Lambda = 0;                                                // pure shortest path ignores danger
            var shortest = BestFirstSearch.Run(g, (0, 2), (8, 2), SearchStrategy.AStar);
            Assert.AreEqual(8, shortest.Cost, 1e-9);
            Assert.Greater(safe.Path.Count, shortest.Path.Count);
        }

        [Test]
        public void Unreachable_ReportsNotFound_AndPathToClosestExploredNode()
        {
            var g = new DangerGrid(6, 3);
            for (int y = 0; y < 3; y++) g.Wall[3, y] = true;             // a full wall splits the room
            var r = BestFirstSearch.Run(g, (0, 1), (5, 1), SearchStrategy.AStar);
            Assert.IsFalse(r.Found);
            Assert.AreEqual(2, r.Path.Last().Item1, "closest reachable column is just before the wall");
        }

        [Test]
        public void ExpansionLimit_StopsTheSearch()
        {
            var g = new DangerGrid(40, 40);
            var r = BestFirstSearch.Run(g, (0, 0), (39, 39), SearchStrategy.UniformCost, maxExpansions: 50);
            Assert.IsFalse(r.Found);
            Assert.IsTrue(r.HitExpansionLimit);
            Assert.AreEqual(50, r.Expanded);
        }

        [Test]
        public void MinHeap_PopsInPriorityOrder_TiesInInsertionOrder()
        {
            var h = new MinHeap<string>();
            h.Push("c", 3); h.Push("a1", 1); h.Push("b", 2); h.Push("a2", 1);
            var order = new List<string>();
            while (h.Count > 0) order.Add(h.Pop(out _));
            CollectionAssert.AreEqual(new[] { "a1", "a2", "b", "c" }, order);
        }
    }
}

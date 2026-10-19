using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Search
{
    // A state space for search (Lecture 04 "Formalizing a Search Problem"): successors with step costs,
    // and a heuristic h(n) estimating the remaining cost to the goal (Lecture 05).
    public interface ISearchProblem<TNode>
    {
        IEnumerable<(TNode next, double cost)> Successors(TNode node);
        double Heuristic(TNode node, TNode goal);
    }

    // The three best-first strategies differ only in the evaluation function f(n):
    //   UniformCost  f = g            (Lecture 04; uninformed, optimal)
    //   Greedy       f = h            (Lecture 05; fast, neither complete in trees nor optimal)
    //   AStar        f = g + h        (Lecture 05; optimal with an admissible/consistent h)
    public enum SearchStrategy { AStar, UniformCost, Greedy }

    public sealed class SearchResult<TNode>
    {
        public bool Found { get; internal set; }
        public bool HitExpansionLimit { get; internal set; }
        public List<TNode> Path { get; } = new List<TNode>();   // start .. goal (or .. closest node when not found)
        public double Cost { get; internal set; }                // g of the returned path
        public int Expanded { get; internal set; }               // nodes popped and expanded
        public int Generated { get; internal set; }              // successors pushed onto the frontier
        public HashSet<TNode> Closed { get; internal set; }      // explored set (for visualising the search)
    }

    public static class BestFirstSearch
    {
        // Graph search: a node is expanded at most once (closed set). With a consistent heuristic the first
        // time A* pops a node its g is optimal, so closed nodes never need reopening (Lecture 05 theorem).
        // A node is goal-tested when it is popped, not when it is generated: testing on generation can return a
        // more expensive path (the classic UCS/A* pitfall).
        public static SearchResult<TNode> Run<TNode>(ISearchProblem<TNode> problem, TNode start, TNode goal,
            SearchStrategy strategy = SearchStrategy.AStar, int maxExpansions = int.MaxValue, IEqualityComparer<TNode> comparer = null)
        {
            comparer = comparer ?? EqualityComparer<TNode>.Default;
            var result = new SearchResult<TNode> { Closed = new HashSet<TNode>(comparer) };
            var g = new Dictionary<TNode, double>(comparer) { [start] = 0 };
            var parent = new Dictionary<TNode, TNode>(comparer);
            var frontier = new MinHeap<TNode>();
            frontier.Push(start, F(strategy, 0, problem.Heuristic(start, goal)));

            TNode closest = start; double closestH = problem.Heuristic(start, goal);
            while (frontier.Count > 0)
            {
                var node = frontier.Pop(out _);
                if (result.Closed.Contains(node)) continue;                 // stale entry: a cheaper copy was expanded already
                if (comparer.Equals(node, goal)) { result.Found = true; Build(result, parent, g, start, node, comparer); return result; }
                if (result.Expanded >= maxExpansions) { result.HitExpansionLimit = true; break; }
                result.Closed.Add(node); result.Expanded++;
                double h = problem.Heuristic(node, goal);
                if (h < closestH) { closestH = h; closest = node; }

                foreach (var (next, cost) in problem.Successors(node))
                {
                    if (cost < 0) throw new InvalidOperationException("Negative step costs are not allowed");
                    if (result.Closed.Contains(next)) continue;
                    double newG = g[node] + cost;
                    if (g.TryGetValue(next, out double oldG) && oldG <= newG) continue;   // already reached more cheaply
                    g[next] = newG; parent[next] = node;
                    frontier.Push(next, F(strategy, newG, problem.Heuristic(next, goal)));
                    result.Generated++;
                }
            }
            // No path (or out of budget): return the route to the explored node closest to the goal.
            Build(result, parent, g, start, closest, comparer);
            return result;
        }

        static double F(SearchStrategy s, double g, double h) =>
            s == SearchStrategy.AStar ? g + h : s == SearchStrategy.UniformCost ? g : h;

        static void Build<TNode>(SearchResult<TNode> r, Dictionary<TNode, TNode> parent, Dictionary<TNode, double> g, TNode start, TNode end, IEqualityComparer<TNode> comparer)
        {
            r.Cost = g[end];
            var node = end;
            r.Path.Add(node);
            while (!comparer.Equals(node, start)) { node = parent[node]; r.Path.Add(node); }
            r.Path.Reverse();
        }
    }
}

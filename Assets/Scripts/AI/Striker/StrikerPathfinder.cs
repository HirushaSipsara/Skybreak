using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.Striker
{
    /// <summary>
    /// Pure C# pathfinding algorithms for the Striker autonomous agent.
    /// Implements:
    /// 1. Informed Search: A* algorithm with Manhattan heuristic for goal-directed patrol and pursuit.
    /// 2. Uninformed Search: Breadth-First Search (BFS) for systematic area sweep around lost target locations.
    /// </summary>
    public static class StrikerPathfinder
    {
        /// <summary>
        /// Internal node structure for A* priority evaluation.
        /// </summary>
        private sealed class AStarNode : IComparable<AStarNode>
        {
            public readonly Vector2Int Position;
            public float GCost; // Exact cost from start to this node
            public float HCost; // Heuristic estimate from this node to goal
            public float FCost => GCost + HCost; // Total evaluation function f(n) = g(n) + h(n)
            public AStarNode Parent;

            public AStarNode(Vector2Int position, float gCost, float hCost, AStarNode parent = null)
            {
                Position = position;
                GCost = gCost;
                HCost = hCost;
                Parent = parent;
            }

            public int CompareTo(AStarNode other)
            {
                if (other == null) return 1;
                int compare = FCost.CompareTo(other.FCost);
                if (compare == 0)
                {
                    // Tie-breaker: prefer node closer to goal (lower hCost)
                    return HCost.CompareTo(other.HCost);
                }
                return compare;
            }
        }

        /// <summary>
        /// Minimal binary min-heap for O(log N) priority queue operations during A* search.
        /// </summary>
        private sealed class MinHeap<T> where T : IComparable<T>
        {
            readonly List<T> items = new List<T>(64);

            public int Count => items.Count;

            public void Push(T item)
            {
                items.Add(item);
                SiftUp(items.Count - 1);
            }

            public T Pop()
            {
                if (items.Count == 0) throw new InvalidOperationException("Heap is empty.");
                T root = items[0];
                int lastIndex = items.Count - 1;
                items[0] = items[lastIndex];
                items.RemoveAt(lastIndex);
                if (items.Count > 0)
                {
                    SiftDown(0);
                }
                return root;
            }

            public void Clear() => items.Clear();

            void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (items[index].CompareTo(items[parent]) < 0)
                    {
                        Swap(index, parent);
                        index = parent;
                    }
                    else break;
                }
            }

            void SiftDown(int index)
            {
                int count = items.Count;
                while (true)
                {
                    int left = 2 * index + 1;
                    int right = 2 * index + 2;
                    int smallest = index;

                    if (left < count && items[left].CompareTo(items[smallest]) < 0)
                        smallest = left;
                    if (right < count && items[right].CompareTo(items[smallest]) < 0)
                        smallest = right;

                    if (smallest != index)
                    {
                        Swap(index, smallest);
                        index = smallest;
                    }
                    else break;
                }
            }

            void Swap(int a, int b)
            {
                T temp = items[a];
                items[a] = items[b];
                items[b] = temp;
            }
        }

        #region Informed Search: A* Algorithm

        /// <summary>
        /// Computes the Manhattan distance between two grid cells:
        /// h(n) = |goal.x - n.x| + |goal.y - n.y|
        /// 
        /// Proof of Admissibility:
        /// On a 4-connected cardinal grid where each step has unit cost c = 1.0, any valid path
        /// between (x1, y1) and (x2, y2) requires at least |x2 - x1| horizontal steps and
        /// |y2 - y1| vertical steps. Thus, h(n) <= h*(n) (the true optimal cost). It never overestimates.
        /// 
        /// Proof of Consistency (Monotonicity):
        /// For any neighbor n' of n, |h(n) - h(n')| <= 1. Since step cost c(n, n') = 1,
        /// h(n) <= c(n, n') + h(n') holds unconditionally.
        /// </summary>
        public static float ManhattanHeuristic(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        }

        /// <summary>
        /// Finds the shortest optimal path between start and goal cells using A* informed search.
        /// </summary>
        /// <param name="grid">The grid containing traversability data.</param>
        /// <param name="start">Origin cell coordinate.</param>
        /// <param name="goal">Destination cell coordinate.</param>
        /// <returns>Sequence of cells from start to goal inclusive, or null if no valid route exists.</returns>
        public static List<Vector2Int> FindAStarPath(StrikerGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (grid == null) return null;
            if (!grid.InBounds(start) || !grid.InBounds(goal)) return null;

            // If start or goal is impassable, search is impossible
            if (!grid.IsPassable(start) || !grid.IsPassable(goal)) return null;

            if (start == goal)
            {
                return new List<Vector2Int> { start };
            }

            MinHeap<AStarNode> openSet = new MinHeap<AStarNode>();
            Dictionary<Vector2Int, AStarNode> openSetLookup = new Dictionary<Vector2Int, AStarNode>(128);
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
            List<Vector2Int> neighborsBuffer = new List<Vector2Int>(4);

            AStarNode startNode = new AStarNode(start, 0f, ManhattanHeuristic(start, goal));
            openSet.Push(startNode);
            openSetLookup[start] = startNode;

            while (openSet.Count > 0)
            {
                AStarNode current = openSet.Pop();
                openSetLookup.Remove(current.Position);

                // Goal reached
                if (current.Position == goal)
                {
                    return ReconstructPath(current);
                }

                closedSet.Add(current.Position);

                // Query 4 cardinal neighbors (North, South, East, West)
                grid.GetCardinalNeighbors(current.Position, neighborsBuffer);
                for (int i = 0; i < neighborsBuffer.Count; i++)
                {
                    Vector2Int neighborPos = neighborsBuffer[i];
                    if (closedSet.Contains(neighborPos)) continue;

                    float tentativeGCost = current.GCost + 1.0f; // Unit cardinal cost

                    if (openSetLookup.TryGetValue(neighborPos, out AStarNode existingNode))
                    {
                        if (tentativeGCost < existingNode.GCost)
                        {
                            existingNode.GCost = tentativeGCost;
                            existingNode.Parent = current;
                            // Re-pushing to refresh priority ordering
                            openSet.Push(new AStarNode(neighborPos, tentativeGCost, existingNode.HCost, current));
                        }
                    }
                    else
                    {
                        float hCost = ManhattanHeuristic(neighborPos, goal);
                        AStarNode neighborNode = new AStarNode(neighborPos, tentativeGCost, hCost, current);
                        openSet.Push(neighborNode);
                        openSetLookup[neighborPos] = neighborNode;
                    }
                }
            }

            // Exhausted reachable search space without reaching goal
            return null;
        }

        static List<Vector2Int> ReconstructPath(AStarNode endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>(32);
            AStarNode curr = endNode;
            while (curr != null)
            {
                path.Add(curr.Position);
                curr = curr.Parent;
            }
            path.Reverse();
            return path;
        }

        #endregion

        #region Uninformed Search: Breadth-First Search (BFS Area Sweep)

        /// <summary>
        /// Systematic area search using Breadth-First Search (BFS) around a focal point (e.g. last known player location).
        /// 
        /// Theoretical Justification (IS Viva):
        /// When Arc breaks line of sight behind an obstacle, the agent enters an uninformed state: there is no target
        /// coordinate to guide an informed heuristic like A*. Using Breadth-First Search ensures the agent explores
        /// outward in concentric rings (shallowest nodes first), guaranteeing that immediate hiding spots and corners
        /// are methodically cleared before searching farther away.
        /// </summary>
        /// <param name="grid">The spatial grid.</param>
        /// <param name="originCell">The center of the sweep search (last known target cell).</param>
        /// <param name="maxRadius">Maximum Manhattan distance to sweep outward.</param>
        /// <param name="maxCells">Maximum number of cells to include in the sweep route.</param>
        /// <returns>Ordered sequence of reachable cells forming an exhaustive sweep pattern.</returns>
        public static List<Vector2Int> BreadthFirstSearchSweep(StrikerGrid grid, Vector2Int originCell, int maxRadius = 4, int maxCells = 16)
        {
            List<Vector2Int> sweepRoute = new List<Vector2Int>(maxCells);
            if (grid == null) return sweepRoute;

            if (!grid.InBounds(originCell))
            {
                originCell = new Vector2Int(
                    Mathf.Clamp(originCell.x, 0, grid.Columns - 1),
                    Mathf.Clamp(originCell.y, 0, grid.Rows - 1)
                );
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>(32);
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            List<Vector2Int> neighbors = new List<Vector2Int>(4);

            queue.Enqueue(originCell);
            visited.Add(originCell);

            while (queue.Count > 0 && sweepRoute.Count < maxCells)
            {
                Vector2Int current = queue.Dequeue();

                // Add to sweep route if passable and not the root starting cell
                if (current != originCell && grid.IsPassable(current))
                {
                    sweepRoute.Add(current);
                }

                grid.GetCardinalNeighbors(current, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector2Int neighbor = neighbors[i];
                    if (visited.Contains(neighbor)) continue;

                    // Bound search depth by Manhattan radius
                    float dist = ManhattanHeuristic(originCell, neighbor);
                    if (dist <= maxRadius)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return sweepRoute;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Converts a sequence of grid coordinates into 3D world waypoints at cell centers.
        /// </summary>
        public static List<Vector3> ConvertToWorldWaypoints(StrikerGrid grid, List<Vector2Int> cellPath, float elevationOffset = 0f)
        {
            if (grid == null || cellPath == null) return new List<Vector3>();

            List<Vector3> worldPoints = new List<Vector3>(cellPath.Count);
            for (int i = 0; i < cellPath.Count; i++)
            {
                worldPoints.Add(grid.CellToWorld(cellPath[i], elevationOffset));
            }
            return worldPoints;
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// Provides A* (informed) and BFS (uninformed) search over a <see cref="VaultHoundGrid"/>.
    ///
    /// IS justification:
    ///   A* — used for direct goal-directed navigation (pursuer hound tracking player,
    ///         hound leaving chamber, searching last sighting). The Manhattan heuristic
    ///         h(n) = |Δx| + |Δz| is admissible and consistent on a 4-connected cardinal
    ///         grid with unit step cost, so A* finds the optimal path and never re-opens
    ///         a closed node.
    ///
    ///   BFS — used by the flanking hound to discover an alternate reachable route that
    ///         avoids the direct corridor already taken by its partner. BFS explores
    ///         in concentric rings from a seed cell without any heuristic, which is
    ///         correct when the flanker lacks information about which directions are
    ///         open relative to the partner's path.
    ///
    /// Admissibility proof:
    ///   In a 4-connected grid with unit cardinal step cost 1, any path between
    ///   (x1,z1) and (x2,z2) requires at least |x2-x1| + |z2-z1| steps.
    ///   Obstacles can only increase this. Therefore h(n) ≤ h*(n) for all n.
    ///
    /// Consistency proof:
    ///   For any node n and cardinal neighbour n', |h(n) - h(n')| ≤ 1 = c(n,n').
    ///   The triangle inequality h(n) ≤ c(n,n') + h(n') holds, so h is consistent.
    ///   Consistent ⟹ admissible; first expansion of each node is optimal.
    /// </summary>
    public static class VaultHoundPathfinder
    {
        // ── A* ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs A* on <paramref name="grid"/> from <paramref name="start"/> to
        /// <paramref name="goal"/>. Returns a world-space waypoint list (cell centres)
        /// or an empty list when no path exists.
        /// </summary>
        public static List<Vector3> FindPath(VaultHoundGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (!grid.IsPassable(start.x, start.y) || !grid.IsPassable(goal.x, goal.y))
                return new List<Vector3>();

            // g-costs and parents stored in flat dictionaries keyed by encoded cell.
            var gCost  = new Dictionary<int, float>();
            var parent = new Dictionary<int, int>();
            var open   = new SortedList<float, int>(new DuplicateKeyComparer());

            int startKey = Encode(start, grid.Width);
            int goalKey  = Encode(goal,  grid.Width);

            gCost[startKey] = 0f;
            open.Add(Heuristic(start, goal), startKey);

            while (open.Count > 0)
            {
                // Pop the lowest f-cost node.
                int current = open.Values[0]; open.RemoveAt(0);
                if (current == goalKey) return ReconstructPath(grid, parent, goalKey);

                Vector2Int currentCell = Decode(current, grid.Width);
                float g = gCost[current];

                foreach (var neighbour in grid.PassableNeighbours(currentCell))
                {
                    int nKey = Encode(neighbour, grid.Width);
                    float newG = g + 1f; // unit step cost
                    if (gCost.TryGetValue(nKey, out float existingG) && newG >= existingG)
                        continue;

                    gCost[nKey] = newG;
                    parent[nKey] = current;
                    float f = newG + Heuristic(neighbour, goal);
                    open.Add(f, nKey);
                }
            }
            return new List<Vector3>(); // unreachable
        }

        // ── BFS (uninformed flanking search) ─────────────────────────────────

        /// <summary>
        /// Breadth-first search outward from <paramref name="seed"/> up to
        /// <paramref name="maxRadius"/> cells. Returns a path from <paramref name="agentCell"/>
        /// toward the best reachable candidate cell that is not on
        /// <paramref name="partnerPath"/> (used so the flanker avoids the pursuer's route).
        ///
        /// Falls back to the direct A* path when no distinct alternate is found.
        /// </summary>
        public static List<Vector3> FindFlankPath(
            VaultHoundGrid grid,
            Vector2Int agentCell,
            Vector2Int seed,
            HashSet<int> partnerPathCells,
            int maxRadius = 5,
            int maxCandidates = 12)
        {
            var visited    = new HashSet<int>();
            var queue      = new Queue<Vector2Int>();
            var candidates = new List<Vector2Int>();

            queue.Enqueue(seed);
            visited.Add(Encode(seed, grid.Width));

            while (queue.Count > 0 && candidates.Count < maxCandidates)
            {
                var cell = queue.Dequeue();
                int key = Encode(cell, grid.Width);

                // Prefer cells NOT shared with partner's direct route.
                if (!partnerPathCells.Contains(key))
                    candidates.Add(cell);

                int dist = Mathf.Abs(cell.x - seed.x) + Mathf.Abs(cell.y - seed.y);
                if (dist >= maxRadius) continue;

                foreach (var n in grid.PassableNeighbours(cell))
                {
                    int nKey = Encode(n, grid.Width);
                    if (visited.Add(nKey)) queue.Enqueue(n);
                }
            }

            // Choose the candidate reachable from agentCell with shortest A* distance.
            float   bestCost = float.MaxValue;
            List<Vector3> bestPath = null;

            foreach (var candidate in candidates)
            {
                var path = FindPath(grid, agentCell, candidate);
                if (path.Count > 0 && path.Count < bestCost)
                {
                    bestCost = path.Count;
                    bestPath = path;
                }
            }

            return bestPath ?? FindPath(grid, agentCell, seed);
        }

        // ── BFS sweep cells (search pattern after sighting lost) ─────────────

        /// <summary>
        /// Returns an ordered list of world-space positions representing a sweep route
        /// expanding outward from <paramref name="seed"/> in concentric BFS rings.
        /// Used when a Hound loses sight of the player and must investigate nearby hiding spots.
        /// </summary>
        public static List<Vector3> BuildSweepRoute(
            VaultHoundGrid grid,
            Vector2Int seed,
            int maxRadius = 4,
            int maxCells  = 12)
        {
            var visited = new HashSet<int>();
            var queue   = new Queue<Vector2Int>();
            var route   = new List<Vector3>();

            queue.Enqueue(seed);
            visited.Add(Encode(seed, grid.Width));

            while (queue.Count > 0 && route.Count < maxCells)
            {
                var cell = queue.Dequeue();
                route.Add(grid.CellCentre(cell));

                int dist = Mathf.Abs(cell.x - seed.x) + Mathf.Abs(cell.y - seed.y);
                if (dist >= maxRadius) continue;

                foreach (var n in grid.PassableNeighbours(cell))
                {
                    int nKey = Encode(n, grid.Width);
                    if (visited.Add(nKey)) queue.Enqueue(n);
                }
            }
            return route;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static float Heuristic(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        static int Encode(Vector2Int cell, int width) => cell.y * width + cell.x;

        static Vector2Int Decode(int key, int width) =>
            new Vector2Int(key % width, key / width);

        static List<Vector3> ReconstructPath(VaultHoundGrid grid,
            Dictionary<int, int> parent, int goalKey)
        {
            var keys = new List<int>();
            int cur = goalKey;
            while (parent.TryGetValue(cur, out int p)) { keys.Add(cur); cur = p; }
            keys.Add(cur); // start node
            keys.Reverse();

            var path = new List<Vector3>(keys.Count);
            foreach (int k in keys)
            {
                var cell = Decode(k, grid.Width);
                path.Add(grid.CellCentre(cell));
            }
            return path;
        }

        // Allows duplicate keys in SortedList (different f-costs can be equal).
        sealed class DuplicateKeyComparer : IComparer<float>
        {
            public int Compare(float x, float y)
            {
                int res = x.CompareTo(y);
                return res == 0 ? 1 : res; // treat equal as "x > y" so no key is dropped
            }
        }
    }
}

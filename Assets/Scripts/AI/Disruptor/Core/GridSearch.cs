using System;
using System.Collections.Generic;

namespace Skybreak.Disruptor.Core
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Z;
        public Cell(int x, int z) { X = x; Z = z; }
        public bool Equals(Cell o) => X == o.X && Z == o.Z;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663);
        public override string ToString() => $"({X},{Z})";
    }

    /// <summary>What the search needs to know about the map. The runtime grid implements this.</summary>
    public interface IGridMap
    {
        int Width { get; }
        int Height { get; }
        /// <summary>Cell is free for the agent's full radius (obstacles are already inflated).</summary>
        bool Walkable(int x, int z);
        /// <summary>Extra cost per metre on this cell, &gt;= 0 (the player's aim cone, for example).</summary>
        double Danger(int x, int z);
    }

    public enum GridPathStatus { Complete, Partial, NoPath, Invalid }

    public sealed class GridPath
    {
        public GridPathStatus Status = GridPathStatus.Invalid;
        public readonly List<Cell> Cells = new List<Cell>();
        public double Cost;         // weighted cost (length x (1 + danger))
        public double Length;       // geometric length in cells
        public int Expanded;
        public bool HitBudget;
    }

    /// <summary>
    /// Threat-aware A* on an 8-connected grid.
    ///   step cost  = geometric length (1 or sqrt 2) x (1 + danger of the cell entered)
    ///   heuristic  = octile distance, the exact cost of the shortest unobstructed 8-connected route
    ///                with zero danger. Danger only ever adds cost, so h never overestimates and,
    ///                being a metric, it is consistent.
    /// Diagonal moves are refused when either orthogonal neighbour is blocked, so a route can never
    /// squeeze between two touching obstacles (no corner cutting).
    /// When the goal cannot be reached, the route to the reachable cell nearest the goal is returned
    /// as <see cref="GridPathStatus.Partial"/>: following it does not mean the goal was reached.
    /// </summary>
    public static class GridSearch
    {
        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        const double Sqrt2 = 1.4142135623730951;
        [ThreadStatic] static double[] gBuffer;
        [ThreadStatic] static int[] parentBuffer;
        [ThreadStatic] static bool[] closedBuffer;

        public static double Octile(Cell a, Cell b)
        {
            int dx = Math.Abs(a.X - b.X), dz = Math.Abs(a.Z - b.Z);
            return Math.Max(dx, dz) + (Sqrt2 - 1) * Math.Min(dx, dz);
        }

        public static bool InBounds(IGridMap m, Cell c) => c.X >= 0 && c.Z >= 0 && c.X < m.Width && c.Z < m.Height;

        public static GridPath FindPath(IGridMap map, Cell start, Cell goal, int budget = 4096)
        {
            var result = new GridPath();
            if (map == null || !InBounds(map, start) || !InBounds(map, goal) || !map.Walkable(start.X, start.Z) || budget < 1) return result;

            int w = map.Width, n = map.Width * map.Height;
            // Buffers are reused between searches: several routes are planned per decision, and
            // allocating three grid-sized arrays each time would feed the garbage collector every second.
            if (gBuffer == null || gBuffer.Length < n) { gBuffer = new double[n]; parentBuffer = new int[n]; closedBuffer = new bool[n]; }
            var g = gBuffer; var parent = parentBuffer; var closed = closedBuffer;
            for (int i = 0; i < n; i++) { g[i] = double.PositiveInfinity; parent[i] = -1; closed[i] = false; }
            int Index(int x, int z) => z * w + x;

            int s = Index(start.X, start.Z), t = Index(goal.X, goal.Z);
            g[s] = 0;
            var open = new MinHeap();
            open.Push(s, Octile(start, goal));
            int nearest = s; double nearestH = Octile(start, goal);

            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (closed[cur]) continue;
                if (cur == t) return Build(result, map, parent, g, cur, GridPathStatus.Complete);
                if (result.Expanded >= budget) { result.HitBudget = true; break; }
                closed[cur] = true; result.Expanded++;

                int cx = cur % w, cz = cur / w;
                double ch = Octile(new Cell(cx, cz), goal);
                if (ch < nearestH) { nearestH = ch; nearest = cur; }

                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], nz = cz + DZ[k];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= map.Height || !map.Walkable(nx, nz)) continue;
                    bool diagonal = k >= 4;
                    if (diagonal && (!map.Walkable(cx + DX[k], cz) || !map.Walkable(cx, cz + DZ[k]))) continue;
                    int ni = Index(nx, nz);
                    if (closed[ni]) continue;
                    double step = (diagonal ? Sqrt2 : 1) * (1 + Math.Max(0, map.Danger(nx, nz)));
                    double candidate = g[cur] + step;
                    if (candidate >= g[ni]) continue;
                    g[ni] = candidate; parent[ni] = cur;
                    open.Push(ni, candidate + Octile(new Cell(nx, nz), goal));
                }
            }
            // Nothing reachable is closer to the goal than where we stand: report failure, never a fake route.
            if (nearest == s) { result.Status = GridPathStatus.NoPath; return result; }
            return Build(result, map, parent, g, nearest, GridPathStatus.Partial);
        }

        static GridPath Build(GridPath result, IGridMap map, int[] parent, double[] g, int end, GridPathStatus status)
        {
            int w = map.Width;
            for (int i = end; i >= 0; i = parent[i]) result.Cells.Add(new Cell(i % w, i / w));
            result.Cells.Reverse();
            result.Cost = g[end];
            for (int i = 1; i < result.Cells.Count; i++)
                result.Length += (result.Cells[i].X != result.Cells[i - 1].X && result.Cells[i].Z != result.Cells[i - 1].Z) ? Sqrt2 : 1;
            result.Status = status;
            return result;
        }

        /// <summary>
        /// String pulling: keeps a cell only when the straight line past it would cross a blocked cell,
        /// cut a blocked corner, or pass through more danger than the A* route it replaces.
        /// The last condition stops smoothing from undoing the threat-aware detour.
        /// </summary>
        public static List<Cell> Smooth(IGridMap map, IReadOnlyList<Cell> cells)
        {
            var output = new List<Cell>();
            if (cells == null || cells.Count == 0) return output;
            output.Add(cells[0]);
            int anchor = 0;
            while (anchor < cells.Count - 1)
            {
                int furthest = anchor + 1;
                for (int j = cells.Count - 1; j > anchor + 1; j--)
                {
                    if (LineClear(map, cells[anchor], cells[j], MaxDanger(map, cells, anchor, j))) { furthest = j; break; }
                }
                output.Add(cells[furthest]);
                anchor = furthest;
            }
            return output;
        }

        static double MaxDanger(IGridMap map, IReadOnlyList<Cell> cells, int from, int to)
        {
            double m = 0;
            for (int i = from; i <= to; i++) m = Math.Max(m, map.Danger(cells[i].X, cells[i].Z));
            return m;
        }

        /// <summary>Walks the segment in quarter-cell steps; every visited cell must be walkable and no
        /// more dangerous than <paramref name="dangerLimit"/>, and every diagonal cell change must have
        /// both orthogonal neighbours free.</summary>
        public static bool LineClear(IGridMap map, Cell a, Cell b, double dangerLimit = double.PositiveInfinity)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dz)) * 4));
            int px = a.X, pz = a.Z;
            for (int i = 1; i <= steps; i++)
            {
                double f = i / (double)steps;
                int x = (int)Math.Round(a.X + dx * f), z = (int)Math.Round(a.Z + dz * f);
                if (x == px && z == pz) continue;
                if (!InBounds(map, new Cell(x, z)) || !map.Walkable(x, z) || map.Danger(x, z) > dangerLimit + 1e-9) return false;
                if (x != px && z != pz && (!map.Walkable(x, pz) || !map.Walkable(px, z))) return false;
                px = x; pz = z;
            }
            return true;
        }

        sealed class MinHeap
        {
            readonly List<(int item, double priority)> heap = new List<(int, double)>();
            public int Count => heap.Count;
            public void Push(int item, double priority)
            {
                heap.Add((item, priority));
                int i = heap.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (heap[p].priority <= heap[i].priority) break;
                    (heap[i], heap[p]) = (heap[p], heap[i]); i = p;
                }
            }
            public int Pop()
            {
                int result = heap[0].item;
                heap[0] = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < heap.Count && heap[l].priority < heap[m].priority) m = l;
                    if (r < heap.Count && heap[r].priority < heap[m].priority) m = r;
                    if (m == i) break;
                    (heap[i], heap[m]) = (heap[m], heap[i]); i = m;
                }
                return result;
            }
        }
    }
}

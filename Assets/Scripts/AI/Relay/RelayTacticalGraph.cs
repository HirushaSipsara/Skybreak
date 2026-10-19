using System.Collections.Generic;
using Skybreak.AI.Algorithms.Search;
using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.AI.Relay
{
    // One planned route and everything needed to explain it.
    public sealed class RelayTacticalPlan
    {
        public bool found, hitLimit;
        public SearchStrategy strategy;
        public float cost, length, exposure;          // g of the path, its metres, and summed exposure of its cells
        public int expanded, generated;
        public readonly List<Vector3> waypoints = new List<Vector3>();
        public readonly List<Vector3> explored = new List<Vector3>();   // closed set, for the overlay
        public Vector3 goal;
        public float plannedAt;
    }

    // Layer 4 (Lecture 05, informed search): the search problem Relay's A* solves.
    //   States:     cells of a 1 m grid laid over the NavMesh around Relay (built lazily, only where searched)
    //   Actions:    move to one of 8 neighbours, if the NavMesh connects them (NavMesh.Raycast is clear)
    //   Step cost:  c(n, n') = |n - n'| + lambda * exposure(n'), exposure = summed confidence of the threats
    //               that have line of sight to n' (capped at 1)
    //   Heuristic:  h(n) = straight-line distance to the goal. Every step costs at least its length because
    //               exposure >= 0, so h never overestimates (admissible), and by the triangle inequality
    //               h(n) <= |n - n'| + h(n') <= c(n, n') + h(n') (consistent): A* graph search is optimal.
    public sealed class RelayTacticalGraph : ISearchProblem<Vector2Int>
    {
        public float cellSize = 1, lambda = 3, maxStepHeight = .6f, eyeHeight = 1.2f;
        public int maxExpansions = 2500;

        Vector3 origin;
        NavMeshQueryFilter filter;
        IReadOnlyList<RelayThreatTrack> threats;
        float now;
        readonly Dictionary<Vector2Int, Vector3?> samples = new Dictionary<Vector2Int, Vector3?>();
        readonly Dictionary<Vector2Int, float> exposures = new Dictionary<Vector2Int, float>();
        static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        public Vector2Int Cell(Vector3 world) => new Vector2Int(Mathf.RoundToInt((world.x - origin.x) / cellSize), Mathf.RoundToInt((world.z - origin.z) / cellSize));

        // A cell exists if the NavMesh has a point inside it; cached for the whole search.
        public Vector3? Sample(Vector2Int c)
        {
            if (samples.TryGetValue(c, out var cached)) return cached;
            Vector3 centre = new Vector3(origin.x + c.x * cellSize, origin.y, origin.z + c.y * cellSize);
            Vector3? result = null;
            if (NavMesh.SamplePosition(centre, out var hit, cellSize * 1.5f, filter) &&
                Mathf.Abs(hit.position.x - centre.x) <= cellSize * .5f + .01f && Mathf.Abs(hit.position.z - centre.z) <= cellSize * .5f + .01f)
                result = hit.position;
            samples[c] = result;
            return result;
        }

        // How visible a cell is to the threats Relay knows about (0 = in cover, 1 = fully watched).
        public float Exposure(Vector2Int c)
        {
            if (exposures.TryGetValue(c, out float e)) return e;
            e = 0;
            var p = Sample(c);
            if (p.HasValue && threats != null)
            {
                Vector3 eye = p.Value + Vector3.up * eyeHeight;
                foreach (var t in threats)
                {
                    Vector3 from = t.Predicted(now), delta = eye - from;
                    if (!Physics.Raycast(from, delta.normalized, delta.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore))   // same line-of-sight rule as DamageSink.Visible
                        e += t.Confidence(now);
                }
            }
            e = Mathf.Min(1, e);
            exposures[c] = e;
            return e;
        }

        public IEnumerable<(Vector2Int next, double cost)> Successors(Vector2Int node)
        {
            var a = Sample(node);
            if (!a.HasValue) yield break;
            foreach (var d in Directions)
            {
                var next = node + d;
                var b = Sample(next);
                if (!b.HasValue || Mathf.Abs(b.Value.y - a.Value.y) > maxStepHeight) continue;
                if (NavMesh.Raycast(a.Value, b.Value, out _, filter)) continue;    // the NavMesh edge is in the way
                float length = Vector3.Distance(a.Value, b.Value);
                yield return (next, length + lambda * Exposure(next));
            }
        }

        public double Heuristic(Vector2Int node, Vector2Int goal)
        {
            var a = Sample(node); var b = Sample(goal);
            return a.HasValue && b.HasValue ? Vector3.Distance(a.Value, b.Value) : 0;
        }

        // Plans from 'from' to 'to'. The grid is anchored on the start so Relay's own cell is (0, 0).
        public RelayTacticalPlan Plan(Vector3 from, Vector3 to, NavMeshQueryFilter queryFilter, IReadOnlyList<RelayThreatTrack> knownThreats,
            float time, SearchStrategy strategy = SearchStrategy.AStar)
        {
            origin = from; filter = queryFilter; threats = knownThreats; now = time;
            samples.Clear(); exposures.Clear();
            var plan = new RelayTacticalPlan { strategy = strategy, goal = to, plannedAt = time };
            var start = Vector2Int.zero;
            var goal = Cell(to);
            if (!Sample(start).HasValue || !NearestSampled(ref goal)) return plan;
            var result = BestFirstSearch.Run(this, start, goal, strategy, maxExpansions);
            plan.found = result.Found; plan.hitLimit = result.HitExpansionLimit;
            plan.cost = (float)result.Cost; plan.expanded = result.Expanded; plan.generated = result.Generated;
            Vector3? previous = null;
            foreach (var c in result.Path)
            {
                var p = Sample(c).Value;
                plan.waypoints.Add(p); plan.exposure += Exposure(c);
                if (previous.HasValue) plan.length += Vector3.Distance(previous.Value, p);
                previous = p;
            }
            foreach (var c in result.Closed) { var p = Sample(c); if (p.HasValue) plan.explored.Add(p.Value); }
            return plan;
        }

        // The goal may sit just off the grid's sampled cells (a wall edge, the player's capsule): use the nearest cell that exists.
        bool NearestSampled(ref Vector2Int goal)
        {
            if (Sample(goal).HasValue) return true;
            for (int r = 1; r <= 2; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = goal + new Vector2Int(dx, dy);
                        if (Sample(c).HasValue) { goal = c; return true; }
                    }
            return false;
        }
    }
}

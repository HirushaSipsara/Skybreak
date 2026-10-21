using System;

namespace Skybreak.Warden.Core
{
    /// <summary>
    /// Waypoint graph and A* route planning for the Crystal Echo Warden.
    /// Implements Lecture 04 (Problem Solving & Search), Lecture 05 (A* Informed Search),
    /// and Lecture 05 (Local Search / Random-Restart Hill Climbing).
    /// Heuristic: f(n) = g(n) + h(n) where h(n) is straight-line Euclidean distance (admissible and consistent).
    /// </summary>
    public sealed class WardenPathPlanner
    {
        public WorldGraph Graph { get; set; }

        public WardenPathPlanner(WorldGraph graph = null)
        {
            Graph = graph;
        }

        /// <summary>
        /// A* search over the waypoint graph with admissible heuristic h(n) = straight-line Euclidean distance.
        /// </summary>
        public Route Plan(int start, int goal, int budget = 256)
        {
            if (Graph == null) return new Route { Status = RouteStatus.Invalid };
            return Search.AStar(Graph, start, goal, budget);
        }

        /// <summary>
        /// Calculates acoustic loss from listener across all links (including closed doors with attenuation).
        /// </summary>
        public double[] CalculateAcousticLoss(int listener)
        {
            if (Graph == null) return Array.Empty<double>();
            return Search.AcousticLoss(Graph, listener);
        }

        /// <summary>
        /// Uniform-cost travel distances from start node to all reachable nodes.
        /// </summary>
        public double[] CalculateTravelCosts(int start)
        {
            if (Graph == null) return Array.Empty<double>();
            return TacticalSearch.TravelCosts(Graph, start);
        }

        /// <summary>
        /// Intercepts the player before reaching the guarded crystal pedestal.
        /// Predicts player's route from the suspected zone to the pedestal using A*,
        /// then chooses the node maximizing (player_arrival_time - warden_travel_time).
        /// </summary>
        public int PlanInterception(int intruderZone, int guardZone, double[] travelCosts, double wardenSpeed = 2.8, double intruderSpeed = 5.0)
        {
            if (Graph == null || !Graph.Valid(intruderZone) || !Graph.Valid(guardZone) || travelCosts == null)
                return guardZone;

            var predicted = Search.AStar(Graph, intruderZone, guardZone, 256);
            if (predicted.Status != RouteStatus.Found || predicted.Nodes.Length == 0)
                return guardZone;

            double bestMargin = double.NegativeInfinity;
            double intruderElapsed = 0;
            int bestNode = guardZone;

            for (int k = 0; k < predicted.Nodes.Length; k++)
            {
                int node = predicted.Nodes[k];
                if (k > 0)
                {
                    intruderElapsed += Point.Distance(Graph[predicted.Nodes[k - 1]], Graph[node]) / Math.Max(0.1, intruderSpeed);
                }

                if (node >= travelCosts.Length || !WorldGraph.Finite(travelCosts[node]))
                    continue;

                double wardenTravelTime = travelCosts[node] / Math.Max(0.1, wardenSpeed);
                double margin = intruderElapsed - wardenTravelTime;

                if (margin > bestMargin)
                {
                    bestMargin = margin;
                    bestNode = node;
                }
            }

            return bestNode;
        }

        /// <summary>
        /// Tactical flanking or cover selection using local search (hill climbing with random restarts).
        /// </summary>
        public int PlanTacticalPoint(TacticalPoint[] candidates, int seed, int preferred = -1, int restarts = 4, int maxSteps = 24)
        {
            if (Graph == null || candidates == null || candidates.Length != Graph.Count)
                return -1;

            return TacticalSearch.Climb(Graph, candidates, seed, preferred, restarts, maxSteps);
        }
    }
}

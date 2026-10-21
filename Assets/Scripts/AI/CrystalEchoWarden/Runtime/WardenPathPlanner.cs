using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    /// <summary>
    /// Runtime adapter for A* route planning and tactical waypoint search.
    /// Manages A* navigation over the waypoint graph, acoustic sound loss queries,
    /// dynamic interception routes, and local-search flanking positions.
    /// Implements Lecture 04 (Search), Lecture 05 (A* Informed Search), and Lecture 05 (Local Search).
    /// </summary>
    public sealed class WardenPathPlanner : MonoBehaviour
    {
        public WardenMap map;

        public Core.WardenPathPlanner CorePlanner { get; private set; }

        void Awake()
        {
            if (map == null) map = GetComponent<WardenBrain>()?.map;
            CorePlanner = new Core.WardenPathPlanner(map?.Graph);
        }

        void EnsurePlanner()
        {
            if (CorePlanner == null) CorePlanner = new Core.WardenPathPlanner(map?.Graph);
            else if (map != null && CorePlanner.Graph != map.Graph) CorePlanner.Graph = map.Graph;
        }

        public Route Plan(int start, int goal, int budget = 256)
        {
            EnsurePlanner();
            return CorePlanner.Plan(start, goal, budget);
        }

        public double[] CalculateTravelCosts(int start)
        {
            EnsurePlanner();
            return CorePlanner.CalculateTravelCosts(start);
        }

        public double[] CalculateAcousticLoss(int listener)
        {
            EnsurePlanner();
            return CorePlanner.CalculateAcousticLoss(listener);
        }

        public int PlanInterception(int intruderZone, int guardZone, double[] travelCosts, float wardenSpeed = 2.8f)
        {
            EnsurePlanner();
            return CorePlanner.PlanInterception(intruderZone, guardZone, travelCosts, wardenSpeed);
        }

        public int PlanTacticalPoint(TacticalPoint[] candidates, int seed, int preferred = -1)
        {
            EnsurePlanner();
            return CorePlanner.PlanTacticalPoint(candidates, seed, preferred);
        }
    }
}

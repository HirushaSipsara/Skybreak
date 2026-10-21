using System;

namespace Skybreak.Warden.Core
{
    /// <summary>
    /// Model-based agent state for the Crystal Echo Warden.
    /// Stores recent sightings, sounds, crystal state, last route, and Bayesian probability zones.
    /// Fulfills Lecture 02 (PEAS & Model-based agents) and Lecture 09 (Bayesian probability).
    /// </summary>
    public sealed class WardenWorldModel
    {
        public BeliefMap Belief { get; }

        public bool HasSighting { get; private set; }
        public int LastSeenZone { get; private set; } = -1;
        public Point LastSeenPosition { get; private set; }
        public double LastSeenTime { get; private set; } = double.NegativeInfinity;

        public bool HasSound { get; private set; }
        public int LastHeardZone { get; private set; } = -1;
        public Point LastHeardOrigin { get; private set; }
        public double LastHeardTime { get; private set; } = double.NegativeInfinity;
        public double LastHeardLoudness { get; private set; }
        public string LastHeardKind { get; private set; } = "None";
        public int TotalSoundsHeard { get; private set; }

        public float CrystalIntegrity { get; private set; } = 100f;
        public float ObjectiveDanger { get; private set; }
        public double LastObjectiveEventTime { get; private set; } = double.NegativeInfinity;
        public bool ObjectiveAvailable { get; private set; } = true;

        public int[] LastRouteNodes { get; private set; } = Array.Empty<int>();
        public double LastRouteCost { get; private set; } = double.PositiveInfinity;
        public double LastRouteTime { get; private set; } = double.NegativeInfinity;

        public WardenWorldModel(int zoneCount)
        {
            Belief = new BeliefMap(zoneCount);
        }

        public void Reset()
        {
            Belief.Reset();
            HasSighting = false;
            LastSeenZone = -1;
            LastSeenTime = double.NegativeInfinity;
            HasSound = false;
            LastHeardZone = -1;
            LastHeardTime = double.NegativeInfinity;
            LastHeardLoudness = 0;
            LastHeardKind = "None";
            TotalSoundsHeard = 0;
            CrystalIntegrity = 100f;
            ObjectiveDanger = 0f;
            LastObjectiveEventTime = double.NegativeInfinity;
            ObjectiveAvailable = true;
            LastRouteNodes = Array.Empty<int>();
            LastRouteCost = double.PositiveInfinity;
            LastRouteTime = double.NegativeInfinity;
        }

        public void RecordSighting(int zone, Point position, double time)
        {
            HasSighting = true;
            LastSeenZone = zone;
            LastSeenPosition = position;
            LastSeenTime = time;
            Belief.See(zone, time);
        }

        public void RecordSound(double[] acousticLoss, double loudness, double heardAmplitude, double time, Point origin, int zone = -1, string kind = "Sound")
        {
            HasSound = true;
            LastHeardZone = zone;
            LastHeardOrigin = origin;
            LastHeardTime = time;
            LastHeardLoudness = loudness;
            LastHeardKind = kind;
            TotalSoundsHeard++;

            var likelihood = BeliefMap.SoundLikelihood(acousticLoss, loudness, heardAmplitude);
            Belief.Observe(likelihood, time);
        }

        public void RecordNegativeEvidence(int zone)
        {
            Belief.ClearVisible(zone);
        }

        public void PredictPlayerMovement(WorldGraph graph, double dt)
        {
            Belief.Predict(graph, dt);
        }

        public void RecordCrystalState(float integrity, float danger, double time, bool available = true)
        {
            CrystalIntegrity = integrity;
            ObjectiveDanger = danger;
            ObjectiveAvailable = available;
            if (danger > 0.05f)
            {
                LastObjectiveEventTime = time;
            }
        }

        public void RecordRoute(int[] routeNodes, double cost, double time)
        {
            LastRouteNodes = routeNodes ?? Array.Empty<int>();
            LastRouteCost = cost;
            LastRouteTime = time;
        }

        public double GetConfidence(double now) => Belief.Confidence(now);

        public int GetMostLikelyZone() => Belief.MostLikely;

        public double GetZoneProbability(int zone) => (zone >= 0 && zone < Belief.Count) ? Belief[zone] : 0;
    }
}

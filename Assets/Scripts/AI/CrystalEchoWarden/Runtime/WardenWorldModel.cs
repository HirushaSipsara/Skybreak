using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    /// <summary>
    /// Runtime adapter for the Warden's internal world model.
    /// Stores recent sightings, sounds, crystal objective state, and probability zones.
    /// Fulfills Lecture 02 (Model-based agent state) and Lecture 09 (Reasoning under uncertainty).
    /// </summary>
    public sealed class WardenWorldModel : MonoBehaviour
    {
        public WardenPerception perception;
        public WardenMap map;
        public WardenObjective objective;

        public Core.WardenWorldModel Model { get; private set; }

        public BeliefMap Belief => Model?.Belief ?? perception?.Belief;
        public double Confidence => Model != null ? Model.GetConfidence(Time.time) : (perception != null && perception.Belief != null ? perception.Belief.Confidence(Time.time) : 0);
        public int MostLikelyZone => Model != null ? Model.GetMostLikelyZone() : (perception != null && perception.Belief != null ? perception.Belief.MostLikely : -1);
        public bool HasSighting => Model != null && Model.HasSighting;
        public Vector3 LastSeenPosition => Model != null ? new Vector3((float)Model.LastSeenPosition.X, (float)Model.LastSeenPosition.Y, (float)Model.LastSeenPosition.Z) : (perception != null ? perception.LastSeen : Vector3.zero);
        public float ObjectiveDanger => Model != null ? Model.ObjectiveDanger : (objective != null ? objective.Danger : 0f);

        void Awake()
        {
            if (perception == null) perception = GetComponent<WardenPerception>();
            if (map == null) map = GetComponent<WardenBrain>()?.map;
            if (objective == null) objective = GetComponent<WardenBrain>()?.objective;
            EnsureModel();
        }

        public void EnsureModel()
        {
            if (map == null || map.Graph == null) return;
            if (Model == null || Model.Belief.Count != map.Graph.Count)
            {
                Model = new Core.WardenWorldModel(map.Graph.Count);
            }
        }

        public void ResetForEncounter()
        {
            Model?.Reset();
        }

        public void RecordSighting(int zone, Vector3 position)
        {
            EnsureModel();
            Model?.RecordSighting(zone, WardenMap.ToPoint(position), Time.time);
        }

        public void RecordSound(double[] loss, double loudness, double heard, Vector3 origin, int zone = -1, string kind = "Sound")
        {
            EnsureModel();
            Model?.RecordSound(loss, loudness, heard, Time.time, WardenMap.ToPoint(origin), zone, kind);
        }

        public void RecordCrystalState(float integrity, float danger)
        {
            EnsureModel();
            Model?.RecordCrystalState(integrity, danger, Time.time, objective == null || objective.Available);
        }

        public void RecordRoute(int[] routeNodes, double cost)
        {
            EnsureModel();
            Model?.RecordRoute(routeNodes, cost, Time.time);
        }

        public void Predict(WorldGraph graph, double dt)
        {
            EnsureModel();
            Model?.PredictPlayerMovement(graph, dt);
        }
    }
}

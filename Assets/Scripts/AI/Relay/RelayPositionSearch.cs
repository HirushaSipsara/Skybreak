using System.Collections.Generic;
using Skybreak.AI.Algorithms.Local;
using Skybreak.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.AI.Relay
{
    public enum RelayPositionGoal { Support, Heal, Defend, Retreat }

    // Layer 5 (Lecture 05, local search): where should Relay stand? Only the final spot matters, not the path to
    // it, so this is an optimisation problem: maximise E(x) over points x on the NavMesh with simulated annealing.
    // E(x) rewards what makes a spot good for the current action and penalises what makes it bad:
    //   + in range of who Relay serves (player for Support/Heal, the target for Defend)
    //   + line of sight to them (the heal beam and shots need it)
    //   + hidden from known threats (1 - exposure)
    //   - standing in the player's line of fire
    //   - travel distance (do not run across the map for a tiny gain)
    // The weights differ per goal; Retreat cares mostly about cover and distance from threats.
    public sealed class RelayPositionSearch : ILocalSearchProblem<Vector3>
    {
        public int iterations = 60;
        public float t0 = 1, alpha = .85f, maxStep = 3, searchRadius = 12, fireLaneLength = 15, fireLaneRadius = 1.1f;

        // context for the current search
        RelayPositionGoal goal;
        Vector3 relay, player, playerForward, anchor;
        DamageSink playerTarget, threatTarget;
        IReadOnlyList<RelayThreatTrack> threats;
        float now, healRange, attackRange, eye = 1.7f;
        NavMeshQueryFilter filter;
        readonly Dictionary<Vector3Int, float> cache = new Dictionary<Vector3Int, float>();

        public LocalSearchResult<Vector3> Last { get; private set; }
        public RelayPositionGoal LastGoal => goal;

        public void Prepare(RelayPositionGoal goal, Vector3 relay, Transform player, DamageSink playerVitals, DamageSink threat,
            Vector3 anchor, IReadOnlyList<RelayThreatTrack> threats, float healRange, float attackRange, NavMeshQueryFilter filter, float now)
        {
            this.goal = goal; this.relay = relay; this.player = player.position; playerForward = player.forward; playerForward.y = 0;
            playerTarget = playerVitals; threatTarget = threat; this.anchor = anchor; this.threats = threats;
            this.healRange = healRange; this.attackRange = attackRange; this.filter = filter; this.now = now;
            cache.Clear();
        }

        public LocalSearchResult<Vector3> Search(Vector3 start, System.Random rng)
        {
            Last = SimulatedAnnealing.Run(this, Snap(start) ?? relay, iterations, t0, alpha, rng, recordTrace: true);
            return Last;
        }

        // ---------------------------------------------------------------- objective E(x)
        public double Score(Vector3 x)
        {
            var key = Vector3Int.RoundToInt(x * 4);   // quarter-metre cache: repeated samples cost nothing
            if (cache.TryGetValue(key, out float cached)) return cached;
            float exposure = Exposure(x), lane = InFireLane(x) ? 1 : 0;
            float dPlayer = Flat(x, player), travel = Flat(x, relay);
            float score;
            switch (goal)
            {
                case RelayPositionGoal.Heal:
                    score = 2.0f * Within(dPlayer, healRange * .85f, 3) + 1.0f * Sees(x, playerTarget) + .8f * (1 - exposure) - 1.5f * lane - .08f * travel;
                    break;
                case RelayPositionGoal.Defend:
                    float dTarget = threatTarget != null ? Flat(x, threatTarget.transform.position) : 0;
                    score = 2.0f * Within(dTarget, attackRange * .9f, 4) + 1.5f * Sees(x, threatTarget) + .3f * (1 - exposure) - 1.5f * lane - .08f * travel;
                    break;
                case RelayPositionGoal.Retreat:
                    score = 1.5f * (1 - exposure) + .15f * Mathf.Min(DistanceFromThreats(x), 12) + .5f * Within(dPlayer, 8, 6) - .1f * travel;
                    break;
                default: // Support: stay in a comfortable band around the player, near the formation spot, out of the way
                    score = 1.0f * Band(dPlayer, 1.8f, 3.5f, 3) + .6f * Sees(x, playerTarget) + 1.0f * (1 - exposure) - 1.5f * lane
                          - .15f * Flat(x, anchor) - .05f * travel;
                    break;
            }
            if (travel > searchRadius) score -= travel - searchRadius;   // stay local
            cache[key] = score;
            return score;
        }

        // Neighbour: a random point within a radius that shrinks as the search cools, snapped onto the NavMesh.
        public Vector3 Neighbour(Vector3 x, double temperature, System.Random rng)
        {
            float step = .5f + (maxStep - .5f) * Mathf.Clamp01((float)(temperature / t0));
            double angle = rng.NextDouble() * System.Math.PI * 2, radius = System.Math.Sqrt(rng.NextDouble()) * step;
            var candidate = x + new Vector3((float)(System.Math.Cos(angle) * radius), 0, (float)(System.Math.Sin(angle) * radius));
            return Snap(candidate) ?? x;
        }

        // ---------------------------------------------------------------- terms
        public float Exposure(Vector3 x)
        {
            if (threats == null) return 0;
            float e = 0; Vector3 eyePoint = x + Vector3.up * eye;
            foreach (var t in threats)
                if (Clear(t.Predicted(now), eyePoint)) e += t.Confidence(now);
            return Mathf.Min(1, e);
        }

        // Inside the player's line of fire: within the lane radius of the segment the player is aiming along, in front of them.
        public bool InFireLane(Vector3 x)
        {
            if (playerForward.sqrMagnitude < .01f) return false;
            Vector3 dir = playerForward.normalized, rel = x - player; rel.y = 0;
            float along = Vector3.Dot(rel, dir);
            if (along < .5f || along > fireLaneLength) return false;
            return (rel - dir * along).magnitude < fireLaneRadius;
        }

        float Sees(Vector3 x, DamageSink target) => target != null && Clear(x + Vector3.up * eye, target.AimPoint) ? 1 : 0;

        float DistanceFromThreats(Vector3 x)
        {
            float d = 12;
            if (threats != null) foreach (var t in threats) d = Mathf.Min(d, Flat(x, t.Predicted(now)));
            return d;
        }

        static bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;   // same rule as DamageSink.Visible: actors (layer 8) do not block sight
            return !Physics.Raycast(from, delta.normalized, delta.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore);
        }

        // 1 inside the range, falling linearly to 0 over 'falloff' metres beyond it.
        static float Within(float d, float range, float falloff) => d <= range ? 1 : Mathf.Clamp01(1 - (d - range) / falloff);
        // 1 inside [lo, hi], falling to 0 over 'falloff' metres outside it.
        static float Band(float d, float lo, float hi, float falloff) => d < lo ? Mathf.Clamp01(1 - (lo - d) / falloff) : Within(d, hi, falloff);
        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        Vector3? Snap(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 1.5f, filter) ? hit.position : (Vector3?)null;
    }
}

using System.Collections.Generic;
using Skybreak.AI.RiftWarden;
using Skybreak.Combat;
using UnityEngine;

namespace Skybreak.AI.Relay
{
    // What Relay knows at one decision cycle: this cycle's sensor readings plus remembered state.
    public struct RelayPercept
    {
        public DamageSink threat;       // most dangerous visible hostile this cycle, or null
        public float threatDanger;      // 0..1, how close that threat is to the player; -1 when there is none
        public bool remembersThreat;    // a threat was seen within the memory window
        public Vector3 threatMemory;    // last known aim point of a threat
        public float playerNeed;        // 1 - player health fraction (0 without a health source)
        public float distanceToPlayer;
        public float ownHealth01;
        public float playerHealth01;    // 1 - playerNeed
        public float healthTrend;       // player health fraction lost per second over the trend window (0 when stable or rising)
        public float threatToPlayer;    // distance from the chosen threat to the player (RelayFuzzyModel.NoThreatDistance when none)
        public bool canHealNow;         // player within heal range and in line of sight
        public bool threatInAttackRange;
    }

    // A remembered hostile (Lecture 03 internal state + transition model). Seen this cycle: confidence 1.
    // Unseen: Relay assumes it kept moving at its last velocity (capped), and trusts that guess less over time.
    public sealed class RelayThreatTrack
    {
        public DamageSink sink;
        public Vector3 lastPosition, velocity;
        public float lastSeen;
        public const float ConfidenceTau = 1.5f, MaxPrediction = 4, ForgetBelow = .1f;
        public float Confidence(float now) => Mathf.Exp(-(now - lastSeen) / ConfidenceTau);
        public Vector3 Predicted(float now) => lastPosition + Vector3.ClampMagnitude(velocity * (now - lastSeen), MaxPrediction);
    }

    // Layer 1 (model-based, Lecture 03): reads the sensors and keeps the internal state that covers what
    // Relay can no longer see. The world is partially observable, so a threat behind a wall is remembered
    // for a short time instead of being forgotten the moment line of sight breaks.
    public sealed class RelayPerception
    {
        public const float TrendWindow = 2;     // seconds of player-health history used for the trend
        float lastSeenAt = -100;
        readonly System.Collections.Generic.Queue<Vector2> healthHistory = new System.Collections.Generic.Queue<Vector2>();   // (time, health01)
        readonly Dictionary<DamageSink, RelayThreatTrack> tracks = new Dictionary<DamageSink, RelayThreatTrack>();
        readonly List<RelayThreatTrack> trackList = new List<RelayThreatTrack>();
        public Vector3 ThreatMemory { get; private set; }
        // Every hostile Relay currently sees or still remembers (confidence >= ForgetBelow), for exposure costs.
        public IReadOnlyList<RelayThreatTrack> Tracks => trackList;

        public void Forget() { lastSeenAt = -100; healthHistory.Clear(); tracks.Clear(); trackList.Clear(); }

        void Track(DamageSink sink, float now)
        {
            Vector3 position = sink.AimPoint;
            if (tracks.TryGetValue(sink, out var t))
            {
                float dt = now - t.lastSeen;
                if (dt > .01f) t.velocity = Vector3.Lerp(t.velocity, (position - t.lastPosition) / dt, .5f);
            }
            else { t = new RelayThreatTrack { sink = sink }; tracks[sink] = t; }
            t.lastPosition = position; t.lastSeen = now;
        }

        void ForgetOld(float now)
        {
            trackList.Clear();
            var expired = new List<DamageSink>();
            foreach (var kv in tracks)
            {
                if (kv.Key == null || !kv.Key.Alive || kv.Value.Confidence(now) < RelayThreatTrack.ForgetBelow) expired.Add(kv.Key);
                else trackList.Add(kv.Value);
            }
            foreach (var k in expired) tracks.Remove(k);
        }

        // Rate of health loss: oldest sample in the window minus now, per second. Healing (a rise) counts as 0.
        float Trend(float now, float health01)
        {
            healthHistory.Enqueue(new Vector2(now, health01));
            while (healthHistory.Count > 1 && now - healthHistory.Peek().x > TrendWindow) healthHistory.Dequeue();
            var oldest = healthHistory.Peek();
            float elapsed = now - oldest.x;
            return elapsed > .1f ? Mathf.Max(0, (oldest.y - health01) / elapsed) : 0;
        }

        public RelayPercept Sense(RelayBrain self, Transform player, DamageSink playerVitals, RelayTuning tuning, float now)
        {
            var percept = new RelayPercept { threatDanger = -1 };
            foreach (var candidate in DamageSink.Active)
            {
                if (candidate == null || !candidate.Alive || candidate.Team != Allegiance.Hostile) continue;
                float range = Vector3.Distance(self.AimPoint, candidate.AimPoint);
                if (range > tuning.senseRange || !DamageSink.Visible(self.AimPoint, candidate, self.transform)) continue;
                Track(candidate, now);                                       // anything hostile that can see Relay is a danger
                var part = candidate as RiftDamagePart; if (part != null && !part.Vulnerable) continue;   // ...but only exposed parts are targets
                float danger = 1 - Mathf.Clamp01(Vector3.Distance(player.position, candidate.transform.position) / 18);
                if (danger > percept.threatDanger) { percept.threatDanger = danger; percept.threat = candidate; }
            }
            ForgetOld(now);
            if (percept.threat != null) { ThreatMemory = percept.threat.AimPoint; lastSeenAt = now; }
            percept.threatMemory = ThreatMemory;
            percept.remembersThreat = now - lastSeenAt < tuning.memorySeconds;
            percept.playerNeed = playerVitals != null ? 1 - playerVitals.Health / playerVitals.MaximumHealth : 0;
            percept.distanceToPlayer = Vector3.Distance(self.transform.position, player.position);
            percept.ownHealth01 = self.Health / self.MaximumHealth;
            percept.playerHealth01 = 1 - percept.playerNeed;
            percept.healthTrend = playerVitals != null ? Trend(now, percept.playerHealth01) : 0;
            percept.threatToPlayer = percept.threat != null ? Vector3.Distance(player.position, percept.threat.transform.position) : RelayFuzzyModel.NoThreatDistance;
            percept.canHealNow = playerVitals != null && percept.distanceToPlayer <= tuning.healRange && DamageSink.Visible(self.AimPoint, playerVitals, self.transform);
            percept.threatInAttackRange = percept.threat != null && Vector3.Distance(self.AimPoint, percept.threat.AimPoint) <= tuning.attackRange;
            return percept;
        }
    }
}

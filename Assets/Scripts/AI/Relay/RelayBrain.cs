using System.Collections.Generic;
using Skybreak.Combat;
using Skybreak.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.AI.Relay
{
    public enum RelayAction { Follow, Heal, Defend, Retreat, Reposition, Disabled }

    // Relay's agent program (Docs/IS/AgentDesign/Relay/RELAY_DESIGN.md): a layered (subsumption) agent.
    //   Layer 0  RelayReflexes   - condition-action rules that override everything (checked every frame)
    //   Layer 1  RelayPerception - sensors + internal state (threat memory, health trend) for a partially observable world
    //   Layer 2  RelayFuzzyModel - fuzzy inference: how urgent healing, defending and self-preservation are
    //   Layer 3  RelayUtility    - expected utility per action and the selection rule
    //   Layer 4  RelayTacticalGraph - A* over a NavMesh grid, step cost = length + lambda x exposure to threats
    //   Layer 5  RelayPositionSearch - simulated annealing for where to stand (cover, range, out of the line of fire)
    // This class runs the decision cycle and owns the actuators (movement, heal beam, defensive shot).
    public sealed class RelayBrain : DamageSink
    {
        public Transform player, visual;
        public DamageSink playerVitals;
        public NavMeshMotor motor;
        public CombatSignals signals;
        public CombatAudio audioCues;
        public float maximumHealth = 90, healAmount = 18, healCooldown = 5, attackCooldown = 1.8f;
        public bool decisionsEnabled = true;
        public RelayAction Action { get; private set; }
        public string Decision { get; private set; } = "Starting support sensors";
        public float FollowScore { get; private set; }
        public float HealScore { get; private set; }
        public float DefendScore { get; private set; }
        public float RetreatScore { get; private set; }
        public float RepositionScore { get; private set; }
        // Layer 2 outputs (0..1) and the full inference trace of the last cycle, for the debug overlay
        public float HealUrgency { get; private set; }
        public float DefendUrgency { get; private set; }
        public float SelfPreservation { get; private set; }
        public Skybreak.AI.Algorithms.Fuzzy.FuzzyResult LastAssessment => fuzzy.Last;
        public RelayPercept LastPercept { get; private set; }
        // Layer 4: Relay's own route planner and its last plan (for the overlay and tests)
        public readonly RelayTacticalGraph planner = new RelayTacticalGraph();
        public RelayTacticalPlan LastPlan { get; private set; }
        public string PlannerStatus { get; private set; } = "No route needed";
        public IReadOnlyList<RelayThreatTrack> ThreatTracks => perception.Tracks;
        // Layer 5: position search and the spot it chose last
        public readonly RelayPositionSearch positions = new RelayPositionSearch();
        public Vector3? TargetSpot { get; private set; }
        public int HealsApplied { get; private set; }
        public int AttacksApplied { get; private set; }
        public override Allegiance Team => Allegiance.Friendly;
        public override float Health => health;
        public override float MaximumHealth => maximumHealth;
        public override Vector3 AimPoint => transform.position + Vector3.up * 1.7f;
        RelayTuning T => AgentTuning.Current.relay;
        void ApplyTuning(bool resetHealth) { if (resetHealth) maximumHealth=T.maximumHealth; healAmount=T.healAmount; healCooldown=T.healCooldown; attackCooldown=T.attackCooldown; }
        float health, nextSense, healReady, attackReady, signalOff;
        readonly RelayPerception perception = new RelayPerception();
        readonly RelayFuzzyModel fuzzy = new RelayFuzzyModel();
        RelayAction lastChoice = RelayAction.Follow;   // what layer 3 chose last cycle (before any Reposition fallback)
        const float ReplanInterval = 1, GoalMovedReplan = 1.5f, DirectApproach = 3, Lookahead = 3;
        int waypointIndex, plannedThreats;
        const float SpotRefresh = .6f, RepositionGain = .1f;
        readonly System.Random rng = new System.Random(3062);    // seeded: the same situation gives the same search
        readonly Dictionary<RelayPositionGoal, (Vector3 spot, float at)> spots = new Dictionary<RelayPositionGoal, (Vector3, float)>();

        void Start()
        {
            ApplyTuning(true); health = maximumHealth;
            if (audioCues == null) audioCues = GetComponent<CombatAudio>();
            if (player == null) { var arc = FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>(); if (arc != null) player = arc.transform; }
            if (player != null && playerVitals == null) playerVitals = player.GetComponent<DamageSink>();
        }
        public override void ReceiveDamage(float amount, Transform source)
        {
            if (!Alive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            health = Mathf.Max(0, health - amount);
            if (!Alive) { Action = RelayAction.Disabled; motor.Stop(); signals.Clear(); Decision = "Relay disabled"; }
        }
        public override float RestoreHealth(float amount)
        {
            if (!Alive || amount <= 0) return 0;
            float before = health; health = Mathf.Min(maximumHealth, health + amount); return health - before;
        }
        public void ResetSupport() { ApplyTuning(true); health = maximumHealth; nextSense = healReady = attackReady = 0; perception.Forget(); lastChoice = Action = RelayAction.Follow; LastPlan = null; PlannerStatus = "No route needed"; spots.Clear(); TargetSpot = null; signals.Clear(); }

        void Update()
        {
            if (visual != null) visual.localPosition = new Vector3(0, 1.7f + Mathf.Sin(Time.time * 2.5f) * .045f, 0);
            if (RelayReflexes.Check(Alive, playerVitals) == RelayReflex.Disabled) return;   // disabled: ReceiveDamage already stopped Relay
            if (!decisionsEnabled || player == null) return;
            if (playerVitals == null) playerVitals = player.GetComponent<DamageSink>();
            if (RelayReflexes.Check(Alive, playerVitals) == RelayReflex.PlayerDown) { motor.Stop(); Decision = "Player down / waiting for restart"; return; }
            if (Time.time > signalOff) signals.Clear();
            if (Time.time < nextSense) return;
            nextSense = Time.time + T.senseInterval; SenseAndDecide();
        }

        // One deliberative cycle: sense (layer 1), assess (layer 2), score and select (layer 3), then act.
        public void SenseAndDecide()
        {
            ApplyTuning(false);
            var percept = perception.Sense(this, player, playerVitals, T, Time.time);
            LastPercept = percept;
            var urgency = fuzzy.Assess(percept.playerHealth01, percept.threatToPlayer, percept.healthTrend, percept.ownHealth01);
            HealUrgency = (float)urgency[RelayFuzzyModel.HealUrgency];
            DefendUrgency = (float)urgency[RelayFuzzyModel.DefendUrgency];
            SelfPreservation = (float)urgency[RelayFuzzyModel.SelfPreservation];
            var scores = RelayUtility.Score(percept, urgency, playerVitals != null && playerVitals.Alive, Time.time >= healReady, Time.time >= attackReady, lastChoice, RepositionValue());
            FollowScore = scores.follow; HealScore = scores.heal; DefendScore = scores.defend; RetreatScore = scores.retreat; RepositionScore = scores.reposition;
            lastChoice = RelayUtility.Select(scores, percept.threat != null);
            switch (lastChoice)
            {
                case RelayAction.Retreat: Retreat(percept); break;
                case RelayAction.Heal: Heal(percept); break;
                case RelayAction.Defend: Defend(percept); break;
                case RelayAction.Reposition: Reposition(percept); break;
                default: Follow(percept); break;
            }
        }

        // ---------------------------------------------------------------- layer 4: tactical movement
        NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = motor.Agent.agentTypeID, areaMask = motor.Agent.areaMask };
        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        // Moves towards 'destination' along Relay's own A* route. Returns false when the destination cannot be reached,
        // so the callers keep their existing Reposition fallbacks.
        bool NavigateTo(Vector3 destination, float stopDistance)
        {
            Vector3 here = transform.position;
            if (Flat(here, destination) <= stopDistance + DirectApproach)
            { PlannerStatus = "Final approach (direct)"; return motor.MoveTo(destination, stopDistance); }

            bool stale = LastPlan == null || Vector3.Distance(destination, LastPlan.goal) > GoalMovedReplan ||
                         Time.time - LastPlan.plannedAt > ReplanInterval || plannedThreats != perception.Tracks.Count;
            if (stale)
            {
                LastPlan = planner.Plan(here, destination, Filter, perception.Tracks, Time.time);
                plannedThreats = perception.Tracks.Count; waypointIndex = 0;
            }
            if (!LastPlan.found)
            {
                PlannerStatus = LastPlan.hitLimit ? "Tactical search over node budget / NavMesh fallback" : "No tactical route on the local grid / NavMesh fallback";
                return motor.MoveTo(destination, stopDistance);
            }
            // Skip reached waypoints, then aim at the farthest one within the lookahead that is in straight NavMesh reach.
            var w = LastPlan.waypoints;
            while (waypointIndex < w.Count - 1 && Flat(here, w[waypointIndex]) < .8f) waypointIndex++;
            int target = waypointIndex; float along = Flat(here, w[waypointIndex]);
            for (int i = waypointIndex + 1; i < w.Count; i++)
            {
                along += Flat(w[i - 1], w[i]);
                if (along > Lookahead || NavMesh.Raycast(here, w[i], out _, Filter)) break;
                target = i;
            }
            bool last = target == w.Count - 1;
            PlannerStatus = $"A* route: waypoint {target + 1}/{w.Count}, {LastPlan.expanded} nodes expanded, exposure {LastPlan.exposure:F1}";
            return motor.MoveTo(last ? destination : w[target], last ? stopDistance : .3f);
        }

        // ---------------------------------------------------------------- layer 5: where to stand
        Vector3 Formation() => player.position + (-player.forward * 2 + player.right * 1.5f) * Mathf.Min(1, T.followDistance / 3.3f);

        // Best spot for a goal by simulated annealing, refreshed every 0.6 s and warm-started from the previous best.
        Vector3 BestSpot(RelayPositionGoal goal, Vector3 anchor, DamageSink threat)
        {
            float now = Time.time;
            positions.Prepare(goal, transform.position, player, playerVitals, threat, anchor, perception.Tracks, T.healRange, T.attackRange, Filter, now);
            bool known = spots.TryGetValue(goal, out var previous);
            if (known && now - previous.at < SpotRefresh) return previous.spot;
            Vector3 start = known && Flat(previous.spot, anchor) < 6 ? previous.spot : anchor;
            var best = positions.Search(start, rng).Best;
            spots[goal] = (best, now); TargetSpot = best;
            return best;
        }

        // Value of moving = how much exposure and line-of-fire blocking the move removes (not merely how bad the
        // current spot is: in open ground every spot is equally exposed and moving gains nothing).
        // Only counted when the search found a spot clearly better overall (by more than 0.1).
        float RepositionValue()
        {
            Vector3 here = transform.position;
            Vector3 spot = BestSpot(RelayPositionGoal.Support, Formation(), null);
            float gain = (float)(positions.Score(spot) - positions.Score(here));
            if (gain <= RepositionGain) return 0;
            float lessExposure = positions.Exposure(here) - positions.Exposure(spot);
            float outOfLane = (positions.InFireLane(here) ? 1 : 0) - (positions.InFireLane(spot) ? 1 : 0);
            return Mathf.Max(0, .3f * lessExposure + .5f * outOfLane);
        }

        // ---------------------------------------------------------------- actuators
        void Reposition(in RelayPercept p)
        {
            Action = RelayAction.Reposition; Decision = "Reposition / current spot is exposed or in the player's line of fire";
            if (!NavigateTo(BestSpot(RelayPositionGoal.Support, Formation(), null), .3f)) Follow(p);
        }

        void Retreat(in RelayPercept p)
        {
            Action = RelayAction.Retreat; Decision = "Retreat / self-preservation has the highest expected utility (recent threat)";
            Vector3 away = transform.position - p.threatMemory; away.y = 0;
            if (away.sqrMagnitude < .1f) away = -player.forward;
            if (!NavigateTo(BestSpot(RelayPositionGoal.Retreat, transform.position + away.normalized * 4, null), .5f)) { Action = RelayAction.Reposition; Decision = "Retreat blocked / seek player-side safe position"; motor.MoveTo(player.position - player.forward * 2, 1); }
        }

        void Heal(in RelayPercept p)
        {
            Action = RelayAction.Heal; Decision = "Heal / player's fuzzy heal urgency has the highest expected utility";
            if (p.canHealNow)
            { motor.Stop(); if (playerVitals.RestoreHealth(healAmount) > 0) { HealsApplied++; healReady = Time.time + healCooldown; signals.Beam(AimPoint, playerVitals.AimPoint, Color.cyan); signalOff = Time.time + .45f; if (audioCues != null) audioCues.Play(CombatSound.Heal, .6f); } }
            else if (!NavigateTo(BestSpot(RelayPositionGoal.Heal, player.position, null), .3f)) { Action = RelayAction.Reposition; Decision = "Heal target unreachable / retry safely"; }
        }

        void Defend(in RelayPercept p)
        {
            var threat = p.threat;
            Action = RelayAction.Defend; Decision = "Defend / visible threat endangers the player most";
            if (Vector3.Distance(AimPoint, threat.AimPoint) <= T.attackRange)
            { motor.Stop(); signals.Beam(AimPoint, threat.AimPoint, Color.cyan); signalOff = Time.time + .2f; threat.ReceiveDamage(T.attackDamage, transform); AttacksApplied++; attackReady = Time.time + attackCooldown; }
            else if (!NavigateTo(BestSpot(RelayPositionGoal.Defend, threat.transform.position, threat), .3f)) { Action = RelayAction.Reposition; Decision = "Firing position blocked / follow player"; motor.MoveTo(player.position, 3); }
        }

        void Follow(in RelayPercept p)
        {
            Action = RelayAction.Follow;
            Decision = playerVitals == null ? "Follow / waiting for player health adapter" : p.remembersThreat ? "Follow / recent threat remembered, actions cooling down" : "Follow / no visible threat";
            if (p.distanceToPlayer > T.followDistance) { if (!NavigateTo(BestSpot(RelayPositionGoal.Support, Formation(), null), .8f)) { Action = RelayAction.Reposition; Decision = "Follow position blocked / waiting to replan"; } }
            else motor.Stop();
            if (visual != null)
            {
                Vector3 aim = (p.threat != null ? p.threat.AimPoint : player.position + player.forward * 4) - visual.position; aim.y = 0;
                if (aim.sqrMagnitude > .01f) visual.rotation = Quaternion.LookRotation(aim);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Skybreak.Combat;
using Skybreak.Disruptor.Core;
using Skybreak.Movement;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    public enum ActionStatus { Running, Succeeded, Failed }

    /// <summary>
    /// Rift Disruptor: a hit-and-run phase drone (Student 4). Design: Docs/IS/RiftDisruptor/DESIGN.md.
    ///
    /// Architecture, top to bottom, run every <see cref="decisionInterval"/> seconds:
    ///   1. Perception      DisruptorPerception  sight, decaying memory, aim pressure (reads the player's crosshair)
    ///   2. Facts           this class           the live world state as a fact bit set
    ///   3. Goal            GoalSelector         utility insistence per goal, with hysteresis
    ///   4. Tactics         DisruptorTactics     scored strike/escape positions with threat-aware A* routes
    ///   5. Plan            GoapPlanner          A* over world states with situation-dependent action costs
    ///   6. Act             this class           runs one plan step per frame, checks preconditions, replans on failure
    /// Learning: DodgeLearner (Beta-Bernoulli) adapts the telegraph and strike distance to this player.
    ///
    /// Movement goes through <see cref="IPathFollower"/> only, and visuals read public state and events,
    /// so the decision logic never depends on a particular motor, model or animation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class DisruptorBrain : DamageSink, IDamageable
    {
        public static readonly List<DisruptorBrain> All = new List<DisruptorBrain>();

        [Header("Scene services")]
        public DisruptorNavGrid grid;
        [Tooltip("Optional. Found automatically (player DamageSink, Arc controller, then the Player tag).")]
        public Transform player;
        public DamageSink playerVitals;

        [Header("Body")]
        public float maximumHealth = 110;
        public float eyeHeight = 1.3f;

        [Header("Senses")]
        public float sightRange = 22;
        public float fieldOfView = 160;
        [Tooltip("Seconds for memory confidence to fall to 37 percent after losing sight.")]
        public float memoryTau = 2.5f;
        [Tooltip("Degrees around the player's crosshair that count as being aimed at.")]
        public float aimCone = 12;
        public LayerMask sightBlockers = ~(1 << 8);

        [Header("Threat-aware routing")]
        public float aimDangerCone = 30;
        [Tooltip("Extra cost per metre at the centre of the player's aim cone (1 = a metre there costs two).")]
        public float aimDangerWeight = 3;

        [Header("Movement")]
        public float approachSpeed = 3.4f;
        public float dashSpeed = 8f;
        public float patrolSpeed = 2.2f;

        [Header("Pulse")]
        public float pulseRadius = 4.6f;
        public float pulseDamage = 20;
        public float pulseCooldown = 4.5f;
        [Tooltip("Damage taken while charging that cancels the pulse (the player's counterplay).")]
        public float staggerDamage = 30;

        [Header("Thinking")]
        public float decisionInterval = 0.15f;
        public float replanInterval = 1.0f;
        public bool decisionsEnabled = true;

        // ------------------------------------------------------------------ state
        readonly DisruptorDomain domain = new DisruptorDomain();
        readonly DisruptorPerception senses = new DisruptorPerception();
        readonly DisruptorPulse pulse = new DisruptorPulse();
        readonly DodgeLearner learner = new DodgeLearner();
        readonly DisruptorTactics tactics = new DisruptorTactics();
        readonly double[] goalScores = new double[4];
        readonly bool[] infeasible = new bool[4];
        IPathFollower follower;
        const float MinReplanGap = 0.25f, MaxDashSeconds = 3f;
        float health, nextDecision, lastPlanAt = -100, actionStartedAt, lastDecisionAt, lastRepathAt;
        int stepIndex, pathVersion;
        bool actionStarted, replanRequested = true;
        Vector3 destination;
        bool hasDestination;

        // ------------------------------------------------------------------ telemetry (overlay, presentation, tests)
        public event Action<DisruptorBrain> PulseCharging, PulseReleased, PulseStaggered, Defeated;
        /// <summary>Raised for every hit taken (amount), before any stagger or defeat handling.</summary>
        public event Action<DisruptorBrain, float> Damaged;
        public DisruptorGoal? Goal { get; private set; }
        public GoapPlan Plan { get; private set; } = new GoapPlan();
        public int StepIndex => stepIndex;
        public GoapAction CurrentAction => Plan != null && Plan.Usable && stepIndex < Plan.Steps.Count ? Plan.Steps[stepIndex] : null;
        public DisruptorActionId? CurrentActionId => CurrentAction != null ? (DisruptorActionId?)CurrentAction.Id : null;
        public uint Facts { get; private set; }
        public IReadOnlyList<double> GoalScores => goalScores;
        public string Decision { get; private set; } = "Starting";
        public DisruptorPerception Senses => senses;
        public DisruptorPulse Pulse => pulse;
        public DodgeLearner Learner => learner;
        public DisruptorTactics Tactics => tactics;
        public IPathFollower Follower => follower;
        public Vector3 Home { get; private set; }
        public float StrikeDistance => (float)learner.StrikeFraction * pulseRadius;
        public float ChargeSeconds => (float)learner.ChargeSeconds;
        public bool Dashing => CurrentActionId == DisruptorActionId.PhaseDash;
        public int PulsesReleased { get; private set; }
        public int PulsesHit { get; private set; }
        public int PulsesStaggered { get; private set; }
        public int Replans { get; private set; }
        public bool LastReleaseHit { get; private set; }
        public float LastThinkMilliseconds { get; private set; }
        public Vector3? Destination => hasDestination ? destination : (Vector3?)null;

        public override Allegiance Team => Allegiance.Hostile;
        public override float Health => health;
        public override float MaximumHealth => maximumHealth;
        public override Vector3 AimPoint => transform.position + Vector3.up * eyeHeight;
        bool IDamageable.IsAlive => Alive;
        float IDamageable.HealthFraction => health / Mathf.Max(1, maximumHealth);

        void Awake()
        {
            health = maximumHealth;
            follower = GetComponent<IPathFollower>();
            if (follower == null) follower = gameObject.AddComponent<BasicPathFollower>();
            Home = transform.position;
        }

        protected override void OnEnable() { base.OnEnable(); if (!All.Contains(this)) All.Add(this); }
        protected override void OnDisable() { base.OnDisable(); All.Remove(this); }

        void Start()
        {
            if (grid == null) grid = FindAnyObjectByType<DisruptorNavGrid>();
            AcquirePlayer();
        }

        void AcquirePlayer()
        {
            if (player == null)
            {
                foreach (var sink in Active) if (sink != null && sink.Team == Allegiance.Player) { player = sink.transform; playerVitals = sink; break; }
                if (player == null) { var c = FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>(); if (c != null) player = c.transform; }
                if (player == null) { var c = FindAnyObjectByType<ArcController>(); if (c != null) player = c.transform; }
                if (player == null) { var go = GameObject.FindWithTag("Player"); if (go != null) player = go.transform; }
            }
            if (player != null && playerVitals == null) playerVitals = player.GetComponentInParent<DamageSink>();
            senses.Bind(player, playerVitals);
        }

        // ------------------------------------------------------------------ damage
        public override void ReceiveDamage(float amount, Transform source)
        {
            if (!Alive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            health = Mathf.Max(0, health - amount);
            if (source != null) senses.NotifyHit(source.position, Time.time);
            Damaged?.Invoke(this, amount);
            if (!Alive) { Die(); return; }
            if (pulse.AbsorbDamage(amount, staggerDamage))
            {
                pulse.Cancel(Time.time, pulseCooldown * 0.5f);
                PulsesStaggered++; Decision = "Staggered: pulse cancelled by damage";
                PulseStaggered?.Invoke(this);
                FailAction();
            }
            else if (!pulse.Busy) replanRequested = true;    // a hit is new information; reconsider
        }

        public bool ApplyDamage(float amount, GameObject source)
        {
            bool wasAlive = Alive;
            ReceiveDamage(amount, source != null ? source.transform : null);
            return wasAlive && !Alive;
        }

        void Die()
        {
            pulse.Cancel(Time.time, pulseCooldown);
            follower?.Stop(); ReleaseClaim();
            Plan = new GoapPlan(); Goal = null; Decision = "Disabled";
            Defeated?.Invoke(this);
        }

        /// <summary>Restores the starting state (lab reset).</summary>
        public void ResetAgent()
        {
            health = maximumHealth; pulse.ResetState(); learner.Reset(); senses.Forget(); tactics.ResetMemory();
            Plan = new GoapPlan(); Goal = null; stepIndex = 0; actionStarted = false; replanRequested = true; ReleaseClaim();
            PulsesReleased = PulsesHit = PulsesStaggered = Replans = 0; follower?.Stop(); Decision = "Reset";
        }

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            if (!Alive || !decisionsEnabled) return;
            float now = Time.time;
            pulse.Tick(now);
            if (player == null || (playerVitals == null && senses.Vitals == null && Time.frameCount % 60 == 0)) AcquirePlayer();
            if (grid == null || !grid.Ready) { Decision = "Waiting for DisruptorNavGrid"; return; }

            if (now >= nextDecision)
            {
                float started = Time.realtimeSinceStartup;
                nextDecision = now + decisionInterval;
                senses.Sense(transform, AimPoint, sightRange, fieldOfView, aimCone, sightBlockers, now, now - lastDecisionAt);
                lastDecisionAt = now;
                Facts = ReadFacts(now);
                Deliberate(now);
                LastThinkMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
            }
            TickAction(now);
        }

        uint ReadFacts(float now)
        {
            Fact f = Fact.None;
            bool known = senses.Visible || senses.Confidence(now, memoryTau) > 0.05f;
            if (known) f |= Fact.PlayerKnown;
            if (senses.Visible) f |= Fact.PlayerVisible;
            if (senses.Visible && Flat(senses.Player.position - transform.position).magnitude <= StrikeDistance + (float)AnchorScoring.RangeTolerance + 0.1f) f |= Fact.AtStrikePosition;
            if (pulse.Ready(now)) f |= Fact.PulseReady;
            if (pulse.Charged) f |= Fact.PulseCharged;
            if (!senses.PlayerHasLine || senses.AimAngle > 45) f |= Fact.Concealed;
            if (tactics.Strike.Found) f |= Fact.StrikeAnchorAvailable;
            if (tactics.Escape.Found) f |= Fact.EscapeAnchorAvailable;
            return (uint)f;
        }

        /// <summary>Decides whether the current plan still stands, and replans if not.</summary>
        void Deliberate(float now)
        {
            var context = new GoalContext
            {
                PlayerVisible = senses.Visible,
                MemoryConfidence = senses.Confidence(now, memoryTau),
                AimPressure = senses.AimPressure,
                Health01 = health / Mathf.Max(1, maximumHealth),
                PulseReady = pulse.Ready(now),
                PulseCooldown01 = pulse.Cooldown01(now),
                PlayerAlive = senses.PlayerAlive,
            };
            GoalSelector.Score(context, goalScores);
            var ranking = GoalSelector.Rank(goalScores, Goal);

            // Never abandon a telegraph halfway, and always finish the relocation that follows a pulse:
            // the hit-and-run rhythm is the point, and a half-dash leaves it standing where it just revealed itself.
            bool committed = (pulse.Busy && senses.PlayerAlive) ||
                             (CurrentActionId == DisruptorActionId.PhaseDash && actionStarted && now - actionStartedAt < MaxDashSeconds);
            var action = CurrentAction;
            string reason = null;
            if (replanRequested) reason = "requested";
            else if (action == null) reason = now - lastPlanAt > replanInterval * 0.5f ? (Plan.Status == PlanStatus.AlreadySatisfied ? "goal held, re-checking" : "no plan, retrying") : null;
            else if (!committed && ranking[0] != Goal && goalScores[(int)ranking[0]] > 0 && !infeasible[(int)ranking[0]]) reason = "goal changed to " + ranking[0];
            else if (!committed && !action.Preconditions.SatisfiedBy(Facts) && actionStarted == false) reason = "next step's preconditions no longer hold";
            else if (!committed && now - lastPlanAt > replanInterval && !IsMove(action)) reason = "periodic re-evaluation";
            else if (!committed && now - lastPlanAt > replanInterval * 1.5f) reason = "periodic re-evaluation";
            // Throttle: a failing world (no routes, no player) must not trigger tactics + planning every cycle.
            if (reason == null || now - lastPlanAt < MinReplanGap) return;

            Replan(now, ranking, reason);
        }

        static bool IsMove(GoapAction a) => a.Id != (int)DisruptorActionId.ChargePulse && a.Id != (int)DisruptorActionId.ReleasePulse;

        void Replan(float now, DisruptorGoal[] ranking, string reason)
        {
            replanRequested = false; lastPlanAt = now; Replans++;
            bool known = (Facts & (uint)Fact.PlayerKnown) != 0;
            tactics.Analyse(this, grid, senses, StrikeDistance, known && senses.PlayerAlive, now);
            Facts = ReadFacts(now);                                           // tactics changes the anchor facts
            domain.ApplyCosts(new CostInputs
            {
                StrikePathLength = tactics.Strike.Found ? tactics.Strike.Path.Length : double.PositiveInfinity,
                StrikeExposure = tactics.Strike.Exposure,
                CloseInDistance = tactics.CloseInDistance,
                EscapePathLength = tactics.Escape.Found ? tactics.Escape.Path.Length : double.PositiveInfinity,
                InvestigateLength = tactics.InvestigatePath != null && tactics.InvestigatePath.HasRoute ? tactics.InvestigatePath.Length : double.PositiveInfinity,
                PatrolLength = tactics.PatrolPath != null && tactics.PatrolPath.HasRoute ? tactics.PatrolPath.Length : double.PositiveInfinity,
                ChargeSeconds = learner.ChargeSeconds,
                DodgeProbability = learner.Mean,
                ApproachSpeed = approachSpeed, DashSpeed = dashSpeed, PatrolSpeed = patrolSpeed,
            });

            // Try goals best-first; the first one the planner can achieve wins. Disrupt is tried as
            // "hit and run" first, then as a plain strike if no escape route exists.
            GoapPlan chosen = null; DisruptorGoal chosenGoal = DisruptorGoal.Guard;
            Array.Clear(infeasible, 0, infeasible.Length);
            foreach (var g in ranking)
            {
                if (goalScores[(int)g] <= 0) continue;
                var p = GoapPlanner.Plan(Facts, DisruptorDomain.GoalFacts(g), domain.Actions);
                if (!p.Usable && g == DisruptorGoal.Disrupt) p = GoapPlanner.Plan(Facts, DisruptorDomain.GoalFacts(g, false), domain.Actions);
                if (p.Usable) { chosen = p; chosenGoal = g; break; }
                infeasible[(int)g] = true;   // don't treat it as a "goal change" again until the periodic re-check
            }
            if (Goal != chosenGoal && pulse.Busy) pulse.Cancel(now, pulseCooldown * 0.5f);
            Goal = chosen != null ? chosenGoal : (DisruptorGoal?)null;
            Plan = chosen ?? new GoapPlan { Status = PlanStatus.NoPlan };
            stepIndex = 0; actionStarted = false;
            if (Plan.Steps.Count == 0) { follower.Stop(); ReleaseClaim(); if (senses.Visible) follower.Face(senses.Player.position); }
            Decision = $"{(Goal.HasValue ? Goal.Value.ToString() : "No goal")} ({reason}): {Plan}";
        }

        // ------------------------------------------------------------------ acting
        void TickAction(float now)
        {
            var action = CurrentAction;
            if (action == null) return;
            if (!actionStarted)
            {
                if (!action.Preconditions.SatisfiedBy(Facts | Expected()))
                { replanRequested = true; return; }
                actionStarted = true; actionStartedAt = now;
                if (!StartAction((DisruptorActionId)action.Id, now)) { FailAction(); return; }
            }
            var status = RunAction((DisruptorActionId)action.Id, now);
            if (status == ActionStatus.Failed) FailAction();
            else if (status == ActionStatus.Succeeded)
            {
                stepIndex++; actionStarted = false;
                if (stepIndex >= Plan.Steps.Count) { ReleaseClaim(); replanRequested = true; }
            }
        }

        // Effects the plan has already produced this run but which the sensors may not show yet
        // (for example PulseCharged in the same frame the charge completes).
        uint Expected()
        {
            uint s = Facts;
            for (int i = 0; i < stepIndex && i < Plan.Steps.Count; i++) s = Plan.Steps[i].Effects.ApplyTo(s);
            return s & (uint)Fact.PulseCharged;
        }

        void FailAction()
        {
            follower?.Stop();
            ReleaseClaim();
            actionStarted = false; replanRequested = true;
            Plan = new GoapPlan { Status = PlanStatus.NoPlan };
        }

        bool StartAction(DisruptorActionId id, float now)
        {
            switch (id)
            {
                case DisruptorActionId.MoveToStrike: return Go(tactics.Strike, approachSpeed, now);
                case DisruptorActionId.PhaseDash: return Go(tactics.Escape, dashSpeed, now);
                case DisruptorActionId.Investigate: return Follow(tactics.InvestigatePath, senses.LastKnown, approachSpeed);
                case DisruptorActionId.Patrol: return Follow(tactics.PatrolPath, tactics.PatrolPoint, patrolSpeed);
                case DisruptorActionId.CloseIn:
                    lastRepathAt = now;
                    return Follow(grid.FindPath(transform.position, senses.Player.position), senses.Player.position, approachSpeed);
                case DisruptorActionId.ChargePulse:
                    follower.Stop(); ReleaseClaim();
                    tactics.MarkUsed(transform.position, now);   // novelty: the next strike should come from elsewhere
                    pulse.Begin(now, ChargeSeconds);
                    pulse.PlayerInsideAtStart = PlayerInsideRing();
                    PulseCharging?.Invoke(this);
                    return true;
                case DisruptorActionId.ReleasePulse: return true;
            }
            return false;
        }

        ActionStatus RunAction(DisruptorActionId id, float now)
        {
            switch (id)
            {
                case DisruptorActionId.ChargePulse:
                    if (!senses.PlayerAlive) { pulse.Cancel(now, pulseCooldown * 0.5f); return ActionStatus.Failed; }
                    follower.Face(senses.Player.position);
                    if (pulse.Charged) return ActionStatus.Succeeded;
                    return pulse.Busy ? ActionStatus.Running : ActionStatus.Failed;   // cancelled by a stagger

                case DisruptorActionId.ReleasePulse:
                    ResolvePulse(now);
                    return ActionStatus.Succeeded;

                case DisruptorActionId.CloseIn:
                    if ((Facts & (uint)Fact.AtStrikePosition) != 0) { follower.Stop(); return ActionStatus.Succeeded; }
                    if (now - actionStartedAt > 6 || !senses.Visible && senses.Confidence(now, memoryTau) < 0.3f) return ActionStatus.Failed;
                    if (Flat(senses.Player.position - destination).magnitude > 1 && now - lastRepathAt > 0.4f)
                    { lastRepathAt = now; if (!Follow(grid.FindPath(transform.position, senses.Player.position), senses.Player.position, approachSpeed)) return ActionStatus.Failed; }
                    return Movement(now, false);

                case DisruptorActionId.MoveToStrike:
                    // Opportunistic: already in range and out of the crosshair? Strike now.
                    if ((Facts & (uint)(Fact.AtStrikePosition | Fact.Concealed)) == (uint)(Fact.AtStrikePosition | Fact.Concealed)) { follower.Stop(); return ActionStatus.Succeeded; }
                    return Movement(now, true);

                case DisruptorActionId.Investigate:
                    if (senses.Visible) { follower.Stop(); return ActionStatus.Succeeded; }
                    var r = Movement(now, false);
                    if (r == ActionStatus.Succeeded) { senses.Forget(); Decision = "Investigated: nobody there, memory cleared"; return ActionStatus.Failed; }
                    return r;

                case DisruptorActionId.Patrol:
                    var p = Movement(now, false);
                    if (p == ActionStatus.Succeeded) tactics.AdvancePatrol();
                    return p;

                case DisruptorActionId.PhaseDash:
                    return Movement(now, true);
            }
            return ActionStatus.Failed;
        }

        /// <summary>Shared movement monitoring: arrival, blockage, invalid routes and map changes.</summary>
        ActionStatus Movement(float now, bool blacklistOnFailure)
        {
            if (grid.Version != pathVersion && hasDestination)
            {
                // A door or barricade changed the map under us: replan the same leg, or give up on it.
                var again = grid.FindPath(transform.position, destination);
                if (again.Status != PathStatus.Complete) { if (blacklistOnFailure) tactics.Blacklist(destination, now); Decision = "Route closed by a map change"; return ActionStatus.Failed; }
                follower.SetPath(again, CurrentSpeed()); pathVersion = again.NavigationVersion;
            }
            switch (follower.Report)
            {
                case MotorReport.Arrived: return ActionStatus.Succeeded;
                case MotorReport.Blocked:
                case MotorReport.Invalid:
                    if (blacklistOnFailure && hasDestination) tactics.Blacklist(destination, now);
                    Decision = "Movement " + follower.Report + ": replanning";
                    return ActionStatus.Failed;
                case MotorReport.Stopped:
                case MotorReport.Idle: return ActionStatus.Failed;
            }
            return ActionStatus.Running;
        }

        float CurrentSpeed()
        {
            switch (CurrentActionId)
            {
                case DisruptorActionId.PhaseDash: return dashSpeed;
                case DisruptorActionId.Patrol: return patrolSpeed;
                default: return approachSpeed;
            }
        }

        bool Go(TacticalChoice choice, float speed, float now)
        {
            if (!choice.Found) return false;
            // The choice may be a second or two old (a dash follows the charge): another Disruptor may
            // have claimed the spot since. Fail the step so the replan picks another.
            if (ClaimedByOther(choice.Point, DisruptorTactics.ClaimRadius)) { Decision = "Spot taken by another Disruptor"; return false; }
            // A re-evaluation that picked the same spot keeps the current route instead of restarting it.
            if (hasDestination && follower.Report == MotorReport.Moving && Flat(destination - choice.Point).magnitude < 1f &&
                follower.CurrentPath != null && follower.CurrentPath.NavigationVersion == grid.Version)
                return true;
            // Reuse the route found during tactics if the map has not changed and it still starts here
            // (a dash after a pulse starts a second or two after the tactics ran).
            bool reusable = choice.Path != null && choice.Path.NavigationVersion == grid.Version && choice.Path.HasRoute &&
                            Flat(choice.Path.Waypoints[0] - transform.position).magnitude < 1f;
            var path = reusable ? choice.Path : grid.FindPath(transform.position, choice.Point);
            if (path.Status != PathStatus.Complete) { tactics.Blacklist(choice.Point, now); return false; }
            return Follow(path, choice.Point, speed);
        }

        bool Follow(PathResult path, Vector3 target, float speed)
        {
            if (path == null || !path.HasRoute) return false;
            destination = target; hasDestination = true; pathVersion = path.NavigationVersion;
            follower.SetPath(path, speed);
            return follower.Report == MotorReport.Moving;
        }

        void ReleaseClaim() { hasDestination = false; }

        public bool ClaimedByOther(Vector3 point, float radius)
        {
            foreach (var other in All)
                if (other != null && other != this && other.Alive && other.hasDestination && Flat(other.destination - point).magnitude < radius) return true;
            return false;
        }

        // ------------------------------------------------------------------ pulse resolution
        bool PlayerInsideRing()
        {
            if (senses.Player == null) return false;
            Vector3 d = senses.Player.position - transform.position;
            return Flat(d).magnitude <= pulseRadius && Mathf.Abs(d.y) < 2.2f;
        }

        void ResolvePulse(float now)
        {
            if (!pulse.Release(now, pulseCooldown)) return;     // already resolved: never damage twice
            PulsesReleased++;
            Vector3 chest = senses.PlayerChest;
            Vector3 core = AimPoint;
            bool shielded = Physics.Raycast(core, (chest - core).normalized, Vector3.Distance(core, chest) - 0.2f, sightBlockers, QueryTriggerInteraction.Ignore);
            bool hit = senses.PlayerAlive && PlayerInsideRing() && !shielded;
            LastReleaseHit = hit;
            if (hit)
            {
                PulsesHit++;
                if (senses.Vitals != null) senses.Vitals.ReceiveDamage(pulseDamage, transform);
                else senses.AltVitals?.ApplyDamage(pulseDamage, gameObject);
            }
            // Learn only from pulses the player had to react to: they started inside the ring.
            if (pulse.PlayerInsideAtStart) learner.Record(!hit);
            Decision = hit ? "Pulse hit" : pulse.PlayerInsideAtStart ? "Pulse dodged" : "Pulse missed";
            PulseReleased?.Invoke(this);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (follower?.CurrentPath != null && follower.CurrentPath.HasRoute)
            {
                var w = follower.CurrentPath.Waypoints;
                Gizmos.color = Dashing ? Color.magenta : Color.cyan;
                Vector3 prev = transform.position + Vector3.up * .1f;
                for (int i = Mathf.Max(0, follower.WaypointIndex); i < w.Length; i++) { Gizmos.DrawLine(prev, w[i] + Vector3.up * .1f); prev = w[i] + Vector3.up * .1f; }
            }
            if (tactics.Strike.Found) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(tactics.Strike.Point + Vector3.up * .2f, .4f); }
            if (tactics.Escape.Found) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(tactics.Escape.Point + Vector3.up * .2f, .4f); }
        }
    }
}

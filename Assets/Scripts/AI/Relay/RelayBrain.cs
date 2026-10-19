using Skybreak.Combat;
using Skybreak.Movement;
using UnityEngine;

namespace Skybreak.AI.Relay
{
    public enum RelayAction { Follow, Heal, Defend, Retreat, Reposition, Disabled }

    // Relay's agent program (Docs/IS/AgentDesign/Relay/RELAY_DESIGN.md): a layered (subsumption) agent.
    //   Layer 0  RelayReflexes   - condition-action rules that override everything (checked every frame)
    //   Layer 1  RelayPerception - sensors + internal state (threat memory) for a partially observable world
    //   Layer 3  RelayUtility    - utility scores per action and the selection rule
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
        public void ResetSupport() { ApplyTuning(true); health = maximumHealth; nextSense = healReady = attackReady = 0; perception.Forget(); Action = RelayAction.Follow; signals.Clear(); }

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

        // One deliberative cycle: sense (layer 1), score and select (layer 3), then act.
        public void SenseAndDecide()
        {
            ApplyTuning(false);
            var percept = perception.Sense(this, player, playerVitals, T, Time.time);
            var scores = RelayUtility.Score(percept, T, playerVitals != null && playerVitals.Alive, Time.time >= healReady, Time.time >= attackReady);
            FollowScore = scores.follow; HealScore = scores.heal; DefendScore = scores.defend; RetreatScore = scores.retreat;
            switch (RelayUtility.Select(scores, percept.threat != null))
            {
                case RelayAction.Retreat: Retreat(percept); break;
                case RelayAction.Heal: Heal(percept); break;
                case RelayAction.Defend: Defend(percept); break;
                default: Follow(percept); break;
            }
        }

        // ---------------------------------------------------------------- actuators
        void Retreat(in RelayPercept p)
        {
            Action = RelayAction.Retreat; Decision = "Own health below retreat threshold + recent threat";
            Vector3 away = transform.position - p.threatMemory; away.y = 0;
            if (away.sqrMagnitude < .1f) away = -player.forward;
            if (!motor.MoveTo(transform.position + away.normalized * 4, .5f)) { Action = RelayAction.Reposition; Decision = "Retreat blocked / seek player-side safe position"; motor.MoveTo(player.position - player.forward * 2, 1); }
        }

        void Heal(in RelayPercept p)
        {
            Action = RelayAction.Heal; Decision = "Player health need exceeds threshold / healing available";
            if (p.distanceToPlayer <= T.healRange && Visible(AimPoint, playerVitals, transform))
            { motor.Stop(); if (playerVitals.RestoreHealth(healAmount) > 0) { HealsApplied++; healReady = Time.time + healCooldown; signals.Beam(AimPoint, playerVitals.AimPoint, Color.cyan); signalOff = Time.time + .45f; if (audioCues != null) audioCues.Play(CombatSound.Heal, .6f); } }
            else if (!motor.MoveTo(player.position, Mathf.Min(2.2f, T.healRange * .75f))) { Action = RelayAction.Reposition; Decision = "Heal target unreachable / retry safely"; }
        }

        void Defend(in RelayPercept p)
        {
            var threat = p.threat;
            Action = RelayAction.Defend; Decision = "Visible threat near player / defensive burst available";
            if (Vector3.Distance(AimPoint, threat.AimPoint) <= T.attackRange)
            { motor.Stop(); signals.Beam(AimPoint, threat.AimPoint, Color.cyan); signalOff = Time.time + .2f; threat.ReceiveDamage(T.attackDamage, transform); AttacksApplied++; attackReady = Time.time + attackCooldown; }
            else if (!motor.MoveTo(threat.transform.position, Mathf.Min(8, T.attackRange * .75f))) { Action = RelayAction.Reposition; Decision = "Firing position blocked / follow player"; motor.MoveTo(player.position, 3); }
        }

        void Follow(in RelayPercept p)
        {
            Action = RelayAction.Follow;
            Decision = playerVitals == null ? "Follow / waiting for player health adapter" : p.remembersThreat ? "Follow / recent threat remembered, actions cooling down" : "Follow / no visible threat";
            if (p.distanceToPlayer > T.followDistance) { Vector3 formation = (-player.forward * 2 + player.right * 1.5f) * Mathf.Min(1, T.followDistance / 3.3f); if (!motor.MoveTo(player.position + formation, .8f)) { Action = RelayAction.Reposition; Decision = "Follow position blocked / waiting to replan"; } }
            else motor.Stop();
            if (visual != null)
            {
                Vector3 aim = (p.threat != null ? p.threat.AimPoint : player.position + player.forward * 4) - visual.position; aim.y = 0;
                if (aim.sqrMagnitude > .01f) visual.rotation = Quaternion.LookRotation(aim);
            }
        }
    }
}

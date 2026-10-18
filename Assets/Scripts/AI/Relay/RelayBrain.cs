using Skybreak.Combat;
using Skybreak.Movement;
using Skybreak.AI.RiftWarden;
using UnityEngine;

namespace Skybreak.AI.Relay
{
    public enum RelayAction { Follow, Heal, Defend, Retreat, Reposition, Disabled }
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
        float health, nextSense, healReady, attackReady, signalOff, lastSeenAt = -100;
        DamageSink threat;
        Vector3 threatMemory;
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
        public void ResetSupport() { ApplyTuning(true); health = maximumHealth; nextSense = healReady = attackReady = 0; threat = null; lastSeenAt = -100; Action = RelayAction.Follow; signals.Clear(); }
        void Update()
        {
            if (visual != null) visual.localPosition = new Vector3(0, 1.7f + Mathf.Sin(Time.time * 2.5f) * .045f, 0);
            if (!Alive || !decisionsEnabled || player == null) return;
            if (playerVitals == null) playerVitals = player.GetComponent<DamageSink>();
            if (playerVitals != null && !playerVitals.Alive) { motor.Stop(); Decision = "Player down / waiting for restart"; return; }
            if (Time.time > signalOff) signals.Clear();
            if (Time.time < nextSense) return;
            nextSense = Time.time + T.senseInterval; SenseAndDecide();
        }
        public void SenseAndDecide()
        {
            ApplyTuning(false); threat = null; float bestThreat = -1;
            foreach (var candidate in Active)
            {
                if (candidate == null || !candidate.Alive || candidate.Team != Allegiance.Hostile) continue;
                var part = candidate as RiftDamagePart; if (part != null && !part.Vulnerable) continue;
                float range = Vector3.Distance(AimPoint, candidate.AimPoint);
                if (range > T.senseRange || !Visible(AimPoint, candidate, transform)) continue;
                float danger = 1 - Mathf.Clamp01(Vector3.Distance(player.position, candidate.transform.position) / 18);
                if (danger > bestThreat) { bestThreat = danger; threat = candidate; }
            }
            if (threat != null) { threatMemory = threat.AimPoint; lastSeenAt = Time.time; }
            bool remembersThreat = Time.time - lastSeenAt < T.memorySeconds;
            float need = playerVitals != null ? 1 - playerVitals.Health / playerVitals.MaximumHealth : 0;
            float distance = Vector3.Distance(transform.position, player.position);
            FollowScore = T.followBase + Mathf.Clamp01(distance / 9) * T.followWeight;
            HealScore = playerVitals != null && playerVitals.Alive && need >= T.healThreshold && Time.time >= healReady ? T.healBase + need * T.healWeight : -1;
            DefendScore = threat != null && Time.time >= attackReady ? T.defendBase + bestThreat * T.defendWeight : -1;
            RetreatScore = health / maximumHealth < T.retreatThreshold && remembersThreat ? T.retreatUtility : -1;
            if (RetreatScore >= Mathf.Max(HealScore, DefendScore, FollowScore))
            {
                Action = RelayAction.Retreat; Decision = "Own health below retreat threshold + recent threat";
                Vector3 away = transform.position - threatMemory; away.y = 0;
                if (away.sqrMagnitude < .1f) away = -player.forward;
                if (!motor.MoveTo(transform.position + away.normalized * 4, .5f)) { Action = RelayAction.Reposition; Decision = "Retreat blocked / seek player-side safe position"; motor.MoveTo(player.position - player.forward * 2, 1); }
                return;
            }
            if (HealScore >= Mathf.Max(DefendScore, FollowScore))
            {
                Action = RelayAction.Heal; Decision = "Player health need exceeds threshold / healing available";
                if (distance <= T.healRange && Visible(AimPoint, playerVitals, transform))
                { motor.Stop(); if (playerVitals.RestoreHealth(healAmount) > 0) { HealsApplied++; healReady = Time.time + healCooldown; signals.Beam(AimPoint, playerVitals.AimPoint, Color.cyan); signalOff = Time.time + .45f; if (audioCues != null) audioCues.Play(CombatSound.Heal, .6f); } }
                else if (!motor.MoveTo(player.position, Mathf.Min(2.2f, T.healRange * .75f))) { Action = RelayAction.Reposition; Decision = "Heal target unreachable / retry safely"; }
                return;
            }
            if (DefendScore >= FollowScore && threat != null)
            {
                Action = RelayAction.Defend; Decision = "Visible threat near player / defensive burst available";
                if (Vector3.Distance(AimPoint, threat.AimPoint) <= T.attackRange)
                { motor.Stop(); signals.Beam(AimPoint, threat.AimPoint, Color.cyan); signalOff = Time.time + .2f; threat.ReceiveDamage(T.attackDamage, transform); AttacksApplied++; attackReady = Time.time + attackCooldown; }
                else if (!motor.MoveTo(threat.transform.position, Mathf.Min(8, T.attackRange * .75f))) { Action = RelayAction.Reposition; Decision = "Firing position blocked / follow player"; motor.MoveTo(player.position, 3); }
                return;
            }
            Action = RelayAction.Follow;
            Decision = playerVitals == null ? "Follow / waiting for player health adapter" : remembersThreat ? "Follow / recent threat remembered, actions cooling down" : "Follow / no visible threat";
            if (distance > T.followDistance) { Vector3 formation = (-player.forward * 2 + player.right * 1.5f) * Mathf.Min(1, T.followDistance / 3.3f); if (!motor.MoveTo(player.position + formation, .8f)) { Action = RelayAction.Reposition; Decision = "Follow position blocked / waiting to replan"; } }
            else motor.Stop();
            if (visual != null)
            {
                Vector3 aim = (threat != null ? threat.AimPoint : player.position + player.forward * 4) - visual.position; aim.y = 0;
                if (aim.sqrMagnitude > .01f) visual.rotation = Quaternion.LookRotation(aim);
            }
        }
    }
}

using System;
using Skybreak.AI.Algorithms.Bandit;
using Skybreak.AI.Algorithms.Prediction;
using Skybreak.Combat;
using Skybreak.Movement;
using Skybreak.Presentation;
using Skybreak.World;
using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.AI.RiftWarden
{
    public enum RiftAction { Dormant, Pursue, Cannon, Slash, DoubleTeleport, BreakLeft, BreakRight, CoreOpen, CoreTeleport, Slam, Recover, Dying, Defeated }
    // The Rift Warden's agent program (Docs/IS/AgentDesign/RiftWarden/RIFT_WARDEN_AI_DESIGN.md).
    //   RiftPerception - sight and memory of the player (layer 1)
    //   RiftPlayerModel - this player's velocity, range, strafe habit and answers to each attack (layer 1)
    //   predictive aim  - cannon and slam lead the target by its smoothed velocity, capped for fairness (layer 2)
    //   RiftAttackLearner - Beta belief per attack and range band about hitting this player; Thompson sampling (layer 3)
    //   RiftComboPlanner - depth-2 expectimax over the player's learned answers: picks the move with the best line (layer 4)
    //   RiftDirector     - phase temperament and fairness (mercy / pressure); tactics: shield the weak arm, punish the
    //                      healer, cut off the player's escape (layer 5)
    //   RiftUtility    - attack scores and the selection rule (decision layer)
    //   this class     - orchestration and execution: the attack state machine with its animation-timed markers
    public sealed class RiftWardenBrain : MonoBehaviour
    {
        public Transform player;
        public DamageSink playerVitals;
        public NavMeshMotor motor;
        public TimedClipPresenter presenter;
        public CombatSignals signals;
        public CombatAudio audioCues;
        public RiftWeaponFeedback weaponFeedback;
        public SkinnedMeshRenderer leftArm, rightArm;
        public RiftDamagePart leftTarget, rightTarget, coreTarget;
        public FinalTeleportVisual returnPoint;
        public Vector3 arenaCentre = new Vector3(42, 0, 84.5f);
        public float arenaRadius = 12, armMaximum = 180, coreMaximum = 260, activationDistance = 19;
        public bool autoActivate = true, decisionsEnabled = true;
        public RiftAction Action { get; private set; } = RiftAction.Dormant;
        public string Decision { get; private set; } = "Waiting for player in arena";
        public float LeftHealth { get; private set; }
        public float RightHealth { get; private set; }
        public float CoreHealth { get; private set; }
        public bool CoreExposed { get; private set; }
        public bool Defeated => Action == RiftAction.Defeated;
        public int ShotsFired { get; private set; }
        public int DamageWindows { get; private set; }
        public int SuccessfulTeleports { get; private set; }
        public float LastRearArrivalTime { get; private set; }
        public event Action BossDefeated;
        WardenTuning T => AgentTuning.Current.warden;
        readonly RiftPerception perception = new RiftPerception();
        readonly RiftPlayerModel model = new RiftPlayerModel();
        public RiftPlayerModel PlayerModel => model;
        [Tooltip("Layer 2 lead factor: 0 aims at the player's current position, 1 at the full constant-velocity prediction.")]
        [Range(0, 1)] public float leadFactor = .65f;
        public const float MaxLead = 2.5f, SlamLeadSeconds = .6f;
        Vector3 lockPosition, lockVelocity;
        float lockHeight = 1.2f;                       // aim height above the target's feet (player chest, or Relay's hover body)
        readonly RiftAttackLearner learner = new RiftAttackLearner();
        public RiftAttackLearner Learner => learner;
        System.Random rng = new System.Random(3062);   // reseeded on reset: the same fight state gives the same draws
        RiftRangeBand attackBand;                      // range band when the current attack started
        readonly RiftComboPlanner planner = new RiftComboPlanner();
        public RiftComboPlanner Planner => planner;
        readonly RiftDirector director = new RiftDirector();
        public RiftDirector Director => director;
        [Tooltip("Let the director set the lead factor, cooldowns, recovery and planning depth. Off = fixed values (tests).")]
        public bool directorEnabled = true;
        public const float HealerWindow = 6, HealerRearm = 12, ShieldBelow = .5f, ShieldAngle = 25, CutOffDistance = 2;
        Skybreak.AI.Relay.RelayBrain healer;
        float healerPunishReady;
        Transform cannonTarget;                        // the player, or Relay when the boss punishes the healer
        public int HealerPunishments { get; private set; }
        // Damage of one successful trial, as a percentage of the player's health (from the tuning): one cannon shot, or one melee hit.
        float TrialDamage(RiftAttackKind kind) => (kind == RiftAttackKind.Cannon ? T.cannonDamageFraction : T.meleeDamageFraction) * 100;
        // Trials per attack: a cannon burst is three shots.
        static int Trials(RiftAttackKind kind) => kind == RiftAttackKind.Cannon ? 3 : 1;
        void ApplyHealthTuning() { armMaximum = T.armHealth; coreMaximum = T.coreHealth; }
        float clock, previous, nextDecision, cannonReady, bladeReady, teleportReady, slamReady, secondRearAt;
        Vector3 lockedAim, warnedLanding;
        bool landingWarned;
        bool active, leftDetached, rightDetached, notified;
        readonly Collider[] overlaps = new Collider[24];
        readonly RaycastHit[] shotHits = new RaycastHit[24];
        readonly DamageSink[] damaged = new DamageSink[24];
        int damagedCount;
        public float PartHealth(RiftPart part) => part == RiftPart.LeftArm ? LeftHealth : part == RiftPart.RightArm ? RightHealth : CoreHealth;
        public bool CanDamage(RiftPart part) => active && Action != RiftAction.Dying && !Defeated && (part != RiftPart.Core || CoreExposed) && PartHealth(part) > 0;
        void Start()
        {
            if (audioCues == null) audioCues = GetComponent<CombatAudio>();
            if (weaponFeedback == null) weaponFeedback = GetComponent<RiftWeaponFeedback>();
            ApplyHealthTuning(); LeftHealth = armMaximum; RightHealth = armMaximum; CoreHealth = coreMaximum;
            if (player == null) { var arc = FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>(); if (arc != null) player = arc.transform; }
            if (player != null && playerVitals == null) playerVitals = player.GetComponent<DamageSink>();
            healer = FindAnyObjectByType<Skybreak.AI.Relay.RelayBrain>();
            presenter.Play("RW_Idle", true); motor.Stop(); if (returnPoint != null) returnPoint.SetPowered(false);
        }
        public void Activate() { if (active || Defeated) return; active = true; Enter(RiftAction.Pursue); }
        public void DamagePart(RiftPart part, float amount)
        {
            if (!CanDamage(part) || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            model.ObservePartDamage(part, Mathf.Min(amount, PartHealth(part)), Time.time);   // damage actually taken, not overkill
            if (part == RiftPart.LeftArm) LeftHealth = Mathf.Max(0, LeftHealth - amount);
            if (part == RiftPart.RightArm) RightHealth = Mathf.Max(0, RightHealth - amount);
            if (part == RiftPart.Core) CoreHealth = Mathf.Max(0, CoreHealth - amount);
            if (CoreHealth <= 0) Enter(RiftAction.Dying);
            else if (part != RiftPart.Core && PartHealth(part) <= 0)
            {
                // Interrupt immediately; the old action can no longer apply damage.
                Enter(part == RiftPart.LeftArm ? RiftAction.BreakLeft : RiftAction.BreakRight);
            }
        }
        public void ResetEncounter()
        {
            active = leftDetached = rightDetached = notified = CoreExposed = false;
            ApplyHealthTuning(); LeftHealth = armMaximum; RightHealth = armMaximum; CoreHealth = coreMaximum;
            leftArm.enabled = rightArm.enabled = true; leftTarget.GetComponent<Collider>().enabled = rightTarget.GetComponent<Collider>().enabled = coreTarget.GetComponent<Collider>().enabled = true;
            cannonReady = bladeReady = teleportReady = slamReady = 0; Action = RiftAction.Dormant; clock = 0; perception.Reset(); model.Reset(); learner.Reset(); rng = new System.Random(3062);
            director.Reset(); healerPunishReady = 0; HealerPunishments = 0; cannonTarget = null;
            signals.Clear(); motor.Stop(); presenter.Play("RW_Idle", true); if (returnPoint != null) returnPoint.SetPowered(false);
        }
        void Update()
        {
            if (player == null) return;
            if (playerVitals == null) playerVitals = player.GetComponent<DamageSink>();
            float dt = Time.deltaTime;
            if (!active && autoActivate && Planar(player.position, arenaCentre) < activationDistance && Planar(player.position, arenaCentre) < arenaRadius + 2) Activate();
            if (!active) { presenter.Advance(dt); return; }
            if (playerVitals != null && !playerVitals.Alive && Action != RiftAction.Dying && !Defeated)
            { motor.Stop(); signals.Clear(); return; }
            bool visible = perception.Sense(transform, player, Time.time, T.perceptionInterval);
            if (visible) model.Observe(player.position, transform.position, dt, Time.time);
            if (playerVitals != null) model.ObserveHealth(playerVitals.Health, Time.time);
            if (directorEnabled)
            {
                float playerHealth01 = playerVitals != null ? playerVitals.Health / Mathf.Max(1, playerVitals.MaximumHealth) : 1;
                float armLoss = Mathf.Max(model.PartPressure(RiftPart.LeftArm, Time.time), model.PartPressure(RiftPart.RightArm, Time.time)) / Mathf.Max(1, armMaximum);
                director.Update(LeftHealth > 0, RightHealth > 0, CoreExposed, playerHealth01, armLoss, Time.time, dt);
                leadFactor = director.LeadFactor; planner.riskWeight = director.RiskWeight; planner.depth = director.PlanDepth;
            }
            previous = clock; clock += dt; presenter.Advance(dt);
            switch (Action)
            {
                case RiftAction.Pursue:
                    if (decisionsEnabled && Time.time >= nextDecision) { nextDecision = Time.time + T.decisionInterval; Choose(visible); }
                    if (Action == RiftAction.Pursue)
                    {
                        string clip = motor.Agent.velocity.sqrMagnitude > .04f ? (CoreExposed ? "RW_CoreWalk" : "RW_Walk") : (CoreExposed ? "RW_CoreIdle" : "RW_Idle");
                        if (!presenter.CurrentClip.EndsWith(clip)) presenter.Play(clip, true);
                    }
                    break;
                case RiftAction.Cannon:
                    var aimAt = cannonTarget != null ? cannonTarget : player;
                    if (clock < .4f) Face(aimAt.position, dt);
                    if (Mark(.4f))
                    {
                        lockHeight = aimAt == player ? 1.2f : (healer != null && aimAt == healer.transform ? healer.AimPoint.y - aimAt.position.y : 1.2f);
                        lockedAim = aimAt.position + Vector3.up * lockHeight; lockPosition = aimAt.position; lockVelocity = aimAt == player ? model.Velocity : Vector3.zero;
                    }
                    if (Mark(17f / 30)) { if (aimAt == player) model.EndResponse(RiftAttackKind.Cannon, player.position); FireCannon(17f / 30 - .4f); }
                    if (Mark(26f / 30)) FireCannon(26f / 30 - .4f);
                    if (Mark(35f / 30)) FireCannon(35f / 30 - .4f);
                    if (clock >= 1.8f) Enter(RiftAction.Recover);
                    break;
                case RiftAction.Slash:
                    if (Mark(20f / 30)) { model.EndResponse(RiftAttackKind.Slash, player.position); if (RightHealth > 0) learner.Record(RiftAttackKind.Slash, attackBand, SlashDamage(2.7f)); }
                    if (clock >= 1.767f) Enter(RiftAction.Recover);
                    break;
                case RiftAction.DoubleTeleport:
                    if (Mark(.18f))
                    {
                        if (!TeleportBehind()) { Enter(RiftAction.Recover); Decision = "Exact rear landing blocked / cancelled"; break; }
                        secondRearAt = clock + T.rearDelay;
                        Decision = "Close rear arrival / turn before second arrival";
                    }
                    // Retime the existing pose: hold its teleport preparation until the second arrival.
                    if (clock >= .18f && clock < secondRearAt) presenter.Seek(17f / 30 + (clock - (secondRearAt - T.rearDelay)) / T.rearDelay * .6f);
                    if (Mark(secondRearAt))
                    {
                        if (!TeleportBehind()) { Enter(RiftAction.Recover); Decision = "New rear landing blocked / no strike"; break; }
                        presenter.Seek(56f / 30); model.EndResponse(RiftAttackKind.DoubleTeleport, player.position); if (RightHealth > 0) learner.Record(RiftAttackKind.DoubleTeleport, attackBand, SlashDamage(2.15f));
                        Decision = "Second close rear arrival / single immediate strike";
                    }
                    if (clock >= secondRearAt + 1.14f) Enter(RiftAction.Recover);
                    break;
                case RiftAction.BreakLeft:
                case RiftAction.BreakRight:
                    if (Mark(.3f)) Detach(Action == RiftAction.BreakLeft);
                    if (clock >= 1.4f) AfterBreak();
                    break;
                case RiftAction.CoreOpen:
                    if (Mark(1.5f)) { CoreExposed = true; if (audioCues != null) audioCues.Play(CombatSound.CoreOpen); }
                    if (clock >= 2) Enter(RiftAction.Pursue);
                    break;
                case RiftAction.CoreTeleport:
                    if (Mark(7f / 30)) LandingWarning(1);
                    if (Mark(20f / 30)) { Teleport(1); presenter.Play("RW_CoreTeleportIn"); }
                    if (clock >= 1.55f) Enter(RiftAction.Slam);
                    break;
                case RiftAction.Slam:
                    if (clock < 38f / 30) lockedAim = GroundPoint(Lead(player.position, model.Velocity, Mathf.Min(1.5f - clock, SlamLeadSeconds)));
                    signals.Circle(lockedAim, T.slamRadius, clock < 1.5f ? new Color(1, .25f, .08f) : Color.magenta);
                    if (Mark(38f / 30)) Decision = "Slam location locked; move out or jump";
                    if (Mark(1.5f)) { model.EndResponse(RiftAttackKind.Slam, player.position); learner.Record(RiftAttackKind.Slam, attackBand, AreaDamage(lockedAim, T.slamRadius, T.meleeDamageFraction, false)); }
                    if (clock >= 3) Enter(RiftAction.Recover);
                    break;
                case RiftAction.Recover:
                    FaceShielded(dt);
                    if (clock >= (directorEnabled ? director.RecoverSeconds(T.recoverSeconds) : T.recoverSeconds)) Enter(RiftAction.Pursue);
                    break;
                case RiftAction.Dying:
                    if (Mark(79f / 30) && !notified) { notified = true; if (returnPoint != null) returnPoint.SetPowered(true); if (audioCues != null) audioCues.Play(CombatSound.Defeat); BossDefeated?.Invoke(); }
                    if (clock >= 3) { Action = RiftAction.Defeated; Decision = "Core destroyed / return point powered"; }
                    break;
            }
        }
        static float Planar(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        bool Mark(float t) => previous < t && clock >= t;
        void Choose(bool visible)
        {
            float range = Planar(transform.position, player.position);
            if (Planar(player.position, arenaCentre) > arenaRadius + 2) { motor.Stop(); Decision = "Player outside arena / waiting"; return; }
            if (!visible)
            {
                bool remembers = perception.Remembers(Time.time, T.memorySeconds);
                Decision = remembers ? "Investigating last seen position" : "No sight or recent memory / holding";
                if (remembers) motor.MoveTo(perception.LastSeen, 2); else motor.Stop(); return;
            }
            var scores = RiftUtility.Score(range, LeftHealth, RightHealth, Time.time >= cannonReady, Time.time >= bladeReady, Time.time >= teleportReady, T);
            if (CoreExposed && Time.time >= slamReady) { Enter(RiftAction.CoreTeleport); Decision = "Exposed-core pressure / teleport then warned slam"; return; }
            if (TryPunishHealer(scores.cannon >= 0)) return;
            // Layer 4: plan two moves ahead against this player's learned answers (expectimax). Layer 3 enters at the root:
            // each attack's value is shifted by (Thompson draw - mean) x payoff, so uncertain attacks still get explored.
            // Moves the real range rules forbid right now (e.g. slash beyond 3 m) are excluded at the root.
            var band = RiftAttackLearner.Band(range);
            var state = new RiftFightState
            {
                band = band, cannonArm = LeftHealth > 0, bladeArm = RightHealth > 0,
                cannonIn = Mathf.Max(0, cannonReady - Time.time), bladeIn = Mathf.Max(0, bladeReady - Time.time), teleportIn = Mathf.Max(0, teleportReady - Time.time),
            };
            double RootBonus(RiftMove m)
            {
                if (m == RiftMove.Cannon && scores.cannon < 0) return double.NegativeInfinity;
                if (m == RiftMove.Slash && scores.blade < 0) return double.NegativeInfinity;
                if (m == RiftMove.DoubleTeleport && scores.teleport < 0) return double.NegativeInfinity;
                if (m == RiftMove.Approach || m == RiftMove.Hold) return 0;
                var kind = RiftComboPlanner.Kind(m); var belief = learner.Belief(kind, band);
                return (belief.Sample(rng) - belief.Mean) * planner.Payoff(kind);
            }
            var plan = planner.Plan(state, model, learner, T, model.DamageRate(Time.time), armMaximum, RootBonus);
            string why = $" / plan: {planner.PlannedLine} (value {plan.Value:F1}, {plan.NodesVisited} nodes)";
            switch (plan.HasAction ? plan.Best : RiftMove.Approach)
            {
                case RiftMove.Cannon: Enter(RiftAction.Cannon); Decision = "Cannon" + why; break;
                case RiftMove.Slash: Enter(RiftAction.Slash); Decision = "Blade slash" + why; break;
                case RiftMove.DoubleTeleport: Enter(RiftAction.DoubleTeleport); Decision = "Double rear teleport" + why; break;
                case RiftMove.Hold: motor.Stop(); FaceShielded(Time.deltaTime); Decision = "Holding for a better opening" + why; break;
                default: { bool ok = motor.MoveTo(CutOffPoint(), CoreExposed ? 4 : 2.5f); Decision = (ok ? "Approach, cutting off the usual escape side" : "Route blocked / retry without walking through cover") + why; break; }
            }
        }
        void Enter(RiftAction action)
        {
            if (action != RiftAction.Cannon) cannonTarget = null;
            Action = action; clock = previous = 0; damagedCount = 0; landingWarned = false; motor.Stop(); signals.Clear();
            if (player != null) attackBand = RiftAttackLearner.Band(Planar(transform.position, player.position));
            string clip = CoreExposed ? "RW_CoreIdle" : "RW_Idle"; bool loop = true;
            switch (action)
            {
                case RiftAction.Pursue: clip = CoreExposed ? "RW_CoreWalk" : "RW_Walk"; nextDecision = Time.time; break;
                case RiftAction.Cannon: clip = "RW_CannonBurst"; cannonReady = Time.time + T.cannonCooldown * Scale; if (cannonTarget == null) cannonTarget = player; if (cannonTarget == player) model.BeginResponse(RiftAttackKind.Cannon, player.position, transform.position); break;
                case RiftAction.Slash: clip = "RW_BladeSlash"; bladeReady = Time.time + T.bladeCooldown * Scale; lockedAim = player.position; Face(lockedAim, 1); signals.Circle(transform.position, 2.7f, Color.red); model.BeginResponse(RiftAttackKind.Slash, player.position, transform.position); break;
                case RiftAction.DoubleTeleport: clip = "RW_DoubleTeleportSlash"; teleportReady = Time.time + T.teleportCooldown * Scale; secondRearAt = .18f + T.rearDelay; model.BeginResponse(RiftAttackKind.DoubleTeleport, player.position, transform.position); break;
                case RiftAction.BreakLeft: clip = "RW_ArmBreakLeft"; break;
                case RiftAction.BreakRight: clip = "RW_ArmBreakRight"; break;
                case RiftAction.CoreOpen: clip = "RW_CoreOpen"; break;
                case RiftAction.CoreTeleport: clip = "RW_CoreTeleportOut"; slamReady = Time.time + (directorEnabled ? director.SlamCooldown(T.slamCooldown) : T.slamCooldown); model.BeginResponse(RiftAttackKind.Slam, player.position, transform.position); break;
                case RiftAction.Slam: clip = "RW_CoreSlam"; lockedAim = GroundPoint(player.position); break;
                case RiftAction.Dying: clip = "RW_Death"; coreTarget.GetComponent<Collider>().enabled = false; break;
            }
            if (action != RiftAction.Pursue && action != RiftAction.Recover) loop = false;
            presenter.Play(clip, loop);
        }
        float Scale => directorEnabled ? director.CooldownScale : 1;

        // Tactic: shield the weak arm. When an arm is below half health and the player has been shooting it most,
        // the boss angles its body up to 25 degrees so that arm turns away from the player.
        void FaceShielded(float dt)
        {
            float offset = 0;
            if (directorEnabled)
            {
                float left = model.PartPressure(RiftPart.LeftArm, Time.time), right = model.PartPressure(RiftPart.RightArm, Time.time);
                if (LeftHealth > 0 && LeftHealth < armMaximum * ShieldBelow && left > right) offset = -ShieldAngle;      // turn left: left arm swings back
                else if (RightHealth > 0 && RightHealth < armMaximum * ShieldBelow && right > left) offset = ShieldAngle;
            }
            Vector3 to = player.position - transform.position; to.y = 0;
            if (to.sqrMagnitude < .01f) return;
            Vector3 dir = Quaternion.Euler(0, offset, 0) * to.normalized;
            Face(transform.position + dir * 5, dt);
            ShieldOffset = offset;
        }
        public float ShieldOffset { get; private set; }

        // Tactic: cut off. Approach the side the player usually strafes to instead of where they are now.
        Vector3 CutOffPoint()
        {
            if (!directorEnabled) return player.position;
            Vector3 to = player.position - transform.position; to.y = 0;
            if (to.sqrMagnitude < .01f) return player.position;
            Vector3 right = Vector3.Cross(Vector3.up, to.normalized);
            return player.position + right * model.StrafeBias * CutOffDistance;
        }

        // Tactic: punish the healer. If Relay healed the player in the last 6 s, can be seen and is in cannon range, the
        // cannon burst goes at Relay instead (at most once every 12 s; not in the desperate core phase).
        bool TryPunishHealer(bool cannonAvailable)
        {
            if (!directorEnabled || !cannonAvailable || healer == null || !healer.Alive || director.Phase == RiftPhase.Desperate) return false;
            if (Time.time - model.LastHealAt > HealerWindow || Time.time < healerPunishReady) return false;
            float range = Planar(transform.position, healer.transform.position);
            if (range > T.cannonRange || Physics.Linecast(transform.position + Vector3.up * 2.6f, healer.AimPoint, ~(1 << 8), QueryTriggerInteraction.Ignore)) return false;
            cannonTarget = healer.transform; healerPunishReady = Time.time + HealerRearm; HealerPunishments++;
            Enter(RiftAction.Cannon); Decision = $"Cannon at Relay / punish the healer (healed {Time.time - model.LastHealAt:F1} s ago)";
            return true;
        }

        void Face(Vector3 point, float dt)
        {
            Vector3 direction = point - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 210 * dt);
        }
        Vector3 GroundPoint(Vector3 target)
        {
            // A jumping target must not lift the low attack or its floor warning.
            var filter = new NavMeshQueryFilter { agentTypeID = motor.Agent.agentTypeID, areaMask = motor.Agent.areaMask };
            target.y = arenaCentre.y;
            return NavMesh.SamplePosition(target, out var ground, 2, filter) ? ground.position : target;
        }
        // Layer 2: where the player will be after 'seconds', on the ground plane, scaled by the lead factor and capped.
        Vector3 Lead(Vector3 position, Vector3 velocity, float seconds)
        {
            var (x, z) = TargetPrediction.Lead(position.x, position.z, velocity.x, velocity.z, seconds, leadFactor, MaxLead);
            return new Vector3((float)x, position.y, (float)z);
        }

        // Each shot of the burst aims where the player is predicted to be at that shot's time after the aim lock,
        // so the burst walks along the player's path instead of hitting the spot they just left.
        void FireCannon(float secondsAfterLock)
        {
            if (LeftHealth <= 0) return;
            Vector3 aim = Lead(lockPosition, lockVelocity, secondsAfterLock) + Vector3.up * lockHeight;
            ShotsFired++; Vector3 from = leftTarget.AimPoint; Vector3 direction = (aim - from).normalized;
            if (audioCues != null) audioCues.Play(CombatSound.CannonShot);
            if (weaponFeedback != null) weaponFeedback.Fire();
            int n = Physics.RaycastNonAlloc(from, direction, shotHits, 24, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity; DamageSink target = null;
            for (int i = 0; i < n; i++)
            {
                var hit = shotHits[i]; if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                closest = hit.distance; target = hit.collider.GetComponentInParent<DamageSink>();
            }
            signals.Shot(from, float.IsInfinity(closest) ? from + direction * 24 : from + direction * closest, new Color(2.2f, .25f, 3));
            signals.FollowMuzzle=leftTarget.transform;
            if (target != null && target.Team != Allegiance.Hostile) target.ReceiveDamage(target.MaximumHealth * T.cannonDamageFraction, transform);
            if (cannonTarget == null || cannonTarget == player) learner.Record(RiftAttackKind.Cannon, attackBand, target != null && target == playerVitals);
        }
        bool SlashDamage(float radius) { if (RightHealth > 0) { if (audioCues != null) audioCues.Play(CombatSound.Slash); return AreaDamage(transform.position, radius, T.meleeDamageFraction, true); } return false; }
        // Returns true when the player was among those hit.
        bool AreaDamage(Vector3 centre, float radius, float fraction, bool directional)
        {
            bool hitPlayer = false;
            DamageWindows++; int n = Physics.OverlapSphereNonAlloc(centre + Vector3.up * .65f, radius, overlaps, 1 << 8, QueryTriggerInteraction.Ignore);
            if (!directional && audioCues != null) audioCues.Play(CombatSound.Hit, 1.1f);
            for (int i = 0; i < n; i++)
            {
                var receiver = overlaps[i].GetComponentInParent<DamageSink>();
                if (receiver == null || !receiver.Alive || receiver.Team == Allegiance.Hostile || receiver.Team == Allegiance.Neutral) continue;
                Vector3 feet = receiver.transform.position;
                // Low attacks can be jumped; facing locks before the impact marker.
                if (feet.y > centre.y + 1.05f || Planar(feet, centre) > radius || (directional && Vector3.Dot(transform.forward, (feet - transform.position).normalized) < .1f)) continue;
                bool already = false; for (int j = 0; j < damagedCount; j++) if (damaged[j] == receiver) already = true;
                if (already || !DamageSink.Visible(centre + Vector3.up * .8f, receiver, transform)) continue;
                if (damagedCount < damaged.Length) damaged[damagedCount++] = receiver;
                receiver.ReceiveDamage(receiver.MaximumHealth * fraction, transform);
                if (receiver == playerVitals) hitPlayer = true;
            }
            return hitPlayer;
        }
        bool Landing(float side, out Vector3 point)
        {
            var agent = motor.GetComponent<NavMeshAgent>(); var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int i = 0; i < 8; i++)
            {
                float angle = i < 2 ? (i == 0 ? 0 : 30) : i * 45;
                Vector3 desired = player.position + Quaternion.Euler(0, angle, 0) * player.forward * side * 4.1f;
                desired.y = arenaCentre.y;
                if (!NavMesh.SamplePosition(desired, out var hit, 1, filter) || Planar(hit.position, arenaCentre) > arenaRadius || Planar(hit.position, player.position) < 3) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * .9f, hit.position + Vector3.up * 2.7f, .8f, ~(1 << 8), QueryTriggerInteraction.Ignore)) continue;
                bool occupied = false;
                foreach (var actor in DamageSink.Active) if (actor != null && actor.Alive && !actor.transform.IsChildOf(transform) && Planar(actor.transform.position, hit.position) < 1.5f) occupied = true;
                if (occupied) continue;
                point = hit.position; return true;
            }
            point = transform.position; return false;
        }
        bool TeleportBehind()
        {
            Vector3 back = -player.forward; back.y = 0;
            if (back.sqrMagnitude < .01f) return false;
            back.Normalize();
            var capsule = player.GetComponent<CharacterController>();
            float distance = Mathf.Max(T.rearDistance, motor.Agent.radius + (capsule != null ? capsule.radius : .45f) + .15f);
            Vector3 desired = player.position + back * distance; desired.y = arenaCentre.y;
            var filter = new NavMeshQueryFilter { agentTypeID = motor.Agent.agentTypeID, areaMask = motor.Agent.areaMask };
            if (!NavMesh.SamplePosition(desired, out var hit, .4f, filter) || Planar(desired, hit.position) > .12f || Planar(hit.position, arenaCentre) > arenaRadius) return false;
            if (Physics.CheckCapsule(hit.position + Vector3.up * .9f, hit.position + Vector3.up * 2.7f, .8f, ~(1 << 8), QueryTriggerInteraction.Ignore)) return false;
            foreach (var actor in DamageSink.Active)
                if (actor != null && actor.Alive && !actor.transform.IsChildOf(transform) && Planar(actor.transform.position, hit.position) < 1.45f) return false;
            if (!motor.Agent.Warp(hit.position)) return false;
            SuccessfulTeleports++; LastRearArrivalTime = Time.time; Face(player.position, 1);
            if (audioCues != null) audioCues.Play(CombatSound.Teleport);
            signals.Shot(transform.position + Vector3.up * .15f, transform.position + Vector3.up * 3.2f, new Color(1.8f, .15f, 2.6f), .18f);
            return true;
        }
        void LandingWarning(float side) { landingWarned = Landing(side, out warnedLanding); if (landingWarned) signals.Circle(warnedLanding, 1.2f, new Color(.75f, .15f, 1)); }
        void Teleport(float side)
        {
            bool clear = landingWarned && !Physics.CheckCapsule(warnedLanding + Vector3.up * .9f, warnedLanding + Vector3.up * 2.7f, .8f, ~(1 << 8), QueryTriggerInteraction.Ignore);
            foreach (var actor in DamageSink.Active) if (actor != null && actor.Alive && !actor.transform.IsChildOf(transform) && Planar(actor.transform.position, warnedLanding) < 1.5f) clear = false;
            if (clear && motor.GetComponent<NavMeshAgent>().Warp(warnedLanding)) { SuccessfulTeleports++; Face(player.position, 1); if (audioCues != null) audioCues.Play(CombatSound.Teleport); }
            else Decision = "No clear landing / teleport cancelled";
            landingWarned = false;
        }
        void Detach(bool left)
        {
            var renderer = left ? leftArm : rightArm;
            if ((left && leftDetached) || (!left && rightDetached)) return;
            if (audioCues != null) audioCues.Play(CombatSound.ArmBreak);
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            var go = new GameObject(left ? "Detached cannon" : "Detached blade"); go.layer = 8;
            go.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation); go.transform.localScale = renderer.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            var collider = go.AddComponent<BoxCollider>(); collider.center = mesh.bounds.center; collider.size = mesh.bounds.size;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 6; rb.AddForce((left ? -transform.right : transform.right) * 2 + Vector3.up, ForceMode.VelocityChange);
            go.AddComponent<TransientMesh>().mesh = mesh; Destroy(go, 6);
            renderer.enabled = false; (left ? leftTarget : rightTarget).GetComponent<Collider>().enabled = false;
            if (left) leftDetached = true; else rightDetached = true;
        }
        void AfterBreak()
        {
            // A second arm may be destroyed during the first arm's reaction.
            if (LeftHealth <= 0 && !leftDetached) { Enter(RiftAction.BreakLeft); return; }
            if (RightHealth <= 0 && !rightDetached) { Enter(RiftAction.BreakRight); return; }
            Enter(LeftHealth <= 0 && RightHealth <= 0 ? RiftAction.CoreOpen : RiftAction.Pursue);
        }
        void OnDisable() { if (motor != null) motor.Stop(); if (signals != null) signals.Clear(); }
    }
}

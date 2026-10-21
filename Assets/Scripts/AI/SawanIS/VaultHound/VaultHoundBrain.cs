using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Skybreak.Combat;

namespace Skybreak.AI.SawanIS.VaultHound
{
    // ── Operational states ────────────────────────────────────────────────────

    /// <summary>Discrete operational states of the Vault Hound agent.</summary>
    public enum HoundState
    {
        /// <summary>Locked inside the chamber, motionless, waiting for the door to open.</summary>
        Chambered,

        /// <summary>Leaving the chamber and heading toward the last reported combat zone.</summary>
        Deploying,

        /// <summary>
        /// Pursuer role: moving directly toward the player via A* through the last
        /// confirmed sighting, updating path on every decision tick.
        /// </summary>
        DirectPursue,

        /// <summary>
        /// Flanker role: taking a BFS-discovered alternate route to converge from a
        /// different direction than the pursuer.
        /// </summary>
        FlankPursue,

        /// <summary>
        /// Sight lost: systematically sweeping cells around the last known position
        /// using the BFS sweep route before returning to Deploying.
        /// </summary>
        Search,

        /// <summary>Close enough to attack: crouches, lunges, swipes.</summary>
        Attack,

        /// <summary>Missed lunge: pausing in recovery before resuming pursuit.</summary>
        LungeRecovery,

        /// <summary>Health reached zero — hound is dead.</summary>
        Dead
    }

    // ── Brain ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Vault Hound — chamber hunter brain (Member 3 / SawanIS).
    ///
    /// DESIGN OVERVIEW
    /// ───────────────
    /// Two instances of this brain are placed, one in each side chamber. They share
    /// one <see cref="HoundPack"/> object that coordinates roles and sightings.
    ///
    /// ENCOUNTER FLOW
    ///   1. Both hounds wait in state Chambered until <see cref="OnChamberOpen"/> is called.
    ///   2. Each hound deploys to the last shared combat sighting from <see cref="HoundPack"/>.
    ///   3. Once one hound sees the player it calls <see cref="HoundPack.ReportSighting"/>;
    ///      the pack assigns the pursuer (direct A* route) and flanker (BFS alternate route).
    ///   4. At close range the hound crouches briefly, then lunges and swipes.
    ///      Missing leaves a 1.2 s recovery window.
    ///   5. On losing sight both hounds execute a BFS sweep. If memory expires they
    ///      return to Deploying and investigate the last known position again.
    ///   6. If one hound is destroyed the survivor calls <see cref="HoundPack.NotifyHoundDestroyed"/>
    ///      and switches to solo DirectPursue.
    ///
    /// IS FEATURES
    ///   • Memory        – lastKnownPlayerPosition + memory confidence decay
    ///   • Search        – BFS sweep around last known position (uninformed)
    ///   • Route planning– A* informed search with Manhattan heuristic (admissible + consistent)
    ///   • Coordination  – HoundPack shared sighting and role assignment
    ///   • Blocked-route – if path is empty or agent is stuck, re-plan from current position
    ///
    /// NAMESPACE  Skybreak.AI.SawanIS.VaultHound
    /// AUTHOR     Sawan (Member 3)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatHealth))]
    public sealed class VaultHoundBrain : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The grid shared by both hounds in this encounter.")]
        [SerializeField] VaultHoundGrid grid;

        [Tooltip("The coordination pack shared by this hound and its partner.")]
        [SerializeField] HoundPack pack;

        [Tooltip("Eye origin for line-of-sight raycasts.")]
        [SerializeField] Transform eyePoint;

        [Header("Perception")]
        [SerializeField, Min(1f)] float sightRange    = 20f;
        [SerializeField, Range(10f, 360f)] float fieldOfView = 130f;
        [SerializeField] LayerMask sightMask          = ~0;

        [Header("Memory")]
        [Tooltip("Seconds before last-known-position memory fully decays.")]
        [SerializeField, Min(0.5f)] float memoryDuration = 5f;

        [Header("Pursuit")]
        [SerializeField, Min(0.1f)] float decisionInterval = 0.2f;
        [SerializeField, Min(0.5f)] float pursueSpeed      = 6.2f;
        [SerializeField, Min(0.5f)] float deploySpeed      = 4.0f;
        [SerializeField, Min(0.1f)] float waypointThreshold = 0.5f;

        [Header("Attack — lunge & swipe")]
        [Tooltip("Distance at which the hound enters the attack sequence.")]
        [SerializeField, Min(0.5f)] float attackTriggerRange = 2.8f;

        [Tooltip("Swipe hit collider radius around the hound at impact moment.")]
        [SerializeField, Min(0.3f)] float swipeRadius = 1.6f;

        [Tooltip("Layers that the swipe can hit (player layer).")]
        [SerializeField] LayerMask swipeHitMask = ~0;

        [Tooltip("Damage dealt on a successful swipe hit.")]
        [SerializeField, Min(1f)] float swipeDamage = 22f;

        [Tooltip("Duration of the pre-lunge crouch warning (seconds).")]
        [SerializeField, Min(0.05f)] float lungeWarnDuration = 0.25f;

        [Tooltip("Duration of the lunge travel phase (seconds).")]
        [SerializeField, Min(0.05f)] float lungeDuration = 0.4f;

        [Tooltip("Time after the swipe frame before impact is resolved.")]
        [SerializeField, Min(0f)] float swipeDelay = 0f; // resolved at lunge end

        [Tooltip("Recovery window after a missed lunge before the hound can act again.")]
        [SerializeField, Min(0.1f)] float missRecoveryDuration = 1.2f;

        [Header("Search")]
        [SerializeField, Range(1, 10)] int sweepRadius    = 4;
        [SerializeField, Range(4,  24)] int sweepMaxCells = 10;

        [Header("BFS flanking")]
        [SerializeField, Range(1,  10)] int flankRadius       = 5;
        [SerializeField, Range(4,  24)] int flankMaxCandidates = 12;

        [Header("Blocked-route recovery")]
        [Tooltip("Seconds stuck below minimumProgress before forcing a replan.")]
        [SerializeField, Min(0.2f)] float stuckSeconds    = 1.0f;
        [SerializeField, Min(0.05f)] float minimumProgress = 0.15f;

        [Header("Debug")]
        [SerializeField] bool showGizmos         = true;
        [SerializeField] bool showDecisionOverlay = true;

        // ── Components ────────────────────────────────────────────────────────

        NavMeshAgent motor;
        CombatHealth health;

        // ── State machine ─────────────────────────────────────────────────────

        HoundState currentState  = HoundState.Chambered;
        HoundState previousState = HoundState.Chambered;

        /// <summary>Read by <see cref="VaultHoundAnimator"/> to drive the Animator Controller.</summary>
        public HoundState CurrentState => currentState;

        // ── Memory ────────────────────────────────────────────────────────────

        Vector3 lastKnownPlayerPosition;
        float   lastSeenTime = float.NegativeInfinity;
        bool    hasLastKnown;

        // ── Pathfinding ───────────────────────────────────────────────────────

        List<Vector3>  currentPath     = new List<Vector3>();
        int            pathIndex;
        int            cachedGridVersion = -1;
        float          lastStuckCheck;
        Vector3        positionAtStuckCheck;
        bool           routeBlocked;

        /// <summary>Encoded cell keys of the current A* path. Exposed to HoundPack
        /// so the flanker can avoid them in BFS.</summary>
        public HashSet<int> CurrentPathCells { get; private set; } = new HashSet<int>();

        // ── Search sweep ──────────────────────────────────────────────────────

        List<Vector3> sweepRoute = new List<Vector3>();
        int           sweepIndex;

        // ── Attack ────────────────────────────────────────────────────────────

        [SerializeField] Transform player;
        float     nextDecisionTime;
        bool      lungeHit; // tracks whether the current lunge connected
        Vector3 chamberPosition;
        Quaternion chamberRotation;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            motor  = GetComponent<NavMeshAgent>();
            health = GetComponent<CombatHealth>();
            chamberPosition = transform.position; chamberRotation = transform.rotation;

            motor.speed         = deploySpeed;
            motor.updateRotation = true;
            motor.autoRepath     = false; // we manage replanning ourselves
            motor.stoppingDistance = .05f;

            health.DamageTaken += OnDamageTaken;
            health.OnDefeated.AddListener(OnDefeated);

            if (pack != null) pack.RegisterHound(this);
            else Debug.LogWarning("[VaultHound] No HoundPack assigned.", this);

            positionAtStuckCheck = transform.position;
        }

        void OnDestroy()
        {
            if (health != null) health.DamageTaken -= OnDamageTaken;
            if (health != null) health.OnDefeated.RemoveListener(OnDefeated);
        }

        void Update()
        {
            if (currentState == HoundState.Chambered ||
                currentState == HoundState.Dead       ||
                currentState == HoundState.Attack     ||
                currentState == HoundState.LungeRecovery)
                return; // handled by coroutines or external events

            if (!health.IsAlive) { TransitionTo(HoundState.Dead); return; }

            AcquirePlayer();
            UpdateGridVersion();
            UpdateStuckCheck();

            if (Time.time >= nextDecisionTime)
            {
                nextDecisionTime = Time.time + decisionInterval;
                Decide();
            }

            FollowPath();
        }

        // ── Chamber release (called externally by ChamberReleaseController) ───

        /// <summary>Called when this hound's chamber door opens. Starts deployment.</summary>
        public void OnChamberOpen()
        {
            if (currentState != HoundState.Chambered) return;
            if (!health.IsAlive) return;
            motor.enabled = true;
            if (!motor.isOnNavMesh) return;
            motor.isStopped = false;
            TransitionTo(HoundState.Deploying);
        }

        public void ResetForChamber()
        {
            StopAllCoroutines(); health.RestoreFullHealth();
            motor.enabled = false; transform.SetPositionAndRotation(chamberPosition, chamberRotation); motor.enabled = true;
            if (motor.isOnNavMesh) { motor.Warp(chamberPosition); motor.ResetPath(); motor.isStopped = true; }
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = true;
            currentState = previousState = HoundState.Chambered;
            currentPath.Clear(); CurrentPathCells.Clear(); sweepRoute.Clear(); pathIndex = sweepIndex = 0;
            hasLastKnown = false; lastSeenTime = float.NegativeInfinity; routeBlocked = false;
            pack?.RegisterHound(this);
            var anim = GetComponent<Animator>(); if (anim != null) { anim.Rebind(); anim.Update(0); }
        }

        // ── Main decision loop ────────────────────────────────────────────────

        void Decide()
        {
            bool canSee = CanSeePlayer();

            // Update memory.
            if (canSee && player != null)
            {
                lastKnownPlayerPosition = player.position;
                lastSeenTime = Time.time;
                hasLastKnown = true;
                pack?.ReportSighting(player.position);
            }

            float memAge = Time.time - lastSeenTime;
            float memConf = hasLastKnown ? Mathf.Clamp01(1f - memAge / Mathf.Max(0.01f, memoryDuration)) : 0f;
            bool  memValid = hasLastKnown && memConf > 0f;

            switch (currentState)
            {
                case HoundState.Deploying:
                    DecideDeploy(canSee, memValid);
                    break;

                case HoundState.DirectPursue:
                case HoundState.FlankPursue:
                    DecidePursue(canSee, memValid);
                    break;

                case HoundState.Search:
                    DecideSearch(canSee, memConf);
                    break;
            }
        }

        void DecideDeploy(bool canSee, bool memValid)
        {
            if (canSee)
            {
                AssignPursuitRole();
                return;
            }

            // If partner reported a sighting use it as the deploy target.
            if (pack != null && pack.TryGetSharedSighting(out Vector3 shared))
            {
                if (!HasPathToward(shared)) PlanPathTo(shared);
                return;
            }

            // No sighting yet — hold in place or wander to center of arena if totally blind.
            if (memValid && !HasPathToward(lastKnownPlayerPosition))
                PlanPathTo(lastKnownPlayerPosition);
        }

        void DecidePursue(bool canSee, bool memValid)
        {
            var role = pack != null && pack.IsFlanker(this) ? HoundState.FlankPursue : HoundState.DirectPursue;
            if (currentState != role) TransitionTo(role);
            // Close enough to attack?
            if (canSee && player != null &&
                Vector3.Distance(transform.position, player.position) <= attackTriggerRange)
            {
                StartCoroutine(AttackSequence());
                return;
            }

            if (!canSee)
            {
                if (memValid)
                {
                    TransitionTo(HoundState.Search);
                    BeginSweep();
                    return;
                }
                TransitionTo(HoundState.Deploying);
                return;
            }

            // Re-plan toward player each decision tick.
            if (pack != null && pack.IsFlanker(this))
            {
                PlanFlankPath();
            }
            else
            {
                if (player != null) PlanPathTo(player.position);
            }
        }

        void DecideSearch(bool canSee, float memConf)
        {
            if (canSee)
            {
                // Re-acquire; reassign roles.
                AssignPursuitRole();
                return;
            }

            if (memConf <= 0f)
            {
                // Memory expired — redeploy.
                TransitionTo(HoundState.Deploying);
                return;
            }

            // Advance sweep route.
            if (sweepIndex < sweepRoute.Count)
            {
                float dist = Vector3.Distance(transform.position, sweepRoute[sweepIndex]);
                if (dist < waypointThreshold + 0.2f)
                {
                    sweepIndex++;
                    if (sweepIndex < sweepRoute.Count) PlanPathTo(sweepRoute[sweepIndex]);
                }
            }

            if (sweepIndex >= sweepRoute.Count)
                TransitionTo(HoundState.Deploying);
        }

        // ── Pursuit role assignment ───────────────────────────────────────────

        void AssignPursuitRole()
        {
            if (pack == null)
            {
                // Solo mode (pack destroyed or single hound).
                TransitionTo(HoundState.DirectPursue);
                if (player != null) PlanPathTo(player.position);
                return;
            }

            if (pack.IsPursuer(this))
            {
                TransitionTo(HoundState.DirectPursue);
                if (player != null) PlanPathTo(player.position);
            }
            else
            {
                TransitionTo(HoundState.FlankPursue);
                PlanFlankPath();
            }
        }

        // ── Path execution ────────────────────────────────────────────────────

        void FollowPath()
        {
            if (!motor.enabled || !motor.isOnNavMesh) return;
            if (routeBlocked) { TryReplan(); return; }
            if (currentPath.Count == 0 || pathIndex >= currentPath.Count) return;

            Vector3 target = currentPath[pathIndex];
            target.y = transform.position.y; // keep on ground

            float dist = Vector3.Distance(transform.position, target);
            if (dist < waypointThreshold) { pathIndex++; return; }

            motor.SetDestination(target);
        }

        void PlanPathTo(Vector3 worldTarget)
        {
            if (grid == null) { motor.SetDestination(worldTarget); return; }

            var startCell = grid.WorldToCell(transform.position);
            var goalCell  = grid.WorldToCell(worldTarget);

            var path = VaultHoundPathfinder.FindPath(grid, startCell, goalCell);
            if (path.Count == 0) { routeBlocked = true; return; }

            currentPath = path;
            pathIndex   = 0;
            routeBlocked = false;
            cachedGridVersion = grid.Version;

            // Encode path cells for flanker avoidance.
            var cells = new HashSet<int>();
            foreach (var wp in path)
            {
                var c = grid.WorldToCell(wp);
                cells.Add(c.y * grid.Width + c.x);
            }
            CurrentPathCells = cells;

            motor.speed = (currentState == HoundState.Deploying) ? deploySpeed : pursueSpeed;
        }

        void PlanFlankPath()
        {
            if (grid == null || player == null) return;

            var agentCell   = grid.WorldToCell(transform.position);
            var seed        = grid.WorldToCell(player.position);
            var partnerKeys = pack?.GetPursuerPathCells(this) ?? new HashSet<int>();

            var path = VaultHoundPathfinder.FindFlankPath(
                grid, agentCell, seed, partnerKeys, flankRadius, flankMaxCandidates);

            if (path.Count == 0) { routeBlocked = true; return; }

            currentPath = path;
            pathIndex   = 0;
            routeBlocked = false;
            motor.speed  = pursueSpeed;
        }

        bool HasPathToward(Vector3 target)
        {
            if (currentPath.Count == 0) return false;
            // Consider "has path" if the last waypoint is within 1.5 m of the target.
            return Vector3.Distance(currentPath[currentPath.Count - 1], target) < 1.5f;
        }

        void TryReplan()
        {
            routeBlocked = false;
            Vector3 target = (hasLastKnown) ? lastKnownPlayerPosition : transform.position;
            PlanPathTo(target);
        }

        // ── Search sweep ──────────────────────────────────────────────────────

        void BeginSweep()
        {
            if (grid == null) return;
            var seedCell = grid.WorldToCell(lastKnownPlayerPosition);
            sweepRoute   = VaultHoundPathfinder.BuildSweepRoute(grid, seedCell, sweepRadius, sweepMaxCells);
            sweepIndex   = 0;
            motor.speed  = deploySpeed;

            if (sweepRoute.Count > 0) PlanPathTo(sweepRoute[0]);
        }

        // ── Attack sequence ───────────────────────────────────────────────────

        IEnumerator AttackSequence()
        {
            if (currentState == HoundState.Attack || currentState == HoundState.LungeRecovery)
                yield break;

            TransitionTo(HoundState.Attack);
            motor.isStopped = true;

            // Phase 1: Crouch / warn
            yield return new WaitForSeconds(lungeWarnDuration);

            if (!health.IsAlive) { TransitionTo(HoundState.Dead); yield break; }

            // Phase 2: Lunge travel
            Vector3 lungeTarget = player != null ? player.position : transform.position + transform.forward * 2f;
            Vector3 startPos    = transform.position;
            float   elapsed     = 0f;
            lungeHit            = false;

            while (elapsed < lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lungeDuration);
                if (!health.IsAlive) yield break;
                Vector3 wanted = Vector3.Lerp(startPos, lungeTarget, t);
                var filter = new NavMeshQueryFilter { agentTypeID = motor.agentTypeID, areaMask = motor.areaMask };
                if (NavMesh.Raycast(transform.position, wanted, out var obstruction, filter)) wanted = obstruction.position;
                motor.Move(wanted - transform.position);
                yield return null;
            }

            // Phase 3: Swipe hit check
            if (swipeDelay > 0f) yield return new WaitForSeconds(swipeDelay);
            lungeHit = TrySwipeHit();

            if (lungeHit)
            {
                // Short pause after a hit before returning to pursue.
                yield return new WaitForSeconds(0.3f);
                if (!health.IsAlive) yield break;
                TransitionTo(pack != null && pack.IsFlanker(this)
                    ? HoundState.FlankPursue
                    : HoundState.DirectPursue);
                motor.isStopped = false;
            }
            else
            {
                // Missed — recovery window.
                TransitionTo(HoundState.LungeRecovery);
                yield return new WaitForSeconds(missRecoveryDuration);
                if (health.IsAlive)
                {
                    TransitionTo(pack != null && pack.IsFlanker(this)
                        ? HoundState.FlankPursue
                        : HoundState.DirectPursue);
                    motor.isStopped = false;
                }
            }
        }

        bool TrySwipeHit()
        {
            var hits = Physics.OverlapSphere(transform.position, swipeRadius, swipeHitMask,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (player == null || (hit.transform != player && !hit.transform.IsChildOf(player))) continue;
                var sink = hit.GetComponentInParent<DamageSink>();
                if (sink != null)
                {
                    if (sink.Team != Allegiance.Player || !sink.Alive || !DamageSink.Visible(transform.position + Vector3.up * .7f, sink, transform)) continue;
                    float before = sink.Health;
                    sink.ReceiveDamage(swipeDamage, transform);
                    return sink.Health < before;
                }
                var dmg = hit.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                // Only hit the player team.
                var ch = hit.GetComponentInParent<CombatHealth>();
                if (ch == null) continue;

                dmg.ApplyDamage(swipeDamage, gameObject);
                return true;
            }
            return false;
        }

        // ── Perception ────────────────────────────────────────────────────────

        bool CanSeePlayer()
        {
            if (player == null) return false;
            Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 0.8f;
            Vector3 target = player.position + Vector3.up * 1.0f;
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            if (distance > sightRange || distance < 0.01f) return false;
            if (Vector3.Angle(transform.forward, offset) > fieldOfView * 0.5f) return false;
            foreach (var hit in Physics.RaycastAll(origin, offset / distance, distance, sightMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.transform != player && !hit.transform.IsChildOf(player) && hit.collider.gameObject.layer != 8) return false;
            return true;
        }

        void AcquirePlayer()
        {
            if (player != null) return;
            foreach (var sink in DamageSink.Active)
                if (sink != null && sink.Team == Allegiance.Player && sink.Alive) { player = sink.transform; return; }
#if UNITY_EDITOR || !UNITY_WEBGL
            // ArcController may not exist in all scenes; use try-find pattern safely.
            var allBehaviours = FindObjectsByType<MonoBehaviour>();
            foreach (var mb in allBehaviours)
            {
                if (mb.GetType().Name == "ArcController") { player = mb.transform; return; }
            }
#endif

            var descent = FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>();
            if (descent != null) { player = descent.transform; return; }

            // Lab targets must be explicitly tagged, otherwise the pair can target each other.
            foreach (var ch in FindObjectsByType<CombatHealth>())
            {
                if (ch == health || !ch.IsAlive || !ch.CompareTag("Player")) continue;
                player = ch.transform;
                return;
            }
        }

        // ── Utility ───────────────────────────────────────────────────────────

        void TransitionTo(HoundState next)
        {
            if (currentState == HoundState.Dead) return;
            previousState = currentState;
            currentState  = next;

            if (next == HoundState.Dead)
            {
                StopAllCoroutines();
                if (motor.enabled && motor.isOnNavMesh) motor.isStopped = true;
                motor.enabled = false;
                foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
                pack?.NotifyHoundDestroyed(this);
            }
        }

        void UpdateGridVersion()
        {
            if (grid == null) return;
            if (grid.Version != cachedGridVersion)
            {
                cachedGridVersion = grid.Version;
                routeBlocked = true; // force replan
            }
        }

        void UpdateStuckCheck()
        {
            if (Time.time - lastStuckCheck < stuckSeconds) return;
            lastStuckCheck = Time.time;

            float progress = Vector3.Distance(transform.position, positionAtStuckCheck);
            if (currentPath.Count > 0 && pathIndex < currentPath.Count && progress < minimumProgress)
                routeBlocked = true;

            positionAtStuckCheck = transform.position;
        }

        void OnDamageTaken(CombatHealth _, GameObject attacker, float amount)
        {
            if (!health.IsAlive) TransitionTo(HoundState.Dead);

            // Remember position of attacker as last known even if not visible.
            if (attacker != null)
            {
                lastKnownPlayerPosition = attacker.transform.position;
                lastSeenTime = Time.time;
                hasLastKnown = true;
            }
        }

        void OnDefeated() => TransitionTo(HoundState.Dead);

        // ── Gizmos ────────────────────────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Gizmos.color = new Color(1f, 0.45f, 0f, 0.8f); // orange sensor colour
            Gizmos.DrawWireSphere(transform.position, attackTriggerRange);
            Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            if (hasLastKnown)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastKnownPlayerPosition, 0.2f);
            }
            Gizmos.color = new Color(1f, 0.45f, 0f, 0.9f);
            for (int i = 1; i < currentPath.Count; i++)
                Gizmos.DrawLine(currentPath[i - 1], currentPath[i]);
        }

        // ── Debug overlay ─────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!showDecisionOverlay || !Application.isPlaying) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = new Color(0.95f, 0.75f, 0.15f) }
            };
            float memConf = hasLastKnown
                ? Mathf.Clamp01(1f - (Time.time - lastSeenTime) / Mathf.Max(0.01f, memoryDuration))
                : 0f;
            string role = pack == null ? "SOLO"
                : pack.IsPursuer(this) ? "PURSUER" : "FLANKER";

            string text = $"VAULT HOUND  [{currentState}]  {role}\n"
                        + $"Memory: {memConf * 100f:F0}%  HP: {health?.HealthFraction * 100f:F0}%\n"
                        + $"Path: {pathIndex}/{currentPath.Count}  Blocked: {routeBlocked}";

            Rect panel = new Rect(14f, 40f, 280f, 60f);
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.85f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, panel.height - 12f),
                text, style);
        }
    }
}

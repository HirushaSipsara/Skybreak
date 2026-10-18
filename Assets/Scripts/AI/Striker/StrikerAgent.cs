using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.Striker
{
    /// <summary>
    /// Explicit operational states of the Striker security agent.
    /// </summary>
    public enum StrikerState
    {
        Patrol,  // Methodical patrol between designated waypoints using A* routing
        Pursue,  // Active pursuit towards intruder's live position using A* routing
        Sweep,   // Systematic area clearance using uninformed BFS around last known location
        Alert    // Low-health defensive emergency state (freezes, locks facing, readies defense)
    }

    /// <summary>
    /// Striker is a goal-based reactive security drone for the Aurora Vault complex.
    /// It balances informed search (A* with Manhattan heuristic) for long-range navigation
    /// with uninformed search (Breadth-First Search) for tactical area clearance upon losing
    /// visual contact with Arc.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatHealth))]
    public sealed class StrikerAgent : MonoBehaviour
    {
        [Header("Grid & Environment")]
        [Tooltip("The navigation grid Striker uses for A* and BFS searches.")]
        [SerializeField] StrikerGrid grid;

        [Header("Perception")]
        [Tooltip("Target player transform to track. If unassigned, automatically finds Arc.")]
        [SerializeField] Transform target;

        [Tooltip("Transform from which eye line-of-sight checks originate.")]
        [SerializeField] Transform eyePoint;

        [Tooltip("Transform from which hitscan weapon traces originate.")]
        [SerializeField] Transform muzzlePoint;

        [Tooltip("Maximum visual detection distance in meters.")]
        [SerializeField, Min(1f)] float sightRange = 18f;

        [Tooltip("Total horizontal field of view angle in degrees.")]
        [SerializeField, Range(10f, 360f)] float fieldOfView = 120f;

        [Tooltip("Layers checked for line of sight blocking (walls, doors, environment).")]
        [SerializeField] LayerMask lineOfSightMask = ~0;

        [Header("Tactics & Combat")]
        [Tooltip("Maximum effective weapon engagement range.")]
        [SerializeField, Min(1f)] float attackRange = 8f;

        [Tooltip("Weapon firing cooldown in seconds.")]
        [SerializeField, Min(0.1f)] float attackCooldown = 1.0f;

        [Tooltip("Damage dealt per weapon burst through IDamageable.")]
        [SerializeField, Min(0f)] float attackDamage = 12f;

        [Tooltip("Time in seconds before memory of last known position decays completely.")]
        [SerializeField, Min(0.5f)] float memoryDuration = 5.0f;

        [Tooltip("Health fraction below which Striker enters Alert defensive state.")]
        [SerializeField, Range(0.05f, 0.5f)] float alertHealthThreshold = 0.25f;

        [Tooltip("Breadth-First Search search radius in grid cells during sweep.")]
        [SerializeField, Range(1, 10)] int sweepSearchRadius = 4;

        [Tooltip("Maximum number of cells visited during BFS sweep.")]
        [SerializeField, Range(4, 32)] int sweepMaxCells = 12;

        [Header("Locomotion")]
        [Tooltip("Movement speed while patrolling (meters per second).")]
        [SerializeField, Min(0.5f)] float patrolSpeed = 3.2f;

        [Tooltip("Movement speed while pursuing intruder (meters per second).")]
        [SerializeField, Min(0.5f)] float pursueSpeed = 4.8f;

        [Tooltip("Turn speed in degrees per second.")]
        [SerializeField, Min(10f)] float turnSpeed = 360f;

        [Tooltip("Distance threshold to advance to next waypoint along path.")]
        [SerializeField, Min(0.1f)] float waypointThreshold = 0.45f;

        [Tooltip("Patrol route waypoints in world space.")]
        [SerializeField] Vector3[] patrolWaypoints;

        [Header("Visualization & Debug")]
        [SerializeField] bool showDebugGizmos = true;
        [SerializeField] bool showDecisionOverlay = true;

        // Core components
        CharacterController controller;
        CombatHealth health;

        // State Machine
        StrikerState currentState = StrikerState.Patrol;
        int currentPatrolIndex;
        float nextAttackTime;
        float nextRepathTime;
        const float RepathInterval = 0.35f;

        // Navigation path
        readonly List<Vector3> activePath = new List<Vector3>(32);
        int currentPathIndex;
        int cachedGridVersion = -1;

        // Model-based memory
        Vector3 lastKnownTargetPosition;
        float lastSeenTime = float.NegativeInfinity;
        bool hasSeenTarget;
        bool targetInSight;
        float targetDistance;

        // Locomotion physics
        float verticalSpeed;

        // Public properties for telemetry and testing
        public StrikerState CurrentState => currentState;
        public bool TargetInSight => targetInSight;
        public float TargetDistance => targetDistance;
        public Vector3 LastKnownPosition => lastKnownTargetPosition;
        public float MemoryConfidence => Mathf.Clamp01(1f - (Time.time - lastSeenTime) / memoryDuration);
        public int RemainingPathPoints => Mathf.Max(0, activePath.Count - currentPathIndex);
        public CombatHealth Health => health;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatHealth>();

            if (eyePoint == null) eyePoint = transform;
            if (muzzlePoint == null) muzzlePoint = eyePoint;

            health.DamageTaken += OnDamageTaken;
            health.OnDefeated.AddListener(OnDefeated);
        }

        void Start()
        {
            if (grid == null)
            {
                grid = FindFirstObjectByType<StrikerGrid>();
            }

            if (target == null)
            {
                TryAcquireTarget();
            }

            if (grid != null)
            {
                cachedGridVersion = grid.Version;
            }

            // Default patrol setup if empty
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                patrolWaypoints = new Vector3[]
                {
                    transform.position,
                    transform.position + transform.forward * 4f,
                    transform.position + transform.right * 4f
                };
            }

            PlanPatrolPath();
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.DamageTaken -= OnDamageTaken;
            }
        }

        void Update()
        {
            if (!health.IsAlive) return;

            if (target == null)
            {
                TryAcquireTarget();
            }

            // 1. Perception Step
            UpdatePerception();

            // 2. Dynamic Grid Change Check (e.g. Security Door opened/closed)
            CheckGridInvalidation();

            // 3. Goal & State Selection
            EvaluateStateTransitions();

            // 4. Execution Step
            ExecuteCurrentState();

            // 5. Movement Physics
            ApplyLocomotion();
        }

        #region Perception

        void TryAcquireTarget()
        {
            // First look for ArcController
            ArcController arc = FindFirstObjectByType<ArcController>();
            if (arc != null)
            {
                target = arc.transform;
                return;
            }

            // Fallback: look by tag
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        void UpdatePerception()
        {
            targetInSight = false;
            targetDistance = float.PositiveInfinity;

            if (target == null) return;

            Vector3 eyePos = eyePoint.position;
            Vector3 targetCenter = target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetCenter - eyePos;
            targetDistance = toTarget.magnitude;

            if (targetDistance > sightRange) return;

            // Field of view angle check
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > fieldOfView * 0.5f) return;

            // Line of sight raycast check (ensure no wall or obstacle blocks vision)
            if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, targetDistance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                // Verify hit object belongs to target
                if (hit.transform == target || hit.transform.IsChildOf(target))
                {
                    targetInSight = true;
                    lastKnownTargetPosition = target.position;
                    lastSeenTime = Time.time;
                    hasSeenTarget = true;
                }
            }
        }

        #endregion

        #region Reactive State Machine

        void CheckGridInvalidation()
        {
            if (grid == null) return;

            if (grid.Version != cachedGridVersion)
            {
                cachedGridVersion = grid.Version;
                // Replan immediately because traversability changed (e.g. door opened/closed)
                ReplanCurrentGoal();
            }
        }

        void EvaluateStateTransitions()
        {
            // Priority 1: Low-health Alert state interrupt
            if (health.HealthFraction <= alertHealthThreshold)
            {
                if (currentState != StrikerState.Alert)
                {
                    TransitionTo(StrikerState.Alert);
                }
                return;
            }

            // Priority 2: Intruder visible in sight -> Pursue
            if (targetInSight)
            {
                if (currentState != StrikerState.Pursue)
                {
                    TransitionTo(StrikerState.Pursue);
                }
                return;
            }

            // Priority 3: Intruder was pursued but recently broke sight -> BFS Sweep
            if (currentState == StrikerState.Pursue && !targetInSight)
            {
                TransitionTo(StrikerState.Sweep);
                return;
            }

            // Priority 4: Sweep finished or memory expired -> Return to Patrol
            if (currentState == StrikerState.Sweep)
            {
                if (MemoryConfidence <= 0f || activePath.Count == 0 || currentPathIndex >= activePath.Count)
                {
                    TransitionTo(StrikerState.Patrol);
                }
                return;
            }

            // Priority 5: Recovered from Alert -> Resume Patrol
            if (currentState == StrikerState.Alert && health.HealthFraction > alertHealthThreshold)
            {
                TransitionTo(StrikerState.Patrol);
            }
        }

        void TransitionTo(StrikerState newState)
        {
            currentState = newState;
            activePath.Clear();
            currentPathIndex = 0;

            switch (currentState)
            {
                case StrikerState.Patrol:
                    PlanPatrolPath();
                    break;
                case StrikerState.Pursue:
                    PlanPursuePath();
                    break;
                case StrikerState.Sweep:
                    PlanSweepPath();
                    break;
                case StrikerState.Alert:
                    // Freeze and prepare defense
                    activePath.Clear();
                    break;
            }
        }

        void ReplanCurrentGoal()
        {
            switch (currentState)
            {
                case StrikerState.Patrol:
                    PlanPatrolPath();
                    break;
                case StrikerState.Pursue:
                    PlanPursuePath();
                    break;
                case StrikerState.Sweep:
                    PlanSweepPath();
                    break;
            }
        }

        #endregion

        #region Planning Algorithms (A* and BFS)

        void PlanPatrolPath()
        {
            if (grid == null || patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            Vector3 destination = patrolWaypoints[currentPatrolIndex];
            Vector2Int startCell = grid.GetClosestPassableCell(transform.position);
            Vector2Int goalCell = grid.GetClosestPassableCell(destination);

            List<Vector2Int> cellPath = StrikerPathfinder.FindAStarPath(grid, startCell, goalCell);
            if (cellPath != null && cellPath.Count > 0)
            {
                activePath.Clear();
                activePath.AddRange(StrikerPathfinder.ConvertToWorldWaypoints(grid, cellPath, 0f));
                currentPathIndex = 0;
            }
        }

        void PlanPursuePath()
        {
            if (grid == null || target == null) return;

            Vector2Int startCell = grid.GetClosestPassableCell(transform.position);
            Vector2Int goalCell = grid.GetClosestPassableCell(target.position);

            List<Vector2Int> cellPath = StrikerPathfinder.FindAStarPath(grid, startCell, goalCell);
            if (cellPath != null && cellPath.Count > 0)
            {
                activePath.Clear();
                activePath.AddRange(StrikerPathfinder.ConvertToWorldWaypoints(grid, cellPath, 0f));
                currentPathIndex = 0;
            }
            nextRepathTime = Time.time + RepathInterval;
        }

        void PlanSweepPath()
        {
            if (grid == null) return;

            Vector2Int centerCell = grid.GetClosestPassableCell(lastKnownTargetPosition);
            List<Vector2Int> sweepCells = StrikerPathfinder.BreadthFirstSearchSweep(grid, centerCell, sweepSearchRadius, sweepMaxCells);

            if (sweepCells != null && sweepCells.Count > 0)
            {
                activePath.Clear();
                activePath.AddRange(StrikerPathfinder.ConvertToWorldWaypoints(grid, sweepCells, 0f));
                currentPathIndex = 0;
            }
            else
            {
                // Fallback: move directly to last known position
                activePath.Clear();
                activePath.Add(lastKnownTargetPosition);
                currentPathIndex = 0;
            }
        }

        #endregion

        #region State Execution

        void ExecuteCurrentState()
        {
            switch (currentState)
            {
                case StrikerState.Patrol:
                    ExecutePatrol();
                    break;
                case StrikerState.Pursue:
                    ExecutePursue();
                    break;
                case StrikerState.Sweep:
                    ExecuteSweep();
                    break;
                case StrikerState.Alert:
                    ExecuteAlert();
                    break;
            }
        }

        void ExecutePatrol()
        {
            if (activePath.Count == 0 || currentPathIndex >= activePath.Count)
            {
                // Advance to next patrol waypoint
                if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                {
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolWaypoints.Length;
                    PlanPatrolPath();
                }
            }
        }

        void ExecutePursue()
        {
            // Repath periodically to track moving player
            if (Time.time >= nextRepathTime)
            {
                PlanPursuePath();
            }

            // Engage with hitscan weapon if in range and line of sight is clear
            if (targetInSight && targetDistance <= attackRange)
            {
                TryAttack();
            }
        }

        void ExecuteSweep()
        {
            // Follow BFS sweep path until exhausted or target spotted
            if (activePath.Count == 0 || currentPathIndex >= activePath.Count)
            {
                // Sweep completed
                TransitionTo(StrikerState.Patrol);
            }
        }

        void ExecuteAlert()
        {
            // Freeze locomotion and lock facing towards danger
            if (target != null)
            {
                Vector3 lookDir = (target.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }

                if (targetInSight && targetDistance <= attackRange)
                {
                    TryAttack();
                }
            }
        }

        void TryAttack()
        {
            if (Time.time < nextAttackTime) return;
            if (target == null) return;

            nextAttackTime = Time.time + attackCooldown;

            // Strict line of sight raycast before applying damage (cannot shoot through walls)
            Vector3 muzzlePos = muzzlePoint.position;
            Vector3 targetChest = target.position + Vector3.up * 1.0f;
            Vector3 aimDir = (targetChest - muzzlePos).normalized;

            if (Physics.Raycast(muzzlePos, aimDir, out RaycastHit hit, attackRange, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(attackDamage, gameObject);
                }
            }
        }

        #endregion

        #region Locomotion

        void ApplyLocomotion()
        {
            // Gravity
            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -1.5f;
            }
            else
            {
                verticalSpeed -= 9.81f * Time.deltaTime;
            }

            // No horizontal movement in Alert state
            if (currentState == StrikerState.Alert)
            {
                controller.Move(Vector3.up * verticalSpeed * Time.deltaTime);
                return;
            }

            if (activePath == null || activePath.Count == 0 || currentPathIndex >= activePath.Count)
            {
                controller.Move(Vector3.up * verticalSpeed * Time.deltaTime);
                return;
            }

            Vector3 currentWaypoint = activePath[currentPathIndex];
            Vector3 toWaypoint = currentWaypoint - transform.position;
            toWaypoint.y = 0f;

            if (toWaypoint.magnitude <= waypointThreshold)
            {
                currentPathIndex++;
                if (currentPathIndex >= activePath.Count)
                {
                    controller.Move(Vector3.up * verticalSpeed * Time.deltaTime);
                    return;
                }
                currentWaypoint = activePath[currentPathIndex];
                toWaypoint = currentWaypoint - transform.position;
                toWaypoint.y = 0f;
            }

            Vector3 moveDir = toWaypoint.normalized;
            float speed = (currentState == StrikerState.Pursue) ? pursueSpeed : patrolSpeed;

            // Rotation
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            Vector3 velocity = moveDir * speed + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        #endregion

        #region Damage and Reaction

        void OnDamageTaken(CombatHealth sender, GameObject source, float damage)
        {
            if (source != null)
            {
                // Update memory of attacker location even if outside FOV
                lastKnownTargetPosition = source.transform.position;
                lastSeenTime = Time.time;
                hasSeenTarget = true;

                // Turn towards attacker
                Vector3 toAttacker = source.transform.position - transform.position;
                toAttacker.y = 0;
                if (toAttacker.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(toAttacker);
                }

                if (currentState == StrikerState.Patrol)
                {
                    TransitionTo(StrikerState.Pursue);
                }
            }
        }

        void OnDefeated()
        {
            activePath.Clear();
        }

        #endregion

        #region Visualization & Telemetry

        void OnGUI()
        {
            if (!showDecisionOverlay) return;

            int panelWidth = 260;
            int panelHeight = 160;
            int x = Screen.width - panelWidth - 20;
            int y = 20;

            GUI.Box(new Rect(x, y, panelWidth, panelHeight), "STRIKER DRONE TELEMETRY");

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true
            };

            string stateColor = currentState switch
            {
                StrikerState.Patrol => "cyan",
                StrikerState.Pursue => "red",
                StrikerState.Sweep => "yellow",
                StrikerState.Alert => "orange",
                _ => "white"
            };

            string losText = targetInSight ? "<color=green>CLEAR</color>" : "<color=grey>BLOCKED</color>";
            float hpPct = health != null ? health.HealthFraction * 100f : 100f;
            float memPct = MemoryConfidence * 100f;

            GUI.Label(new Rect(x + 10, y + 26, panelWidth - 20, 20), $"State: <b><color={stateColor}>{currentState}</color></b>", labelStyle);
            GUI.Label(new Rect(x + 10, y + 46, panelWidth - 20, 20), $"Health: {hpPct:F0}% | Alert: {alertHealthThreshold * 100f:F0}%", labelStyle);
            GUI.Label(new Rect(x + 10, y + 66, panelWidth - 20, 20), $"Line of Sight: {losText} ({targetDistance:F1}m)", labelStyle);
            GUI.Label(new Rect(x + 10, y + 86, panelWidth - 20, 20), $"Memory Confidence: {memPct:F0}%", labelStyle);
            GUI.Label(new Rect(x + 10, y + 106, panelWidth - 20, 20), $"Path Nodes Left: {RemainingPathPoints}", labelStyle);
            GUI.Label(new Rect(x + 10, y + 126, panelWidth - 20, 20), $"Grid Topology Ver: {(grid != null ? grid.Version.ToString() : "N/A")}", labelStyle);
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // FOV cone
            Vector3 eyePos = eyePoint != null ? eyePoint.position : transform.position;
            Gizmos.color = targetInSight ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(1f, 0.9f, 0.2f, 0.3f);
            Vector3 leftRay = Quaternion.Euler(0, -fieldOfView * 0.5f, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, fieldOfView * 0.5f, 0) * transform.forward;
            Gizmos.DrawRay(eyePos, leftRay * sightRange);
            Gizmos.DrawRay(eyePos, rightRay * sightRange);
            Gizmos.DrawRay(eyePos, transform.forward * sightRange);

            // Active path line
            if (activePath != null && activePath.Count > 0)
            {
                Gizmos.color = Color.cyan;
                Vector3 prev = transform.position;
                for (int i = currentPathIndex; i < activePath.Count; i++)
                {
                    Gizmos.DrawLine(prev, activePath[i] + Vector3.up * 0.1f);
                    Gizmos.DrawWireSphere(activePath[i] + Vector3.up * 0.1f, 0.15f);
                    prev = activePath[i];
                }
            }

            // Last known target position
            if (hasSeenTarget && MemoryConfidence > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lastKnownTargetPosition + Vector3.up * 0.5f, 0.4f);
            }
        }

        #endregion
    }
}

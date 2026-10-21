using UnityEngine;

namespace Skybreak.Movement
{
    /// <summary>
    /// Turns a planner's path array into smooth, collision-aware CharacterController movement
    /// (Docs/GV/AgentMovement/RIFT_DISRUPTOR_MOVEMENT.md).
    ///
    /// Each frame:
    ///   1. Target speed = requested speed x heading alignment x corner factor, capped by the braking
    ///      speed sqrt(2 a d) for the distance d left on the path, so it stops on the final waypoint.
    ///   2. Current speed eases towards it (separate acceleration and deceleration).
    ///   3. A movement budget (speed x dt) is spent along the path. Each segment is a separate
    ///      CharacterController.Move, so a fast agent never cuts a corner within one frame, and a waypoint
    ///      only advances when the body actually reached it.
    ///   4. Yaw turns towards the travel direction at no more than turnRate degrees per second.
    ///   5. Velocity is measured from the actual displacement after collisions. Animation reads that,
    ///      so walking into a closed door shows as stopping, not running on the spot.
    /// The follower never chooses a destination: it reports Arrived, Blocked or Invalid and the brain decides.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class SmoothPathFollower : MonoBehaviour, IPathFollower
    {
        [Header("Speed")]
        public float acceleration = 9f;
        public float deceleration = 14f;

        [Header("Turning")]
        [Tooltip("Degrees per second. Rotation never exceeds turnRate x dt in one frame.")]
        public float turnRate = 360f;
        [Tooltip("Heading error below which full speed is allowed.")]
        public float fullSpeedAngle = 20f;
        [Tooltip("Heading error at which forward speed reaches zero (turn on the spot first).")]
        public float turnInPlaceAngle = 100f;

        [Header("Corners")]
        [Tooltip("Speed fraction kept through a 90 degree corner.")]
        [Range(0.1f, 1f)] public float cornerSpeedFraction = 0.45f;
        public float cornerLookahead = 1.4f;

        [Header("Waypoints")]
        public float arriveRadius = 0.12f;

        [Header("Route validation")]
        public bool validateSegments = true;
        [Tooltip("Solid geometry. Layer 8 (actors) is excluded so agents never invalidate each other's routes.")]
        public LayerMask obstacleMask = ~(1 << 8);
        [Range(0.1f, 1f)] public float validationRadiusFraction = 0.35f;

        [Header("Blocked detection")]
        public float blockedSeconds = 0.5f;
        [Range(0.05f, 0.9f)] public float blockedRatio = 0.25f;

        [Header("Gravity")]
        public float gravity = -20f;

        const int MaxSegmentMovesPerFrame = 4;

        CharacterController body;
        PathResult path;
        Vector3[] points = System.Array.Empty<Vector3>();
        int index;
        float requestedSpeed, vertical, stuckTime, lastYaw;
        Vector3? faceTarget;

        public MotorReport Report { get; private set; } = MotorReport.Idle;
        public Vector3 PlanarVelocity { get; private set; }
        public int WaypointIndex => index;
        public PathResult CurrentPath => path;
        /// <summary>Speed the motor is trying to move at this frame (before collisions).</summary>
        public float CurrentSpeed { get; private set; }
        public float RequestedSpeed => requestedSpeed;
        /// <summary>Measured yaw rate, degrees per second (positive = turning right).</summary>
        public float YawRate { get; private set; }
        /// <summary>Why the last path was rejected, for debugging.</summary>
        public string LastIssue { get; private set; } = "";
        /// <summary>Largest planar distance moved in one frame since the last path started (teleport check).</summary>
        public float MaxFrameStep { get; private set; }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.minMoveDistance = 0;   // the 1 mm default drops small steps at high frame rates
            lastYaw = transform.eulerAngles.y;
        }

        // ------------------------------------------------------------------ commands
        public void SetPath(PathResult result, float speed)
        {
            faceTarget = null; stuckTime = 0; MaxFrameStep = 0; LastIssue = "";
            if (result == null || !result.HasRoute) { Reject("empty or NoPath route"); return; }
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed)) { Reject("invalid speed"); return; }
            var copy = (Vector3[])result.Waypoints.Clone();   // the planner may reuse its buffers
            for (int i = 0; i < copy.Length; i++)
                if (!Finite(copy[i])) { Reject($"non-finite waypoint {i}"); return; }
            if (validateSegments)
            {
                Vector3 from = transform.position;
                for (int i = 0; i < copy.Length; i++)
                {
                    if (SegmentBlocked(from, copy[i])) { Reject($"segment {i} crosses solid geometry"); return; }
                    from = copy[i];
                }
            }
            path = result; points = copy; index = 0; requestedSpeed = speed;
            Report = MotorReport.Moving;
        }

        public void Stop()
        {
            path = null; points = System.Array.Empty<Vector3>();
            if (Report == MotorReport.Moving) Report = MotorReport.Stopped;
            else if (Report != MotorReport.Invalid) Report = MotorReport.Stopped;
        }

        public void Face(Vector3 worldPoint) { if (Finite(worldPoint)) faceTarget = worldPoint; }

        void Reject(string why)
        {
            path = null; points = System.Array.Empty<Vector3>(); LastIssue = why; Report = MotorReport.Invalid;
        }

        // A thin sphere at waist height. Clearance is the planner's job (it inflates obstacles by the body
        // radius); this catches what changed after planning: a door that closed or a barricade pushed
        // across the route. It is deliberately thinner than the body because smoothed segments run
        // between cell centres and may legitimately pass closer to a wall than a centre does.
        bool SegmentBlocked(Vector3 a, Vector3 b)
        {
            Vector3 lift = Vector3.up * Mathf.Max(body.radius + 0.25f, body.center.y);
            Vector3 d = Flat(b - a);
            float length = d.magnitude;
            if (length < 0.01f) return false;
            return Physics.SphereCast(a + lift, body.radius * validationRadiusFraction, d / length, out _, length, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 start = transform.position;
            Vector3 travelDirection = Vector3.zero;

            if (Report == MotorReport.Moving) travelDirection = FollowPath(dt);
            else CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0, deceleration * dt);

            // Gravity is its own move so it never eats the horizontal budget.
            vertical = body.isGrounded ? -2f : Mathf.Max(vertical + gravity * dt, -30f);
            if (body.enabled) body.Move(Vector3.up * vertical * dt);

            Vector3 moved = Flat(transform.position - start);
            PlanarVelocity = moved / dt;
            if (Report == MotorReport.Moving || Report == MotorReport.Arrived) MaxFrameStep = Mathf.Max(MaxFrameStep, moved.magnitude);

            Turn(travelDirection, dt);
            float yaw = transform.eulerAngles.y;
            YawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
        }

        Vector3 FollowPath(float dt)
        {
            Vector3 position = transform.position;
            // Consume waypoints we are already standing on (e.g. a replacement path starting here).
            while (index < points.Length - 1 && Flat(points[index] - position).magnitude <= arriveRadius) index++;
            if (index >= points.Length || (index == points.Length - 1 && Flat(points[index] - position).magnitude <= arriveRadius))
            { Arrive(); return Vector3.zero; }

            Vector3 toNext = Flat(points[index] - position);
            Vector3 direction = toNext.normalized;

            // 1. target speed
            float headingError = Vector3.Angle(Flat(transform.forward), direction);
            float alignment = 1 - Mathf.Clamp01((headingError - fullSpeedAngle) / Mathf.Max(1, turnInPlaceAngle - fullSpeedAngle));
            float target = requestedSpeed * alignment * CornerFactor(position);
            target = Mathf.Min(target, Mathf.Sqrt(2 * deceleration * RemainingDistance(position)));
            // 2. ease
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, (target > CurrentSpeed ? acceleration : deceleration) * dt);

            // 3. spend the budget segment by segment
            float budget = CurrentSpeed * dt, requested = budget, achieved = 0;
            for (int moves = 0; budget > 1e-5f && index < points.Length && moves < MaxSegmentMovesPerFrame; moves++)
            {
                Vector3 here = transform.position;
                Vector3 leg = Flat(points[index] - here);
                float legLength = leg.magnitude;
                if (legLength <= 1e-4f) { index++; continue; }
                float stepLength = Mathf.Min(budget, legLength);
                Vector3 step = leg / legLength * stepLength;
                if (body.enabled) body.Move(step);
                float actual = Flat(transform.position - here).magnitude;
                achieved += actual; budget -= stepLength;
                if (actual < stepLength * blockedRatio) break;                  // collision ate the step: stop spending
                if (stepLength >= legLength - 1e-4f && Flat(points[index] - transform.position).magnitude <= arriveRadius)
                {
                    if (index == points.Length - 1) { Arrive(); return direction; }
                    index++;
                }
            }

            // Blocked: we asked for real movement and got under blockedRatio of it, for long enough.
            if (requested > 1e-4f && achieved < requested * blockedRatio) stuckTime += dt; else stuckTime = 0;
            if (stuckTime > blockedSeconds) { Report = MotorReport.Blocked; CurrentSpeed = 0; }
            return index < points.Length ? Flat(points[index] - transform.position).normalized : direction;
        }

        void Arrive() { Report = MotorReport.Arrived; CurrentSpeed = 0; stuckTime = 0; }

        float CornerFactor(Vector3 position)
        {
            if (index + 1 >= points.Length) return 1;
            Vector3 inbound = Flat(points[index] - position), outbound = Flat(points[index + 1] - points[index]);
            float d = inbound.magnitude;
            if (d > cornerLookahead || outbound.sqrMagnitude < 1e-4f || inbound.sqrMagnitude < 1e-4f) return 1;
            float turn = Vector3.Angle(inbound, outbound);
            float slow = Mathf.Lerp(1, cornerSpeedFraction, Mathf.Clamp01(turn / 90f));
            return Mathf.Lerp(slow, 1, d / cornerLookahead);
        }

        float RemainingDistance(Vector3 position)
        {
            float total = Flat(points[index] - position).magnitude;
            for (int i = index + 1; i < points.Length; i++) total += Flat(points[i] - points[i - 1]).magnitude;
            return total;
        }

        void Turn(Vector3 travelDirection, float dt)
        {
            Vector3 want = travelDirection;
            if (want.sqrMagnitude < 1e-4f && faceTarget.HasValue && Report != MotorReport.Moving) want = Flat(faceTarget.Value - transform.position);
            if (want.sqrMagnitude < 1e-4f) return;
            var goal = Quaternion.LookRotation(want.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnRate * dt);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        void OnDrawGizmosSelected()
        {
            if (points == null || points.Length == 0) return;
            Gizmos.color = Report == MotorReport.Moving ? Color.cyan : Color.gray;
            Vector3 prev = transform.position + Vector3.up * .05f;
            for (int i = index; i < points.Length; i++) { Gizmos.DrawLine(prev, points[i] + Vector3.up * .05f); Gizmos.DrawWireSphere(points[i], .08f); prev = points[i] + Vector3.up * .05f; }
        }
    }
}

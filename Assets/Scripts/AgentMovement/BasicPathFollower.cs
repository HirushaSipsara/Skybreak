using UnityEngine;

namespace Skybreak.Movement
{
    /// <summary>
    /// Minimal <see cref="IPathFollower"/>: constant speed along the waypoints through a CharacterController,
    /// facing snapped to the travel direction. It exists so a brain can be developed and tested before the
    /// smooth motor/animation layer; the brain only ever talks to the interface, so the motor can be swapped.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class BasicPathFollower : MonoBehaviour, IPathFollower
    {
        public float arriveRadius = 0.15f;
        public float blockedSeconds = 0.5f;
        public float gravity = -20f;

        CharacterController body;
        PathResult path;
        Vector3[] points = System.Array.Empty<Vector3>();
        int index;
        float speed, stuck, vertical;
        Vector3? faceTarget;

        public MotorReport Report { get; private set; } = MotorReport.Idle;
        public Vector3 PlanarVelocity { get; private set; }
        public int WaypointIndex => index;
        public PathResult CurrentPath => path;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            // Default 0.001 m silently drops small per-frame steps at high frame rates (speed x dt < 1 mm).
            body.minMoveDistance = 0;
        }

        public void SetPath(PathResult result, float requestedSpeed)
        {
            faceTarget = null; stuck = 0;
            if (result == null || !result.HasRoute || requestedSpeed <= 0 || float.IsNaN(requestedSpeed))
            { path = null; points = System.Array.Empty<Vector3>(); Report = MotorReport.Invalid; return; }
            foreach (var p in result.Waypoints)
                if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z)) { Report = MotorReport.Invalid; return; }
            path = result; points = (Vector3[])result.Waypoints.Clone(); index = 0; speed = requestedSpeed;
            Report = MotorReport.Moving;
        }

        public void Stop() { path = null; points = System.Array.Empty<Vector3>(); Report = MotorReport.Stopped; PlanarVelocity = Vector3.zero; }
        public void Face(Vector3 worldPoint) { faceTarget = worldPoint; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 before = transform.position;
            Vector3 step = Vector3.zero;
            if (Report == MotorReport.Moving)
            {
                while (index < points.Length && Planar(points[index] - before).magnitude <= arriveRadius) index++;
                if (index >= points.Length) { Report = MotorReport.Arrived; }
                else
                {
                    Vector3 to = Planar(points[index] - before);
                    step = to.normalized * Mathf.Min(speed * dt, to.magnitude);
                    if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                }
            }
            else if (faceTarget.HasValue)
            {
                Vector3 to = Planar(faceTarget.Value - before);
                if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            vertical = body.isGrounded ? -1f : vertical + gravity * dt;
            if (body.enabled) body.Move(step + Vector3.up * vertical * dt);
            Vector3 moved = Planar(transform.position - before);
            PlanarVelocity = moved / dt;
            if (Report == MotorReport.Moving && step.sqrMagnitude > 1e-6f)
            {
                // Blocked when we achieve under a quarter of the requested displacement for a while.
                stuck = moved.magnitude < step.magnitude * 0.25f ? stuck + dt : 0;
                if (stuck > blockedSeconds) Report = MotorReport.Blocked;
            }
        }

        static Vector3 Planar(Vector3 v) { v.y = 0; return v; }
    }
}

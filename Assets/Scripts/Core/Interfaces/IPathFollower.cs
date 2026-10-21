using UnityEngine;

namespace Skybreak.Movement
{
    public enum PathStatus { Complete, Partial, NoPath }
    public enum MotorReport { Idle, Moving, Arrived, Blocked, Invalid, Stopped }

    /// <summary>
    /// A route computed by an agent's planner (Docs/TeamGuide/STUDENT_4_AGENT_MOVEMENT.md, section 4).
    /// The follower takes its own copy of <see cref="Waypoints"/>, so a planner can reuse its buffers.
    /// Reaching the end of a Partial route does not mean the original goal was reached.
    /// </summary>
    public sealed class PathResult
    {
        public PathStatus Status;
        public Vector3[] Waypoints = System.Array.Empty<Vector3>();
        public int NavigationVersion;
        public int Expanded;
        public float Length;

        public static PathResult None(int version) => new PathResult { Status = PathStatus.NoPath, NavigationVersion = version };
        public bool HasRoute => Status != PathStatus.NoPath && Waypoints != null && Waypoints.Length > 0;
    }

    /// <summary>
    /// Shared contract between an agent brain and whatever turns a path array into movement.
    /// The follower never decides where to go: it reports Arrived / Blocked / Invalid and the brain replans.
    /// </summary>
    public interface IPathFollower
    {
        void SetPath(PathResult path, float speed);
        void Stop();
        /// <summary>Turn on the spot towards a world point (ignored while following a path).</summary>
        void Face(Vector3 worldPoint);
        MotorReport Report { get; }
        Vector3 PlanarVelocity { get; }
        int WaypointIndex { get; }
        PathResult CurrentPath { get; }
    }
}

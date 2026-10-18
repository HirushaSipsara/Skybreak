using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.Movement
{
    public interface IAgentMotor { bool MoveTo(Vector3 destination, float stopDistance); void Stop(); bool Blocked { get; } }
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshMotor : MonoBehaviour, IAgentMotor
    {
        public NavMeshAgent Agent { get; private set; }
        public bool Blocked { get; private set; }
        public string Status { get; private set; } = "Idle";
        NavMeshPath path;
        float nextPlan, stalled;
        Vector3 lastPosition, requested;
        bool moving;
        void Awake() { Agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); lastPosition = transform.position; }
        public bool MoveTo(Vector3 destination, float stopDistance)
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (path == null) path = new NavMeshPath();
            if (!Agent.enabled || !Agent.isOnNavMesh) { Blocked = true; Status = "Off navigation"; return false; }
            Agent.stoppingDistance = stopDistance;
            if (Time.time < nextPlan && moving && (destination - requested).sqrMagnitude < 1) return !Blocked;
            nextPlan = Time.time + .4f; requested = destination;
            var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            if (!NavMesh.SamplePosition(destination, out var hit, 1.2f, filter) ||
                !Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            { Stop(); Blocked = true; Status = "Unreachable / waiting to replan"; return false; }
            Agent.isStopped = false; Agent.SetPath(path); moving = true; Blocked = false; Status = "Following complete path"; return true;
        }
        public void Stop()
        {
            moving = false; stalled = 0;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
            Status = "Stopped";
        }
        void Update()
        {
            if (moving && Agent.isOnNavMesh && !Agent.pathPending)
            {
                if (Agent.remainingDistance <= Agent.stoppingDistance + .15f) { moving = false; Blocked = false; Status = "Arrived"; }
                else
                {
                    stalled = (transform.position - lastPosition).sqrMagnitude < .000025f ? stalled + Time.deltaTime : 0;
                    if (stalled > .8f) { Blocked = true; nextPlan = 0; Status = "Blocked / replan requested"; }
                }
            }
            lastPosition = transform.position;
        }
    }
}

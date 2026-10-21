using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    /// <summary>
    /// Movement controller and animation trigger dispatcher for the Crystal Echo Warden.
    /// Manages waypoint navigation via CharacterController (and optional NavMeshAgent integration),
    /// collision-aware movement replanning, orientation, and animation state triggers.
    /// Fulfills "WardenMovement: NavMesh movement and animation triggers" from the architecture specification.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class WardenMovement : MonoBehaviour
    {
        public WardenMap map;
        public float speed = 2.8f;
        public float rotationSpeed = 220f;

        public int[] Path { get; protected set; } = System.Array.Empty<int>();
        public RouteStatus LastResult { get; protected set; } = RouteStatus.Invalid;
        public bool Blocked { get; protected set; }
        public bool Arrived => cursor >= Path.Length;

        public float CurrentSpeed { get; protected set; }
        public float TurnRate { get; protected set; }

        protected int cursor, goal = -1, version = -1;
        protected float retryAt, stuckFor;
        protected CharacterController body;
        protected UnityEngine.AI.NavMeshAgent navAgent;
        protected Vector3 lastPosition;
        protected float lastYaw;

        public event System.Action<MotionClip> AnimationTriggered;

        protected virtual void Awake()
        {
            body = GetComponent<CharacterController>();
            navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null) navAgent.updatePosition = false; // Synchronized with CharacterController
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
        }

        public virtual void Stop()
        {
            Path = System.Array.Empty<int>();
            cursor = 0;
            goal = -1;
            stuckFor = 0;
            CurrentSpeed = 0;
            if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh) navAgent.isStopped = true;
        }

        public virtual void ResetForEncounter()
        {
            Stop();
            version = -1;
            Blocked = false;
            retryAt = 0;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
        }

        public virtual void Go(int node)
        {
            if (map == null || map.Graph == null || !map.Graph.Valid(node)) { Stop(); return; }
            if (node == goal && version == map.Graph.Version && !Blocked) return;
            if (Time.time < retryAt) return;

            int start = map.AccessibleNode(transform.position);
            var route = Search.AStar(map.Graph, start, node, 256);
            LastResult = route.Status;
            goal = node;
            version = map.Graph.Version;
            Blocked = false;
            stuckFor = 0;
            Path = route.Nodes;
            cursor = 0;

            if (route.Status != RouteStatus.Found)
            {
                retryAt = Time.time + 1;
                Blocked = true;
            }
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0)
            {
                Vector3 moved = transform.position - lastPosition;
                moved.y = 0;
                CurrentSpeed = moved.magnitude / dt;
                float yaw = transform.eulerAngles.y;
                TurnRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
                lastPosition = transform.position;
                lastYaw = yaw;
            }

            if (map == null || map.Graph == null || Arrived || body == null || !body.enabled) return;

            if (version != map.Graph.Version)
            {
                int destination = goal;
                Stop();
                Go(destination);
                return;
            }

            Vector3 destinationPoint = map.nodes[Path[cursor]];
            Vector3 delta = destinationPoint - transform.position;

            if (delta.magnitude < 0.16f)
            {
                cursor++;
                return;
            }

            Vector3 old = transform.position;
            if (map.ClearSegment(old, destinationPoint))
            {
                Vector3 step = delta.normalized * Mathf.Min(delta.magnitude, speed * dt);
                body.Move(step);
                if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                {
                    navAgent.nextPosition = transform.position;
                }
            }

            if ((transform.position - old).sqrMagnitude < 0.000001f) stuckFor += dt;
            else stuckFor = 0;

            if (stuckFor > 0.65f)
            {
                Blocked = true;
                retryAt = Time.time + 0.8f;
                Path = System.Array.Empty<int>();
                cursor = 0;
                map.Refresh();
            }

            Face(delta);
        }

        public virtual void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
            }
        }

        public void TriggerAnimation(MotionClip clip)
        {
            AnimationTriggered?.Invoke(clip);
        }

        protected virtual void OnDisable() { Stop(); }
    }
}

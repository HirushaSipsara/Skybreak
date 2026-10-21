using UnityEngine;
using UnityEngine.Events;

namespace Skybreak.PhysicsInteraction
{
    /// <summary>
    /// Explicit door operational states.
    /// </summary>
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    /// <summary>
    /// Reusable physical security door. Moves its leaf via kinematic Rigidbody physics during FixedUpdate,
    /// blocks passage when closed, clears the doorway when open, and refuses to crush actors.
    /// </summary>
    [SelectionBase]
    public sealed class SlidingSecurityDoor : MonoBehaviour, IInteractable
    {
        [Header("Physics Leaf")]
        [Tooltip("The Rigidbody attached to the moving door leaf. Must be kinematic with interpolation enabled.")]
        [SerializeField] Rigidbody leafRigidbody;

        [Tooltip("Local position of the leaf when fully closed.")]
        [SerializeField] Vector3 closedLocalPosition = Vector3.zero;

        [Tooltip("Local position of the leaf when fully open (e.g. raised 3.2m to clear the 3m opening).")]
        [SerializeField] Vector3 openLocalPosition = new Vector3(0f, 3.2f, 0f);

        [Tooltip("Linear travel speed in meters per second.")]
        [SerializeField, Min(0.1f)] float moveSpeed = 2.4f;

        [Header("Initial State")]
        [SerializeField] DoorState initialState = DoorState.Closed;

        [Header("Threshold Clearance (Anti-Crush)")]
        [Tooltip("Local center of the threshold overlap volume.")]
        [SerializeField] Vector3 thresholdCenter = new Vector3(0f, 1.5f, 0f);

        [Tooltip("Half-extents of the threshold check box (X: half-width, Y: half-height, Z: half-depth).")]
        [SerializeField] Vector3 thresholdHalfExtents = new Vector3(1.4f, 1.45f, 0.55f);

        [Tooltip("Layers checked for obstructions in the doorway.")]
        [SerializeField] LayerMask obstructionMask = ~0;

        [Header("Events")]
        [SerializeField] UnityEvent onOpened = new UnityEvent();
        [SerializeField] UnityEvent onClosed = new UnityEvent();
        [SerializeField] UnityEvent<DoorState> onStateChanged = new UnityEvent<DoorState>();

        [Header("AI Grid Integration (IS Module)")]
        [Tooltip("Notify VaultHoundGrid when door opens or closes so AI agents dynamically update pathfinding.")]
        [SerializeField] bool updateHoundGrid = true;

        DoorState state = DoorState.Closed;

        public DoorState State => state;
        public bool IsOpen => state == DoorState.Open;
        public bool IsClosed => state == DoorState.Closed;
        public bool IsMoving => state == DoorState.Opening || state == DoorState.Closing;

        public UnityEvent OnOpened => onOpened;
        public UnityEvent OnClosed => onClosed;
        public UnityEvent<DoorState> OnStateChanged => onStateChanged;

        // IInteractable implementation
        public string Prompt
        {
            get
            {
                switch (state)
                {
                    case DoorState.Closed:
                    case DoorState.Closing:
                        return "F  /  Open security door";
                    case DoorState.Open:
                    case DoorState.Opening:
                        return "F  /  Close security door";
                    default:
                        return "F  /  Operate door";
                }
            }
        }

        public bool CanInteract(GameObject actor)
        {
            // If currently open and doorway is obstructed, do not offer close interaction
            if (state == DoorState.Open && IsObstructed()) return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            Toggle();
        }

        void Awake()
        {
            if (leafRigidbody == null)
            {
                leafRigidbody = GetComponentInChildren<Rigidbody>();
            }

            if (leafRigidbody == null)
            {
                Debug.LogError($"[SlidingSecurityDoor] No leaf Rigidbody assigned on '{name}'!", this);
                enabled = false;
                return;
            }

            // Configure leaf Rigidbody for reliable physics timestep movement
            leafRigidbody.isKinematic = true;
            leafRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            leafRigidbody.useGravity = false;

            state = initialState;
            Vector3 startLocal = (state == DoorState.Open) ? openLocalPosition : closedLocalPosition;
            leafRigidbody.transform.localPosition = startLocal;
        }

        public void Open()
        {
            if (state == DoorState.Open || state == DoorState.Opening) return;
            TransitionTo(DoorState.Opening);
        }

        public void Close()
        {
            if (state == DoorState.Closed || state == DoorState.Closing) return;

            // Refuse to close if the doorway is already occupied
            if (IsObstructed())
            {
                return;
            }

            TransitionTo(DoorState.Closing);
        }

        public void Toggle()
        {
            if (state == DoorState.Closed || state == DoorState.Closing)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        void TransitionTo(DoorState newState)
        {
            if (state == newState) return;
            state = newState;
            onStateChanged?.Invoke(state);

            if (state == DoorState.Open)
            {
                SyncGridPassability(true);
                onOpened?.Invoke();
            }
            else if (state == DoorState.Closed)
            {
                SyncGridPassability(false);
                onClosed?.Invoke();
            }
            else if (state == DoorState.Closing)
            {
                SyncGridPassability(false);
            }
        }

        void SyncGridPassability(bool isPassable)
        {
            if (!updateHoundGrid) return;
            var gridType = System.Type.GetType("Skybreak.AI.SawanIS.VaultHound.VaultHoundGrid, Assembly-CSharp");
            if (gridType != null)
            {
                var instanceProp = gridType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance != null)
                {
                    Vector3 center = transform.TransformPoint(thresholdCenter);
                    Vector3 size = thresholdHalfExtents * 2f;
                    var method = gridType.GetMethod("SetAreaPassable", new System.Type[] { typeof(Bounds), typeof(bool) });
                    method?.Invoke(instance, new object[] { new Bounds(center, size), isPassable });
                }
            }
        }

        void FixedUpdate()
        {
            if (leafRigidbody == null) return;

            if (state == DoorState.Opening)
            {
                Vector3 targetWorld = transform.TransformPoint(openLocalPosition);
                Vector3 nextPos = Vector3.MoveTowards(leafRigidbody.position, targetWorld, moveSpeed * Time.fixedDeltaTime);
                leafRigidbody.MovePosition(nextPos);

                if (Vector3.Distance(nextPos, targetWorld) < 0.001f)
                {
                    leafRigidbody.MovePosition(targetWorld);
                    TransitionTo(DoorState.Open);
                }
            }
            else if (state == DoorState.Closing)
            {
                // Safety anti-crush: if an actor steps into the threshold during closing, immediately reopen
                if (IsObstructed())
                {
                    TransitionTo(DoorState.Opening);
                    return;
                }

                Vector3 targetWorld = transform.TransformPoint(closedLocalPosition);
                Vector3 nextPos = Vector3.MoveTowards(leafRigidbody.position, targetWorld, moveSpeed * Time.fixedDeltaTime);
                leafRigidbody.MovePosition(nextPos);

                if (Vector3.Distance(nextPos, targetWorld) < 0.001f)
                {
                    leafRigidbody.MovePosition(targetWorld);
                    TransitionTo(DoorState.Closed);
                }
            }
        }

        /// <summary>
        /// Checks whether the threshold volume is occupied by the player, a physics body, or an AI agent.
        /// </summary>
        public bool IsObstructed()
        {
            Vector3 worldCenter = transform.TransformPoint(thresholdCenter);
            Quaternion worldRotation = transform.rotation;

            Collider[] overlapping = Physics.OverlapBox(worldCenter, thresholdHalfExtents, worldRotation, obstructionMask, QueryTriggerInteraction.Ignore);
            foreach (var col in overlapping)
            {
                if (col == null) continue;
                // Ignore all colliders belonging to the door assembly itself
                if (col.transform.IsChildOf(transform)) continue;

                // Recognize physical actors: CharacterController, dynamic/kinematic Rigidbody, or NavMeshAgent
                if (col is CharacterController || col.attachedRigidbody != null || col.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            // Clearance check box gizmo (green wireframe)
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(thresholdCenter), transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, thresholdHalfExtents * 2f);
            Gizmos.matrix = previousMatrix;

            // Visual leaf movement travel path (cyan line)
            Gizmos.color = Color.cyan;
            Vector3 worldClosed = transform.TransformPoint(closedLocalPosition);
            Vector3 worldOpen = transform.TransformPoint(openLocalPosition);
            Gizmos.DrawLine(worldClosed, worldOpen);
            Gizmos.DrawWireSphere(worldClosed, 0.1f);
            Gizmos.DrawWireSphere(worldOpen, 0.1f);
        }
    }
}

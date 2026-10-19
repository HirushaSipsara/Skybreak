using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Skybreak.PhysicsInteraction
{
    /// <summary>
    /// Heavy physical barricade that can be pushed by the player into tactical positions
    /// (e.g. blocking doorways, creating cover). Automatically freezes tilt rotations to prevent
    /// tipping and dynamically manages NavMesh obstacle carving when stationary.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class MovableBarricade : MonoBehaviour
    {
        [Header("Physical Properties")]
        [Tooltip("Mass in kg. Heavy enough to feel substantial, light enough for the player to push.")]
        [SerializeField, Min(10f)] float barricadeMass = 80f;

        [Tooltip("Linear drag applied when sliding on the floor to prevent an ice-skating effect.")]
        [SerializeField, Min(0.1f)] float floorFrictionDrag = 3.5f;

        [Tooltip("Angular drag to stabilize residual rotation.")]
        [SerializeField, Min(0.1f)] float angularDrag = 5f;

        [Header("AI & Navigation Integration")]
        [Tooltip("Whether to automatically configure an attached NavMeshObstacle to carve when stationary.")]
        [SerializeField] bool updateNavMeshCarving = true;

        [Tooltip("Velocity threshold below which the barricade is considered stationary.")]
        [SerializeField] float settledVelocityThreshold = 0.05f;

        [Header("Events")]
        [SerializeField] UnityEvent onPushed = new UnityEvent();
        [SerializeField] UnityEvent onSettled = new UnityEvent();

        Rigidbody rb;
        NavMeshObstacle navObstacle;
        bool isMoving;

        public bool IsMoving => isMoving;
        public Rigidbody Rigidbody => rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            navObstacle = GetComponent<NavMeshObstacle>();

            ConfigureRigidbody();
            ConfigureNavObstacle();
        }

        void ConfigureRigidbody()
        {
            rb.mass = barricadeMass;
            rb.linearDamping = floorFrictionDrag;
            rb.angularDamping = angularDrag;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Freeze X and Z rotations so the barricade slides flat along the floor without toppling
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void ConfigureNavObstacle()
        {
            if (navObstacle != null && updateNavMeshCarving)
            {
                navObstacle.carving = true;
                navObstacle.carveOnlyStationary = true;
                navObstacle.carvingMoveThreshold = 0.15f;
                navObstacle.carvingTimeToStationary = 0.4f;
            }
        }

        void FixedUpdate()
        {
            float speed = rb.linearVelocity.sqrMagnitude;

            if (speed > settledVelocityThreshold * settledVelocityThreshold)
            {
                if (!isMoving)
                {
                    isMoving = true;
                    onPushed?.Invoke();
                }
            }
            else
            {
                if (isMoving)
                {
                    isMoving = false;
                    onSettled?.Invoke();
                }
            }
        }

        void OnValidate()
        {
            if (rb != null)
            {
                rb.mass = barricadeMass;
                rb.linearDamping = floorFrictionDrag;
            }
        }
    }
}

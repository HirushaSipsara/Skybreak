using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Skybreak.PhysicsInteraction
{
    /// <summary>
    /// Lifecycle state of a physical movable/throwable prop.
    /// </summary>
    public enum PropState
    {
        Resting,    // Dynamic physics simulation on the floor/environment
        Carried,    // Held by the player (kinematic movement in FixedUpdate)
        Thrown      // High-speed ballistic flight with continuous collision detection
    }

    /// <summary>
    /// Reusable dynamic physics prop that can be picked up, carried safely without wall-clipping,
    /// dropped, or thrown with an impulse force. Implements IInteractable for seamless PlayerInteractor support.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsProp : MonoBehaviour, IInteractable
    {
        [Header("Carry & Positioning")]
        [Tooltip("Target distance in front of the view camera while carried.")]
        [SerializeField, Min(0.5f)] float holdDistance = 1.7f;

        [Tooltip("Minimum allowable distance when compressed against a wall.")]
        [SerializeField, Min(0.2f)] float minHoldDistance = 0.5f;

        [Tooltip("Vertical offset relative to camera line of sight (negative lowers towards chest).")]
        [SerializeField] float verticalHoldOffset = -0.2f;

        [Header("Throw Physics")]
        [Tooltip("Instantaneous impulse force applied along camera forward on throw.")]
        [SerializeField, Min(1f)] float throwImpulse = 15f;

        [Tooltip("Collision layers that block the carried prop (must exclude player layer 8).")]
        [SerializeField] LayerMask obstructionMask = ~(1 << 8);

        [Header("Kinetic Impact")]
        [Tooltip("Minimum relative collision speed (m/s) required to register a heavy impact.")]
        [SerializeField, Min(1f)] float minImpactSpeed = 3.5f;

        [Tooltip("Damage dealt per unit of impact velocity (Speed * Mass * Factor).")]
        [SerializeField] float impactDamageFactor = 1.2f;

        [Header("Events")]
        [SerializeField] UnityEvent onPickedUp = new UnityEvent();
        [SerializeField] UnityEvent onDropped = new UnityEvent();
        [SerializeField] UnityEvent onThrown = new UnityEvent();
        [SerializeField] UnityEvent<float> onImpact = new UnityEvent<float>();

        Rigidbody rb;
        Collider propCollider;
        Collider playerCollider;
        Camera viewCamera;

        PropState state = PropState.Resting;
        bool originalGravity;
        bool originalKinematic;
        float propBoundingRadius = 0.4f;
        int pickUpFrame = -1;

        public PropState State => state;
        public bool IsCarried => state == PropState.Carried;
        public Rigidbody Rigidbody => rb;

        // IInteractable implementation
        public string Prompt => state == PropState.Resting ? "F  /  Pick up prop" : string.Empty;

        public bool CanInteract(GameObject actor)
        {
            return state == PropState.Resting && actor != null;
        }

        public void Interact(GameObject actor)
        {
            PickUp(actor);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            propCollider = GetComponent<Collider>();

            if (propCollider == null)
            {
                propCollider = GetComponentInChildren<Collider>();
            }

            // Calculate approximate bounding sphere radius for anti-clipping spherecasts
            if (propCollider != null)
            {
                propBoundingRadius = Mathf.Max(propCollider.bounds.extents.x, propCollider.bounds.extents.y, propCollider.bounds.extents.z);
            }

            // Ensure physics settings are configured for smooth interpolation
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            originalGravity = rb.useGravity;
            originalKinematic = rb.isKinematic;
        }

        void Update()
        {
            // Skip the frame it was picked up, so the same F press doesn't drop it again
            if (state != PropState.Carried || Time.frameCount == pickUpFrame) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            // F key drops quietly, G key or Left Click throws forward
            if (keyboard.fKey.wasPressedThisFrame)
            {
                Drop();
            }
            else if (keyboard.gKey.wasPressedThisFrame || (mouse != null && mouse.leftButton.wasPressedThisFrame))
            {
                Throw();
            }
        }

        void FixedUpdate()
        {
            if (state != PropState.Carried || viewCamera == null) return;

            Transform camTrans = viewCamera.transform;
            Vector3 camPos = camTrans.position;
            Vector3 camForward = camTrans.forward;

            // Target position ahead of camera, with slight downward chest offset
            float targetDistance = holdDistance;
            Vector3 rayOrigin = camPos;

            // Anti-clipping spherecast: prevent pushing the carried prop through solid environment walls
            if (Physics.SphereCast(rayOrigin, propBoundingRadius * 0.85f, camForward, out RaycastHit hit, holdDistance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                // Pull the object back safely in front of the obstruction surface
                targetDistance = Mathf.Clamp(hit.distance - 0.08f, minHoldDistance, holdDistance);
            }

            Vector3 finalTarget = camPos + (camForward * targetDistance) + (Vector3.up * verticalHoldOffset);

            // Move kinematic Rigidbody cleanly during physics timestep
            rb.MovePosition(finalTarget);
            rb.MoveRotation(Quaternion.Euler(0f, camTrans.eulerAngles.y, 0f));
        }

        public void PickUp(GameObject actor)
        {
            if (state == PropState.Carried || actor == null) return;
            pickUpFrame = Time.frameCount;

            // Retrieve camera from actor or main camera
            viewCamera = actor.GetComponentInChildren<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            state = PropState.Carried;

            // Cache actor collider (e.g. CharacterController) and temporarily ignore collision
            playerCollider = actor.GetComponent<Collider>();
            if (playerCollider != null && propCollider != null)
            {
                Physics.IgnoreCollision(propCollider, playerCollider, true);
            }

            // Save physics settings and switch to kinematic while carried
            originalGravity = rb.useGravity;
            originalKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            onPickedUp?.Invoke();
        }

        public void Drop()
        {
            if (state != PropState.Carried) return;
            Release(false, Vector3.zero);
            onDropped?.Invoke();
        }

        public void Throw()
        {
            if (state != PropState.Carried || viewCamera == null) return;

            Vector3 throwDirection = viewCamera.transform.forward;
            Vector3 impulse = throwDirection * throwImpulse;

            Release(true, impulse);
            onThrown?.Invoke();
        }

        void Release(bool isThrow, Vector3 impulseForce)
        {
            state = isThrow ? PropState.Thrown : PropState.Resting;

            // Restore dynamic physics simulation
            rb.isKinematic = originalKinematic;
            rb.useGravity = originalGravity;

            if (isThrow)
            {
                // Continuous collision detection prevents high-speed tunneling through thin walls
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.AddForce(impulseForce, ForceMode.Impulse);
            }
            else
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            // Restore player collision
            if (playerCollider != null && propCollider != null)
            {
                Physics.IgnoreCollision(propCollider, playerCollider, false);
                playerCollider = null;
            }

            viewCamera = null;
        }

        void OnCollisionEnter(Collision collision)
        {
            // Evaluate impact speed
            float relativeSpeed = collision.relativeVelocity.magnitude;

            if (state == PropState.Thrown)
            {
                // Once an impact occurs, settle back to resting and discrete collision to save CPU cycles
                state = PropState.Resting;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                if (relativeSpeed >= minImpactSpeed)
                {
                    float damage = relativeSpeed * rb.mass * impactDamageFactor;
                    onImpact?.Invoke(damage);

                    // Deliver kinetic damage to enemies or destructibles if present
                    collision.collider.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, propBoundingRadius);
        }
    }
}

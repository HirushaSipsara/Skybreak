using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Skybreak;

namespace Skybreak.PhysicsInteraction
{
    public enum PropState { Resting, Carried, Thrown }
    /// <summary>
    /// Interactive physical prop that can be picked up, carried with spring-damped physics,
    /// dropped, or launched with impulse force to deal kinetic impact damage to targets.
    /// Implements <see cref="IInteractable"/> for seamless integration with <see cref="Skybreak.Player.PlayerInteractor"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class PhysicsProp : MonoBehaviour, IInteractable
    {
        [Header("Prop Identity")]
        [Tooltip("Display name used in the HUD interaction prompt.")]
        [SerializeField] string propName = "Supply Crate";

        [Header("Carry Physics Settings")]
        [Tooltip("Distance in meters in front of camera to hold the object.")]
        [SerializeField, Min(1f)] float holdDistance = 2.2f;

        [Tooltip("Spring damping multiplier driving the Rigidbody toward the hold point.")]
        [SerializeField, Min(5f)] float followSpeed = 16f;

        [Tooltip("Maximum velocity allowed while being carried to prevent physics explosion against walls.")]
        [SerializeField, Min(1f)] float maxCarryVelocity = 10f;

        [Tooltip("Break distance: if prop gets pinned by geometry further than this from target, drop automatically.")]
        [SerializeField, Min(2f)] float breakDistance = 4f;

        [Header("Throw Physics Settings")]
        [Tooltip("Impulse force applied when throwing the object forward.")]
        [SerializeField, Min(1f)] float throwImpulse = 18f;

        [Tooltip("Upward trajectory bias to give thrown props an authentic parabolic arc.")]
        [SerializeField, Range(0f, 0.5f)] float upwardArcBias = 0.12f;

        [Tooltip("Angular impulse added on launch to create natural tumble rotation.")]
        [SerializeField] float throwSpinImpulse = 4f;

        [Header("Kinetic Impact Damage")]
        [Tooltip("Minimum impact collision speed required to inflict kinetic damage.")]
        [FormerlySerializedAs("minImpactSpeed")]
        [SerializeField, Min(1f)] float minDamageSpeed = 3.5f;

        [Tooltip("Base damage multiplier: Damage = 0.5 * Mass * Speed^2 * Multiplier.")]
        [SerializeField, Min(0.01f)] float kineticDamageMultiplier = 0.15f;

        [Tooltip("Maximum damage this prop can deal in a single impact.")]
        [SerializeField, Min(5f)] float maxDamage = 80f;

        [Header("Audio Feedback")]
        [SerializeField] AudioClip pickupSound;
        [SerializeField] AudioClip throwSound;
        [SerializeField] AudioClip impactSound;

        [Header("Events")]
        [SerializeField] UnityEvent onPickedUp = new UnityEvent();
        [SerializeField] UnityEvent onDropped = new UnityEvent();
        [SerializeField] UnityEvent onThrown = new UnityEvent();
        [SerializeField] UnityEvent<float> onImpact = new UnityEvent<float>();

        // Internal State
        Rigidbody rb;
        Collider col;
        AudioSource audioSource;

        Transform holdAnchor;
        GameObject currentCarrier;
        Collider carrierCollider;

        bool isCarried;
        bool isThrown;
        float throwTimestamp;
        int pickUpFrame = -1;
        bool originalGravity, originalKinematic;
        float originalDamping;
        readonly System.Collections.Generic.List<Collider> ignoredCarriers = new System.Collections.Generic.List<Collider>();

        public bool IsCarried => isCarried;
        public Rigidbody AttachedRigidbody => rb;
        public Rigidbody Rigidbody => rb;
        public PropState State => isCarried ? PropState.Carried : isThrown ? PropState.Thrown : PropState.Resting;

        // IInteractable implementation
        public string Prompt => isCarried ? "F  /  Drop object" : $"F  /  Pick up {propName}";

        public bool CanInteract(GameObject actor) => actor != null && (!isCarried || currentCarrier == actor);

        public void Interact(GameObject actor)
        {
            if (!isCarried)
            {
                Pickup(actor);
            }
            else
            {
                Drop();
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            audioSource = GetComponent<AudioSource>();

            // Ensure continuous dynamic detection to eliminate fast-motion wall tunneling
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void Update()
        {
            if (!isCarried || Time.frameCount == pickUpFrame) return;

            // Handle throw and drop inputs while held
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // Left click or primary attack: Throw prop forward
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || (keyboard != null && keyboard.gKey.wasPressedThisFrame))
            {
                Throw();
                return;
            }

            // Right click: Gentle drop
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                Drop();
                return;
            }
        }

        void FixedUpdate()
        {
            if (!isCarried || holdAnchor == null) return;

            Vector3 targetPosition = holdAnchor.position + holdAnchor.forward * holdDistance;
            Vector3 delta = targetPosition - rb.position;

            // Auto-break carry if forced too far behind geometry
            if (delta.magnitude > breakDistance)
            {
                Drop();
                return;
            }

            // Apply spring velocity damping toward anchor
            Vector3 desiredVelocity = delta * followSpeed;
            if (desiredVelocity.magnitude > maxCarryVelocity)
            {
                desiredVelocity = desiredVelocity.normalized * maxCarryVelocity;
            }

            rb.linearVelocity = desiredVelocity;
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
        }

        public void Pickup(GameObject carrier)
        {
            if (isCarried || carrier == null) return;
            pickUpFrame = Time.frameCount;
            originalGravity = rb.useGravity; originalKinematic = rb.isKinematic; originalDamping = rb.linearDamping;
            rb.isKinematic = false;

            currentCarrier = carrier;
            carrierCollider = carrier.GetComponent<Collider>() ?? carrier.GetComponentInChildren<Collider>();
            if (carrierCollider != null && col != null)
            {
                Physics.IgnoreCollision(col, carrierCollider, true);
                if (!ignoredCarriers.Contains(carrierCollider)) ignoredCarriers.Add(carrierCollider);
            }

            // Find look camera on carrier
            var cam = carrier.GetComponentInChildren<Camera>();
            holdAnchor = cam != null ? cam.transform : carrier.transform;

            isCarried = true;
            isThrown = false;
            rb.useGravity = false;
            rb.linearDamping = 1f;

            PlaySound(pickupSound);
            onPickedUp.Invoke();
        }

        public void PickUp(GameObject actor) => Pickup(actor);

        public void Drop()
        {
            if (!isCarried) return;

            RestorePhysicsState();
            PlaySound(pickupSound);
            onDropped.Invoke();
        }

        public void Throw()
        {
            if (!isCarried) return;

            Vector3 launchDirection = holdAnchor != null
                ? (holdAnchor.forward + Vector3.up * upwardArcBias).normalized
                : transform.forward;

            RestorePhysicsState();

            // Apply impulse force
            if (!rb.isKinematic)
            {
                rb.linearVelocity = launchDirection * (throwImpulse / Mathf.Max(0.5f, rb.mass * 0.1f));
                rb.AddTorque(Random.insideUnitSphere * throwSpinImpulse, ForceMode.Impulse);
            }

            isThrown = true;
            throwTimestamp = Time.time;

            PlaySound(throwSound);
            onThrown.Invoke();
        }

        void RestorePhysicsState()
        {
            isCarried = false;
            rb.useGravity = originalGravity;
            rb.isKinematic = originalKinematic;
            rb.linearDamping = originalDamping;

            // Restore collision with carrier after a brief clearing delay
            if (carrierCollider != null && col != null && isActiveAndEnabled)
            {
                StartCoroutine(RestoreCollisionWithCarrierRoutine(carrierCollider));
            }

            currentCarrier = null;
            carrierCollider = null;
            holdAnchor = null;
        }

        System.Collections.IEnumerator RestoreCollisionWithCarrierRoutine(Collider other)
        {
            yield return new WaitForSeconds(0.25f);
            if (col != null && other != null)
            {
                Physics.IgnoreCollision(col, other, false);
                ignoredCarriers.Remove(other);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;

            // Play impact sound if hitting surfaces with noticeable energy
            if (impactSpeed > 1.2f)
            {
                PlaySound(impactSound, Mathf.Clamp01(impactSpeed / 8f));
            }

            // Check kinetic damage on impact if recently thrown
            if (isThrown && Time.time - throwTimestamp < 3.5f && impactSpeed >= minDamageSpeed)
            {
                // Ek = 0.5 * m * v^2
                float kineticEnergy = 0.5f * rb.mass * (impactSpeed * impactSpeed);
                float damage = Mathf.Clamp(kineticEnergy * kineticDamageMultiplier, 5f, maxDamage);

                onImpact.Invoke(damage);
                var sink = collision.collider.GetComponentInParent<Skybreak.Combat.DamageSink>();
                if (sink != null && sink.Alive)
                {
                    sink.ReceiveDamage(damage, transform);
                }
                else
                {
                    var target = collision.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive) target.ApplyDamage(damage, gameObject);
                }

                isThrown = false;
            }
        }

        void OnDisable()
        {
            if (isCarried) RestorePhysicsState();
            StopAllCoroutines();
            foreach (var other in ignoredCarriers) if (col != null && other != null) Physics.IgnoreCollision(col, other, false);
            ignoredCarriers.Clear();
        }

        void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(clip, volume);
        }

        void OnDrawGizmosSelected()
        {
            if (isCarried && holdAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 holdPos = holdAnchor.position + holdAnchor.forward * holdDistance;
                Gizmos.DrawWireSphere(holdPos, 0.25f);
                Gizmos.DrawLine(holdAnchor.position, holdPos);
            }
        }
    }
}

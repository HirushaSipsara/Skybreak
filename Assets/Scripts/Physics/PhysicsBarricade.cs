using UnityEngine;
using Skybreak;

namespace Skybreak.PhysicsInteraction
{
    /// <summary>
    /// Heavy physical barricade that can be pushed by player contact or an interaction shove.
    /// Simulates floor friction and angular resistance.
    /// Automatically notifies the VaultHoundGrid when displaced so AI pathfinders dynamically re-plan.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class PhysicsBarricade : MonoBehaviour, IInteractable
    {
        [Header("Barricade Properties")]
        [Tooltip("Display name used in the HUD interaction prompt.")]
        [SerializeField] string barricadeName = "Heavy Barricade";

        [Tooltip("Mass in kg (heavy, requiring deliberate force to push).")]
        [SerializeField, Min(20f)] float barricadeMass = 120f;

        [Tooltip("Impulse applied when player deliberately shoves the barricade via [F].")]
        [SerializeField, Min(10f)] float shoveImpulse = 150f;

        [Header("AI Grid Integration (IS Module)")]
        [Tooltip("Notify VaultHoundGrid when barricade moves so hounds re-plan around the new obstruction.")]
        [SerializeField] bool notifyHoundGrid = true;

        [Tooltip("Displacement distance required before triggering a grid re-plan.")]
        [SerializeField, Min(0.2f)] float replanDisplacementThreshold = 0.8f;

        [Header("Audio Feedback")]
        [SerializeField] AudioClip shoveSound;
        [SerializeField] AudioClip scrapingSound;

        Rigidbody rb;
        Collider col;
        AudioSource audioSource;
        Vector3 lastGridSyncPosition;

        // IInteractable implementation
        public string Prompt => $"F  /  Shove {barricadeName}";

        public bool CanInteract(GameObject actor) => true;

        public void Interact(GameObject actor)
        {
            // Apply directed shove force away from actor
            Vector3 pushDir = (transform.position - actor.transform.position).normalized;
            pushDir.y = 0.05f; // Slight upward component to overcome static friction
            pushDir.Normalize();

            rb.AddForce(pushDir * shoveImpulse, ForceMode.Impulse);

            if (shoveSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(shoveSound);
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            audioSource = GetComponent<AudioSource>();

            rb.mass = barricadeMass;
            rb.linearDamping = 3.2f;   // Realistic floor friction
            rb.angularDamping = 4.5f;  // Resists spinning wildly
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            lastGridSyncPosition = transform.position;
        }

        void FixedUpdate()
        {
            // Scrape sound when moving across floor
            if (audioSource != null && scrapingSound != null)
            {
                float speed = rb.linearVelocity.magnitude;
                if (speed > 0.15f && !audioSource.isPlaying)
                {
                    audioSource.clip = scrapingSound;
                    audioSource.loop = true;
                    audioSource.volume = Mathf.Clamp01(speed / 3f);
                    audioSource.Play();
                }
                else if (speed <= 0.15f && audioSource.isPlaying && audioSource.clip == scrapingSound)
                {
                    audioSource.Stop();
                }
            }

            // Sync with AI grid if pushed significantly
            if (notifyHoundGrid)
            {
                float distMoved = Vector3.Distance(transform.position, lastGridSyncPosition);
                if (distMoved >= replanDisplacementThreshold)
                {
                    lastGridSyncPosition = transform.position;
                    var gridType = System.Type.GetType("Skybreak.AI.SawanIS.VaultHound.VaultHoundGrid, Assembly-CSharp");
                    if (gridType != null)
                    {
                        var instanceProp = gridType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var instance = instanceProp?.GetValue(null);
                        var method = gridType.GetMethod("NotifyWorldChanged");
                        method?.Invoke(instance, null);
                    }
                }
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            // If struck by an ArcExplorer or CharacterController, encourage dynamic push
            var cc = collision.collider.GetComponent<CharacterController>();
            if (cc != null)
            {
                Vector3 pushDir = collision.relativeVelocity;
                pushDir.y = 0f;
                rb.AddForce(pushDir * 2f, ForceMode.Impulse);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(lastGridSyncPosition, replanDisplacementThreshold);
        }
    }
}

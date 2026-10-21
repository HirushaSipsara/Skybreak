using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.Player
{
    /// <summary>
    /// Detects interactive objects aimed at by the player and triggers interaction on 'F' keypress.
    /// Uses the existing player and camera without adding redundant controllers or listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Aim & Detection")]
        [Tooltip("Camera used for aiming. If unassigned, automatically retrieves Camera.main or camera on player.")]
        [SerializeField] Camera viewCamera;

        [Tooltip("Maximum player interaction range in meters.")]
        [SerializeField, Min(0.5f)] float maxInteractionDistance = 3.5f;

        [Tooltip("Collision layers considered during raycast. Must include environment geometry to prevent interacting through walls. Excludes player (layer 8).")]
        [SerializeField] LayerMask interactionMask = ~(1 << 8);

        [Header("Physics Push")]
        [Tooltip("Enables horizontal physics pushing when walking into dynamic Rigidbodies.")]
        [SerializeField] bool enablePhysicsPush = true;

        [Tooltip("Horizontal push force applied to dynamic objects.")]
        [SerializeField, Min(1f)] float pushForce = 60f;

        [Tooltip("Maximum mass in kg that Arc can push without an exoskeleton.")]
        [SerializeField, Min(10f)] float maxPushMass = 250f;

        [Header("Prompt Display")]
        [SerializeField] bool showPrompt = true;

        IInteractable currentInteractable;
        string currentPrompt;
        GUIStyle promptStyle;

        public IInteractable CurrentInteractable => currentInteractable;
        public string CurrentPrompt => currentPrompt;
        public float MaxInteractionDistance => maxInteractionDistance;

        void Awake()
        {
            EnsureCamera();
        }

        void EnsureCamera()
        {
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>();
                if (viewCamera == null)
                {
                    viewCamera = Camera.main;
                }
            }
        }

        void Update()
        {
            if(!MissionFlowController.InputAllowed){currentInteractable=null;currentPrompt=null;return;}
            EnsureCamera();
            if (viewCamera == null) return;

            UpdateDetection();
            HandleInteractionInput();
        }

        void UpdateDetection()
        {
            currentInteractable = null;
            currentPrompt = null;

            // Cast from screen centre
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float castDistance = maxInteractionDistance + 6f;

            if (Physics.Raycast(ray, out RaycastHit hit, castDistance, interactionMask, QueryTriggerInteraction.Ignore))
            {
                // Verify distance from the player root, not merely camera pivot
                float distanceToPlayer = Vector3.Distance(transform.position, hit.point);
                if (distanceToPlayer <= maxInteractionDistance)
                {
                    // Find IInteractable on the hit object or its parents
                    var candidate = hit.collider.GetComponentInParent<IInteractable>();
                    if (candidate != null && candidate.CanInteract(gameObject))
                    {
                        currentInteractable = candidate;
                        currentPrompt = candidate.Prompt;
                    }
                }
            }
        }

        void HandleInteractionInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F key triggers exactly one deliberate interaction per key press
            if (keyboard.fKey.wasPressedThisFrame && currentInteractable != null)
            {
                if (currentInteractable.CanInteract(gameObject))
                {
                    currentInteractable.Interact(gameObject);
                }
            }
        }

        void OnGUI()
        {
            if (!showPrompt || string.IsNullOrEmpty(currentPrompt)) return;

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = new Color(0.95f, 0.92f, 0.82f, 1f);
            }

            float boxWidth = 260f;
            float boxHeight = 34f;
            float posX = (Screen.width - boxWidth) * 0.5f;
            float posY = Screen.height * 0.5f + 42f;

            // Subtle dark background panel matching project aesthetic
            GUI.color = new Color(0.035f, 0.09f, 0.12f, 0.88f);
            GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), GUIContent.none);
            GUI.color = Color.white;

            GUI.Label(new Rect(posX, posY, boxWidth, boxHeight), currentPrompt, promptStyle);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!enablePhysicsPush) return;

            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            // Reject downward contact: standing on an object must not push it through the floor
            if (hit.moveDirection.y < -0.3f) return;

            // Prevent pushing objects that are too massive for the player
            if (body.mass > maxPushMass) return;

            // Compute purely horizontal push direction
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (pushDir.sqrMagnitude < 0.001f) return;

            // Apply continuous physical force during contact
            body.AddForce(pushDir * pushForce, ForceMode.Force);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, maxInteractionDistance);
        }
    }
}

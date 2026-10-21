using UnityEngine;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// Drives the VaultHound Animator Controller from the brain's current state.
    ///
    /// Animator parameter contract (create these in the Animator Controller):
    ///
    ///   int   "State"     — matches HoundState enum int value
    ///   float "Speed"     — locomotion speed (0 = still, 1 = walk, 2 = run)
    ///   bool  "IsAlive"   — false triggers Death clip
    ///   bool  "Searching" — plays Sniff clip during Search state
    ///
    /// Clip mapping:
    ///   Idle          ← Chambered, stopped
    ///   Alert         ← Chambered (first frame) / deploying start
    ///   Walk          ← moving slowly (deploy speed)
    ///   Run           ← moving fast (pursue speed)
    ///   Sniff         ← Search state (BFS sweep)
    ///   Lunge         ← Attack state (lunge phase)
    ///   Hit           ← LungeRecovery (miss reaction)
    ///   Death         ← Dead state
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class VaultHoundAnimator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Brain on the same or parent GameObject.")]
        [SerializeField] VaultHoundBrain brain;

        [Tooltip("NavMeshAgent or CharacterController for speed sampling.")]
        [SerializeField] UnityEngine.AI.NavMeshAgent motor;

        [Tooltip("Locomotion speed that maps to Walk blend (meters per second).")]
        [SerializeField, Min(0.1f)] float walkThreshold = 2.0f;

        // ── Private ───────────────────────────────────────────────────────────

        Animator anim;

        // Cached parameter hashes for zero-string-lookup performance.
        static readonly int ParamState     = Animator.StringToHash("State");
        static readonly int ParamSpeed     = Animator.StringToHash("Speed");
        static readonly int ParamIsAlive   = Animator.StringToHash("IsAlive");
        static readonly int ParamSearching = Animator.StringToHash("Searching");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (brain == null) brain = GetComponentInParent<VaultHoundBrain>();
            if (motor == null) motor = GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
        }

        void Update()
        {
            if (brain == null || anim == null) return;

            HoundState state = brain.CurrentState;

            // IsAlive
            bool alive = state != HoundState.Dead;
            anim.SetBool(ParamIsAlive, alive);
            if (!alive) return;

            // State int
            anim.SetInteger(ParamState, (int)state);

            // Searching bool
            anim.SetBool(ParamSearching, state == HoundState.Search);

            // Speed float: 0 = idle, 1 = walk, 2 = run
            float velocity = motor != null ? motor.velocity.magnitude : 0f;
            float speedParam = 0f;
            if (velocity > 0.1f)
                speedParam = velocity < walkThreshold ? 1f : 2f;
            anim.SetFloat(ParamSpeed, speedParam);
        }
    }
}

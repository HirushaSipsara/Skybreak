using UnityEngine;

namespace Skybreak.Movement
{
    /// <summary>
    /// Shared bridge from movement to the Animator, usable by any agent with an <see cref="IPathFollower"/>.
    ///
    ///   Speed           measured planar speed (after collisions), damped, drives the Idle/Walk/Run blend tree
    ///   LocomotionRate  playback rate so steps match ground speed between blend-tree samples (less foot sliding)
    ///   Charging/Dead   bools; Release/Hurt triggers, fired once per event by the agent's own glue code
    ///
    /// After the Animator has posed the rig (LateUpdate), two bounded procedural layers are added on top:
    /// a head look-at towards <see cref="lookTarget"/>, and a body lean into turns from the measured yaw rate.
    /// Every bone they touch is keyed in the clips, so the Animator rewrites it each frame and the offsets never accumulate.
    /// Root motion is off: the motor alone moves the GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class AgentAnimationPresenter : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Ground speed (m/s) the walk clip was authored for.")]
        public float walkReferenceSpeed = 2.6f;
        [Tooltip("Ground speed (m/s) the run clip was authored for.")]
        public float runReferenceSpeed = 7.5f;
        public float speedDampTime = 0.08f;

        [Header("Look at")]
        public Transform headBone;
        public Vector3? lookTarget;
        public float headYawLimit = 65, headPitchLimit = 30, headTurnSpeed = 220;

        [Header("Lean into turns")]
        public Transform leanBone;
        public float maxLean = 7;

        static readonly int SpeedId = Animator.StringToHash("Speed"), RateId = Animator.StringToHash("LocomotionRate"),
            ChargingId = Animator.StringToHash("Charging"), ReleaseId = Animator.StringToHash("Release"),
            HurtId = Animator.StringToHash("Hurt"), DeadId = Animator.StringToHash("Dead");

        IPathFollower follower;
        SmoothPathFollower smooth;
        Quaternion headOffset = Quaternion.identity;
        float lean;
        bool dead;

        public float AnimatedSpeed => animator != null ? animator.GetFloat(SpeedId) : 0;
        public bool Dead => dead;

        void Awake()
        {
            follower = GetComponent<IPathFollower>();
            smooth = follower as SmoothPathFollower;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        void Update()
        {
            if (animator == null || follower == null) return;
            float speed = dead ? 0 : follower.PlanarVelocity.magnitude;
            animator.SetFloat(SpeedId, speed, speedDampTime, Time.deltaTime);
            animator.SetFloat(RateId, LocomotionRate(speed));
        }

        /// <summary>Playback rate: 1 at each authored speed, interpolated in between, clamped to stay natural.</summary>
        public float LocomotionRate(float speed)
        {
            if (speed < 0.3f) return 1;
            float reference = speed <= walkReferenceSpeed ? walkReferenceSpeed
                            : speed >= runReferenceSpeed ? runReferenceSpeed
                            : Mathf.Lerp(walkReferenceSpeed, runReferenceSpeed, (speed - walkReferenceSpeed) / (runReferenceSpeed - walkReferenceSpeed));
            return Mathf.Clamp(speed / reference, 0.6f, 1.5f);
        }

        // ------------------------------------------------------------------ action requests (from agent glue code)
        public void SetCharging(bool on) { if (animator != null && !dead) animator.SetBool(ChargingId, on); }
        public void PlayRelease() { if (animator != null && !dead) animator.SetTrigger(ReleaseId); }
        public void PlayHurt() { if (animator != null && !dead) animator.SetTrigger(HurtId); }

        public void SetDead(bool value)
        {
            dead = value;
            if (animator == null) return;
            animator.ResetTrigger(ReleaseId); animator.ResetTrigger(HurtId);
            animator.SetBool(ChargingId, false);
            animator.SetBool(DeadId, value);
        }

        // ------------------------------------------------------------------ procedural layers
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (headBone != null)
            {
                Quaternion want = Quaternion.identity;
                if (!dead && lookTarget.HasValue)
                {
                    // Express the target direction in the head's parent space, relative to the body's facing.
                    Transform parent = headBone.parent != null ? headBone.parent : transform;
                    Vector3 local = parent.InverseTransformDirection(lookTarget.Value - headBone.position);
                    Vector3 rest = parent.InverseTransformDirection(transform.forward);
                    float yaw = Mathf.DeltaAngle(Mathf.Atan2(rest.x, rest.z) * Mathf.Rad2Deg, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                    float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                    want = Quaternion.Euler(Mathf.Clamp(pitch, -headPitchLimit, headPitchLimit), Mathf.Clamp(yaw, -headYawLimit, headYawLimit), 0);
                }
                headOffset = Quaternion.RotateTowards(headOffset, want, headTurnSpeed * dt);
                headBone.localRotation = headOffset * headBone.localRotation;
            }
            if (leanBone != null && smooth != null)
            {
                float speed01 = Mathf.Clamp01(follower.PlanarVelocity.magnitude / Mathf.Max(0.1f, runReferenceSpeed));
                float wanted = dead ? 0 : -Mathf.Clamp(smooth.YawRate / Mathf.Max(1, smooth.turnRate), -1, 1) * maxLean * (0.3f + 0.7f * speed01);
                lean = Mathf.MoveTowards(lean, wanted, 40 * dt);
                leanBone.localRotation = Quaternion.Euler(0, 0, lean) * leanBone.localRotation;
            }
        }
    }
}

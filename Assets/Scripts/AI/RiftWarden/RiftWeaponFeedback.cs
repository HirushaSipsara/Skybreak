using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // A bounded rotation at the shoulder joint; never translate the shoulder away from the torso.
    [DefaultExecutionOrder(100)]
    public sealed class RiftWeaponFeedback : MonoBehaviour
    {
        public Transform cannonBone;
        float shotAt = -100;
        Skybreak.Presentation.TimedClipPresenter presenter;
        Transform posedBone;
        Quaternion animatedRotation;
        bool applied;
        public int Recoils { get; private set; }
        public float Strength => Mathf.Clamp01(1 - (Time.time - shotAt) / .22f);
        public void Fire() { shotAt = Time.time; Recoils++; }
        void OnEnable()
        {
            presenter = GetComponent<Skybreak.Presentation.TimedClipPresenter>();
            if (presenter != null) presenter.BeforePoseEvaluation += RestorePose;
        }
        public void RestorePose()
        {
            if (applied && posedBone != null) posedBone.localRotation = animatedRotation;
            applied = false;
        }
        void LateUpdate()
        {
            // Also restore when the graph is paused, the player is dead, or LateUpdate repeats.
            RestorePose();
            float kick = Strength; if (cannonBone == null || kick <= 0) return;
            posedBone = cannonBone; animatedRotation = cannonBone.localRotation;
            cannonBone.localRotation = animatedRotation * Quaternion.Euler(-6 * kick, 0, 2 * kick);
            applied = true;
        }
        void OnDisable()
        {
            RestorePose(); shotAt = -100;
            if (presenter != null) presenter.BeforePoseEvaluation -= RestorePose;
        }
    }
}

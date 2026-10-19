using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    public enum RiftPhase { Measured, Enraged, Desperate }
    public enum RiftMood { Normal, Mercy, Pressure }

    // Layer 5: the boss's temperament (a hierarchical state machine: the phase sets the parameters the attack states
    // run with) and a fairness director (dynamic difficulty adjustment, beyond the lectures).
    //   Phase      Measured (both arms) -> Enraged (one arm lost) -> Desperate (core exposed)
    //   Mercy      player below 25% health: 6 s of weaker aim lead, no combos, slower recovery (then 12 s before it can recur)
    //   Pressure   player above 80% health while the boss lost over 30% of an arm recently: sharper lead, faster cooldowns
    // Hard limits never change: telegraph timings, damage values and the jump rule for low attacks.
    public sealed class RiftDirector
    {
        public const float MercyBelow = .25f, MercySeconds = 6, MercyRearm = 12, PressureAbove = .8f, PressureLoss = .3f;
        public const float LeadNormal = .65f, LeadMercy = .4f, LeadPressure = .85f;

        public RiftPhase Phase { get; private set; }
        public RiftMood Mood { get; private set; }
        public int MercyWindows { get; private set; }
        public float PressureSeconds { get; private set; }
        float mercyUntil = -100, mercyReadyAt;

        // Phase parameters
        public float CooldownScale => (Phase == RiftPhase.Enraged ? .8f : 1f) * (Mood == RiftMood.Pressure ? .9f : Mood == RiftMood.Mercy ? 1.2f : 1f);
        public float RecoverSeconds(float baseSeconds) => (Phase == RiftPhase.Measured ? baseSeconds : .85f) + (Mood == RiftMood.Mercy ? .5f : 0);
        public float RiskWeight => Phase == RiftPhase.Measured ? 1f : Phase == RiftPhase.Enraged ? .6f : .3f;
        public float SlamCooldown(float baseSeconds) => Phase == RiftPhase.Desperate ? baseSeconds * (5.5f / 7f) : baseSeconds;
        public float LeadFactor => Mood == RiftMood.Mercy ? LeadMercy : Mood == RiftMood.Pressure ? LeadPressure : LeadNormal;
        public int PlanDepth => Mood == RiftMood.Mercy ? 1 : 2;   // mercy: no planned combos

        public void Reset() { Phase = RiftPhase.Measured; Mood = RiftMood.Normal; MercyWindows = 0; PressureSeconds = 0; mercyUntil = -100; mercyReadyAt = 0; }

        public void Update(bool leftArm, bool rightArm, bool coreExposed, float playerHealth01, float worstRecentArmLoss01, float now, float dt)
        {
            Phase = coreExposed ? RiftPhase.Desperate : (!leftArm || !rightArm) ? RiftPhase.Enraged : RiftPhase.Measured;
            if (playerHealth01 < MercyBelow && now >= mercyReadyAt && now >= mercyUntil)
            {
                mercyUntil = now + MercySeconds; mercyReadyAt = now + MercySeconds + MercyRearm; MercyWindows++;
            }
            if (now < mercyUntil) Mood = RiftMood.Mercy;
            else if (playerHealth01 > PressureAbove && worstRecentArmLoss01 > PressureLoss) { Mood = RiftMood.Pressure; PressureSeconds += dt; }
            else Mood = RiftMood.Normal;
        }
    }
}

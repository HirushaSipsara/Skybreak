using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    public struct RiftScores { public float cannon, blade, teleport; }
    public enum RiftChoice { CoreTeleport, Slash, DoubleTeleport, Cannon, Approach }

    // Decision layer (utility-based, Lecture 03): a score per attack, -1 when it is unavailable (arm destroyed,
    // cooling down or out of range). This is the boss's original rule set, kept as the baseline that the adaptive
    // layers (learner, expectimax planner, director) build on.
    public static class RiftUtility
    {
        public static RiftScores Score(float range, float leftHealth, float rightHealth, bool cannonReady, bool bladeReady, bool teleportReady, WardenTuning t)
        {
            RiftScores s;
            s.cannon = leftHealth > 0 && cannonReady && range < t.cannonRange ? t.cannonBase + Mathf.Clamp01(range / 12) * t.cannonWeight : -1;
            s.blade = rightHealth > 0 && bladeReady && range < t.bladeRange ? t.bladeUtility : -1;
            s.teleport = rightHealth > 0 && teleportReady && range > t.teleportMinRange && range < t.teleportMaxRange ? t.teleportUtility : -1;
            return s;
        }

        // Exposed core: teleport and slam whenever it is ready. Otherwise the highest score wins: blade, then teleport,
        // then cannon; with nothing available, approach.
        public static RiftChoice Select(in RiftScores s, bool coreExposed, bool slamReady)
        {
            if (coreExposed && slamReady) return RiftChoice.CoreTeleport;
            if (s.blade > s.cannon && s.blade > s.teleport) return RiftChoice.Slash;
            if (s.teleport > s.cannon) return RiftChoice.DoubleTeleport;
            if (s.cannon >= 0) return RiftChoice.Cannon;
            return RiftChoice.Approach;
        }
    }
}

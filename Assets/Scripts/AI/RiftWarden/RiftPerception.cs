using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // Layer 1 (model-based, Lecture 03): what the boss knows about the player. The arena is mostly open but the
    // player can still break line of sight, so the last sighting is remembered for a few seconds.
    public sealed class RiftPerception
    {
        float nextSight, seenAt = -100;
        bool cachedVisible;
        Vector3 lastSeen;

        public bool Visible => cachedVisible;
        public Vector3 LastSeen => lastSeen;
        public float SeenAt => seenAt;
        public bool Remembers(float now, float memorySeconds) => now - seenAt < memorySeconds;

        public void Reset() { nextSight = 0; seenAt = -100; }

        // Sight is re-checked every 'interval' seconds (a raycast from the boss's chest to the player's chest that
        // ignores actors on layer 8); every frame the player is visible, the sighting is refreshed.
        public bool Sense(Transform self, Transform player, float now, float interval)
        {
            if (now >= nextSight)
            {
                nextSight = now + interval;
                cachedVisible = !Physics.Linecast(self.position + Vector3.up * 2.6f, player.position + Vector3.up * 1.2f, ~(1 << 8), QueryTriggerInteraction.Ignore);
            }
            if (cachedVisible) { lastSeen = player.position; seenAt = now; }
            return cachedVisible;
        }
    }
}

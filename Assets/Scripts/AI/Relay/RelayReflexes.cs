using Skybreak.Combat;

namespace Skybreak.AI.Relay
{
    public enum RelayReflex { None, Disabled, PlayerDown }

    // Layer 0 (simple reflex, Lecture 03): condition-action rules checked every frame before any deliberation.
    // Rules are in priority order; the first one that matches wins and the deliberative layers are skipped
    // (subsumption). Rules: 1) Relay has no health -> stay disabled; 2) the player is down -> stop and wait.
    public static class RelayReflexes
    {
        public static RelayReflex Check(bool relayAlive, DamageSink playerVitals)
        {
            if (!relayAlive) return RelayReflex.Disabled;
            if (playerVitals != null && !playerVitals.Alive) return RelayReflex.PlayerDown;
            return RelayReflex.None;
        }
    }
}

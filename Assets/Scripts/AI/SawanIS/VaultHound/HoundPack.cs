using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// Shared memory and coordination state for an instance of two Vault Hounds
    /// that hunt together after their chamber doors open.
    ///
    /// IS justification — partner coordination:
    ///   Both Hound brains read from and write to this shared object so that
    ///   the pair behaves as a coordinated unit rather than two independent pursuers:
    ///   - One Hound is assigned the PURSUER role (direct A* route to last sighting).
    ///   - The other becomes the FLANKER (BFS-discovered alternate route).
    ///   - When the pursuer sees the player it shares the sighting immediately so the
    ///     flanker can converge on the confirmed position rather than searching blindly.
    ///   - If one Hound is destroyed the survivor switches autonomously to solo pursuit.
    ///
    /// Usage:
    ///   Attach this component to a parent or manager object, then serialize a reference
    ///   on each <see cref="VaultHoundBrain"/>. Both brains call <see cref="RegisterHound"/>
    ///   in Awake and use the shared API to coordinate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoundPack : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>Duration for which a shared sighting stays valid after being reported.</summary>
        [SerializeField, Min(0.5f)] public float sharedMemorySeconds = 3.5f;

        // ── Pack state ────────────────────────────────────────────────────────

        VaultHoundBrain houndA;
        VaultHoundBrain houndB;

        Vector3 sharedLastSighting;
        float   sharedSightingTime = float.NegativeInfinity;
        bool    hasSharedSighting;

        VaultHoundBrain pursuer;  // the hound on the direct route
        VaultHoundBrain flanker;  // the hound on the alternate BFS route

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Call from each Hound's Awake. Assigns A/B slots and initial roles.</summary>
        public void RegisterHound(VaultHoundBrain hound)
        {
            if (hound == houndA || hound == houndB) return;
            if (houndA == null)       { houndA = hound; }
            else if (houndB == null)  { houndB = hound; AssignInitialRoles(); }
            else Debug.LogWarning("[HoundPack] More than two hounds registered.", this);
        }

        public void ResetForEncounter()
        {
            houndA = houndB = pursuer = flanker = null;
            hasSharedSighting = false; sharedSightingTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Called when a Hound spots the player. Broadcasts the position to both
        /// members so the flanker can converge.
        /// </summary>
        public void ReportSighting(Vector3 playerPosition)
        {
            sharedLastSighting  = playerPosition;
            sharedSightingTime  = Time.time;
            hasSharedSighting   = true;
        }

        /// <summary>
        /// Returns true and sets <paramref name="position"/> when there is a valid
        /// shared sighting within <see cref="sharedMemorySeconds"/>.
        /// </summary>
        public bool TryGetSharedSighting(out Vector3 position)
        {
            position = sharedLastSighting;
            if (!hasSharedSighting) return false;
            if (Time.time - sharedSightingTime > sharedMemorySeconds)
            {
                hasSharedSighting = false;
                return false;
            }
            return true;
        }

        /// <summary>True if this pack has a currently valid shared sighting.</summary>
        public bool HasActiveSighting => hasSharedSighting &&
            (Time.time - sharedSightingTime <= sharedMemorySeconds);

        /// <returns>True when <paramref name="hound"/> is the current pursuer.</returns>
        public bool IsPursuer(VaultHoundBrain hound) => hound == pursuer;

        /// <returns>True when <paramref name="hound"/> is the current flanker.</returns>
        public bool IsFlanker(VaultHoundBrain hound) => hound == flanker;

        /// <summary>
        /// Called when one hound is destroyed. The survivor automatically
        /// switches to SOLO_PURSUE regardless of its previous role.
        /// </summary>
        public void NotifyHoundDestroyed(VaultHoundBrain destroyed)
        {
            if (destroyed == houndA) houndA = null;
            else if (destroyed == houndB) houndB = null;

            var survivor = houndA ?? houndB;
            if (survivor != null)
            {
                pursuer = survivor;
                flanker = null;
            }
        }

        /// <summary>
        /// Returns the partner's current A* path cells (encoded integers) so the
        /// flanker can avoid them when running its BFS search.
        /// Returns null when only one hound remains.
        /// </summary>
        public HashSet<int> GetPursuerPathCells(VaultHoundBrain requester)
        {
            var partner = (requester == houndA) ? houndB : houndA;
            return partner?.CurrentPathCells;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void AssignInitialRoles()
        {
            // houndA always starts as pursuer (serialisation order in the scene).
            pursuer = houndA;
            flanker = houndB;
        }
    }
}

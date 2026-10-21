using UnityEngine;
using Skybreak.Combat;
using Skybreak.PhysicsInteraction;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// Monitors Area 1 enemies tagged "Area1Enemy" and fires the chamber door
    /// release event exactly once when every such enemy is defeated.
    ///
    /// IS justification — triggering condition:
    ///   This is a simple world-state monitor. It counts only entities carrying
    ///   the "Area1Enemy" tag, which explicitly excludes Relay, chamber-resident
    ///   Hounds, and enemies belonging to later areas. The release fires exactly
    ///   once, after which the counter becomes inert.
    ///
    /// Integration:
    ///   1. Tag every Area 1 enemy GameObject as "Area1Enemy" in the Inspector.
    ///   2. Place this component on a persistent manager object in the scene.
    ///   3. Subscribe to <see cref="OnChambersOpen"/> or wire the UnityEvent in
    ///      the Inspector to the chamber door activators.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChamberReleaseController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Tag applied to every Area 1 enemy that counts toward the clear condition.\n" +
                 "Must NOT include: Relay, Vault Hounds inside chambers, Area 2+ enemies.")]
        [SerializeField] string area1Tag = "Area1Enemy";

        [Tooltip("Doors / triggers to activate when all Area 1 enemies are cleared.")]
        [SerializeField] GameObject[] chamberDoors;

        [Tooltip("Optional animator(s) on the doors. Will trigger the 'Open' bool parameter.")]
        [SerializeField] Animator[] doorAnimators;

        [Header("Debug")]
        [SerializeField] bool logEvents = true;
        [SerializeField] DamageSink[] area1Agents = new DamageSink[0];
        [SerializeField] DungeonHoldingDoor[] holdingDoors = new DungeonHoldingDoor[0];
        [SerializeField] VaultHoundBrain[] hounds = new VaultHoundBrain[0];
        [SerializeField] VaultHoundGrid grid;
        [SerializeField] HoundPack pack;
        float gatesReadyAt = -1;
        bool deployed;
        public bool Released => released;
        public int TotalEnemies => totalArea1;
        public int DefeatedEnemies => deadArea1;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires once when the chamber doors should open.</summary>
        public System.Action OnChambersOpen;

        // ── State ─────────────────────────────────────────────────────────────

        int totalArea1;
        int deadArea1;
        bool released;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Start()
        {
            if (area1Agents.Length > 0) { totalArea1 = area1Agents.Length; return; }
            // Count all living Area 1 enemies and subscribe to their defeat events.
            var allHealth = FindObjectsByType<CombatHealth>();
            foreach (var h in allHealth)
            {
                if (!h.CompareTag(area1Tag)) continue;
                totalArea1++;
                h.OnDefeated.AddListener(() => OnArea1EnemyDefeated());
            }

            if (logEvents)
                Debug.Log($"[ChamberRelease] Tracking {totalArea1} Area 1 enemies (tag: '{area1Tag}').", this);
        }

        void Update()
        {
            // The shared combat reset restores the guards before this observer runs.
            if (released)
                foreach (var enemy in area1Agents)
                    if (enemy != null && enemy.Alive) { ResetEncounter(); break; }
            if (!released && area1Agents.Length > 0)
            {
                deadArea1 = 0;
                foreach (var enemy in area1Agents) if (enemy == null || !enemy.Alive) deadArea1++;
                if (deadArea1 >= totalArea1) Release();
            }
            if (!released || deployed) return;
            foreach (var door in holdingDoors) if (door != null && !door.IsOpen) { gatesReadyAt = -1; return; }
            if (gatesReadyAt < 0) gatesReadyAt = Time.time;
            // Allow the lifted gates' carving obstacles to leave the navigation mesh before planning.
            if (Time.time - gatesReadyAt < .25f) return;
            grid?.Bake();
            foreach (var hound in hounds) if (hound != null) hound.OnChamberOpen();
            deployed = true;
        }

        public void ResetEncounter()
        {
            released = deployed = false; deadArea1 = 0; gatesReadyAt = -1;
            pack?.ResetForEncounter();
            foreach (var hound in hounds) if (hound != null) hound.ResetForChamber();
            foreach (var door in holdingDoors) if (door != null) door.Close();
            grid?.Bake();
        }

        // ── Callbacks ─────────────────────────────────────────────────────────

        void OnArea1EnemyDefeated()
        {
            if (released) return;
            deadArea1++;
            if (logEvents)
                Debug.Log($"[ChamberRelease] {deadArea1}/{totalArea1} Area 1 enemies defeated.", this);

            if (deadArea1 >= totalArea1) Release();
        }

        void Release()
        {
            if (released) return;
            released = true;
            foreach (var sink in DamageSink.Active)
                if (sink != null && sink.Team == Allegiance.Player && sink.Alive) { pack?.ReportSighting(sink.transform.position); break; }
            foreach (var door in holdingDoors) if (door != null) door.Open();

            if (logEvents) Debug.Log("[ChamberRelease] All Area 1 enemies cleared — opening chambers!", this);

            // Activate door GameObjects (enable collider changes, triggers, etc.)
            foreach (var door in chamberDoors)
                if (door != null) door.SetActive(true);

            // Drive door animation if animators are wired.
            foreach (var anim in doorAnimators)
                if (anim != null) anim.SetBool("Open", true);

            OnChambersOpen?.Invoke();
        }

        // ── Debug ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!Application.isPlaying || released) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                normal    = { textColor = new Color(1f, 0.85f, 0.3f) }
            };
            GUI.Label(new Rect(14f, 14f, 260f, 20f),
                $"CHAMBER LOCK  {deadArea1}/{totalArea1} cleared", style);
        }
    }
}

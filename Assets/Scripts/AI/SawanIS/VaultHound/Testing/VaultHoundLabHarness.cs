using UnityEngine;
using UnityEngine.InputSystem;
using Skybreak.AI.SawanIS.VaultHound;

namespace Skybreak.AI.SawanIS.VaultHound.Testing
{
    /// <summary>
    /// Interactive test harness for Vault Hound viva demonstrations.
    /// Provides on-screen diagnostic telemetry and keyboard triggers
    /// to prove memory decay, BFS area sweep, survivor mode, and re-planning.
    /// </summary>
    public sealed class VaultHoundLabHarness : MonoBehaviour
    {
        [Header("Lab References")]
        [SerializeField] VaultHoundBrain houndA;
        [SerializeField] VaultHoundBrain houndB;
        [SerializeField] HoundPack pack;
        [SerializeField] VaultHoundGrid grid;
        [SerializeField] Transform target;
        [SerializeField] GameObject testObstacle;

        bool showOverlay = true;

        void Start()
        {
            if (houndA != null) houndA.OnChamberOpen();
            if (houndB != null) houndB.OnChamberOpen();
        }

        void Update()
        {
            // Keyboard controls for live viva demo
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) ToggleObstacle();
            if (keyboard.digit2Key.wasPressedThisFrame) EliminateHoundA();
            if (keyboard.tabKey.wasPressedThisFrame) showOverlay = !showOverlay;
        }

        public void ToggleObstacle()
        {
            if (testObstacle != null)
            {
                testObstacle.SetActive(!testObstacle.activeSelf);
                Physics.SyncTransforms();
                if (grid != null) grid.Bake();
            }
        }

        public void EliminateHoundA()
        {
            if (houndA != null)
            {
                var health = houndA.GetComponent<CombatHealth>();
                if (health != null && health.IsAlive)
                {
                    health.ApplyDamage(9999f, gameObject);
                }
            }
        }

        void OnGUI()
        {
            if (!showOverlay) return;

            GUI.Box(new Rect(10, 10, 360, 240), "Vault Hound Viva Demonstration Harness");
            GUILayout.BeginArea(new Rect(20, 35, 340, 210));

            GUILayout.Label("<b>[Controls for Examiner]</b>");
            GUILayout.Label("[1] Toggle Corridor Obstacle (Test Re-plan)");
            GUILayout.Label("[2] Eliminate Hound A (Test Survivor Solo Mode)");
            GUILayout.Label("[Tab] Toggle this HUD");

            GUILayout.Space(8);
            GUILayout.Label("<b>[Live Telemetry]</b>");
            if (houndA != null)
            {
                GUILayout.Label($"Hound A: State = {houndA.CurrentState}");
            }
            if (houndB != null)
            {
                GUILayout.Label($"Hound B: State = {houndB.CurrentState}");
            }
            if (pack != null)
            {
                GUILayout.Label($"Pack Sighting Active: {pack.HasActiveSighting}");
                if (houndA != null) GUILayout.Label($"Hound A Role: {(pack.IsPursuer(houndA) ? "Pursuer" : "Flanker")}");
                if (houndB != null) GUILayout.Label($"Hound B Role: {(pack.IsPursuer(houndB) ? "Pursuer" : "Flanker")}");
            }

            GUILayout.EndArea();
        }
    }
}

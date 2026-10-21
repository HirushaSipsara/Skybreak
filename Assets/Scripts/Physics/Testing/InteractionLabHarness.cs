using UnityEngine;

namespace Skybreak.PhysicsInteraction.Testing
{
    /// <summary>
    /// Interactive demonstration HUD for GV Player Interaction & Physics examination.
    /// Provides live on-screen controls, physics telemetry, and status indicators.
    /// </summary>
    public sealed class InteractionLabHarness : MonoBehaviour
    {
        bool showHUD = true;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                showHUD = !showHUD;
            }
        }

        void OnGUI()
        {
            if (!showHUD) return;

            GUI.Box(new Rect(10, 10, 380, 260), "SE3032 GV: Player Interaction & Physics Lab");
            GUILayout.BeginArea(new Rect(20, 35, 360, 230));

            GUILayout.Label("<b>[Player Controls]</b>");
            GUILayout.Label("• <b>[WASD]</b>: Movement  |  <b>[Mouse]</b>: Aim & Look");
            GUILayout.Label("• <b>[F]</b>: Interact (Open/Close Door, Pick Up Prop, Shove)");
            GUILayout.Label("• <b>[Left Click]</b>: Throw Held Prop (Kinetic Impulse)");
            GUILayout.Label("• <b>[Right Click]</b>: Drop Held Prop gently");
            GUILayout.Label("• <b>[Tab]</b>: Toggle this Telemetry Guide");

            GUILayout.Space(8);
            GUILayout.Label("<b>[Examiner Verification Stations]</b>");
            GUILayout.Label("1. <b>Security Door</b>: Kinematic MovePosition + Anti-Crush");
            GUILayout.Label("2. <b>Heavy Barricade</b>: Pushable with friction + AI grid sync");
            GUILayout.Label("3. <b>Physics Props</b>: Damped spring carry + Impulse throw");
            GUILayout.Label("4. <b>Target Dummy</b>: Receives Ek = 0.5*m*v^2 kinetic damage");

            GUILayout.EndArea();
        }
    }
}

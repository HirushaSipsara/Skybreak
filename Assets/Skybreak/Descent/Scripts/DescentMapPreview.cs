using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.Descent
{
    // Inspection controls for the map pass. Character and combat systems are added separately.
    public sealed class DescentMapPreview : MonoBehaviour
    {
        public Camera view;
        public Transform[] viewpoints;
        public string[] captions;
        int shot = 1;
        float yaw, pitch;
        GUIStyle title, hint;
        TextMesh[] signs;

        public void ShowShot(int index)
        {
            if (viewpoints == null || index < 0 || index >= viewpoints.Length) return;
            shot = index;
            view.fieldOfView = index == 0 ? 32 : 65;
            view.transform.SetPositionAndRotation(viewpoints[index].position, viewpoints[index].rotation);
            Vector3 angles = view.transform.eulerAngles;
            yaw = angles.y; pitch = angles.x > 180 ? angles.x - 360 : angles.x;
        }

        void Start() { signs = FindObjectsByType<TextMesh>(FindObjectsSortMode.None); ShowShot(1); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        void LateUpdate()
        {
            // Dynamic fonts can replace the atlas when the inspection HUD adds characters.
            if (signs == null) return;
            foreach (var sign in signs)
            {
                if (sign == null || sign.font == null) continue;
                var material = sign.GetComponent<MeshRenderer>().sharedMaterial;
                if (material != null && material.mainTexture != sign.font.material.mainTexture)
                    material.mainTexture = sign.font.material.mainTexture;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null || mouse == null || view == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) ShowShot(0);
            if (keyboard.f2Key.wasPressedThisFrame) ShowShot(1);
            if (keyboard.f3Key.wasPressedThisFrame) ShowShot(2);
            if (keyboard.f4Key.wasPressedThisFrame) ShowShot(3);
            if (keyboard.f5Key.wasPressedThisFrame) ShowShot(4);
            if (mouse.rightButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (mouse.rightButton.wasReleasedThisFrame || keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue(); yaw += delta.x * .10f; pitch = Mathf.Clamp(pitch - delta.y * .10f, -85, 85);
                view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction += view.transform.forward;
            if (keyboard.sKey.isPressed) direction -= view.transform.forward;
            if (keyboard.dKey.isPressed) direction += view.transform.right;
            if (keyboard.aKey.isPressed) direction -= view.transform.right;
            if (keyboard.eKey.isPressed) direction += Vector3.up;
            if (keyboard.qKey.isPressed) direction -= Vector3.up;
            float speed = keyboard.leftShiftKey.isPressed ? 18 : 7;
            view.transform.position += Vector3.ClampMagnitude(direction, 1) * speed * Time.deltaTime;
        }

        void OnGUI()
        {
            GUI.contentColor = Color.white;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
                title.normal.textColor = new Color(.94f,.90f,.77f);
                hint = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                hint.normal.textColor = new Color(.64f,.86f,.87f);
            }
            // Reapply after editor skin refreshes to keep inspection labels readable.
            title.normal.textColor = new Color(.94f,.90f,.77f);
            hint.normal.textColor = new Color(.64f,.86f,.87f);
            GUI.color = new Color(.035f,.09f,.12f,.86f); GUI.DrawTexture(new Rect(20,20,450,100), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(35,28,420,35), "SKYBREAK / THE DESCENT", title);
            GUI.Label(new Rect(35,66,420,23), captions != null && shot < captions.Length ? captions[shot] : "MAP PREVIEW", hint);
            GUI.Label(new Rect(35,90,420,23), "F1–F5 views  ·  RMB look  ·  WASD move  ·  Q/E height", hint);
        }
    }

}

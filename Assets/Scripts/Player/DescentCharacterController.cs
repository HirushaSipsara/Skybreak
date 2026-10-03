using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.Descent
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DescentCharacterController : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform visual, leftLeg, rightLeg, leftArm, rightArm;
        public float walkSpeed = 4.7f, sprintSpeed = 7.6f, jumpHeight = 1.15f;
        public float gravity = 22f, sensitivity = .10f, cameraDistance = 5.4f;
        public float cameraPivotHeight = 1.6f, cameraShoulderOffset = .65f, hideBodyDistance = 1.3f;
        public LayerMask environmentMask = ~(1 << 8);
        CharacterController capsule;
        Renderer[] bodyRenderers;
        TextMesh[] signs;
        Vector3 spawn;
        float yaw, pitch = 10, verticalSpeed, groundedGrace, jumpBuffer, gait;
        float actualCameraDistance;
        bool cursorCaptured;
        GUIStyle heading, controls;

        public bool Grounded => capsule != null && capsule.isGrounded;
        public float VerticalSpeed => verticalSpeed;

        void Awake()
        {
            capsule = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = Camera.main;
            spawn = transform.position; yaw = transform.eulerAngles.y;
            bodyRenderers = visual.GetComponentsInChildren<Renderer>();
            signs = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            actualCameraDistance = cameraDistance;
        }

        void Start() { CaptureCursor(true); UpdateCamera(1); }

        void CaptureCursor(bool capture)
        {
            cursorCaptured = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        void OnApplicationFocus(bool focused) { if (!focused) CaptureCursor(false); }
        void OnDisable() { CaptureCursor(false); }

        void Update()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CaptureCursor(false);
            if (!cursorCaptured && mouse != null && mouse.leftButton.wasPressedThisFrame) CaptureCursor(true);
            Vector2 input = Vector2.zero; bool jump = false, sprint = false;
            if (cursorCaptured && Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse != null)
                {
                    Vector2 look = mouse.delta.ReadValue();
                    yaw += look.x * sensitivity; pitch = Mathf.Clamp(pitch - look.y * sensitivity, -80, 78);
                }
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y++;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y--;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x++;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x--;
                    jump = keyboard.spaceKey.wasPressedThisFrame;
                    sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                }
            }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            MoveCharacter(input, jump, sprint, dt);
            if (transform.position.y < -12) ReturnToStart();
        }

        // Movement remains independent of input devices and uses the scene's physical colliders.
        public void MoveCharacter(Vector2 input, bool jump, bool sprint, float dt)
        {
            input = Vector2.ClampMagnitude(input, 1);
            groundedGrace = capsule.isGrounded ? .12f : Mathf.Max(0, groundedGrace - dt);
            jumpBuffer = jump ? .12f : Mathf.Max(0, jumpBuffer - dt);
            if (capsule.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (jumpBuffer > 0 && groundedGrace > 0)
            {
                verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);
                jumpBuffer = groundedGrace = 0;
            }
            verticalSpeed -= gravity * dt;
            Vector3 horizontal = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y) * (sprint ? sprintSpeed : walkSpeed);
            capsule.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            float pace = input.magnitude;
            gait += dt * (sprint ? 13 : 9) * pace;
            float swing = Mathf.Sin(gait) * 32 * pace * (capsule.isGrounded ? 1 : .2f);
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-swing * .7f, 0, -5);
            rightArm.localRotation = Quaternion.Euler(swing * .7f, 0, 5);
            visual.localPosition = new Vector3(0, capsule.isGrounded ? Mathf.Abs(Mathf.Sin(gait)) * .025f * pace : 0, 0);
        }

        void LateUpdate()
        {
            UpdateCamera(Time.deltaTime);
            foreach (var sign in signs)
            {
                if (sign == null || sign.font == null) continue;
                var material = sign.GetComponent<MeshRenderer>().sharedMaterial;
                if (material != null && material.mainTexture != sign.font.material.mainTexture)
                    material.mainTexture = sign.font.material.mainTexture;
            }
        }

        public void UpdateCamera(float dt)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = transform.position + Vector3.up * cameraPivotHeight;
            Vector3 offset = rotation * new Vector3(cameraShoulderOffset, 0, -cameraDistance);
            float desiredDistance = offset.magnitude;
            if (Physics.SphereCast(pivot, .22f, offset.normalized, out var hit, desiredDistance, environmentMask, QueryTriggerInteraction.Ignore))
                desiredDistance = Mathf.Max(.25f, hit.distance - .10f);
            // Pull forward immediately to avoid crossing walls; ease back out into open space.
            actualCameraDistance = desiredDistance < actualCameraDistance ? desiredDistance : Mathf.Lerp(actualCameraDistance, desiredDistance, 1 - Mathf.Exp(-8 * dt));
            viewCamera.transform.SetPositionAndRotation(pivot + offset.normalized * actualCameraDistance, rotation);
            foreach (var renderer in bodyRenderers) renderer.enabled = actualCameraDistance > hideBodyDistance;
        }

        public void ReturnToStart()
        {
            capsule.enabled = false; transform.position = spawn; capsule.enabled = true;
            verticalSpeed = groundedGrace = jumpBuffer = 0; yaw = 0; pitch = 10;
        }

        void OnGUI()
        {
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                controls = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            }
            heading.normal.textColor = new Color(.94f,.90f,.77f);
            controls.normal.textColor = new Color(.64f,.86f,.87f);
            GUI.contentColor = Color.white;
            GUI.color = new Color(.035f,.09f,.12f,.85f);
            GUI.DrawTexture(new Rect(16,16,470,80), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(30,23,435,26), "ARC / EXPLORE THE DESCENT", heading);
            GUI.Label(new Rect(30,51,435,23), "WASD move  ·  Mouse look  ·  Shift sprint  ·  Space jump", controls);
            GUI.Label(new Rect(30,71,435,20), cursorCaptured ? "Esc releases cursor" : "Click the Game view to control Arc", controls);
        }
    }
}

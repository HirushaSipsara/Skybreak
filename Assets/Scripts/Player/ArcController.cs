using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArcController : MonoBehaviour
    {
        public Camera viewCamera;
        public float walkSpeed = 6f;
        public float jumpSpeed = 8f;
        public float lookSensitivity = .11f;
        public float shotDamage = 22f;
        public float chargeDamage = 65f;

        CharacterController controller;
        float yaw = 180f, pitch = -7f, verticalSpeed, fireCooldown, dashCooldown, dashTimer, chargeTime, hitFlash;
        Vector3 dashDirection;
        int jumps;
        public int DisabledTargets { get; private set; }
        public float DashCooldown => dashCooldown;
        public bool Charging => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public float ChargeTime => chargeTime;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || viewCamera == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                bool lockCursor = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !lockCursor;
            }
            float dt = Time.deltaTime;
            fireCooldown = Mathf.Max(0, fireCooldown - dt);
            dashCooldown = Mathf.Max(0, dashCooldown - dt);
            dashTimer = Mathf.Max(0, dashTimer - dt);
            hitFlash = Mathf.Max(0, hitFlash - dt);
            if (Cursor.lockState == CursorLockMode.Locked && mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue();
                yaw += look.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -68f, 68f);
            }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 pivot = transform.position + Vector3.up * 1.45f;
            Quaternion aimRotation = Quaternion.Euler(-pitch, yaw, 0);
            Vector3 desiredCamera = pivot + Quaternion.Euler(0, yaw, 0) * new Vector3(.72f, .75f, -4.6f);
            if (Physics.Linecast(pivot, desiredCamera, out RaycastHit cameraHit))
                desiredCamera = Vector3.Lerp(pivot, cameraHit.point, .86f);
            viewCamera.transform.SetPositionAndRotation(desiredCamera, Quaternion.LookRotation(pivot + aimRotation * Vector3.forward * 25f - desiredCamera));

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y++;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x++;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x--;
            move = Vector2.ClampMagnitude(move, 1);
            Vector3 wish = Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y);
            if (controller.isGrounded && verticalSpeed < 0)
            {
                verticalSpeed = -1.5f;
                jumps = 0;
            }
            if (keyboard.spaceKey.wasPressedThisFrame && jumps < 2)
            {
                verticalSpeed = jumps == 0 ? jumpSpeed : jumpSpeed * .94f;
                jumps++;
            }
            if ((keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) && dashCooldown <= 0)
            {
                dashDirection = wish.sqrMagnitude > .01f ? wish.normalized : transform.forward;
                dashTimer = .20f;
                dashCooldown = 2f;
                verticalSpeed = Mathf.Max(0, verticalSpeed);
            }
            if (dashTimer <= 0) verticalSpeed -= 20f * dt;
            Vector3 horizontal = dashTimer > 0 ? dashDirection * 20f : wish * walkSpeed;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);

            if (Cursor.lockState != CursorLockMode.Locked || mouse == null) return;
            if (mouse.leftButton.isPressed && fireCooldown <= 0) Fire(false);
            if (mouse.rightButton.isPressed) chargeTime += dt;
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                if (chargeTime >= .7f && fireCooldown <= 0) Fire(true);
                chargeTime = 0;
            }
        }

        void Fire(bool charged)
        {
            fireCooldown = charged ? .65f : .18f;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!Physics.Raycast(ray, out RaycastHit hit, 85f, ~0, QueryTriggerInteraction.Ignore)) return;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.ApplyDamage(charged ? chargeDamage : shotDamage, gameObject)) DisabledTargets++;
            hitFlash = .16f;
        }

        void OnGUI()
        {
            if (viewCamera == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = hitFlash > 0 ? new Color(1f, .55f, .25f) : Color.white;
            GUI.Label(new Rect(Screen.width / 2f - 15, Screen.height / 2f - 16, 30, 32), "+", style);
            GUI.Box(new Rect(18, Screen.height - 68, 345, 45), "ARC   W A S D  MOVE    SPACE ×2  JUMP    SHIFT  DASH\nLEFT CLICK  FIRE    RIGHT HOLD  CHARGE    F  INTERACT");
        }
    }
}

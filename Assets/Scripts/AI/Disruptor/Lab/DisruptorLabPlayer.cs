using Skybreak.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.AI.Disruptor.Lab
{
    /// <summary>
    /// First-person test player for the Disruptor lab: health as a player <see cref="DamageSink"/>,
    /// hitscan rifle that damages any DamageSink or IDamageable. Scripted checks drive it through
    /// <see cref="SetLook"/>, <see cref="Teleport"/> and <see cref="ScriptedMove"/> with input disabled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DisruptorLabPlayer : DamageSink
    {
        public Camera eye;
        public bool acceptInput = true;
        public float maximumHealth = 100, shotDamage = 16, shotInterval = .2f, walkSpeed = 4.5f, sprintSpeed = 7f;

        CharacterController body;
        float health, yaw, pitch, vertical, fireAt, hurtFlash;
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }

        public override Allegiance Team => Allegiance.Player;
        public override float Health => health;
        public override float MaximumHealth => maximumHealth;
        public override Vector3 AimPoint => transform.position + Vector3.up * 1.3f;

        void Awake() { body = GetComponent<CharacterController>(); health = maximumHealth; }

        public override void ReceiveDamage(float amount, Transform source)
        {
            if (amount > 0 && !float.IsNaN(amount) && !float.IsInfinity(amount)) { health = Mathf.Max(0, health - amount); hurtFlash = .25f; }
        }

        public void ResetPlayer(Vector3 position, float facingYaw)
        {
            Teleport(position); health = maximumHealth; vertical = 0; SetLook(facingYaw, 0); fireAt = 0;
        }

        public void Teleport(Vector3 position)
        {
            body.enabled = false; transform.position = position; body.enabled = true; Physics.SyncTransforms();
        }

        public void SetLook(float newYaw, float newPitch)
        {
            yaw = newYaw; pitch = Mathf.Clamp(newPitch, -80, 80);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (eye != null) eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        /// <summary>Turns the camera to look straight at a world point.</summary>
        public void LookAt(Vector3 point)
        {
            Vector3 d = point - (eye != null ? eye.transform.position : transform.position + Vector3.up * 1.6f);
            float y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float p = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            SetLook(y, p);
        }

        public void ScriptedMove(Vector3 worldVelocity, float dt)
        {
            vertical = body.isGrounded ? -2 : vertical - 18 * dt;
            body.Move((worldVelocity + Vector3.up * vertical) * dt);
        }

        /// <summary>Fires one shot along the camera ray. Returns the receiver it damaged, if any.</summary>
        public Object Fire()
        {
            if (!Alive || eye == null) return null;
            ShotsFired++;
            if (!Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 80, ~0, QueryTriggerInteraction.Ignore)) return null;
            var sink = hit.collider.GetComponentInParent<DamageSink>();
            if (sink != null && sink != this) { sink.ReceiveDamage(shotDamage, transform); ShotsHit++; return sink; }
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null) { damageable.ApplyDamage(shotDamage, gameObject); ShotsHit++; return hit.collider; }
            return null;
        }

        void Update()
        {
            hurtFlash = Mathf.Max(0, hurtFlash - Time.deltaTime);
            if (!acceptInput) return;
            var k = Keyboard.current; var m = Mouse.current;
            if (k == null || m == null) return;
            if (k.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; return; }
            if (!Alive || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = m.delta.ReadValue();
            SetLook(yaw + look.x * .1f, pitch - look.y * .1f);
            Vector3 move = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), 0, (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            float speed = k.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (body.isGrounded && k.spaceKey.wasPressedThisFrame) vertical = 6;
            ScriptedMove(transform.TransformDirection(move.normalized) * speed, dt);
            if (m.leftButton.isPressed && Time.time >= fireAt) { fireAt = Time.time + shotInterval; Fire(); }
        }

        void OnGUI()
        {
            if (!acceptInput) return;
            GUI.Label(new Rect(Screen.width / 2f - 6, Screen.height / 2f - 12, 24, 24), "+");
            if (hurtFlash > 0) GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
        }

        protected override void OnDisable() { base.OnDisable(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}

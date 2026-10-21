using Skybreak.Combat;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    /// <summary>
    /// Sensors and memory (a model-based agent's internal state). The world is partially observable:
    /// the Disruptor only knows where the player is while it can see them, then remembers the last
    /// position with a confidence that decays exponentially, c(t) = exp(-(t - t_seen) / tau).
    ///
    /// Its distinctive sensor is aim awareness: it watches the player's camera ray. Being in the
    /// crosshair builds "aim pressure" over about 0.6 s; out of the crosshair the pressure drains.
    /// A hit also reveals where the shot came from and adds pressure, so shooting it from hiding
    /// is not free.
    /// </summary>
    public sealed class DisruptorPerception
    {
        public Transform Player { get; private set; }
        public DamageSink Vitals { get; private set; }
        public IDamageable AltVitals { get; private set; }

        public bool Visible { get; private set; }
        public Vector3 LastKnown { get; private set; }
        public float LastSeen { get; private set; } = float.NegativeInfinity;
        public Vector3 PlayerVelocity { get; private set; }
        /// <summary>Degrees between the player's aim and the direction from their eye to the Disruptor.</summary>
        public float AimAngle { get; private set; } = 180;
        /// <summary>The player has an unobstructed line to the Disruptor.</summary>
        public bool PlayerHasLine { get; private set; }
        public float AimPressure { get; private set; }
        public Vector3 PlayerEye { get; private set; }
        public Vector3 PlayerAim { get; private set; } = Vector3.forward;

        public const float FillSeconds = 0.6f, DrainSeconds = 1.2f, HitPressure = 0.35f;

        public void Bind(Transform player, DamageSink vitals)
        {
            Player = player; Vitals = vitals;
            AltVitals = player != null && vitals == null ? player.GetComponentInParent<IDamageable>() : null;
        }

        public bool PlayerAlive => Player != null && Player.gameObject.activeInHierarchy &&
                                   (Vitals != null ? Vitals.Alive : AltVitals == null || AltVitals.IsAlive);

        public float PlayerHealth01 => Vitals != null ? Vitals.Health / Mathf.Max(1, Vitals.MaximumHealth) : AltVitals != null ? AltVitals.HealthFraction : 1;

        public float Confidence(float now, float tau) => Visible ? 1 : Mathf.Exp(-(now - LastSeen) / Mathf.Max(0.05f, tau));

        public Vector3 PlayerChest => Vitals != null ? Vitals.AimPoint : Player != null ? Player.position + Vector3.up * 1.2f : LastKnown;

        public void Forget() { LastSeen = float.NegativeInfinity; Visible = false; AimPressure = 0; PlayerVelocity = Vector3.zero; }

        /// <summary>Called when a hit lands: the Disruptor knows roughly where the shot came from.</summary>
        public void NotifyHit(Vector3 sourcePosition, float now)
        {
            if (!Visible) { LastKnown = sourcePosition; LastSeen = Mathf.Max(LastSeen, now - 0.5f); }
            AimPressure = Mathf.Clamp01(AimPressure + HitPressure);
        }

        public void Sense(Transform self, Vector3 eye, float range, float fieldOfView, float aimCone, LayerMask blockers, float now, float dt)
        {
            Visible = false; PlayerHasLine = false; AimAngle = 180;
            if (Player == null || !PlayerAlive) { AimPressure = 0; return; }

            Vector3 chest = PlayerChest;
            Vector3 toPlayer = chest - eye;
            float distance = toPlayer.magnitude;
            bool clear = distance > 0.01f && !Physics.Raycast(eye, toPlayer / distance, distance - 0.2f, blockers, QueryTriggerInteraction.Ignore);
            Vector3 flat = toPlayer; flat.y = 0;
            bool inCone = Vector3.Angle(self.forward, flat) <= fieldOfView * 0.5f || distance < 2.5f;   // close enough to sense regardless
            Visible = clear && distance <= range && inCone;
            PlayerHasLine = clear;

            if (Visible)
            {
                if (dt > 0 && !float.IsNegativeInfinity(LastSeen) && now - LastSeen < 0.5f)
                    PlayerVelocity = Vector3.Lerp(PlayerVelocity, (Player.position - LastKnown) / Mathf.Max(dt, now - LastSeen), 0.4f);
                LastKnown = Player.position; LastSeen = now;
            }

            // Aim: the camera ray when the player owns or drives one, else the body's facing.
            ReadAim();
            Vector3 toMe = eye - PlayerEye;
            AimAngle = toMe.sqrMagnitude > 1e-4f ? Vector3.Angle(PlayerAim, toMe) : 0;
            bool targeted = clear && AimAngle <= aimCone;
            AimPressure = Mathf.Clamp01(AimPressure + (targeted ? dt / FillSeconds : -dt / DrainSeconds));
        }

        void ReadAim()
        {
            Camera cam = Player.GetComponentInChildren<Camera>();
            if (cam == null && Camera.main != null && Vector3.Distance(Camera.main.transform.position, Player.position) < 10) cam = Camera.main;
            if (cam != null) { PlayerEye = cam.transform.position; PlayerAim = cam.transform.forward; }
            else { PlayerEye = Player.position + Vector3.up * 1.5f; PlayerAim = Player.forward; }
        }
    }
}

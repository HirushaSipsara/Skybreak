using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    public enum PulsePhase { Ready, Charging, Charged, Cooldown }

    /// <summary>
    /// The pulse attack's timing state, with no visuals and no damage: the brain resolves the hit and the
    /// presentation reads <see cref="Progress"/> to draw the telegraph ring. Each pulse has an id so a
    /// release can deal damage at most once.
    /// </summary>
    public sealed class DisruptorPulse
    {
        public PulsePhase Phase { get; private set; } = PulsePhase.Ready;
        public int PulseId { get; private set; }
        public float ChargeSeconds { get; private set; }
        public float StartedAt { get; private set; }
        public float ReadyAt { get; private set; }
        public float CooldownSeconds { get; private set; } = 1;
        public float DamageWhileCharging { get; private set; }
        public bool PlayerInsideAtStart { get; set; }
        int resolvedId = -1;

        public bool Ready(float now) => Phase == PulsePhase.Ready || (Phase == PulsePhase.Cooldown && now >= ReadyAt);
        public bool Charged => Phase == PulsePhase.Charged;
        public bool Busy => Phase == PulsePhase.Charging || Phase == PulsePhase.Charged;
        public float Progress(float now) => Phase == PulsePhase.Charging ? Mathf.Clamp01((now - StartedAt) / Mathf.Max(0.01f, ChargeSeconds)) : Phase == PulsePhase.Charged ? 1 : 0;
        public float Cooldown01(float now) => Phase == PulsePhase.Cooldown && now < ReadyAt ? Mathf.Clamp01((ReadyAt - now) / Mathf.Max(0.01f, CooldownSeconds)) : 0;

        public void Begin(float now, float chargeSeconds)
        {
            PulseId++; Phase = PulsePhase.Charging; StartedAt = now; ChargeSeconds = Mathf.Max(0.05f, chargeSeconds);
            DamageWhileCharging = 0;
        }

        public void Tick(float now)
        {
            if (Phase == PulsePhase.Charging && now - StartedAt >= ChargeSeconds) Phase = PulsePhase.Charged;
            if (Phase == PulsePhase.Cooldown && now >= ReadyAt) Phase = PulsePhase.Ready;
        }

        /// <returns>True when this damage staggers (cancels) the charge.</returns>
        public bool AbsorbDamage(float amount, float staggerThreshold)
        {
            if (!Busy) return false;
            DamageWhileCharging += amount;
            return DamageWhileCharging >= staggerThreshold;
        }

        /// <summary>Marks the release. Returns false if this pulse was already resolved (no double damage).</summary>
        public bool Release(float now, float cooldown)
        {
            if (Phase != PulsePhase.Charged || resolvedId == PulseId) return false;
            resolvedId = PulseId;
            StartCooldown(now, cooldown);
            return true;
        }

        /// <summary>Stagger, death or a forced replan: the pulse is lost, with a shorter cooldown.</summary>
        public void Cancel(float now, float cooldown)
        {
            if (!Busy) return;
            resolvedId = PulseId;
            StartCooldown(now, cooldown);
        }

        public void ResetState() { Phase = PulsePhase.Ready; DamageWhileCharging = 0; resolvedId = PulseId; }

        void StartCooldown(float now, float cooldown) { Phase = PulsePhase.Cooldown; CooldownSeconds = Mathf.Max(0.1f, cooldown); ReadyAt = now + CooldownSeconds; }
    }
}

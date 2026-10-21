using Skybreak.Combat;
using UnityEngine;

namespace Skybreak.AI.SawanIS.VaultHound
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class VaultHoundDamageAdapter : DamageSink
    {
        CombatHealth health;
        void Awake() { health = GetComponent<CombatHealth>(); }
        public override Allegiance Team => Allegiance.Hostile;
        public override float Health => health != null ? health.CurrentHealth : 0;
        public override float MaximumHealth => health != null ? health.MaximumHealth : 0;
        public override Vector3 AimPoint => transform.position + Vector3.up * .65f;
        public override void ReceiveDamage(float amount, Transform source)
        {
            if (health != null && amount > 0 && !float.IsNaN(amount) && !float.IsInfinity(amount))
                health.ApplyDamage(amount, source != null ? source.gameObject : null);
        }
    }
}

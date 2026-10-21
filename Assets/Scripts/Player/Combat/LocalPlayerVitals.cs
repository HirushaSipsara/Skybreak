using Skybreak.Combat;
using UnityEngine;
namespace Skybreak.LocalCombatLab
{
    // Shared player damage adapter, also used by the combat test scenes.
    public sealed class LocalPlayerVitals : DamageSink
    {
        public float health = 100;
        public override Allegiance Team => Allegiance.Player;
        public override float Health => health;
        public override float MaximumHealth => 100;
        public override Vector3 AimPoint => transform.position + Vector3.up * 1.2f;
        public override void ReceiveDamage(float amount, Transform source)
        {
            if (!Alive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            health = Mathf.Max(0,health-amount); var feedback=GetComponent<LocalPlayerFeedback>(); if(feedback!=null)feedback.Hurt(source);
        }
        public override float RestoreHealth(float amount) { if (!Alive || amount <= 0) return 0; float before = health; health = Mathf.Min(100, health + amount); return health - before; }
    }
}

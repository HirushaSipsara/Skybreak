using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.Combat
{
    public enum Allegiance { Player, Friendly, Hostile, Neutral }
    public interface IDamageReceiver { void ReceiveDamage(float amount, Transform source); }
    public interface IHealthStatus { float Health { get; } float MaximumHealth { get; } bool Alive { get; } }

    // Student 2 can implement this adapter on the final player without changing either brain.
    public abstract class DamageSink : MonoBehaviour, IDamageReceiver, IHealthStatus
    {
        public static readonly List<DamageSink> Active = new List<DamageSink>();
        public abstract Allegiance Team { get; }
        public abstract float Health { get; }
        public abstract float MaximumHealth { get; }
        public bool Alive => Health > 0;
        public virtual Vector3 AimPoint => transform.position + Vector3.up;
        public abstract void ReceiveDamage(float amount, Transform source);
        public virtual float RestoreHealth(float amount) => 0;
        protected virtual void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        protected virtual void OnDisable() { Active.Remove(this); }
        public static bool Visible(Vector3 origin, DamageSink target, Transform observer)
        {
            Vector3 delta = target.AimPoint - origin;
            // Actors are on layer 8. Walls/cover are solid on the environment layers.
            return !Physics.Raycast(origin, delta.normalized, delta.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore);
        }
    }
}

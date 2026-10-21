using UnityEngine;

namespace Skybreak
{
    public sealed class SecurityTarget : MonoBehaviour, IDamageable
    {
        public float maximumHealth = 70f;
        public float health = 70f;
        public bool IsAlive => health > 0f;
        public float HealthFraction => maximumHealth <= 0f ? 0f : Mathf.Clamp01(health / maximumHealth);

        public bool ApplyDamage(float amount, GameObject source)
        {
            return TakeDamage(amount);
        }

        public bool TakeDamage(float amount)
        {
            if (health <= 0 || amount <= 0f) return false;
            health = Mathf.Max(0f, health - amount);
            if (health > 0) return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}

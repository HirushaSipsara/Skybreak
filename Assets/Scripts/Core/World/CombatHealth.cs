using System;
using UnityEngine;
using UnityEngine.Events;

namespace Skybreak
{
    /// <summary>Small reusable health component for player and enemy combatants.</summary>
    public sealed class CombatHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maximumHealth = 100f;
        [SerializeField] bool disableObjectOnDeath;
        [SerializeField] UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        [SerializeField] UnityEvent onDefeated = new UnityEvent();

        float currentHealth;
        bool defeated;

        public bool IsAlive => !defeated;
        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public float HealthFraction => maximumHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maximumHealth);
        public event Action<CombatHealth, GameObject, float> DamageTaken;
        public UnityEvent<float> OnHealthChanged => onHealthChanged;
        public UnityEvent OnDefeated => onDefeated;

        void Awake()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            currentHealth = maximumHealth;
        }

        public bool ApplyDamage(float amount, GameObject source)
        {
            if (defeated || amount <= 0f) return false;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            onHealthChanged.Invoke(HealthFraction);
            DamageTaken?.Invoke(this, source, amount);

            if (currentHealth > 0f) return false;

            defeated = true;
            onDefeated.Invoke();
            if (disableObjectOnDeath) gameObject.SetActive(false);
            return true;
        }

        public void RestoreFullHealth()
        {
            defeated = false;
            currentHealth = maximumHealth;
            onHealthChanged.Invoke(HealthFraction);
        }
    }
}

using UnityEngine;

namespace Skybreak
{
    public sealed class SecurityTarget : MonoBehaviour
    {
        public float health = 70f;
        public bool TakeDamage(float amount)
        {
            if (health <= 0) return false;
            health -= amount;
            if (health > 0) return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}
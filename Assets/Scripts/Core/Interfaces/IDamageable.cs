namespace Skybreak
{
    /// <summary>Common contract for actors that can receive combat damage.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        float HealthFraction { get; }

        /// <returns>True when this hit defeated the target.</returns>
        bool ApplyDamage(float amount, UnityEngine.GameObject source);
    }
}
